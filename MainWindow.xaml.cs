using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using InvoiceApp.ViewModels;

namespace InvoiceApp
{
    public partial class MainWindow : Window
    {
        // Držíme jednu instanci ViewModelu pro celou dobu běhu okna.
        private readonly MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            _mainViewModel = new MainViewModel();

            // Výchozí stránka – Dashboard
            Dashboard_Click(this, new RoutedEventArgs());
        }

        // Zvýraznění aktivního tlačítka (nyní přes Styles)
        private void SetActive(Button? active)
        {
            // reset všech tlačítek na základní styl
            var buttons = new[] { NavDashboard, NavInvoices, NavOrders, NavSuppliers, NavCustomers, NavSettings, NavItems, NavHistory };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                btn.Style = (Style)FindResource("NavButtonStyle");
            }

            // aktivní na Active Style
            if (active != null)
            {
                active.Style = (Style)FindResource("NavButtonActiveStyle");
            }
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox toggle)
            {
                bool isDark = toggle.IsChecked == true;
                string themeFile = isDark ? "Theme.Dark.xaml" : "Theme.Light.xaml";
                
                // Předpokládáme, že první MergedDictionary v App.xaml je Theme (Dark/Light)
                if (Application.Current.Resources.MergedDictionaries.Count > 0)
                {
                    Application.Current.Resources.MergedDictionaries[0].Source = 
                        new Uri($"Styles/{themeFile}", UriKind.Relative);
                }
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.DashboardPage();
            SetActive(NavDashboard);
        }

        private void Invoices_Click(object sender, RoutedEventArgs e)
        {
            NavigateToInvoicePage(true);
        }

        private void NavigateToInvoicePage(bool createNew)
        {
            var page = new Views.InvoicePage();
            page.DataContext = _mainViewModel;

            if (createNew && _mainViewModel.NewInvoiceCommand.CanExecute(null))
            {
                _mainViewModel.NewInvoiceCommand.Execute(null);
            }

            _ = _mainViewModel.LoadSavedParties();
            ContentHost.Content = page;
            SetActive(NavInvoices);
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            NavigateToOrderPage(true);
        }

        private void NavigateToOrderPage(bool createNew)
        {
            var page = new Views.InvoicePage();
            page.DataContext = _mainViewModel;

            if (createNew && _mainViewModel.NewOrderCommand.CanExecute(null))
            {
                _mainViewModel.NewOrderCommand.Execute(null);
            }

            _ = _mainViewModel.LoadSavedParties();
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

        private void Items_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.ItemsPage();
            SetActive(NavItems);
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            ContentHost.Content = new Views.HistoryPage();
            SetActive(NavHistory);
        }

        public void EditDocument(Models.Invoice doc)
        {
            _mainViewModel.LoadDocument(doc);
            
            if (doc.Type == Models.DocType.Invoice)
            {
                NavigateToInvoicePage(false);
            }
            else
            {
                NavigateToOrderPage(false);
            }
        }

        public void ConvertOrder(Models.Invoice order)
        {
            _mainViewModel.CreateFromOrder(order);
            NavigateToInvoicePage(false); 
        }
    }
}
