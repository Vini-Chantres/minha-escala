using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MinhaEscala.Application;
using MinhaEscala.Domain;
using MinhaEscala.Infrastructure;
using Npgsql;

namespace MinhaEscala.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_DATABASE_URL"))) Skip = "Defina TEST_DATABASE_URL para executar com PostgreSQL real."; }
}
[CollectionDefinition("postgres", DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<ApiFactory>;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public ConcurrentQueue<string> EmailLogs { get; } = new();
    public string DatabaseUrl => Environment.GetEnvironmentVariable("TEST_DATABASE_URL")!;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var parsed = new NpgsqlConnectionStringBuilder(AppSettings.ParseConnectionString(DatabaseUrl, true));
        if (!parsed.Database!.StartsWith("escala_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Testes exigem base exclusiva cujo nome começa com escala_test_.");
        builder.UseEnvironment("Development").UseSetting("DATABASE_URL", DatabaseUrl)
            .UseSetting("JWT_SECRET", "test-only-secret-with-32-bytes-minimum-001")
            .UseSetting("JWT_ISSUER", "test-issuer").UseSetting("JWT_AUDIENCE", "test-audience")
            .UseSetting("APP_URL", "http://localhost:5173").UseSetting("SMTP_HOST", "").UseSetting("APPLY_MIGRATIONS", "false");
        builder.ConfigureLogging(l => l.AddProvider(new CaptureProvider(EmailLogs)));
        builder.ConfigureServices(s => s.AddTransient<IStartupFilter, TestIpFilter>());
    }
    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(DatabaseUrl)) return;
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
    Task IAsyncLifetime.DisposeAsync() { Dispose(); return Task.CompletedTask; }
    public HttpClient Client(bool cookie = false)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = cookie, BaseAddress = new Uri("http://localhost") });
        client.DefaultRequestHeaders.Add("X-CSRF", "1");
        client.DefaultRequestHeaders.Add("X-Test-IP", $"127.0.{Random.Shared.Next(1, 255)}.{Random.Shared.Next(1, 255)}"); return client;
    }
    public string LastLinkToken(string kind) => Regex.Match(EmailLogs.Last(x => x.Contains($"#{kind}=")), $"#{kind}=([A-F0-9]+)").Groups[1].Value;
    private sealed class TestIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app => {
            app.Use(async (context, continuation) => {
                if (IPAddress.TryParse(context.Request.Headers["X-Test-IP"], out var ip)) context.Connection.RemoteIpAddress = ip;
                await continuation();
            }); next(app);
        };
    }
    private sealed class CaptureProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capture(logs, categoryName);
        public void Dispose() { }
        private sealed class Capture(ConcurrentQueue<string> logs, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            { if (category.EndsWith("EmailSender", StringComparison.Ordinal)) logs.Enqueue(formatter(state, exception)); }
        }
    }
}

[Collection("postgres")]
public sealed class ApiTests(ApiFactory factory)
{
    private const string Password = "Senha-segura-de-testes-123";
    private async Task<(HttpClient Client, AuthDto Auth, string Email, string Cookie)> Register(string? invite = null, string? targetEmail = null)
    {
        var client = factory.Client(); var email = targetEmail ?? $"test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Pessoa teste", email, Password, invite));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth, email, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }
    [PostgresFact]
    public async Task HealthAndAnonymousProtection()
    {
        using var c = factory.Client(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/unknown-route")).StatusCode);
    }
    [PostgresFact]
    public async Task RegisterLoginAndValidation()
    {
        var (c, auth, email, cookie) = await Register(); using var client = c;
        Assert.Equal("Owner", auth.User.Role);
        Assert.Contains("minha_escala_refresh=", cookie);
        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me"); Assert.Equal(auth.User.Id, me!.Id);
        using var other = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.PostAsJsonAsync("/api/auth/login", new LoginRequest(email.ToUpperInvariant(), Password))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/auth/register", new RegisterRequest("X", "bad", "short"))).StatusCode);
    }
    [PostgresFact]
    public async Task CrudPersistsAndUpsertsOneRecordPerDate()
    {
        var (c, auth, email, _) = await Register(); using var client = c;
        var request = new EntryRequest(new DateOnly(2026, 9, 20), "folga", "UTI");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/entries/2026-09-20", request)).StatusCode);
        await client.PutAsJsonAsync("/api/entries/2026-09-20", request with { Type = "trabalho", Sector = "Clínica 2" });
        using var login = factory.Client();
        var session = (await (await login.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password))).Content.ReadFromJsonAsync<AuthDto>())!;
        login.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var entries = (await login.GetFromJsonAsync<EntryDto[]>("/api/entries"))!;
        Assert.Single(entries); Assert.Equal("trabalho", entries[0].Type); Assert.Equal("Clínica 2", entries[0].Sector);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.Entries.Where(x => x.UserId == auth.User.Id).ToListAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/entries/2026-09-20")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<EntryDto[]>("/api/entries"))!);
    }
    [PostgresFact]
    public async Task TenantAndUserIsolation()
    {
        var (a, _, _, _) = await Register(); var (b, _, _, _) = await Register(); using var first = a; using var second = b;
        await first.PutAsJsonAsync("/api/entries/2026-09-21", new EntryRequest(new DateOnly(2026, 9, 21), "folga", "Privado"));
        Assert.Empty((await second.GetFromJsonAsync<EntryDto[]>("/api/entries"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await second.DeleteAsync("/api/entries/2026-09-21")).StatusCode);
        await second.PutAsJsonAsync("/api/entries/2026-09-21", new EntryRequest(new DateOnly(2026, 9, 21), "trabalho", "Outro"));
        Assert.Equal("Privado", (await first.GetFromJsonAsync<EntryDto[]>("/api/entries"))!.Single().Sector);
        var users = (await second.GetFromJsonAsync<UserDto[]>("/api/organization/users"))!; Assert.Single(users);
    }
    [PostgresFact]
    public async Task RefreshRotatesAndReplayRevokesSession()
    {
        var (c, _, _, cookie) = await Register(); using var client = c;
        using var refresh = factory.Client(); refresh.DefaultRequestHeaders.Add("Cookie", cookie);
        var rotated = await refresh.PostAsync("/api/auth/refresh", null); Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(cookie, rotated.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        var newAuth = (await rotated.Content.ReadFromJsonAsync<AuthDto>())!;
        var replay = await refresh.PostAsync("/api/auth/refresh", null); Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newAuth.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/entries")).StatusCode);
    }
    [PostgresFact]
    public async Task LogoutImmediatelyRevokesAccessAndRefresh()
    {
        var (c, _, _, cookie) = await Register(); using var client = c;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/entries")).StatusCode);
        using var refresh = factory.Client(); refresh.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await refresh.PostAsync("/api/auth/refresh", null)).StatusCode);
    }
    [PostgresFact]
    public async Task PasswordRecoveryUsesOneTimeTokenAndRevokesSessions()
    {
        var (c, _, email, _) = await Register(); using var client = c; using var anonymous = factory.Client();
        var known = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new ForgotRequest(email));
        var unknown = await anonymous.PostAsJsonAsync("/api/auth/forgot-password", new ForgotRequest($"unknown-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var resetToken = factory.LastLinkToken("reset");
        var reset = new ResetRequest(resetToken, "Nova-senha-segura-123456");
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, reset.Password))).StatusCode);
    }
    [PostgresFact]
    public async Task InvitationsMemberPermissionsAndDeactivation()
    {
        var (c, owner, _, _) = await Register(); using var client = c;
        var email = $"member-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/organization/invitations", new InviteRequest(email))).StatusCode);
        var (m, member, _, _) = await Register(factory.LastLinkToken("invite"), email); using var memberClient = m;
        Assert.Equal("Member", member.User.Role); Assert.Equal(owner.User.TenantId, member.User.TenantId);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/organization/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync("/api/organization/audit")).StatusCode);
        await memberClient.PutAsJsonAsync("/api/entries/2026-09-22", new EntryRequest(new DateOnly(2026, 9, 22), "trabalho", "UTI"));
        Assert.Empty((await client.GetFromJsonAsync<EntryDto[]>("/api/entries"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/organization/users/{member.User.Id}/status", new StatusRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await memberClient.GetAsync("/api/entries")).StatusCode);
        await client.PatchAsJsonAsync($"/api/organization/users/{member.User.Id}/status", new StatusRequest(true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await memberClient.GetAsync("/api/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync($"/api/organization/users/{owner.User.Id}/status", new StatusRequest(false))).StatusCode);
    }
    [PostgresFact]
    public async Task ImportIsAtomicAndPreservesExistingDates()
    {
        var (c, _, _, _) = await Register(); using var client = c;
        await client.PutAsJsonAsync("/api/entries/2026-09-23", new EntryRequest(new DateOnly(2026, 9, 23), "folga", "Original"));
        var entries = new[] { new EntryRequest(new DateOnly(2026, 9, 23), "trabalho", "Novo"), new EntryRequest(new DateOnly(2026, 9, 24), "trabalho", "UTI") };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/entries/import", entries)).StatusCode);
        var saved = (await client.GetFromJsonAsync<EntryDto[]>("/api/entries"))!; Assert.Equal(2, saved.Length); Assert.Equal("Original", saved[0].Sector);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/entries/import", new[] { entries[1], entries[1] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/entries/import", new[] { new EntryRequest(new DateOnly(2026, 9, 25), "invalid", "") })).StatusCode);
        Assert.Equal(2, (await client.GetFromJsonAsync<EntryDto[]>("/api/entries"))!.Length);
    }
    [PostgresFact]
    public async Task CsrfAndOriginAreEnforced()
    {
        using var c = factory.Client(); c.DefaultRequestHeaders.Remove("X-CSRF");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", Password))).StatusCode);
        c.DefaultRequestHeaders.Add("X-CSRF", "1"); c.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", Password))).StatusCode);
    }
    [PostgresFact]
    public async Task AuditIsTenantScopedAndContainsNoCredentials()
    {
        var (c, auth, _, _) = await Register(); using var client = c;
        await client.PutAsJsonAsync("/api/entries/2026-09-26", new EntryRequest(new DateOnly(2026, 9, 26), "folga", "Confidencial"));
        var response = await client.GetAsync("/api/organization/audit"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("entry.create", json); Assert.DoesNotContain(Password, json); Assert.DoesNotContain(auth.AccessToken, json); Assert.DoesNotContain("Confidencial", json);
    }
    [PostgresFact]
    public async Task AuthenticationRateLimitIsEnforced()
    {
        using var client = factory.Client();
        for (var i = 0; i < 12; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        var denied = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode); Assert.True(denied.Headers.Contains("Retry-After"));
    }
}
