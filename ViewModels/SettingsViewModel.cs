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

        [ObservableProperty]
        private string currentYear;

        [ObservableProperty]
        private int nextInvoiceNumber;

        [ObservableProperty]
        private int nextOrderNumber;

        public SettingsViewModel()
        {
            _numService = InvoiceNumberService.Instance;
            CurrentYear = DateTime.Now.ToString("yyyy");
            LoadCounters();
        }

        private void LoadCounters()
        {
            NextInvoiceNumber = _numService.GetNextInvoiceNumber(CurrentYear);
            NextOrderNumber = _numService.GetNextOrderNumber(CurrentYear);
        }

        [RelayCommand]
        private void SaveSettings()
        {
            try
            {
                _numService.SetNextInvoiceNumber(CurrentYear, NextInvoiceNumber);
                _numService.SetNextOrderNumber(CurrentYear, NextOrderNumber);
                MessageBox.Show("Nastavení uloženo.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při ukládání: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
