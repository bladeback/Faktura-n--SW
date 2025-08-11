using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using InvoiceApp.ViewModels;

namespace InvoiceApp.Views
{
    public partial class InvoicePage : UserControl
    {
        public InvoicePage()
        {
            InitializeComponent();

            Loaded += (_, __) => UpdateVatColumnVisibility();
            DataContextChanged += InvoicePage_DataContextChanged;
        }

        private MainViewModel? VM => DataContext as MainViewModel;

        private void InvoicePage_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= Vm_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += Vm_PropertyChanged;

            UpdateVatColumnVisibility();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SupplierIsVatPayer) ||
                e.PropertyName == nameof(MainViewModel.Current))
            {
                UpdateVatColumnVisibility();
            }
        }

        private void UpdateVatColumnVisibility()
        {
            if (ItemsGrid == null || VM == null) return;

            var vatCol = ItemsGrid.Columns
                .FirstOrDefault(c => (c.Header?.ToString() ?? "").Trim().Equals("DPH (%)"));
            if (vatCol == null) return;

            bool show = VM.SupplierIsVatPayer;
            vatCol.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // Enter v editačním combu DPH uloží hodnotu
        private void VatCombo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            // commit cell + row
            ItemsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ItemsGrid.CommitEdit(DataGridEditingUnit.Row, true);

            // přesun na další buňku vpravo (komfort)
            ItemsGrid.Dispatcher.BeginInvoke(() =>
            {
                ItemsGrid.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }, DispatcherPriority.Background);

            e.Handled = true;
        }
    }
}
