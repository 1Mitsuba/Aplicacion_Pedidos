using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.Enums;
using Aplicacion_Pedidos.Services.Notifications;
using Aplicacion_Pedidos.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace Aplicacion_Pedidos.Pages.Orders
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IConfiguration _configuration;

        public DetailsModel(ApplicationDbContext context, INotificationService notificationService, IConfiguration configuration)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [BindProperty]
        public Order Order { get; set; } = default!;

        public PaginatedList<Order>? RelatedOrders { get; set; }

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                _notificationService.AddNotification(TempData, "ID de pedido no especificado.", NotificationType.Error);
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                _notificationService.AddNotification(TempData, "Pedido no encontrado.", NotificationType.Error);
                return NotFound();
            }

            // Verificar acceso
            if (User.IsInRole(UserRole.Cliente.ToString()))
            {
                var userIdClaim = User.FindFirst("UserId");
                if (userIdClaim == null)
                {
                    _notificationService.AddNotification(TempData, "No se pudo verificar la identidad del usuario.", NotificationType.Error);
                    return Forbid();
                }

                if (!int.TryParse(userIdClaim.Value, out int userId))
                {
                    _notificationService.AddNotification(TempData, "ID de usuario inválido.", NotificationType.Error);
                    return Forbid();
                }

                if (order.UserId != userId)
                {
                    _notificationService.AddNotification(TempData, "No tiene permiso para ver este pedido.", NotificationType.Warning);
                    return Forbid();
                }
            }

            Order = order;

            // Cargar pedidos relacionados del mismo cliente
            var relatedOrdersQuery = _context.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == order.UserId && o.Id != order.Id)
                .OrderByDescending(o => o.OrderDate);

            RelatedOrders = await PaginatedList<Order>.CreateAsync(
                relatedOrdersQuery, 
                PageIndex, 
                _configuration.GetValue("RelatedOrdersPageSize", 5));

            return Page();
        }

        public async Task<IActionResult> OnPostChangeStatusAsync(int id, OrderStatus newStatus)
        {
            if (!User.IsInRole(UserRole.Admin.ToString()) && !User.IsInRole(UserRole.Empleado.ToString()))
            {
                _notificationService.AddNotification(TempData, "No tiene permisos para realizar esta acción.", NotificationType.Error);
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
                    _notificationService.AddNotification(TempData, "El pedido no fue encontrado.", NotificationType.Error);
                    return NotFound();
                }

                // Validaciones de cambio de estado
                if (order.Status == OrderStatus.Cancelado)
                {
                    _notificationService.AddNotification(TempData, "No se puede cambiar el estado de un pedido cancelado.", NotificationType.Warning);
                    return RedirectToPage(new { id });
                }

                if (order.Status == OrderStatus.Entregado && newStatus != OrderStatus.Cancelado)
                {
                    _notificationService.AddNotification(TempData, "No se puede cambiar el estado de un pedido entregado.", NotificationType.Warning);
                    return RedirectToPage(new { id });
                }

                // Validar secuencia lógica de estados
                if (!IsValidStatusTransition(order.Status, newStatus))
                {
                    _notificationService.AddNotification(TempData, $"La transición de estado de {order.Status} a {newStatus} no es válida.", NotificationType.Warning);
                    return RedirectToPage(new { id });
                }

                // Manejo especial para cancelación
                if (newStatus == OrderStatus.Cancelado)
                {
                    foreach (var item in order.OrderItems.Where(oi => oi.Product != null))
                    {
                        item.Product!.Stock += item.Quantity;
                        _context.Update(item.Product);
                    }
                    _notificationService.AddNotification(TempData, "Stock restaurado para todos los productos del pedido.", NotificationType.Info);
                }

                var oldStatus = order.Status;
                order.Status = newStatus;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _notificationService.AddNotification(TempData, $"Estado del pedido actualizado exitosamente de {oldStatus} a {newStatus}.", NotificationType.Success);

                // Notificación específica según el estado
                switch (newStatus)
                {
                    case OrderStatus.Procesando:
                        _notificationService.AddNotification(TempData, "El pedido ha entrado en proceso de preparación.", NotificationType.Info);
                        break;
                    case OrderStatus.Enviado:
                        _notificationService.AddNotification(TempData, "El pedido ha sido enviado al cliente.", NotificationType.Info);
                        break;
                    case OrderStatus.Entregado:
                        _notificationService.AddNotification(TempData, "¡Pedido entregado exitosamente!", NotificationType.Success);
                        break;
                    case OrderStatus.Cancelado:
                        _notificationService.AddNotification(TempData, "El pedido ha sido cancelado y el stock ha sido restaurado.", NotificationType.Warning);
                        break;
                }

                return RedirectToPage(new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _notificationService.AddNotification(TempData, $"Error al cambiar el estado del pedido: {ex.Message}", NotificationType.Error);
                return RedirectToPage(new { id });
            }
        }

        private static bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
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