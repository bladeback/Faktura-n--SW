using System.Windows.Controls;

namespace InvoiceApp.Views
{
    public partial class HistoryPage : UserControl
    {
        public HistoryPage()
        {
            InitializeComponent();
            
            if (DataContext is ViewModels.HistoryViewModel vm)
            {
                vm.RequestEdit += Vm_RequestEdit;
                vm.RequestConvert += Vm_RequestConvert;
            }
        }

        private void Vm_RequestEdit(Models.Invoice doc)
        {
            var window = System.Windows.Window.GetWindow(this) as MainWindow;
            window?.EditDocument(doc);
        }

        private void Vm_RequestConvert(Models.Invoice order)
        {
            var window = System.Windows.Window.GetWindow(this) as MainWindow;
            window?.ConvertOrder(order);
        }
    }
}
