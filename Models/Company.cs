using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public partial class Company : ObservableObject
    {
        [ObservableProperty] private string? name;
        [ObservableProperty] private string? address;
        [ObservableProperty] private string? city;
        [ObservableProperty] private string? postalCode;
        [ObservableProperty] private string? iCO;
        [ObservableProperty] private string? dIC;
        [ObservableProperty] private string? bank;

        /// <summary>
        /// Domácí číslo účtu (např. 12-3456789012/0100)
        /// </summary>
        [ObservableProperty] private string? accountNumber;

        [ObservableProperty] private string? iBAN;
        [ObservableProperty] private string? email;
        [ObservableProperty] private string? phone;
        [ObservableProperty] private bool isVatPayer;
    }
}
