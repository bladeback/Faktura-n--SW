using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public enum DocType
    {
        Invoice,
        Order
    }

    public partial class Party : ObservableObject
    {
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string address = string.Empty;
        [ObservableProperty] private string city = string.Empty;
        [ObservableProperty] private string iCO = string.Empty;
        [ObservableProperty] private string dIC = string.Empty;

        [ObservableProperty] private string bank = string.Empty;

        // NOVÉ: domácí číslo účtu (např. 12-3456789012/0100)
        [ObservableProperty] private string accountNumber = string.Empty;

        [ObservableProperty] private string iBAN = string.Empty;

        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string phone = string.Empty;
    }

    public partial class Invoice : ObservableObject
    {
        [ObservableProperty] private DocType type = DocType.Invoice;
        [ObservableProperty] private string number = string.Empty;

        [ObservableProperty] private DateTime issueDate = DateTime.Today;
        [ObservableProperty] private DateTime dueDate = DateTime.Today.AddDays(14);

        [ObservableProperty] private Party supplier = new();
        [ObservableProperty] private Party customer = new();

        // Položky dokladu
        public ObservableCollection<InvoiceItem> Items { get; } = new();

        // Platební a ostatní údaje
        [ObservableProperty] private string variableSymbol = string.Empty;
        [ObservableProperty] private string currency = "CZK";
        [ObservableProperty] private string notes = string.Empty;

        // IBAN pro platbu – bereme ze Suppliera (může být přepsán ve ViewModelu)
        public string PaymentIban => Supplier?.IBAN ?? string.Empty;

        // Souhrny (bez ohledu na plátcovství; to řeší UI/PDF)
        public decimal SubtotalNet => Items.Sum(i => i.Quantity * i.UnitPrice);
        public decimal VatTotal => Items.Sum(i => i.Quantity * i.UnitPrice * i.VatRate);
        public decimal Total => SubtotalNet + VatTotal;
    }
}
