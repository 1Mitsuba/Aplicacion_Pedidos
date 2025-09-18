using System.ComponentModel.DataAnnotations;
using Aplicacion_Pedidos.Models.Enums;

namespace Aplicacion_Pedidos.Models.ViewModels
{
    public class OrderSearchViewModel
    {
        [Display(Name = "Buscar")]
        public string? SearchTerm { get; set; }

        [Display(Name = "Estado")]
        public OrderStatus? Status { get; set; }

        [Display(Name = "Cliente")]
        public int? UserId { get; set; }

        [Display(Name = "Desde")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Hasta")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Monto Mínimo")]
        public decimal? MinTotal { get; set; }

        [Display(Name = "Monto Máximo")]
        public decimal? MaxTotal { get; set; }

        [Display(Name = "Ordenar por")]
        public string? SortBy { get; set; }

        public IEnumerable<Order>? Orders { get; set; }
    }
}