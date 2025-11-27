using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceApp.ViewModels;

namespace InvoiceApp
{
    public partial class MainWindow : Window
    {
        // Držíme jednu instanci ViewModelu pro celou dobu běhu okna,
        // aby se při přepínání stránek neztrácela rozpracovaná data.
        private readonly MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();

            // Výchozí stránka – Faktury (aby to dávalo smysl)
            // Ale pokud chceš startovat na Dodavatelích, klidně to změň.
            // Pro "výchozí" stav zavoláme Invoices_Click, aby se nastavilo vše potřebné.
            Invoices_Click(this, new RoutedEventArgs());
        }

        // Zvýraznění aktivního tlačítka (pouze ta, která v XAML existují)
        private void SetActive(Button? active)
        {
            // reset všech tlačítek
            var buttons = new[] { NavInvoices, NavOrders, NavSuppliers, NavCustomers, NavSettings };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.ClearValue(Control.BackgroundProperty);
                btn.ClearValue(Control.ForegroundProperty);
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
            // 1. Nastavíme obsah na InvoicePage
            var page = new Views.InvoicePage();
            
            // 2. Předáme sdílený ViewModel
            page.DataContext = _mainViewModel;
            
            // 3. VŽDY zavoláme NewInvoice, aby se vygenerovalo nové číslo a vyčistil formulář.
            // Uživatel to tak očekává (klik na "Faktury" = chci dělat novou fakturu).
            if (_mainViewModel.NewInvoiceCommand.CanExecute(null))
            {
                _mainViewModel.NewInvoiceCommand.Execute(null);
            }

            ContentHost.Content = page;
            SetActive(NavInvoices);
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            // 1. Nastavíme obsah na InvoicePage (stejný formulář)
            var page = new Views.InvoicePage();

            // 2. Předáme sdílený ViewModel
            page.DataContext = _mainViewModel;

            // 3. VŽDY zavoláme NewOrder, aby se vygenerovalo nové číslo a vyčistil formulář.
            // Uživatel to tak očekává (klik na "Objednávky" = chci dělat novou objednávku).
            if (_mainViewModel.NewOrderCommand.CanExecute(null))
            {
                _mainViewModel.NewOrderCommand.Execute(null);
            }

            ContentHost.Content = page;
            SetActive(NavOrders);
        }

        private void Suppliers_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.SuppliersPage();
            SetActive(NavSuppliers);
        }

        private void Customers_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.CustomersPage();
            SetActive(NavCustomers);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.SettingsPage();
            SetActive(NavSettings);
        }
    }
}
