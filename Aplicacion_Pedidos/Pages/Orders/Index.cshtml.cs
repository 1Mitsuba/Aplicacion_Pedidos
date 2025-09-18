using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.ViewModels;
using Aplicacion_Pedidos.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Aplicacion_Pedidos.Pages.Orders
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public OrderSearchViewModel SearchModel { get; set; } = new();
        public List<User> Users { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .AsQueryable();

            // Si es cliente, solo ver sus pedidos
            if (User.IsInRole(UserRole.Cliente.ToString()))
            {
                var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                query = query.Where(o => o.UserId == userId);
            }
            else
            {
                // Filtro por cliente (solo para admin y empleados)
                if (SearchModel.UserId.HasValue)
                {
                    query = query.Where(o => o.UserId == SearchModel.UserId);
                }

                // Cargar usuarios para el filtro
                Users = await _context.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Name)
                    .ToListAsync();
            }

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(SearchModel.SearchTerm))
            {
                query = query.Where(o =>
                    o.User.Name.Contains(SearchModel.SearchTerm) ||
                    o.OrderItems.Any(oi => oi.Product.Name.Contains(SearchModel.SearchTerm) ||
                                         oi.Product.SKU.Contains(SearchModel.SearchTerm)) ||
                    o.Notes.Contains(SearchModel.SearchTerm) ||
                    o.ShippingAddress.Contains(SearchModel.SearchTerm));
            }

            if (SearchModel.Status.HasValue)
            {
                query = query.Where(o => o.Status == SearchModel.Status.Value);
            }

            if (SearchModel.StartDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date >= SearchModel.StartDate.Value.Date);
            }

            if (SearchModel.EndDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date <= SearchModel.EndDate.Value.Date);
            }

            if (SearchModel.MinTotal.HasValue)
            {
                query = query.Where(o => o.Total >= SearchModel.MinTotal.Value);
            }

            if (SearchModel.MaxTotal.HasValue)
            {
                query = query.Where(o => o.Total <= SearchModel.MaxTotal.Value);
            }

            // Ordenamiento
            query = SearchModel.SortBy?.ToLower() switch
            {
                "date_asc" => query.OrderBy(o => o.OrderDate),
                "date_desc" => query.OrderByDescending(o => o.OrderDate),
                "total_asc" => query.OrderBy(o => o.Total),
                "total_desc" => query.OrderByDescending(o => o.Total),
                "status_asc" => query.OrderBy(o => o.Status),
                "status_desc" => query.OrderByDescending(o => o.Status),
                "customer_asc" => query.OrderBy(o => o.User.Name),
                "customer_desc" => query.OrderByDescending(o => o.User.Name),
                _ => query.OrderByDescending(o => o.OrderDate)
            };

            SearchModel.Orders = await query.ToListAsync();

            return Page();
        }
    }
}