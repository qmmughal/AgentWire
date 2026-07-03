using AgentWire.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Infrastructure.Data
{
    public class AgentWireDbContext : DbContext
    {
        public AgentWireDbContext(DbContextOptions<AgentWireDbContext> options) : base(options)
        {
        }

        public DbSet<AIPacket> AIPackets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<AIPacket>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TraceId);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}
