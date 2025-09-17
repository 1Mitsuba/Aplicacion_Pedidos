using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Aplicacion_Pedidos.Models.Base;

namespace Aplicacion_Pedidos.Models
{
    public class Product : BaseEntity
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
        [Display(Name = "Nombre")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "La descripción debe tener entre 10 y 500 caracteres")]
        [Display(Name = "Descripción")]
        public string Description { get; set; } = null!;

        [Required(ErrorMessage = "El precio es requerido")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999.99, ErrorMessage = "El precio debe estar entre $0.01 y $999,999.99")]
        [RegularExpression(@"^\d+(\.\d{1,2})?$", ErrorMessage = "El precio debe tener máximo 2 decimales")]
        [DataType(DataType.Currency)]
        [Display(Name = "Precio")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "El stock es requerido")]
        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo")]
        [Display(Name = "Stock")]
        public int Stock { get; set; }

        [StringLength(50, MinimumLength = 3, ErrorMessage = "El SKU debe tener entre 3 y 50 caracteres")]
        [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "El SKU solo puede contener letras, números y guiones")]
        [Display(Name = "SKU")]
        public string? SKU { get; set; }

        [StringLength(200)]
        [Display(Name = "URL de Imagen")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Estado")]
        public bool IsActive { get; set; } = true;

        [NotMapped]
        [Display(Name = "Imagen")]
        [DataType(DataType.Upload)]
        [AllowedExtensions(new string[] { ".jpg", ".jpeg", ".png", ".gif" }, ErrorMessage = "Solo se permiten archivos de imagen (.jpg, .jpeg, .png, .gif)")]
        [MaxFileSize(5 * 1024 * 1024, ErrorMessage = "El tamaño máximo permitido es 5MB")]
        public IFormFile? ImageFile { get; set; }

        // Propiedades de solo lectura para formato
        [NotMapped]
        public string StatusText => IsActive ? "Activo" : "Inactivo";

        [NotMapped]
        public string StatusClass => IsActive ? "bg-success" : "bg-danger";

        [NotMapped]
        public string StockStatus => Stock > 0 ? "Disponible" : "Agotado";

        [NotMapped]
        public string StockStatusClass => Stock > 0 ? "bg-success" : "bg-danger";
    }

    // Validación personalizada para extensiones de archivo permitidas
    public class AllowedExtensionsAttribute : ValidationAttribute
    {
        private readonly string[] _extensions;

        public AllowedExtensionsAttribute(string[] extensions)
        {
            _extensions = extensions;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is IFormFile file)
            {
                var extension = Path.GetExtension(file.FileName);
                if (!_extensions.Contains(extension.ToLower()))
                {
                    return new ValidationResult(ErrorMessage);
                }
            }
            return ValidationResult.Success;
        }
    }

    // Validación personalizada para tamaño máximo de archivo
    public class MaxFileSizeAttribute : ValidationAttribute
    {
        private readonly int _maxFileSize;

        public MaxFileSizeAttribute(int maxFileSize)
        {
            _maxFileSize = maxFileSize;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is IFormFile file)
            {
                if (file.Length > _maxFileSize)
                {
                    return new ValidationResult(ErrorMessage);
                }
            }
            return ValidationResult.Success;
        }
    }
}