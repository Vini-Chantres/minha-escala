using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MinhaEscala.Application;
using MinhaEscala.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.Read(builder.Configuration, builder.Environment.IsDevelopment());
if (!builder.Environment.IsDevelopment() && (string.IsNullOrWhiteSpace(builder.Configuration["SMTP_HOST"]) || string.IsNullOrWhiteSpace(builder.Configuration["SMTP_FROM"])))
    throw new InvalidOperationException("Configure SMTP_HOST e SMTP_FROM para recuperação de senha em produção.");
builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(settings.ConnectionString));
builder.Services.AddScoped<AuthService>(); builder.Services.AddScoped<EmailSender>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterValidator>();
builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>());
builder.Services.AddProblemDetails();
builder.Services.AddHttpsRedirection(o => o.HttpsPort = 443);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(settings.AppUrl).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.OnRejected = async (context, ct) => {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails { Status = 429, Title = "Muitas tentativas. Aguarde um minuto." }, ct);
    };
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = settings.Issuer, ValidAudience = settings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(15),
        NameClaimType = "sub", RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents {
        OnTokenValidated = async context => {
            var p = context.Principal!;
            if (!Guid.TryParse(p.FindFirst("sub")?.Value, out var userId) ||
                !Guid.TryParse(p.FindFirst("tenant_id")?.Value, out var tenantId) ||
                !Guid.TryParse(p.FindFirst("sid")?.Value, out var sessionId) ||
                !int.TryParse(p.FindFirst("version")?.Value, out var version)) { context.Fail("Invalid session"); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;
            var valid = await (from s in db.Sessions join u in db.Users on s.UserId equals u.Id
                where s.Id == sessionId && s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now &&
                u.TenantId == tenantId && u.IsActive && u.SecurityVersion == version && u.Role == p.FindFirst("role")!.Value
                select s.Id).AnyAsync(context.HttpContext.RequestAborted);
            if (!valid) context.Fail("Revoked session");
        }
    };
});
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseMiddleware<VercelProxyMiddleware>();
app.UseExceptionHandler(errorApp => errorApp.Run(async context => {
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = error is ApiException apiError ? apiError.Status : error is DbUpdateException ? 409 : 500;
    var title = error is ApiException known ? known.Message : status == 409 ? "Os dados foram alterados em outra sessão. Atualize e tente novamente." : "Não foi possível concluir. Tente novamente.";
    if (status == 500) app.Logger.LogError("Falha interna {ErrorType}. TraceId={TraceId}", error?.GetType().Name, context.TraceIdentifier);
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Extensions = { ["traceId"] = context.TraceIdentifier } });
}));
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseCors();
// Protege os endpoints que usam cookie de refresh contra CSRF, inclusive login.
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api/auth") && context.Request.Method != "GET" && context.Request.Method != "OPTIONS")
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (context.Request.Headers["X-CSRF"].ToString() != "1" || origin.Length > 0 && origin != settings.AppUrl)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 403, Title = "Origem de requisição inválida." }); return;
        }
    }
    await next();
});
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapControllers();
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) => {
    try {
        if (await db.Database.CanConnectAsync(ct)) return Results.Ok(new { status = "healthy" });
    } catch (Exception) { /* A resposta pública não divulga conexão ou credenciais. */ }
    return Results.Json(new { status = "unhealthy" }, statusCode: 503);
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
if (builder.Configuration.GetValue<bool>("APPLY_MIGRATIONS")) {
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
app.Run();

public partial class Program;
