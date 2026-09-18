using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MinhaEscala.Application;
using MinhaEscala.Domain;

namespace MinhaEscala.Infrastructure;

public sealed class AuthService(AppDbContext db, AppSettings settings, EmailSender emailSender)
{
    private static readonly string DummyHash = PasswordSecurity.Hash(PasswordSecurity.RandomToken());
    public static UserDto ToDto(User u) => new(u.Id, u.TenantId, u.Name, u.Email, u.Role, u.IsActive);
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<TokenPair> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new ApiException(409, "Não foi possível cadastrar este e-mail. Tente entrar ou recuperar a senha.");
        Invitation? invitation = null;
        Guid tenantId;
        if (!string.IsNullOrEmpty(request.InvitationToken))
        {
            var hash = PasswordSecurity.TokenHash(request.InvitationToken);
            invitation = await db.Invitations.SingleOrDefaultAsync(x => x.Hash == hash, ct);
            if (invitation is null || invitation.IsUsed || invitation.ExpiresAt <= DateTimeOffset.UtcNow || invitation.Email != email)
                throw new ApiException(400, "Convite inválido ou expirado.");
            tenantId = invitation.TenantId; invitation.IsUsed = true;
        }
        else
        {
            var tenant = new Tenant { Name = request.Name.Trim() }; db.Tenants.Add(tenant); tenantId = tenant.Id;
        }
        var user = new User { TenantId = tenantId, Name = request.Name.Trim(), Email = email,
            PasswordHash = PasswordSecurity.Hash(request.Password), Role = invitation is null ? "Owner" : "Member" };
        db.Users.Add(user); db.Audit(tenantId, user.Id, "user.register", user.Id.ToString());
        var pair = CreateSession(user);
        await db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task<TokenPair> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        var valid = PasswordSecurity.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        if (user is null || !valid || !user.IsActive) throw new ApiException(401, "E-mail ou senha incorretos.");
        var pair = CreateSession(user); db.Audit(user.TenantId, user.Id, "auth.login", user.Id.ToString());
        await db.SaveChangesAsync(ct); return pair;
    }

    private TokenPair CreateSession(User user)
    {
        var session = new AuthSession { UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) };
        db.Sessions.Add(session); return Issue(user, session);
    }
    private TokenPair Issue(User user, AuthSession session)
    {
        var refresh = PasswordSecurity.RandomToken();
        db.RefreshTokens.Add(new RefreshToken { SessionId = session.Id, Hash = PasswordSecurity.TokenHash(refresh) });
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        var claims = new[] {
            new Claim("sub", user.Id.ToString()), new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("role", user.Role), new Claim("sid", session.Id.ToString()),
            new Claim("version", user.SecurityVersion.ToString()), new Claim("jti", Guid.NewGuid().ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            DateTime.UtcNow, expires.UtcDateTime, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret)), SecurityAlgorithms.HmacSha256));
        return new TokenPair(new AuthDto(new JwtSecurityTokenHandler().WriteToken(token), expires, ToDto(user)), refresh, session.ExpiresAt);
    }

    public async Task<TokenPair> RefreshAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length > 128) throw new ApiException(401, "Entre novamente.");
        var hash = PasswordSecurity.TokenHash(rawToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(x => x.Hash == hash, ct);
        if (token is null) throw new ApiException(401, "Entre novamente.");
        var session = await db.Sessions.SingleAsync(x => x.Id == token.SessionId, ct);
        if (token.ConsumedAt is not null)
        {
            session.IsRevoked = true; await db.SaveChangesAsync(ct);
            throw new ApiException(401, "Sessão invalidada. Entre novamente.");
        }
        if (session.IsRevoked || session.ExpiresAt <= DateTimeOffset.UtcNow) throw new ApiException(401, "Entre novamente.");
        var user = await db.Users.SingleAsync(x => x.Id == session.UserId, ct);
        if (!user.IsActive) throw new ApiException(401, "Entre novamente.");
        token.ConsumedAt = DateTimeOffset.UtcNow;
        var pair = Issue(user, session);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            await db.Sessions.Where(x => x.Id == session.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true), ct);
            throw new ApiException(401, "Sessão invalidada. Entre novamente.");
        }
        return pair;
    }

    public async Task LogoutAsync(Guid sessionId, Guid userId, Guid tenantId, CancellationToken ct)
    {
        var session = await db.Sessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);
        if (session is not null) session.IsRevoked = true;
        db.Audit(tenantId, userId, "auth.logout", userId.ToString()); await db.SaveChangesAsync(ct);
    }

    public async Task ForgotAsync(ForgotRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.IsActive, ct);
        if (user is null) return;
        var raw = PasswordSecurity.RandomToken();
        var old = await db.PasswordResets.Where(x => x.UserId == user.Id && !x.IsUsed).ToListAsync(ct);
        foreach (var reset in old) reset.IsUsed = true;
        db.PasswordResets.Add(new PasswordReset { UserId = user.Id, Hash = PasswordSecurity.TokenHash(raw), ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30) });
        db.Audit(user.TenantId, user.Id, "auth.forgot", user.Id.ToString()); await db.SaveChangesAsync(ct);
        // Fragmento evita que o token apareça em logs de requisição HTTP e em Referer.
        await emailSender.SendAsync(user.Email, "Redefina sua senha — Minha Escala",
            $"Use este link em até 30 minutos: {settings.AppUrl}/#reset={raw}\nSe não foi você, ignore esta mensagem.", ct);
    }

    public async Task ResetAsync(ResetRequest request, CancellationToken ct)
    {
        var hash = PasswordSecurity.TokenHash(request.Token);
        var reset = await db.PasswordResets.SingleOrDefaultAsync(x => x.Hash == hash, ct);
        if (reset is null || reset.IsUsed || reset.ExpiresAt <= DateTimeOffset.UtcNow) throw new ApiException(400, "Link inválido ou expirado. Solicite outro.");
        var user = await db.Users.SingleAsync(x => x.Id == reset.UserId, ct);
        if (!user.IsActive) throw new ApiException(400, "Link inválido ou expirado. Solicite outro.");
        user.PasswordHash = PasswordSecurity.Hash(request.Password); user.SecurityVersion++; user.UpdatedAt = DateTimeOffset.UtcNow;
        reset.IsUsed = true;
        var sessions = await db.Sessions.Where(x => x.UserId == user.Id && !x.IsRevoked).ToListAsync(ct);
        foreach (var session in sessions) session.IsRevoked = true;
        db.Audit(user.TenantId, user.Id, "auth.reset", user.Id.ToString());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ApiException(400, "Link já utilizado. Solicite outro."); }
    }
}
