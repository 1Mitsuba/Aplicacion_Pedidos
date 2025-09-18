using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.Enums;
using Aplicacion_Pedidos.Models.ViewModels;
using Aplicacion_Pedidos.Filters;

namespace Aplicacion_Pedidos.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Orders
        public async Task<IActionResult> Index(OrderSearchViewModel searchModel)
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
                if (searchModel.UserId.HasValue)
                {
                    query = query.Where(o => o.UserId == searchModel.UserId);
                }
            }

            // Filtros
            if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
            {
                query = query.Where(o =>
                    o.User.Name.Contains(searchModel.SearchTerm) ||
                    o.OrderItems.Any(oi => oi.Product.Name.Contains(searchModel.SearchTerm) ||
                                         oi.Product.SKU.Contains(searchModel.SearchTerm)) ||
                    o.Notes.Contains(searchModel.SearchTerm) ||
                    o.ShippingAddress.Contains(searchModel.SearchTerm));
            }

            if (searchModel.Status.HasValue)
            {
                query = query.Where(o => o.Status == searchModel.Status.Value);
            }

            if (searchModel.StartDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date >= searchModel.StartDate.Value.Date);
            }

            if (searchModel.EndDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date <= searchModel.EndDate.Value.Date);
            }

            if (searchModel.MinTotal.HasValue)
            {
                query = query.Where(o => o.Total >= searchModel.MinTotal.Value);
            }

            if (searchModel.MaxTotal.HasValue)
            {
                query = query.Where(o => o.Total <= searchModel.MaxTotal.Value);
            }

            // Ordenamiento
            query = searchModel.SortBy?.ToLower() switch
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

            searchModel.Orders = await query.ToListAsync();

            // Preparar datos para filtros
            if (!User.IsInRole(UserRole.Cliente.ToString()))
            {
                ViewBag.Users = await _context.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Name)
                    .ToListAsync();
            }

            ViewBag.MinTotal = await _context.Orders.MinAsync(o => o.Total);
            ViewBag.MaxTotal = await _context.Orders.MaxAsync(o => o.Total);

            return View(searchModel);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
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

            return View(order);
        }

        // GET: Orders/Create
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Create()
        {
            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.Stock > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(new Order
            {
                OrderDate = DateTime.Now,
                Status = OrderStatus.Pendiente
            });
        }

        // POST: Orders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Create([Bind("UserId,OrderDate,Status,Notes,ShippingAddress")] Order order, List<OrderItem> items)
        {
            if (!ModelState.IsValid || items?.Any() != true)
            {
                ModelState.AddModelError("", "Debe agregar al menos un producto al pedido.");
                await PrepareViewBagForCreate();
                return View(order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Verificar y actualizar stock en una sola operación
                var productIds = items.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // Validar productos y stock
                foreach (var item in items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        ModelState.AddModelError("", $"Producto no encontrado (ID: {item.ProductId})");
                        return View(order);
                    }

                    if (!product.IsActive)
                    {
                        ModelState.AddModelError("", $"El producto {product.Name} no está activo");
                        return View(order);
                    }

                    if (item.Quantity <= 0)
                    {
                        ModelState.AddModelError("", $"La cantidad debe ser mayor que 0 para {product.Name}");
                        return View(order);
                    }

                    if (item.Quantity > product.Stock)
                    {
                        ModelState.AddModelError("", $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}");
                        return View(order);
                    }

                    // Configurar item y actualizar stock
                    item.UnitPrice = product.Price;
                    item.CalculateSubtotal();
                    product.Stock -= item.Quantity;
                    _context.Update(product);
                }

                // Calcular total del pedido
                order.Total = items.Sum(i => i.Subtotal);
                order.CreatedAt = DateTime.UtcNow;
                order.OrderItems = items;

                _context.Add(order);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Pedido creado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Ha ocurrido un error al crear el pedido. " + ex.Message);
                await PrepareViewBagForCreate();
                return View(order);
            }
        }

        // GET: Orders/Edit/5
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(order);
        }

        // POST: Orders/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,OrderDate,Status,Notes,ShippingAddress")] Order order, List<OrderItem> items)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid || items?.Any() != true)
            {
                await PrepareViewBagForEdit();
                return View(order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (existingOrder == null)
                {
                    return NotFound();
                }

                // Si el pedido está cancelado o entregado, no permitir cambios
                if (existingOrder.Status == OrderStatus.Cancelado || existingOrder.Status == OrderStatus.Entregado)
                {
                    ModelState.AddModelError("", "No se pueden modificar pedidos cancelados o entregados");
                    await PrepareViewBagForEdit();
                    return View(order);
                }

                // Restaurar stock de productos existentes
                foreach (var item in existingOrder.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.Stock += item.Quantity;
                        _context.Update(item.Product);
                    }
                }

                // Verificar y actualizar stock de nuevos productos
                var productIds = items.Select(i => i.ProductId).Distinct().ToList();
                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // Validar productos y stock
                foreach (var item in items)
                {
                    if (!products.TryGetValue(item.ProductId, out var product))
                    {
                        ModelState.AddModelError("", $"Producto no encontrado (ID: {item.ProductId})");
                        await PrepareViewBagForEdit();
                        return View(order);
                    }

                    if (!product.IsActive)
                    {
                        ModelState.AddModelError("", $"El producto {product.Name} no está activo");
                        await PrepareViewBagForEdit();
                        return View(order);
                    }

                    if (item.Quantity <= 0)
                    {
                        ModelState.AddModelError("", $"La cantidad debe ser mayor que 0 para {product.Name}");
                        await PrepareViewBagForEdit();
                        return View(order);
                    }

                    if (item.Quantity > product.Stock)
                    {
                        ModelState.AddModelError("", $"Stock insuficiente para {product.Name}. Disponible: {product.Stock}");
                        await PrepareViewBagForEdit();
                        return View(order);
                    }

                    // Configurar item y actualizar stock
                    item.UnitPrice = product.Price;
                    item.CalculateSubtotal();
                    product.Stock -= item.Quantity;
                    _context.Update(product);
                }

                // Eliminar items existentes
                _context.OrderItems.RemoveRange(existingOrder.OrderItems);

                // Actualizar campos básicos
                existingOrder.UserId = order.UserId;
                existingOrder.OrderDate = order.OrderDate;
                existingOrder.Status = order.Status;
                existingOrder.Notes = order.Notes;
                existingOrder.ShippingAddress = order.ShippingAddress;
                existingOrder.UpdatedAt = DateTime.UtcNow;
                existingOrder.Total = items.Sum(i => i.Subtotal);
                existingOrder.OrderItems = items;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Pedido actualizado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", "Ha ocurrido un error al actualizar el pedido: " + ex.Message);
                await PrepareViewBagForEdit();
                return View(order);
            }
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order != null)
                {
                    // Restaurar stock de productos
                    foreach (var item in order.OrderItems)
                    {
                        if (item.Product != null)
                        {
                            item.Product.Stock += item.Quantity;
                            _context.Update(item.Product);
                        }
                    }

                    _context.Orders.Remove(order);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    TempData["Success"] = "Pedido eliminado exitosamente.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al eliminar el pedido: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Orders/MyOrders
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> MyOrders(OrderSearchViewModel searchModel)
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            var query = _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
            {
                query = query.Where(o =>
                    o.OrderItems.Any(oi => oi.Product.Name.Contains(searchModel.SearchTerm) ||
                                         oi.Product.SKU.Contains(searchModel.SearchTerm)) ||
                    o.Notes.Contains(searchModel.SearchTerm) ||
                    o.ShippingAddress.Contains(searchModel.SearchTerm));
            }

            if (searchModel.Status.HasValue)
            {
                query = query.Where(o => o.Status == searchModel.Status.Value);
            }

            if (searchModel.StartDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date >= searchModel.StartDate.Value.Date);
            }

            if (searchModel.EndDate.HasValue)
            {
                query = query.Where(o => o.OrderDate.Date <= searchModel.EndDate.Value.Date);
            }

            // Ordenamiento
            query = searchModel.SortBy?.ToLower() switch
            {
                "date_asc" => query.OrderBy(o => o.OrderDate),
                "total_asc" => query.OrderBy(o => o.Total),
                "total_desc" => query.OrderByDescending(o => o.Total),
                "status_asc" => query.OrderBy(o => o.Status),
                "status_desc" => query.OrderByDescending(o => o.Status),
                _ => query.OrderByDescending(o => o.OrderDate)
            };

            searchModel.Orders = await query.ToListAsync();

            return View("Index", searchModel);
        }

        // GET: Orders/Statistics
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Statistics()
        {
            var statistics = new OrderStatisticsViewModel();
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            // Totales generales
            statistics.TotalOrders = await _context.Orders.CountAsync();
            statistics.TotalRevenue = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelado)
                .SumAsync(o => o.Total);
            statistics.TotalProducts = await _context.Products.CountAsync();
            statistics.TotalCustomers = await _context.Users
                .Where(u => u.Role == UserRole.Cliente)
                .CountAsync();

            // Pedidos por estado
            statistics.PendingOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Pendiente);
            statistics.ProcessingOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Procesando);
            statistics.ShippedOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Enviado);
            statistics.DeliveredOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Entregado);
            statistics.CanceledOrders = await _context.Orders
                .CountAsync(o => o.Status == OrderStatus.Cancelado);

            // Estadísticas adicionales
            var validOrders = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelado)
                .ToListAsync();

            if (validOrders.Any())
            {
                statistics.AverageOrderValue = validOrders.Average(o => o.Total);
                statistics.HighestOrderValue = validOrders.Max(o => o.Total);
                statistics.LowestOrderValue = validOrders.Min(o => o.Total);
            }

            statistics.TotalItemsSold = await _context.OrderItems
                .Where(oi => oi.Order.Status != OrderStatus.Cancelado)
                .SumAsync(oi => oi.Quantity);

            // Ingresos por período
            statistics.RevenueToday = await _context.Orders
                .Where(o => o.OrderDate.Date == today && o.Status != OrderStatus.Cancelado)
                .SumAsync(o => o.Total);

            statistics.RevenueThisWeek = await _context.Orders
                .Where(o => o.OrderDate >= startOfWeek && o.Status != OrderStatus.Cancelado)
                .SumAsync(o => o.Total);

            statistics.RevenueThisMonth = await _context.Orders
                .Where(o => o.OrderDate >= startOfMonth && o.Status != OrderStatus.Cancelado)
                .SumAsync(o => o.Total);

            // Productos más vendidos (top 5)
            statistics.TopProducts = await _context.OrderItems
                .Where(oi => oi.Order.Status != OrderStatus.Cancelado)
                .GroupBy(oi => new { oi.ProductId, oi.Product.Name, oi.Product.SKU })
                .Select(g => new TopProductViewModel
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    SKU = g.Key.SKU,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.Subtotal)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(5)
                .ToListAsync();

            // Clientes más frecuentes (top 5)
            statistics.TopCustomers = await _context.Orders
                .Where(o => o.Status != OrderStatus.Cancelado)
                .GroupBy(o => new { o.UserId, o.User.Name })
                .Select(g => new TopCustomerViewModel
                {
                    UserId = g.Key.UserId,
                    CustomerName = g.Key.Name,
                    OrderCount = g.Count(),
                    TotalSpent = g.Sum(o => o.Total)
                })
                .OrderByDescending(c => c.OrderCount)
                .Take(5)
                .ToListAsync();

            return View(statistics);
        }

        private async Task PrepareViewBagForCreate()
        {
            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.Stock > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        private async Task PrepareViewBagForEdit()
        {
            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }
    }
}