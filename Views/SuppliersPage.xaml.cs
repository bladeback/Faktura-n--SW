using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using InvoiceApp.Models;

namespace InvoiceApp.Views
{
    public partial class SuppliersPage : UserControl
    {
        // Hlavní kolekce pro DataGrid (je na ni navázán XAML)
        public ObservableCollection<Company> Suppliers { get; } = new();

        // Nezávislá kopie pro filtrování (aby šlo "vrátit" všechny záznamy)
        private readonly List<Company> _allSuppliers = new();

        // Banky pro editor (pokud nemáš, necháme prázdné)
        private List<Bank> _banks = new();

        public SuppliersPage()
        {
            InitializeComponent();
            DataContext = this;

            _banks = LoadBanksFromJson();
            LoadSuppliers();
        }


        // === TLAČÍTKA ===

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveSuppliers();
            MessageBox.Show("Dodavatelé uloženi.", "Uloženo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var model = new Company();

            var dlg = new SupplierEditorWindow(model, _banks)
            {
                Owner = Window.GetWindow(this)
            };

            if (dlg.ShowDialog() == true)
            {
                Suppliers.Add(model);
                _allSuppliers.Add(model);
                SaveSuppliers();
            }
        }

        // Pro kompatibilitu, kdyby XAML ještě volal starý handler:
        private void DeleteSelected_Click(object sender, RoutedEventArgs e) => Delete_Click(sender, e);

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SuppliersGrid.SelectedItem is not Company selected)
            {
                MessageBox.Show("Vyber dodavatele, kterého chceš smazat.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Opravdu smazat \"{selected.Name}\"?",
                                "Smazat dodavatele",
                                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            Suppliers.Remove(selected);
            _allSuppliers.Remove(selected);
            SaveSuppliers();
        }

        private void SuppliersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SuppliersGrid.SelectedItem is not Company selected) return;

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

            var dlg = new SupplierEditorWindow(copy, _banks)
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

                SaveSuppliers();
            }
        }

        // === HLEDÁNÍ (jednoduchý filtr nad _allSuppliers) ===

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var term = (SearchBox.Text ?? string.Empty).Trim().ToLowerInvariant();

            Suppliers.Clear();

            IEnumerable<Company> src = _allSuppliers;
            if (!string.IsNullOrWhiteSpace(term))
            {
                src = _allSuppliers.Where(c =>
                    (c.Name ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.ICO ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.City ?? string.Empty).ToLowerInvariant().Contains(term) ||
                    (c.Email ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            foreach (var c in src)
                Suppliers.Add(c);
        }
        private void LoadSuppliers()
        {
            try
            {
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvoiceApp", "suppliers.json");
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var list = System.Text.Json.JsonSerializer.Deserialize<List<Company>>(json);
                    if (list != null)
                    {
                        _allSuppliers.Clear();
                        _allSuppliers.AddRange(list);
                        Suppliers.Clear();
                        foreach (var c in list) Suppliers.Add(c);
                    }
                }
            }
            catch { /* ignore */ }
        }

        private void SaveSuppliers()
        {
            try
            {
                var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvoiceApp");
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                var path = System.IO.Path.Combine(folder, "suppliers.json");
                var json = System.Text.Json.JsonSerializer.Serialize(_allSuppliers);
                System.IO.File.WriteAllText(path, json);
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
