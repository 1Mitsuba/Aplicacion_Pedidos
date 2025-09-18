using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.ViewModels;
using Aplicacion_Pedidos.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace Aplicacion_Pedidos.Pages.Orders
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public IndexModel(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [BindProperty(SupportsGet = true)]
        public OrderSearchViewModel SearchModel { get; set; } = new();
        public List<User> Users { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            // Configurar tamaño de página desde configuración
            SearchModel.PageSize = _configuration.GetValue("PageSize", 10);

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
            var direction = SearchModel.SortDirection?.ToLower() == "desc" ? "desc" : "asc";
            query = (SearchModel.SortBy?.ToLower(), direction) switch
            {
                ("date", "asc") => query.OrderBy(o => o.OrderDate),
                ("date", "desc") => query.OrderByDescending(o => o.OrderDate),
                ("total", "asc") => query.OrderBy(o => o.Total),
                ("total", "desc") => query.OrderByDescending(o => o.Total),
                ("status", "asc") => query.OrderBy(o => o.Status),
                ("status", "desc") => query.OrderByDescending(o => o.Status),
                ("customer", "asc") => query.OrderBy(o => o.User.Name),
                ("customer", "desc") => query.OrderByDescending(o => o.User.Name),
                _ => query.OrderByDescending(o => o.OrderDate)
            };

            SearchModel.Orders = await PaginatedList<Order>.CreateAsync(
                query, SearchModel.PageIndex, SearchModel.PageSize);

            return Page();
        }
    }
}