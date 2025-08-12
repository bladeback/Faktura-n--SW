using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;

namespace InvoiceApp.Views
{
    public partial class SuppliersPage : UserControl
    {

        // veřejný bind na DataGrid
        public ObservableCollection<Company> Suppliers { get; } = new();

        // služby
        private readonly SuppliersService _store = new();
        private readonly List<Bank> _banks;

        private ICollectionView? _view;

        public SuppliersPage()
        {
            InitializeComponent();

            // načti banky z banks.json (kopíruje se do výstupu přes csproj)
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "banks.json");
                using var fs = File.OpenRead(path);
                _banks = JsonSerializer.Deserialize<List<Bank>>(fs, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<Bank>();
            }
            catch
            {
                _banks = new List<Bank>();
            }

            // async init po načtení vizuálního stromu
            Loaded += async (_, __) =>
            {
                await LoadAsync();
            };

            DataContext = this;
        }

        // --------- Data ---------

        private async Task LoadAsync()
        {
            var list = await _store.LoadAsync();
            Suppliers.Clear();
            foreach (var c in list) Suppliers.Add(c);

            _view = CollectionViewSource.GetDefaultView(Suppliers);
            if (_view != null) _view.Filter = FilterPredicate;
        }

        // --------- Hledání ---------

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
            => _view?.Refresh();

        private bool FilterPredicate(object obj)
        {
            if (obj is not Company c) return true;
            var q = SearchBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(q)) return true;

            return (c.Name?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false)
                || (c.ICO?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (c.City?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false)
                || (c.Email?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false);
        }

        // --------- Akce: Přidat / Upravit (dvojklik) / Smazat / Uložit ---------

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            var model = new Company();
            var dlg = new SupplierEditorWindow(model, _banks) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                Suppliers.Add(model);
                await _store.SaveAsync(Suppliers.ToList());
            }
        }

        private async void SuppliersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Pro jistotu ukonči případné rozpracované editace (kdyby byly)
            try
            {
                SuppliersGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                SuppliersGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            catch { /* ignore */ }

            if (SuppliersGrid.SelectedItem is not Company selected) return;

            // kopie -> editace -> promítnout zpět
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

            var dlg = new SupplierEditorWindow(copy, _banks) { Owner = Window.GetWindow(this) };
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

                // ŽÁDNÝ Items.Refresh() – Company dědí z ObservableObject, takže DataGrid se sám překreslí.
                await _store.SaveAsync(Suppliers.ToList());
            }
        }


        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (SuppliersGrid.SelectedItems.Count == 0) return;

            var count = SuppliersGrid.SelectedItems.Count;
            var msg = count == 1
                ? "Opravdu smazat vybraného dodavatele?"
                : $"Opravdu smazat {count} vybraných dodavatelů?";
            if (MessageBox.Show(msg, "Smazat", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var toRemove = SuppliersGrid.SelectedItems.Cast<Company>().ToList();
            foreach (var c in toRemove) Suppliers.Remove(c);

            await _store.SaveAsync(Suppliers.ToList());
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            await _store.SaveAsync(Suppliers.ToList());
            MessageBox.Show("Dodavatelé uloženi.", "Uloženo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
