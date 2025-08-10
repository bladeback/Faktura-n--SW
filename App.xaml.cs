using System.Windows;

namespace InvoiceApp
{
    public partial class App : Application
    {
        public App()
        {
            // Připojení handleru z kódu (žádný atribut v XAML)
            this.DispatcherUnhandledException += Application_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // MainWindow je v namespace InvoiceApp (ne ve Views)
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("Došlo k neočekávané chybě: \n\n" + e.Exception,
                            "Chyba aplikace",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
