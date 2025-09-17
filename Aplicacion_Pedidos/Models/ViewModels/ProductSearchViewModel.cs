namespace Aplicacion_Pedidos.Models.ViewModels
{
    public class ProductSearchViewModel
    {
        public string? SearchTerm { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? IsActive { get; set; }
        public bool InStock { get; set; }
        public string? SortBy { get; set; }
        public IEnumerable<Product>? Products { get; set; }
    }
}