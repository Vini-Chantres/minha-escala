using Microsoft.EntityFrameworkCore;
using MinhaEscala.Domain;

namespace MinhaEscala.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ScheduleEntry> Entries => Set<ScheduleEntry>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e => { e.ToTable("tenants"); e.Property(x => x.Name).HasMaxLength(100); });
        b.Entity<User>(e => {
            e.ToTable("users", t => t.HasCheckConstraint("CK_users_role", "\"Role\" IN ('Owner', 'Member')"));
            e.HasIndex(x => x.Email).IsUnique(); e.HasAlternateKey(x => new { x.Id, x.TenantId });
            e.Property(x => x.Name).HasMaxLength(100); e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.PasswordHash).HasMaxLength(256); e.Property(x => x.Role).HasMaxLength(20);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ScheduleEntry>(e => {
            e.ToTable("schedule_entries", t => t.HasCheckConstraint("CK_entries_type", "\"Type\" IN ('folga', 'trabalho')"));
            e.HasIndex(x => new { x.UserId, x.Date }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.UserId });
            e.Property(x => x.Type).HasMaxLength(20); e.Property(x => x.Sector).HasMaxLength(120);
            e.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId, x.TenantId })
                .HasPrincipalKey(x => new { x.Id, x.TenantId }).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AuthSession>(e => {
            e.ToTable("auth_sessions"); e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RefreshToken>(e => {
            e.ToTable("refresh_tokens"); e.HasIndex(x => x.Hash).IsUnique(); e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.ConsumedAt).IsConcurrencyToken();
            e.HasOne<AuthSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PasswordReset>(e => {
            e.ToTable("password_resets"); e.HasIndex(x => x.Hash).IsUnique(); e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.IsUsed).IsConcurrencyToken();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Invitation>(e => {
            e.ToTable("invitations"); e.HasIndex(x => x.Hash).IsUnique(); e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.Email).HasMaxLength(254); e.Property(x => x.IsUsed).IsConcurrencyToken();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AuditLog>(e => {
            e.ToTable("audit_logs"); e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.Property(x => x.Action).HasMaxLength(50); e.Property(x => x.Resource).HasMaxLength(100);
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public void Audit(Guid tenantId, Guid? userId, string action, string resource) =>
        AuditLogs.Add(new AuditLog { TenantId = tenantId, UserId = userId, Action = action, Resource = resource });
}
