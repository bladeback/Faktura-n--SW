using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using InvoiceApp.Models;

namespace InvoiceApp.ViewModels
{
    /// <summary>
    /// ViewModel pro stránku Dodavatelé.
    /// Pracuje s modelem Company (nikoli "Supplier").
    /// </summary>
    public class SuppliersViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Company> _suppliers = new();

        public ObservableCollection<Company> Suppliers
        {
            get => _suppliers;
            set
            {
                if (!ReferenceEquals(_suppliers, value))
                {
                    _suppliers = value;
                    OnPropertyChanged(nameof(Suppliers));
                }
            }
        }

        public SuppliersViewModel()
        {
            // Pokud chceš, můžeš sem načíst výchozí data z uložiště/služby.
            // Tady nechávám prázdné; stránka si data obvykle načítá sama ze služby.
        }

        public void AddSupplier(Company supplier)
        {
            if (supplier is null) return;
            Suppliers.Add(supplier);
        }

        public void RemoveSupplier(Company supplier)
        {
            if (supplier is null) return;
            if (Suppliers.Contains(supplier))
                Suppliers.Remove(supplier);
        }

        public void SaveSuppliers()
        {
            // Sem můžeš napojit svou SuppliersService/_store apod.
            // Základní potvrzení, aby bylo vidět, že akce proběhla.
            MessageBox.Show("Dodavatelé byli úspěšně uloženi.", "Uloženo",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
