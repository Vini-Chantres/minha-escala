using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinhaEscala.Application;
using MinhaEscala.Domain;

namespace MinhaEscala.Infrastructure;

[ApiController, Authorize(Roles = "Owner"), Route("api/organization")]
public sealed class OrganizationController(AppDbContext db, EmailSender sender, AppSettings settings) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<UserDto>>> Users(CancellationToken ct) =>
        Ok((await db.Users.AsNoTracking().Where(x => x.TenantId == this.TenantId()).OrderBy(x => x.Name).ToListAsync(ct)).Select(AuthService.ToDto));
    [HttpPost("invitations")]
    public async Task<IActionResult> Invite(InviteRequest request, CancellationToken ct) {
        var email = AuthService.NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new ApiException(409, "Não foi possível convidar este e-mail.");
        var raw = PasswordSecurity.RandomToken();
        var invitation = new Invitation { TenantId = this.TenantId(), Email = email, Hash = PasswordSecurity.TokenHash(raw), ExpiresAt = DateTimeOffset.UtcNow.AddDays(2) };
        db.Invitations.Add(invitation); db.Audit(this.TenantId(), this.UserId(), "user.invite", invitation.Id.ToString()); await db.SaveChangesAsync(ct);
        await sender.SendAsync(email, "Convite — Minha Escala", $"Crie sua conta na organização em até 48 horas: {settings.AppUrl}/#invite={raw}", ct);
        return Ok(new { message = "Convite enviado. Em desenvolvimento, verifique também o terminal da API." });
    }
    [HttpPatch("users/{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request, CancellationToken ct) {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == this.TenantId(), ct);
        if (user is null) return NotFound();
        if (user.Role == "Owner") throw new ApiException(400, "O proprietário não pode ser desativado.");
        user.IsActive = request.IsActive; user.SecurityVersion++; user.UpdatedAt = DateTimeOffset.UtcNow;
        db.Audit(this.TenantId(), this.UserId(), request.IsActive ? "user.activate" : "user.deactivate", id.ToString());
        await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, CancellationToken ct = default) {
        if (page is < 1 or > 10000) throw new ApiException(400, "Página inválida.");
        var query = db.AuditLogs.AsNoTracking().Where(x => x.TenantId == this.TenantId());
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        return Ok(new { items, total, page });
    }
}
public sealed record StatusRequest(bool IsActive);
