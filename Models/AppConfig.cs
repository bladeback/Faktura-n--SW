using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public partial class AppConfig : ObservableObject
    {
        [ObservableProperty] private string footerInvoice = "Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře Vám můžeme účtovat zákonný úrok z prodlení.";
        [ObservableProperty] private string footerOrder = "Faktura bude vystavena v den vydání. Splatnost faktur 14 dní ode dne doručení. Objednatel souhlasí se zněním objednávky a potvrzením vzniká závazná objednávka služby.";
        [ObservableProperty] private string invoiceUrl = "";
        [ObservableProperty] private string invoiceUrlText = "";
    }
}
