using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinhaEscala.Application;
using MinhaEscala.Domain;

namespace MinhaEscala.Infrastructure;

[ApiController, Authorize, Route("api/entries")]
public sealed class EntriesController(AppDbContext db, IValidator<EntryRequest> validator) : ControllerBase
{
    private IQueryable<ScheduleEntry> Mine => db.Entries.Where(x => x.UserId == this.UserId() && x.TenantId == this.TenantId());
    private static EntryDto Dto(ScheduleEntry x) => new(x.Id, x.Date, x.Type, x.Sector);
    [HttpGet]
    public async Task<ActionResult<IEnumerable<EntryDto>>> List(CancellationToken ct) => Ok((await Mine.AsNoTracking().OrderBy(x => x.Date).ToListAsync(ct)).Select(Dto));
    [HttpPut("{date}")]
    public async Task<ActionResult<EntryDto>> Save(DateOnly date, EntryRequest request, CancellationToken ct) {
        if (date != request.Date) throw new ApiException(400, "A data da URL deve coincidir com o registro.");
        var entry = await Mine.SingleOrDefaultAsync(x => x.Date == date, ct);
        var created = entry is null;
        if (entry is null) { entry = new ScheduleEntry { UserId = this.UserId(), TenantId = this.TenantId(), Date = date }; db.Entries.Add(entry); }
        entry.Type = request.Type; entry.Sector = request.Sector.Trim(); entry.UpdatedAt = DateTimeOffset.UtcNow;
        db.Audit(this.TenantId(), this.UserId(), created ? "entry.create" : "entry.update", entry.Id.ToString());
        await db.SaveChangesAsync(ct); return Ok(Dto(entry));
    }
    [HttpDelete("{date}")]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken ct) {
        var entry = await Mine.SingleOrDefaultAsync(x => x.Date == date, ct);
        if (entry is null) return NotFound();
        db.Entries.Remove(entry); db.Audit(this.TenantId(), this.UserId(), "entry.delete", entry.Id.ToString());
        await db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpPost("import")]
    public async Task<IActionResult> Import(EntryRequest[] entries, CancellationToken ct) {
        if (entries.Length is 0 or > 3660 || entries.Any(x => x is null) || entries.Select(x => x.Date).Distinct().Count() != entries.Length)
            throw new ApiException(400, "Importe de 1 a 3660 datas distintas.");
        foreach (var entry in entries) {
            if (entry is null || !(await validator.ValidateAsync(entry, ct)).IsValid) throw new ApiException(400, "Arquivo contém registros inválidos.");
        }
        var existing = (await Mine.Select(x => x.Date).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var request in entries.Where(x => !existing.Contains(x.Date))) {
            db.Entries.Add(new ScheduleEntry { TenantId = this.TenantId(), UserId = this.UserId(), Date = request.Date, Type = request.Type, Sector = request.Sector.Trim() }); added++;
        }
        db.Audit(this.TenantId(), this.UserId(), "entry.import", $"count:{added}"); await db.SaveChangesAsync(ct);
        return Ok(new { imported = added, skipped = entries.Length - added });
    }
}
