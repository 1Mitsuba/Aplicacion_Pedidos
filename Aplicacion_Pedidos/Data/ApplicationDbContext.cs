using Microsoft.EntityFrameworkCore;

namespace Aplicacion_Pedidos.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSet properties will be added here as we create our models
    }
}