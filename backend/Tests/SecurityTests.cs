using FluentValidation;
using MinhaEscala.Application;
using MinhaEscala.Infrastructure;
using Npgsql;

namespace MinhaEscala.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void PasswordsAreSaltedAndVerified()
    {
        var a = PasswordSecurity.Hash("minha-senha-segura-123"); var b = PasswordSecurity.Hash("minha-senha-segura-123");
        Assert.NotEqual(a, b); Assert.True(PasswordSecurity.Verify("minha-senha-segura-123", a));
        Assert.False(PasswordSecurity.Verify("outra-senha", a)); Assert.False(PasswordSecurity.Verify("x", "invalid"));
        Assert.False(PasswordSecurity.Verify("x", "pbkdf2-sha256$999999999$ab$cd"));
    }
    [Fact]
    public void TokensAreRandomAndStoredAsHashes()
    {
        var token = PasswordSecurity.RandomToken(); Assert.Equal(64, token.Length);
        Assert.NotEqual(token, PasswordSecurity.RandomToken()); Assert.NotEqual(token, PasswordSecurity.TokenHash(token));
    }
    [Theory]
    [InlineData("folga", "UTI", true)]
    [InlineData("trabalho", "", true)]
    [InlineData("admin", "UTI", false)]
    [InlineData("folga", null, false)]
    public void EntryBoundaryValidation(string type, string? sector, bool valid) =>
        Assert.Equal(valid, new EntryValidator().Validate(new EntryRequest(new DateOnly(2026, 9, 17), type, sector!)).IsValid);
    [Fact]
    public void WeakPasswordAndInvalidEmailAreRejected()
    {
        Assert.False(new RegisterValidator().Validate(new RegisterRequest("V", "invalid", "short")).IsValid);
        Assert.False(new EntryValidator().Validate(new EntryRequest(new DateOnly(1800, 1, 1), "folga", "")).IsValid);
        Assert.False(new EntryValidator().Validate(new EntryRequest(new DateOnly(2026, 1, 1), "folga", new string('x', 121))).IsValid);
    }
    [Fact]
    public void NeonUriDecodesCredentialsAndEnforcesCertificateValidation()
    {
        var parsed = new NpgsqlConnectionStringBuilder(AppSettings.ParseConnectionString("postgresql://test:p%40ss%3Aword@host.neon.tech/neondb?sslmode=require&channel_binding=require", false));
        Assert.Equal(ChannelBinding.Require, parsed.ChannelBinding);
        Assert.Equal("p@ss:word", parsed.Password); Assert.Equal(SslMode.VerifyFull, parsed.SslMode); Assert.False(parsed.IncludeErrorDetail);
        Assert.Throws<InvalidOperationException>(() => AppSettings.ParseConnectionString("Host=localhost;Database=test;Username=test;SSL Mode=Disable", false));
    }
    [Fact]
    public void LocalTestDatabaseCanDisableTlsOnlyInDevelopment()
    {
        var parsed = new NpgsqlConnectionStringBuilder(AppSettings.ParseConnectionString("postgresql://test:password@127.0.0.1:55439/escala_test_unit?sslmode=disable", true));
        Assert.Equal(SslMode.Disable, parsed.SslMode); Assert.Equal(55439, parsed.Port);
    }
}
