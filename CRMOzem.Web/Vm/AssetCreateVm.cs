namespace CRMOzem.Web.Vm
{
    public sealed class AssetCreateVm
    {
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Model { get; set; } = "";
        public string? SerialNumber { get; set; }
        public string? InventoryTag { get; set; }
        public string? ImeiNo { get; set; }
        public string? BarcodeNo { get; set; }
    }
}
