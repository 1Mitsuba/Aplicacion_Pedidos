using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Aplicacion_Pedidos.Data;
using Aplicacion_Pedidos.Models.Base;

namespace Aplicacion_Pedidos.Models
{
    public class OrderItem : BaseEntity, IValidatableObject
    {
        [Required(ErrorMessage = "El pedido es requerido")]
        [Display(Name = "Pedido")]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [Required(ErrorMessage = "El producto es requerido")]
        [Display(Name = "Producto")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [Required(ErrorMessage = "La cantidad es requerida")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que 0")]
        [Display(Name = "Cantidad")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "El precio unitario es requerido")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio unitario debe ser mayor que 0")]
        [Display(Name = "Precio Unitario")]
        [DataType(DataType.Currency)]
        public decimal UnitPrice { get; set; }

        [Required(ErrorMessage = "El subtotal es requerido")]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal Subtotal { get; set; }

        // Propiedades de navegación adicionales
        [NotMapped]
        [Display(Name = "Nombre del Producto")]
        public string ProductName => Product?.Name ?? "Producto no disponible";

        [NotMapped]
        [Display(Name = "SKU")]
        public string? ProductSKU => Product?.SKU;

        [NotMapped]
        [Display(Name = "¿En Stock?")]
        public bool HasStock => Product?.Stock >= Quantity;

        [NotMapped]
        public string StockStatusClass => HasStock ? "text-success" : "text-danger";

        // Métodos
        public void CalculateSubtotal()
        {
            Subtotal = Quantity * UnitPrice;
        }

        public bool ValidateStock()
        {
            return Product?.Stock >= Quantity;
        }

        public void SetupFromProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));

            ProductId = product.Id;
            UnitPrice = product.Price;
            Product = product;
        }

        // Método para actualizar el stock del producto
        public async Task UpdateProductStock(ApplicationDbContext context, bool isNewItem = true)
        {
            if (Product == null)
                Product = await context.Products.FindAsync(ProductId);

            if (Product != null)
            {
                if (isNewItem)
                {
                    // Si es un item nuevo, reducir el stock
                    Product.Stock -= Quantity;
                }
                else
                {
                    // Si es una actualización, ajustar la diferencia
                    var originalItem = await context.OrderItems.AsNoTracking()
                        .FirstOrDefaultAsync(oi => oi.Id == Id);
                    if (originalItem != null)
                    {
                        int difference = Quantity - originalItem.Quantity;
                        Product.Stock -= difference;
                    }
                }

                context.Update(Product);
            }
        }

        // Validaciones adicionales
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var context = (ApplicationDbContext)validationContext.GetService(typeof(ApplicationDbContext));

            if (context != null)
            {
                var product = context.Products.Find(ProductId);
                if (product != null)
                {
                    if (Quantity > product.Stock)
                    {
                        yield return new ValidationResult(
                            $"No hay suficiente stock. Stock disponible: {product.Stock}",
                            new[] { nameof(Quantity) });
                    }

                    if (UnitPrice != product.Price)
                    {
                        yield return new ValidationResult(
                            "El precio unitario no coincide con el precio actual del producto",
                            new[] { nameof(UnitPrice) });
                    }
                }
            }
        }
    }
}