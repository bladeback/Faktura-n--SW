using InvoiceApp.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InvoiceApp.Views
{
    public partial class InvoicePage : UserControl
    {
        public InvoicePage()
        {
            InitializeComponent();
        }

        private void VatCombo_KeyDown(object sender, KeyEventArgs e)
        {
            // Povolit jen čísla a ovládací klávesy, pokud chceme...
            // Ale WPF ComboBox IsEditable to řeší celkem dobře sám.
        }

        private void ItemName_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is SavedItem saved)
            {
                // Najdeme řádek (InvoiceItem), který editujeme
                if (cb.DataContext is InvoiceItem row)
                {
                    row.UnitPrice = saved.UnitPrice;
                    row.VatRate = saved.VatRate;
                    row.Unit = saved.Unit;
                    // Name se nastaví bindingem na Text, ale pro jistotu:
                    row.Name = saved.Name;
                }
            }
        }
    }
}
