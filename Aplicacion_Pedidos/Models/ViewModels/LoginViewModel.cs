using System.ComponentModel.DataAnnotations;

namespace Aplicacion_Pedidos.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Formato de email inv�lido")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "La contrase�a es requerida")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        public bool RememberMe { get; set; }
    }
}