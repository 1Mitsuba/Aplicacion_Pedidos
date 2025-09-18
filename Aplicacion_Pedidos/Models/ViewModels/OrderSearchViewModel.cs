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
        [Range(0, double.MaxValue, ErrorMessage = "El monto mínimo debe ser mayor o igual a 0")]
        public decimal? MinTotal { get; set; }

        [Display(Name = "Monto Máximo")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto máximo debe ser mayor o igual a 0")]
        public decimal? MaxTotal { get; set; }

        [Display(Name = "Ordenar por")]
        public string SortBy { get; set; } = "date";

        [Display(Name = "Dirección")]
        public string SortDirection { get; set; } = "desc";

        public int PageSize { get; set; } = 10;
        public int PageIndex { get; set; } = 1;

        public PaginatedList<Order>? Orders { get; set; }

        public string GetSortIcon(string column)
        {
            if (string.IsNullOrEmpty(SortBy) || !SortBy.Equals(column, StringComparison.OrdinalIgnoreCase))
                return "";

            return SortDirection?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "?" : "?";
        }

        public string GetSortDirection(string column)
        {
            if (string.IsNullOrEmpty(SortBy) || !SortBy.Equals(column, StringComparison.OrdinalIgnoreCase))
                return "asc";

            return SortDirection?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? "desc" : "asc";
        }
    }
}