using System.Windows;

namespace InvoiceApp.Views
{
    public partial class SecureInputWindow : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public SecureInputWindow()
        {
            InitializeComponent();
            InputBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputBox.Text;
            DialogResult = true;
        }
    }
}
