using Microsoft.EntityFrameworkCore;

namespace Aplicacion_Pedidos.Data.Extensions
{
    public static class ModelBuilderExtensions
    {
        public static void AddAuditProperties(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType.IsSubclassOf(typeof(Models.Base.BaseEntity)))
                {
                    modelBuilder.Entity(entityType.Name)
                        .Property("CreatedAt")
                        .IsRequired();
                }
            }
        }
    }
}