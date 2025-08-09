using System;
using System.Collections.Generic;
using System.Linq;

namespace InvoiceApp.Models
{
    public enum DocType { Invoice, Order }

    public class Invoice
    {
        public DocType Type { get; set; } = DocType.Invoice;
        public string Number { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; } = DateTime.Today;
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);
        public string VariableSymbol { get; set; } = string.Empty; // VS
        public Company Supplier { get; set; } = new();
        public Company Customer { get; set; } = new();
        public string Currency { get; set; } = "CZK";
        public System.Collections.Generic.List<InvoiceItem> Items { get; set; } = new();
        public string Notes { get; set; } = string.Empty;

        public decimal TotalNet => Items.Sum(i => i.LineNet);
        public decimal TotalVat => Items.Sum(i => i.LineVat);
        public decimal Total => Items.Sum(i => i.LineTotal);

        public string PaymentIban => Supplier.IBAN;
    }
}
