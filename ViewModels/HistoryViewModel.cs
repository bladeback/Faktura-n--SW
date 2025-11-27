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

        public ObservableCollection<Invoice> Documents { get; } = new();

        [ObservableProperty]
        private Invoice? selectedDocument;

        public HistoryViewModel()
        {
            LoadData();
        }

        private async void LoadData()
        {
            var list = await _service.LoadAsync();
            Documents.Clear();
            foreach (var doc in list) Documents.Add(doc);
        }

        [RelayCommand]
        private async Task MarkAsPaid(Invoice? doc)
        {
            if (doc == null) return;
            doc.IsPaid = !doc.IsPaid;
            await _service.SaveDocumentAsync(doc); // Uloží změnu stavu
        }

        public event System.Action<Invoice>? RequestEdit;

        [RelayCommand]
        private void EditDocument(Invoice? doc)
        {
            if (doc != null)
            {
                RequestEdit?.Invoke(doc);
            }
        }
    }
}
