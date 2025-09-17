using System.ComponentModel.DataAnnotations;
using Aplicacion_Pedidos.Models.Base;
using Aplicacion_Pedidos.Models.Enums;

namespace Aplicacion_Pedidos.Models
{
    public class User : BaseEntity
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        [StringLength(150, ErrorMessage = "El email no puede exceder los 150 caracteres")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "El rol es requerido")]
        public UserRole Role { get; set; }

        // Campos adicionales
        [StringLength(15)]
        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        [StringLength(200)]
        public string? Address { get; set; }

        // Campos de auditoría heredados de BaseEntity
        // Id
        // CreatedAt
        // UpdatedAt
    }
}