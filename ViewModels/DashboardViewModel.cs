using CommunityToolkit.Mvvm.ComponentModel;
using InvoiceApp.Models;
using InvoiceApp.Services;
using System;
using System.Linq;

namespace InvoiceApp.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DocumentService _service = new();

        [ObservableProperty] private decimal totalRevenueYear;
        [ObservableProperty] private decimal totalRevenueMonth;
        [ObservableProperty] private decimal totalUnpaid;
        [ObservableProperty] private int countInvoicesYear;
        [ObservableProperty] private int countOrdersYear;

        public DashboardViewModel()
        {
            LoadStats();
        }

        public async void LoadStats()
        {
            var docs = await _service.LoadAsync();
            var now = DateTime.Now;

            var invoices = docs.Where(d => d.Type == DocType.Invoice).ToList();
            var orders = docs.Where(d => d.Type == DocType.Order).ToList();

            // Celkem za tento rok (faktury)
            TotalRevenueYear = invoices
                .Where(i => i.IssueDate.Year == now.Year)
                .Sum(i => i.Total);

            // Celkem za tento měsíc (faktury)
            TotalRevenueMonth = invoices
                .Where(i => i.IssueDate.Year == now.Year && i.IssueDate.Month == now.Month)
                .Sum(i => i.Total);

            // Celkem neuhrazeno (všechny roky)
            TotalUnpaid = invoices
                .Where(i => !i.IsPaid)
                .Sum(i => i.Total);

            // Počty
            CountInvoicesYear = invoices.Count(i => i.IssueDate.Year == now.Year);
            CountOrdersYear = orders.Count(o => o.IssueDate.Year == now.Year);
        }
    }
}
