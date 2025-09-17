using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aplicacion_Pedidos.Data.Seeders
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

            // Verifica si ya existen usuarios
            if (await context.Users.AnyAsync())
            {
                return; // La base de datos ya tiene usuarios
            }

            var users = new User[]
            {
                new User
                {
                    Name = "Administrador",
                    Email = "admin@example.com",
                    Password = "Admin123!", // En producción usar hash
                    Role = UserRole.Admin,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Name = "Empleado Demo",
                    Email = "empleado@example.com",
                    Password = "Empleado123!",
                    Role = UserRole.Empleado,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                },
                new User
                {
                    Name = "Cliente Demo",
                    Email = "cliente@example.com",
                    Password = "Cliente123!",
                    Role = UserRole.Cliente,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                }
            };

            context.Users.AddRange(users);
            await context.SaveChangesAsync();
        }
    }
}