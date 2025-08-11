using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;            // <– přidáno
using InvoiceApp.ViewModels;
using System.Text.RegularExpressions;

namespace InvoiceApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Po načtení okna nastavíme viditelnost sloupce DPH
            Loaded += (_, __) => UpdateVatColumnVisibility();

            // Pokud už je DataContext nastavený z XAML, rovnou se napojíme
            if (DataContext is INotifyPropertyChanged vmNow)
                vmNow.PropertyChanged += Vm_PropertyChanged;

            // Když se vymění DataContext (VM), znovu se navážeme
            DataContextChanged += MainWindow_DataContextChanged;
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= Vm_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += Vm_PropertyChanged;

            UpdateVatColumnVisibility();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Reagujeme na změnu plátcovství i výměnu Current
            if (e.PropertyName == nameof(MainViewModel.SupplierIsVatPayer) ||
                e.PropertyName == nameof(MainViewModel.Current))
            {
                UpdateVatColumnVisibility();
            }
        }

        private void UpdateVatColumnVisibility()
        {
            if (VM == null || VatColumn == null) return;

            bool show = VM.SupplierIsVatPayer;   // u plátce zobrazit, u neplátce skrýt
            VatColumn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // Enter v editačním combu DPH -> hned potvrdit editaci
        private void VatCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ItemsGrid != null)
                {
                    ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                e.Handled = true;
            }
        }
        private void BankCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VM?.Current?.Supplier == null) return;

            var acc = VM.Current.Supplier.AccountNumber;
            if (string.IsNullOrWhiteSpace(acc)) return;

            // vytáhni kód banky z účtu
            var m = Regex.Match(acc.Trim(), @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
            if (!m.Success) return;

            var code = m.Groups[3].Value; // 4 číslice za lomítkem
            var byCode = VM.Banks.FirstOrDefault(b =>
                string.Equals((b.Code ?? "").Trim(), code, StringComparison.Ordinal));

            // pokud nesedí vybraný záznam s kódem z účtu, vrať zpět správnou banku
            if (byCode != null && !ReferenceEquals(VM.SelectedBank, byCode))
            {
                VM.SelectedBank = byCode;
            }
        }
    }
}
