using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data.Extensions;
using Aplicacion_Pedidos.Models;

namespace Aplicacion_Pedidos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.AddAuditProperties();

            // Configuración específica para User
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Aseguramos que el Email se guarde en minúsculas
            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .HasConversion(
                    v => v.ToLowerInvariant(),
                    v => v
                );
        }

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is Models.Base.BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    ((Models.Base.BaseEntity)entry.Entity).CreatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    ((Models.Base.BaseEntity)entry.Entity).UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}