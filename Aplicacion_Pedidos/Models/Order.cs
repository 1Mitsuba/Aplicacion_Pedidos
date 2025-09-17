using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Aplicacion_Pedidos.Models.Base;
using Aplicacion_Pedidos.Models.Enums;

namespace Aplicacion_Pedidos.Models
{
    public class Order : BaseEntity
    {
        [Required(ErrorMessage = "El cliente es requerido")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "La fecha de pedido es requerida")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Fecha de Pedido")]
        public DateTime OrderDate { get; set; }

        [Required(ErrorMessage = "El estado es requerido")]
        [Display(Name = "Estado")]
        public OrderStatus Status { get; set; }

        [Required(ErrorMessage = "El total es requerido")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El total debe ser mayor que 0")]
        [Display(Name = "Total")]
        public decimal Total { get; set; }

        [StringLength(500)]
        [Display(Name = "Notas")]
        public string? Notes { get; set; }

        [Display(Name = "Dirección de Envío")]
        [StringLength(200)]
        public string? ShippingAddress { get; set; }

        // Campos calculados y de solo lectura
        [NotMapped]
        public string StatusText => Status.ToString();

        [NotMapped]
        public string StatusClass => Status switch
        {
            OrderStatus.Pendiente => "bg-warning",
            OrderStatus.Procesando => "bg-info",
            OrderStatus.Enviado => "bg-primary",
            OrderStatus.Entregado => "bg-success",
            OrderStatus.Cancelado => "bg-danger",
            _ => "bg-secondary"
        };

        // Relación con los items del pedido
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}