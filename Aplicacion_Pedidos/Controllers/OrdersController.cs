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
            if (ModelState.IsValid && items?.Any() == true)
            {
                try
                {
                    order.CreatedAt = DateTime.UtcNow;
                    order.Total = 0;

                    foreach (var item in items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product == null || item.Quantity <= 0 || item.Quantity > product.Stock)
                        {
                            ModelState.AddModelError("", "Producto no válido o cantidad insuficiente");
                            return View(order);
                        }

                        item.UnitPrice = product.Price;
                        item.CalculateSubtotal();
                        order.Total += item.Subtotal;
                        
                        // Actualizar stock
                        product.Stock -= item.Quantity;
                        _context.Update(product);
                    }

                    order.OrderItems = items;
                    _context.Add(order);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Pedido creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Ha ocurrido un error al crear el pedido.");
                }
            }

            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive && p.Stock > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(order);
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
        public async Task<IActionResult> Edit(int id, [Bind("Id,UserId,OrderDate,Status,Notes,ShippingAddress")] Order order)
        {
            if (id != order.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingOrder = await _context.Orders
                        .Include(o => o.OrderItems)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == id);

                    if (existingOrder == null)
                    {
                        return NotFound();
                    }

                    order.Total = existingOrder.Total;
                    order.UpdatedAt = DateTime.UtcNow;
                    _context.Update(order);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Pedido actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Ha ocurrido un error al actualizar el pedido.");
                    }
                }
            }

            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .ToListAsync();

            return View(order);
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
    }}