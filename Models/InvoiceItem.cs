using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public partial class InvoiceItem : ObservableObject
    {
        [ObservableProperty] private string name = "Nová položka";
        [ObservableProperty] private string unit = "ks";

        // MUSÍ být decimal, aby šlo zadat např. 6,34
        [ObservableProperty] private decimal quantity = 1m;

        // Necháváme decimal kvůli výpočtům; do UI zaokrouhlíme konvertorem na celé
        [ObservableProperty] private decimal unitPrice = 1000m;

        // 0.21 = 21 %
        [ObservableProperty] private decimal vatRate = 0.21m;
    }
}
