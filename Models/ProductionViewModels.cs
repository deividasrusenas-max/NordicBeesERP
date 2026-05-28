namespace NordicBeesERP.Models
{
    public class NewBatchViewModel
    {
        public string? ProductName { get; set; }
        public decimal? Quantity { get; set; }
        public int WarehouseId { get; set; }
    }

    public class WarehouseStockViewModel
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? WarehouseName { get; set; }
        public string? LotNumber { get; set; }
        public decimal Quantity { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public int WarehouseId { get; set; }
    }


    public class ServiceResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}