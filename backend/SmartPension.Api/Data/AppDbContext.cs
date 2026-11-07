using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartPension.Api.Models;

namespace SmartPension.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> o) : base(o) { }

    public DbSet<Pensioner> Pensioners => Set<Pensioner>();
    public DbSet<WithdrawalRequest> Withdrawals => Set<WithdrawalRequest>();
    public DbSet<LifeVerification> LifeVerifications => Set<LifeVerification>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Pensioner>().HasIndex(p => p.PensionId).IsUnique();
        b.Entity<Pensioner>().Property(p => p.Phone).HasMaxLength(20);
        b.Entity<Pensioner>().HasIndex(p => p.Phone).IsUnique();
        b.Entity<Pensioner>().HasIndex(p => p.PensionCode).IsUnique();
        b.Entity<Pensioner>().HasIndex(p => p.Nid).IsUnique();
        b.Entity<Pensioner>().Property(p => p.MonthlyPension).HasPrecision(18, 2);
        b.Entity<WithdrawalRequest>().Property(w => w.Amount).HasPrecision(18, 2);
        b.Entity<Pensioner>().Property(p => p.FaceReference).HasColumnType("longtext");
    }
}
