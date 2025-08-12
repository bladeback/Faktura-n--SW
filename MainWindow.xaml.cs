using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Výchozí stránka – ať se aplikace hned zobrazí
            ContentHost.Content = new Views.SuppliersPage();
            SetActive(NavSuppliers);
        }

        // Zvýraznění aktivního tlačítka (pouze ta, která v XAML existují)
        private void SetActive(Button? active)
        {
            // reset
            if (NavInvoices != null)
            {
                NavInvoices.ClearValue(Control.BackgroundProperty);
                NavInvoices.ClearValue(Control.ForegroundProperty);
            }
            if (NavSuppliers != null)
            {
                NavSuppliers.ClearValue(Control.BackgroundProperty);
                NavSuppliers.ClearValue(Control.ForegroundProperty);
            }

            // aktivní
            if (active != null)
            {
                active.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB
                active.Foreground = Brushes.White;
            }
        }

        private void Invoices_Click(object sender, RoutedEventArgs e)
        {
            // Pokud stránka faktur není implementovaná, necháme Dodavatele
            try
            {
                ContentHost.Content = new Views.InvoicePage();
            }
            catch
            {
                ContentHost.Content = new Views.SuppliersPage();
            }
            SetActive(NavInvoices);
        }

        private void Suppliers_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.SuppliersPage();
            SetActive(NavSuppliers);
        }
    }
}
