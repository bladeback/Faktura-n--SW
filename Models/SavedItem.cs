using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public partial class SavedItem : ObservableObject
    {
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private decimal unitPrice;
        [ObservableProperty] private decimal vatRate = 0.21m; // 21% default
        [ObservableProperty] private string unit = "ks";
    }
}
