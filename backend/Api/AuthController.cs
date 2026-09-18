using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MinhaEscala.Application;

namespace MinhaEscala.Infrastructure;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(AuthService auth, AppDbContext db, IHostEnvironment environment, ILogger<AuthController> logger) : ControllerBase
{
    private const string CookieName = "minha_escala_refresh";
    private CookieOptions Cookie(DateTimeOffset? expires = null) => new() {
        HttpOnly = true, Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expires, IsEssential = true
    };
    private AuthDto SetSession(TokenPair pair) {
        Response.Cookies.Append(CookieName, pair.RefreshToken, Cookie(pair.RefreshExpiresAt)); return pair.Auth;
    }
    [HttpPost("register")]
    public async Task<ActionResult<AuthDto>> Register(RegisterRequest request, CancellationToken ct) => Ok(SetSession(await auth.RegisterAsync(request, ct)));
    [HttpPost("login")]
    public async Task<ActionResult<AuthDto>> Login(LoginRequest request, CancellationToken ct) => Ok(SetSession(await auth.LoginAsync(request, ct)));
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthDto>> Refresh(CancellationToken ct) {
        try { return Ok(SetSession(await auth.RefreshAsync(Request.Cookies[CookieName], ct))); }
        catch (ApiException) { Response.Cookies.Delete(CookieName, Cookie()); throw; }
    }
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct) {
        await auth.LogoutAsync(Guid.Parse(User.FindFirst("sid")!.Value), this.UserId(), this.TenantId(), ct);
        Response.Cookies.Delete(CookieName, Cookie()); return NoContent();
    }
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct) => Ok(AuthService.ToDto(await db.Users.SingleAsync(x => x.Id == this.UserId() && x.TenantId == this.TenantId(), ct)));
    [HttpPost("forgot-password")]
    public async Task<IActionResult> Forgot(ForgotRequest request, CancellationToken ct) {
        try { await auth.ForgotAsync(request, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { logger.LogError("Falha na recuperação de senha: {ErrorType}", e.GetType().Name); }
        return Ok(new { message = "Se este e-mail estiver cadastrado, você receberá um link de recuperação." });
    }
    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetRequest request, CancellationToken ct) {
        await auth.ResetAsync(request, ct); Response.Cookies.Delete(CookieName, Cookie()); return NoContent();
    }
}

internal static class CurrentUser
{
    public static Guid UserId(this ControllerBase controller) => Guid.Parse(controller.User.FindFirst("sub")!.Value);
    public static Guid TenantId(this ControllerBase controller) => Guid.Parse(controller.User.FindFirst("tenant_id")!.Value);
}
