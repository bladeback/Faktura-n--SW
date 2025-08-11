using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using InvoiceApp.Models;
using InvoiceApp.Services;

namespace InvoiceApp.Views
{
    public partial class SuppliersPage : UserControl
    {
        private readonly SuppliersService _store = new();
        private readonly AresService _ares = new();
        private readonly BankService _bankService = new();

        public ObservableCollection<Company> Suppliers { get; } = new();

        private ICollectionView? _view;

        public SuppliersPage()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += OnLoaded;
            PreviewKeyDown += SuppliersPage_PreviewKeyDown; // Delete, Ctrl+S
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;

            var list = await _store.LoadAsync();
            Suppliers.Clear();
            foreach (var c in list) Suppliers.Add(c);

            _view = CollectionViewSource.GetDefaultView(Suppliers);
            if (_view != null) _view.Filter = FilterPredicate;
        }

        // ====== Hledání
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

        // ====== Přidat / Smazat / Uložit
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var row = new Company { Name = "Nový dodavatel" };
            Suppliers.Add(row);

            SuppliersGrid.SelectedItem = row;
            SuppliersGrid.ScrollIntoView(row);
            if (SuppliersGrid.Columns.Count > 0)
            {
                SuppliersGrid.CurrentCell = new DataGridCellInfo(row, SuppliersGrid.Columns[0]);
                SuppliersGrid.BeginEdit();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = SuppliersGrid.SelectedItems.Cast<Company>().ToList();
            foreach (var c in selected) Suppliers.Remove(c);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            SuppliersGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            SuppliersGrid.CommitEdit(DataGridEditingUnit.Row, true);

            await _store.SaveAsync(Suppliers.ToList());
            MessageBox.Show("Dodavatelé uloženi.", "Uloženo",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ====== Po editaci řádku – doplnění z ARES + banka z kódu účtu
        private void SuppliersGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                SuppliersGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                SuppliersGrid.CommitEdit(DataGridEditingUnit.Row, true);

                if (e.Row.Item is Company c)
                {
                    await AutofillFromAresAsync(c);
                    TryAutoSelectBank(c);
                    _view?.Refresh();
                }
            }), DispatcherPriority.Background);
        }

        private async Task AutofillFromAresAsync(Company row)
        {
            var ico = row.ICO?.Trim();
            if (string.IsNullOrEmpty(ico)) return;

            var data = await _ares.GetByIcoAsync(ico);
            // GetByIcoAsync vrací tuple (Name, Address, City, Dic); nelze porovnat na null,
            // ověřme, že nejsou všechna pole prázdná:
            if (string.IsNullOrWhiteSpace(data.Name)
             && string.IsNullOrWhiteSpace(data.Address)
             && string.IsNullOrWhiteSpace(data.City)
             && string.IsNullOrWhiteSpace(data.Dic))
                return;

            // doplň jen prázdná pole, uživatelský vstup nepřepisuj
            row.Name = string.IsNullOrWhiteSpace(row.Name) ? data.Name : row.Name;
            row.Address = string.IsNullOrWhiteSpace(row.Address) ? data.Address : row.Address;
            row.City = string.IsNullOrWhiteSpace(row.City) ? data.City : row.City;
            row.DIC = string.IsNullOrWhiteSpace(row.DIC) ? data.Dic : row.DIC;

            if (!string.IsNullOrWhiteSpace(row.DIC))
                row.IsVatPayer = true;
        }

        private void TryAutoSelectBank(Company row)
        {
            var code = ExtractBankCode(row.AccountNumber);
            if (string.IsNullOrEmpty(code)) return;

            // nepotřebujeme speciální FindByCode – vezmeme z načteného seznamu
            var bank = _bankService.GetBanks().FirstOrDefault(b => b.Code == code);
            if (bank != null)
                row.Bank = bank.Name;
        }

        private static string? ExtractBankCode(string? account)
        {
            if (string.IsNullOrWhiteSpace(account)) return null;
            var slash = account.LastIndexOf('/');
            if (slash < 0 || slash + 1 >= account.Length) return null;
            var digits = new string(account[(slash + 1)..].Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? null : digits;
        }

        // ====== Klávesové zkratky (Delete, Ctrl+S)
        private void SuppliersPage_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete && SuppliersGrid.IsKeyboardFocusWithin)
            {
                Delete_Click(SuppliersGrid, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control
                && e.Key == System.Windows.Input.Key.S)
            {
                Save_Click(SuppliersGrid, new RoutedEventArgs());
                e.Handled = true;
            }
        }
    }
}
