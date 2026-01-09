using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;

namespace InvoiceApp.Views
{
    public partial class CustomersPage : UserControl, System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        // Hlavní kolekce pro DataGrid
        public ObservableCollection<Company> Customers { get; } = new();

        public int Count => Customers.Count;

        // Nezávislá kopie pro filtrování
        private readonly List<Company> _allCustomers = new();

        // Banky pro editor
        private List<Bank> _banks = new();

        private readonly CustomersService _service = new();

        public CustomersPage()
        {
            InitializeComponent();
            DataContext = this;

            _banks = LoadBanksFromJson();
            LoadCustomers();
        }


        // === TLAČÍTKA ===



        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            var model = new Company();

            // Použijeme SupplierEditorWindow, ale s titulkem "Odběratel"
            var dlg = new SupplierEditorWindow(model, _banks, "Odběratel")
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                Customers.Add(model);
                _allCustomers.Add(model);
                OnPropertyChanged(nameof(Count));
                await SaveCustomers();
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e) => Delete_Click(sender, e);

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (CustomersGrid.SelectedItem is not Company selected)
            {
                MessageBox.Show("Vyber odběratele, kterého chceš smazat.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Opravdu smazat \"{selected.Name}\"?",
                                "Smazat odběratele",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            Customers.Remove(selected);
            _allCustomers.Remove(selected);
            OnPropertyChanged(nameof(Count));
            await SaveCustomers();
        }

        private async void Row_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row || row.DataContext is not Company selected) return;

            var copy = new Company
            {
                Name = selected.Name,
                Address = selected.Address,
                City = selected.City,
                PostalCode = selected.PostalCode,
                ICO = selected.ICO,
                DIC = selected.DIC,
                Bank = selected.Bank,
                AccountNumber = selected.AccountNumber,
                IBAN = selected.IBAN,
                Email = selected.Email,
                Phone = selected.Phone,
                IsVatPayer = selected.IsVatPayer
            };

            var dlg = new SupplierEditorWindow(copy, _banks, "Odběratel")
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                selected.Name = copy.Name;
                selected.Address = copy.Address;
                selected.City = copy.City;
                selected.PostalCode = copy.PostalCode;
                selected.ICO = copy.ICO;
                selected.DIC = copy.DIC;
                selected.Bank = copy.Bank;
                selected.AccountNumber = copy.AccountNumber;
                selected.IBAN = copy.IBAN;
                selected.Email = copy.Email;
                selected.Phone = copy.Phone;
                selected.IsVatPayer = copy.IsVatPayer;

                await SaveCustomers();
            }
        }

        // === HLEDÁNÍ ===

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

            Customers.Clear();

            IEnumerable<Company> src = _allCustomers;
            if (!string.IsNullOrWhiteSpace(term))
            {
                src = _allCustomers.Where(c =>
                    (c.Name ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.ICO ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.City ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.Email ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            foreach (var c in src)
                Customers.Add(c);
            
            OnPropertyChanged(nameof(Count));
        }

        private async void LoadCustomers()
        {
            try
            {
                var list = await _service.LoadAsync();
                _allCustomers.Clear();
                _allCustomers.AddRange(list);
                Customers.Clear();
                foreach (var c in list) Customers.Add(c);
                OnPropertyChanged(nameof(Count));
            }
            catch { /* ignore */ }
        }

        private async System.Threading.Tasks.Task SaveCustomers()
        {
            try
            {
                await _service.SaveAsync(_allCustomers);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při ukládání: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static List<Bank> LoadBanksFromJson()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var path = System.IO.Path.Combine(baseDir, "banks.json");
                if (!System.IO.File.Exists(path))
                    path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "banks.json");

                if (!System.IO.File.Exists(path))
                    return new List<Bank>();

                var json = System.IO.File.ReadAllText(path);
                var banks = System.Text.Json.JsonSerializer.Deserialize<List<Bank>>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return banks ?? new List<Bank>();
            }
            catch
            {
                return new List<Bank>();
            }
        }
    }
}

