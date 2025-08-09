using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    // Díky ObservableObject se generuje INotifyPropertyChanged pro všechny [ObservableProperty]
    public partial class InvoiceItem : ObservableObject
    {
        [ObservableProperty] private string name = "Nová položka";
        [ObservableProperty] private string unit = "ks";
        [ObservableProperty] private decimal quantity = 1m;
        [ObservableProperty] private decimal unitPrice = 1000m;
        // 0.21 = 21 % DPH (u neplátce se ignoruje)
        [ObservableProperty] private decimal vatRate = 0.21m;
    }
}
