using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MinhaEscala.Infrastructure;

public sealed record AppSettings(string ConnectionString, string JwtSecret, string Issuer, string Audience, string AppUrl)
{
    public static AppSettings Read(IConfiguration c, bool development)
    {
        string Required(string key) => !string.IsNullOrWhiteSpace(c[key]) ? c[key]! : throw new InvalidOperationException($"Configure {key} no backend.");
        var secret = Required("JWT_SECRET");
        if (System.Text.Encoding.UTF8.GetByteCount(secret) < 32 || secret.StartsWith("SUBSTITUA", StringComparison.Ordinal))
            throw new InvalidOperationException("JWT_SECRET deve conter pelo menos 32 bytes aleatórios.");
        var appUrl = Required("APP_URL").TrimEnd('/');
        if (!Uri.TryCreate(appUrl, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.Scheme != "https" && !(development && uri.Scheme == "http" && uri.IsLoopback))
            throw new InvalidOperationException("APP_URL deve ser uma origem HTTPS (HTTP localhost somente em Development).");
        return new AppSettings(ParseConnectionString(Required("DATABASE_URL"), development), secret,
            Required("JWT_ISSUER"), Required("JWT_AUDIENCE"), appUrl);
    }

    public static string ParseConnectionString(string value, bool development)
    {
        NpgsqlConnectionStringBuilder result;
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(value);
            var credentials = uri.UserInfo.Split(':', 2);
            if (credentials.Length != 2) throw new InvalidOperationException("DATABASE_URL precisa de usuário e senha.");
            result = new NpgsqlConnectionStringBuilder {
                Host = uri.Host, Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Username = Uri.UnescapeDataString(credentials[0]), Password = Uri.UnescapeDataString(credentials[1]),
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')), SslMode = SslMode.VerifyFull
            };
            // sslmode=require de URLs Neon é fortalecido para validar também certificado/hostname.
            foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length == 2 && pair[0] == "channel_binding")
                    result.ChannelBinding = pair[1] switch {
                        "require" => ChannelBinding.Require, "prefer" => ChannelBinding.Prefer, "disable" => ChannelBinding.Disable,
                        _ => throw new InvalidOperationException("channel_binding inválido em DATABASE_URL.")
                    };
                if (pair.Length == 2 && pair[0] == "sslmode" && pair[1] == "disable" && development && (uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1"))
                    result.SslMode = SslMode.Disable;
            }
        }
        else result = new NpgsqlConnectionStringBuilder(value);
        if (!development && result.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Em produção, use SSL Mode=VerifyFull para o PostgreSQL.");
        result.IncludeErrorDetail = false;
        result.Timeout = 15;
        result.CommandTimeout = 30;
        return result.ConnectionString;
    }
}
