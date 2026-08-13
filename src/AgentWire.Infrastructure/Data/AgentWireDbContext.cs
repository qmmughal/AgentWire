using AgentWire.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Infrastructure.Data
{
    public class AgentWireDbContext : DbContext
    {
        public AgentWireDbContext(DbContextOptions<AgentWireDbContext> options) : base(options)
        {
        }

        public DbSet<AIPacket> AIPackets { get; set; } = null!;
        public DbSet<Organization> Organizations { get; set; } = null!;
        public DbSet<AppUser> AppUsers { get; set; } = null!;
        public DbSet<ApiKey> ApiKeys { get; set; } = null!;
        public DbSet<SecurityFinding> SecurityFindings { get; set; } = null!;
        public DbSet<ReplayResult> ReplayResults { get; set; } = null!;
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AIPacket>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TraceId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.OrganizationId);
            });

            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Slug).IsUnique();
            });

            modelBuilder.Entity<AppUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasOne<Organization>()
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ApiKey>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.KeyHash).IsUnique();
                entity.HasOne<Organization>()
                    .WithMany()
                    .HasForeignKey(e => e.OrganizationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SecurityFinding>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AIPacketId);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasOne<AIPacket>()
                    .WithMany()
                    .HasForeignKey(e => e.AIPacketId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ReplayResult>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OriginalPacketId);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasOne<AIPacket>()
                    .WithMany()
                    .HasForeignKey(e => e.OriginalPacketId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<AuditLogEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.EventType);
            });
        }
    }
}
