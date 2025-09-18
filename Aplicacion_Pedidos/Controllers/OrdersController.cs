using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models;
using Aplicacion_Pedidos.Models.Enums;
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
        public async Task<IActionResult> Index()
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

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
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

            if (!ModelState.IsValid)
            {
                await PrepareViewBagForEdit();
                return View(order);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingOrder = await _context.Orders
                    .Include(o => o.OrderItems)
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

                // Actualizar campos básicos
                existingOrder.UserId = order.UserId;
                existingOrder.OrderDate = order.OrderDate;
                existingOrder.Status = order.Status;
                existingOrder.Notes = order.Notes;
                existingOrder.ShippingAddress = order.ShippingAddress;
                existingOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Pedido actualizado exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                if (!OrderExists(order.Id))
                {
                    return NotFound();
                }
                else
                {
                    ModelState.AddModelError("", "Ha ocurrido un error al actualizar el pedido.");
                    await PrepareViewBagForEdit();
                    return View(order);
                }
            }
        }

        // GET: Orders/Delete/5
        [AuthorizeRoles(UserRole.Admin)]
        public async Task<IActionResult> Delete(int? id)
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

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
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
                TempData["Success"] = "Pedido eliminado exitosamente.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.Id == id);
        }

        // GET: Orders/MyOrders
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> MyOrders()
        {
            var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View("Index", orders);
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