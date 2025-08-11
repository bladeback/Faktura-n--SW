using System.Windows;

namespace InvoiceApp
{
    public partial class MainWindow : Window
    {
        private readonly Views.InvoicePage _invoicePage = new();
        private readonly Views.SuppliersPage _suppliersPage = new();

        public MainWindow()
        {
            InitializeComponent();

            // Až je vizuální strom hotový (ContentHost existuje),
            // nastavíme výchozí stránku.
            Loaded += (_, __) =>
            {
                NavInvoices.IsChecked = true;
                ContentHost.Content = _invoicePage;
            };
        }

        private void NavRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Během InitializeComponent může handler vystřelit moc brzy.
            if (!IsLoaded || ContentHost == null) return;

            if (ReferenceEquals(sender, NavInvoices))
            {
                ContentHost.Content = _invoicePage;
            }
            else if (ReferenceEquals(sender, NavSuppliers))
            {
                ContentHost.Content = _suppliersPage;
            }
        }
    }
}
