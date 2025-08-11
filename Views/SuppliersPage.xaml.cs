using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using InvoiceApp.Models;
using InvoiceApp.Services;

namespace InvoiceApp.Views
{
    public partial class SuppliersPage : UserControl
    {
        private readonly SuppliersService _service = new();
        private readonly BankService _bankService = new();

        public ObservableCollection<Company> Suppliers { get; } = new();
        public ObservableCollection<Bank> Banks { get; } = new();

        private ICollectionView? _view;

        public SuppliersPage()
        {
            InitializeComponent();
            DataContext = this;
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            // načti banky pro combo
            Banks.Clear();
            foreach (var b in _bankService.GetBanks()) Banks.Add(b);

            // načti dodavatele
            var list = await _service.LoadAsync();
            Suppliers.Clear();
            foreach (var c in list) Suppliers.Add(c);

            _view = CollectionViewSource.GetDefaultView(Suppliers);
        }

        private async Task SaveAsync()
            => await _service.SaveAsync(Suppliers.ToList());

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_view == null) _view = CollectionViewSource.GetDefaultView(Suppliers);
            var q = (sender as TextBox)?.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(q)) { _view.Filter = null; return; }

            _view.Filter = o =>
            {
                if (o is not Company c) return false;
                return (c.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || (c.ICO ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            };
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var c = new Company();
            Suppliers.Add(c);
            SuppliersGrid.SelectedItem = c;
            SuppliersGrid.ScrollIntoView(c);
            if (SuppliersGrid.Columns.Count > 0)
                SuppliersGrid.CurrentCell = new DataGridCellInfo(c, SuppliersGrid.Columns[0]);
            SuppliersGrid.BeginEdit();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = SuppliersGrid.SelectedItems.Cast<Company>().ToList();
            if (selected.Count == 0) return;

            foreach (var c in selected) Suppliers.Remove(c);
            await SaveAsync();
        }

        private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();

        private void SuppliersGrid_RowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            Dispatcher.BeginInvoke(new Action(async () => await SaveAsync()));
        }
    }
}
