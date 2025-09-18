using Aplicacion_Pedidos.Models.Enums;

namespace Aplicacion_Pedidos.Models.ViewModels
{
    public class OrderStatisticsViewModel
    {
        // Totales generales
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalProducts { get; set; }
        public int TotalCustomers { get; set; }

        // Pedidos por estado
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CanceledOrders { get; set; }

        // Estadísticas adicionales
        public decimal AverageOrderValue { get; set; }
        public decimal HighestOrderValue { get; set; }
        public decimal LowestOrderValue { get; set; }
        public int TotalItemsSold { get; set; }

        // Períodos
        public decimal RevenueToday { get; set; }
        public decimal RevenueThisWeek { get; set; }
        public decimal RevenueThisMonth { get; set; }

        // Productos más vendidos
        public List<TopProductViewModel> TopProducts { get; set; } = new();

        // Clientes más frecuentes
        public List<TopCustomerViewModel> TopCustomers { get; set; } = new();
    }

    public class TopProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public string? SKU { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class TopCustomerViewModel
    {
        public int UserId { get; set; }
        public string CustomerName { get; set; } = null!;
        public int OrderCount { get; set; }
        public decimal TotalSpent { get; set; }
    }
}