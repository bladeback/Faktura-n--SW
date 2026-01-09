using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace InvoiceApp.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly DocumentService _service = new();
        private readonly PdfService _pdf = new(); // Pro případné otevření PDF (pokud bychom ukládali cestu)
        // Ale my ukládáme jen data. PDF se generuje znovu nebo bychom museli ukládat cestu k PDF.
        // Pro teď jen data.

        public ObservableCollection<Invoice> Invoices { get; } = new();
        public ObservableCollection<Invoice> Orders { get; } = new();

        [ObservableProperty]
        private Invoice? selectedDocument;

        private List<Invoice> AllDocuments = new();

        public ObservableCollection<string> FilterOptions { get; } = new();

        [ObservableProperty]
        private string selectedFilter = "Vše";

        partial void OnSelectedFilterChanged(string value) => ApplyFilter();

        public HistoryViewModel()
        {
            LoadData();
        }

        private async void LoadData()
        {
            var list = await _service.LoadAsync();
            
            // Seřadíme data od nejnovějších
            AllDocuments = list.OrderByDescending(d => d.IssueDate).ToList();

            // Generování možností filtru
            FilterOptions.Clear();
            FilterOptions.Add("Vše");
            
            var today = System.DateTime.Today;
            FilterOptions.Add($"Tento rok ({today.Year})");
            FilterOptions.Add($"Minulý rok ({today.Year - 1})");
            
            // Přidáme měsíce aktuálního roku (od ledna do prosince)
            var cultura = new System.Globalization.CultureInfo("cs-CZ");
            for (int m = 1; m <= 12; m++)
            {
                var monthName = cultura.DateTimeFormat.GetMonthName(m);
                // První písmeno velké
                monthName = char.ToUpper(monthName[0]) + monthName.Substring(1);
                FilterOptions.Add($"{monthName} {today.Year}");
            }

            // Výchozí filtr
            SelectedFilter = "Vše";
            
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Invoices.Clear();
            Orders.Clear();

            IEnumerable<Invoice> filtered = AllDocuments;

            if (SelectedFilter != "Vše")
            {
                var today = System.DateTime.Today;
                var currentYear = today.Year;

                if (SelectedFilter.Contains($"Tento rok ({currentYear})"))
                {
                    filtered = filtered.Where(d => d.IssueDate.Year == currentYear);
                }
                else if (SelectedFilter.Contains($"Minulý rok ({currentYear - 1})"))
                {
                    filtered = filtered.Where(d => d.IssueDate.Year == currentYear - 1);
                }
                else
                {
                    // "Leden 2026", "Únor 2026" atd.
                    // Zkusíme najít měsíc a rok v názvu filtru
                    // Jednoduché parsování: vezmeme rok na konci
                    var parts = SelectedFilter.Split(' ');
                    if (parts.Length >= 2 && int.TryParse(parts.Last(), out int year))
                    {
                        var monthStr = parts.First();
                        var cultura = new System.Globalization.CultureInfo("cs-CZ");
                        // Najdeme číslo měsíce
                        int month = -1;
                        for (int m = 1; m <= 12; m++)
                        {
                            if (string.Equals(cultura.DateTimeFormat.GetMonthName(m), monthStr, System.StringComparison.CurrentCultureIgnoreCase))
                            {
                                month = m;
                                break;
                            }
                        }

                        if (month != -1)
                        {
                            filtered = filtered.Where(d => d.IssueDate.Year == year && d.IssueDate.Month == month);
                        }
                    }
                }
            }

            foreach (var doc in filtered)
            {
                if (doc.Type == DocType.Invoice) Invoices.Add(doc);
                else Orders.Add(doc);
            }
        }

        [RelayCommand]
        private async Task MarkAsPaid(Invoice? doc)
        {
            if (doc == null) return;
            doc.IsPaid = !doc.IsPaid;
            await _service.SaveDocumentAsync(doc); 
        }

        public event System.Action<Invoice>? RequestEdit;
        public event System.Action<Invoice>? RequestConvert;

        [RelayCommand]
        private void EditDocument(Invoice? doc)
        {
            if (doc != null) RequestEdit?.Invoke(doc);
        }

        [RelayCommand]
        private void CreateInvoice(Invoice? order)
        {
            if (order != null) RequestConvert?.Invoke(order);
        }
    }
}
