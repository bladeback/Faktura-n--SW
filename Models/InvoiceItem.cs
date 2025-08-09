namespace InvoiceApp.Models
{
    public class InvoiceItem
    {
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "ks";
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal VatRate { get; set; } // e.g. 0, 0.12, 0.21
        public decimal LineNet => Quantity * UnitPrice;
        public decimal LineVat => decimal.Round(LineNet * VatRate, 2);
        public decimal LineTotal => LineNet + LineVat;
    }
}
