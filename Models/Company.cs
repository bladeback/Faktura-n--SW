namespace InvoiceApp.Models
{
    public class Company
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string ICO { get; set; } = string.Empty; // Company ID
        public string DIC { get; set; } = string.Empty; // VAT ID
        public string IBAN { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}
