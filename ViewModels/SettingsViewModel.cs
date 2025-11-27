using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Services;
using System;
using System.Windows;

namespace InvoiceApp.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly InvoiceNumberService _numService;
        private readonly ConfigService _configService = new();

        [ObservableProperty]
        private string currentYear;

        [ObservableProperty]
        private int nextInvoiceNumber;

        [ObservableProperty]
        private int nextOrderNumber;

        [ObservableProperty] private string footerInvoice = "";
        [ObservableProperty] private string footerOrder = "";
        [ObservableProperty] private string invoiceUrl = "";
        [ObservableProperty] private string invoiceUrlText = "";

        public SettingsViewModel()
        {
            _numService = InvoiceNumberService.Instance;
            CurrentYear = DateTime.Now.ToString("yyyy");
            LoadCounters();
            LoadConfig();
        }

        private void LoadCounters()
        {
            NextInvoiceNumber = _numService.GetNextInvoiceNumber(CurrentYear);
            NextOrderNumber = _numService.GetNextOrderNumber(CurrentYear);
        }

        private async void LoadConfig()
        {
            var config = await _configService.LoadAsync();
            FooterInvoice = config.FooterInvoice;
            FooterOrder = config.FooterOrder;
            InvoiceUrl = config.InvoiceUrl;
            InvoiceUrlText = config.InvoiceUrlText;
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            try
            {
                _numService.SetNextInvoiceNumber(CurrentYear, NextInvoiceNumber);
                _numService.SetNextOrderNumber(CurrentYear, NextOrderNumber);

                var config = new Models.AppConfig
                {
                    FooterInvoice = FooterInvoice,
                    FooterOrder = FooterOrder,
                    InvoiceUrl = InvoiceUrl,
                    InvoiceUrlText = InvoiceUrlText
                };
                await _configService.SaveAsync(config);

                MessageBox.Show("Nastavení uloženo.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při ukládání: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper command for inserting tags - in a real MVVM scenario with TextBox binding, 
        // manipulating selection is tricky without code-behind or behaviors.
        // For simplicity, we will just append the tag to the focused property or rely on the user to type it.
        // BUT, to make it user friendly as requested, we can use a simple approach:
        // We can't easily know which TextBox is focused in pure ViewModel.
        // So we will skip the ViewModel command for tags and handle it in View code-behind for this specific "editor" feature.
        // It's a pragmatic choice for a "small editor".
        
        // However, we need to expose the properties to be bound.


        partial void OnCurrentYearChanged(string value)
        {
            // Pokud uživatel změní rok, načteme čítače pro ten rok
            if (value.Length == 4 && int.TryParse(value, out _))
            {
                LoadCounters();
            }
        }
    }
}
