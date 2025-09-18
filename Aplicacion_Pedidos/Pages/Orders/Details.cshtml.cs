using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Aplicacion_Pedidos.Pages.Orders
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Order Order { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            // Verificar acceso
            if (User.IsInRole(UserRole.Cliente.ToString()))
            {
                var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                if (order.UserId != userId)
                {
                    return Forbid();
                }
            }

            Order = order;
            return Page();
        }

        public async Task<IActionResult> OnPostChangeStatusAsync(int id, OrderStatus newStatus)
        {
            if (!User.IsInRole(UserRole.Admin.ToString()) && !User.IsInRole(UserRole.Empleado.ToString()))
            {
                return Forbid();
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                {
                    return NotFound();
                }

                // Validaciones de cambio de estado
                if (order.Status == OrderStatus.Cancelado)
                {
                    TempData["Error"] = "No se puede cambiar el estado de un pedido cancelado.";
                    return RedirectToPage(new { id });
                }

                if (order.Status == OrderStatus.Entregado && newStatus != OrderStatus.Cancelado)
                {
                    TempData["Error"] = "No se puede cambiar el estado de un pedido entregado.";
                    return RedirectToPage(new { id });
                }

                // Validar secuencia lógica de estados
                if (!IsValidStatusTransition(order.Status, newStatus))
                {
                    TempData["Error"] = "La transición de estado solicitada no es válida.";
                    return RedirectToPage(new { id });
                }

                // Manejo especial para cancelación
                if (newStatus == OrderStatus.Cancelado)
                {
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.Stock += item.Quantity;
                            _context.Update(item.Product);
                        }
                    }
                }

                order.Status = newStatus;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Estado del pedido actualizado a {newStatus}.";
                return RedirectToPage(new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al cambiar el estado del pedido: " + ex.Message;
                return RedirectToPage(new { id });
            }
        }

        public bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
        {
            return (currentStatus, newStatus) switch
            {
                (OrderStatus.Pendiente, OrderStatus.Procesando) => true,
                (OrderStatus.Pendiente, OrderStatus.Cancelado) => true,
                (OrderStatus.Procesando, OrderStatus.Enviado) => true,
                (OrderStatus.Procesando, OrderStatus.Cancelado) => true,
                (OrderStatus.Enviado, OrderStatus.Entregado) => true,
                (OrderStatus.Enviado, OrderStatus.Cancelado) => true,
                (OrderStatus.Entregado, OrderStatus.Cancelado) => true,
                _ => false
            };
        }
    }
}