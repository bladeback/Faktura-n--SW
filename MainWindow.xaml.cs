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
            
            // 3. Ujistíme se, že jsme v režimu "Faktura"
            // Pokud už tam jsme, nic se nestane. Pokud ne, přepne se to.
            // Volitelně můžeme zavolat NewInvoiceCommand, pokud chceme VŽDY novou fakturu při kliknutí.
            // Ale logičtější je asi jen přepnout typ, aby uživatel nepřišel o rozpracovanou práci,
            // pokud jen omylem klikl jinam.
            if (_mainViewModel.Current.Type != Models.DocType.Invoice)
            {
                // Pokud přepínáme z Objednávky na Fakturu, asi chceme zachovat data?
                // Nebo raději čistý štít?
                // Prozatím jen přepneme typ dokladu.
                _mainViewModel.Current.Type = Models.DocType.Invoice;
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

            // 3. Přepneme na režim "Objednávka"
            if (_mainViewModel.Current.Type != Models.DocType.Order)
            {
                _mainViewModel.Current.Type = Models.DocType.Order;
            }

            ContentHost.Content = page;
            SetActive(NavOrders);
        }

        private void Suppliers_Click(object sender, RoutedEventArgs e)
        {
            // Dodavatelé mají vlastní logiku a vlastní data (seznam),
            // takže tam nepotřebujeme MainViewModel (nebo si ho stránka vytvoří sama,
            // ale SuppliersPage má DataContext = this).
            ContentHost.Content = new Views.SuppliersPage();
            SetActive(NavSuppliers);
        }

        private void Customers_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.ConstructionPage();
            SetActive(NavCustomers);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.ConstructionPage();
            SetActive(NavSettings);
        }
    }
}
