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
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductsController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Products
        public async Task<IActionResult> Index()
        {
            return View(await _context.Products.ToListAsync());
        }

        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // GET: Products/Create
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,Stock,SKU,ImageFile,IsActive")] Product product)
        {
            // Validación adicional para precio y stock
            if (product.Price <= 0)
            {
                ModelState.AddModelError("Price", "El precio debe ser mayor que cero");
            }
            if (product.Stock < 0)
            {
                ModelState.AddModelError("Stock", "El stock no puede ser negativo");
            }

            // Validación de SKU único si se proporciona
            if (!string.IsNullOrEmpty(product.SKU) && await _context.Products.AnyAsync(p => p.SKU == product.SKU))
            {
                ModelState.AddModelError("SKU", "Este SKU ya está en uso");
            }

            if (ModelState.IsValid)
            {
                if (product.ImageFile != null)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Asegurarse de que el directorio existe
                    Directory.CreateDirectory(uploadsFolder);

                    // Validar el tipo de archivo usando el contenido real
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(product.ImageFile.ContentType.ToLower()))
                    {
                        ModelState.AddModelError("ImageFile", "Solo se permiten archivos de imagen (JPEG, PNG, GIF)");
                        return View(product);
                    }

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await product.ImageFile.CopyToAsync(fileStream);
                    }

                    product.ImageUrl = "/images/products/" + uniqueFileName;
                }

                product.CreatedAt = DateTime.UtcNow;
                _context.Add(product);
                
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Producto creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Ha ocurrido un error al guardar el producto. Por favor, intente nuevamente.");
                }
            }
            return View(product);
        }

        // GET: Products/Edit/5
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin, UserRole.Empleado)]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,Stock,SKU,ImageFile,IsActive")] Product product)
        {
            if (id != product.Id)
            {
                return NotFound();
            }

            // Validación adicional para precio y stock
            if (product.Price <= 0)
            {
                ModelState.AddModelError("Price", "El precio debe ser mayor que cero");
            }
            if (product.Stock < 0)
            {
                ModelState.AddModelError("Stock", "El stock no puede ser negativo");
            }

            // Validación de SKU único si se proporciona
            if (!string.IsNullOrEmpty(product.SKU))
            {
                var existingSku = await _context.Products
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.SKU == product.SKU && p.Id != id);
                if (existingSku != null)
                {
                    ModelState.AddModelError("SKU", "Este SKU ya está en uso");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingProduct = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    if (product.ImageFile != null)
                    {
                        // Validar el tipo de archivo usando el contenido real
                        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                        if (!allowedTypes.Contains(product.ImageFile.ContentType.ToLower()))
                        {
                            ModelState.AddModelError("ImageFile", "Solo se permiten archivos de imagen (JPEG, PNG, GIF)");
                            return View(product);
                        }

                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + product.ImageFile.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Asegurarse de que el directorio existe
                        Directory.CreateDirectory(uploadsFolder);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await product.ImageFile.CopyToAsync(fileStream);
                        }

                        // Eliminar imagen anterior si existe
                        if (!string.IsNullOrEmpty(existingProduct.ImageUrl))
                        {
                            var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        product.ImageUrl = "/images/products/" + uniqueFileName;
                    }
                    else
                    {
                        product.ImageUrl = existingProduct.ImageUrl;
                    }

                    product.UpdatedAt = DateTime.UtcNow;
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Producto actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Ha ocurrido un error al actualizar el producto. Por favor, intente nuevamente.");
                    }
                }
            }
            return View(product);
        }

        // GET: Products/Delete/5
        [AuthorizeRoles(UserRole.Admin)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(m => m.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [AuthorizeRoles(UserRole.Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                // Eliminar imagen si existe
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    var filePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}