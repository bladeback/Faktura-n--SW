using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Services;
using InvoiceApp.Views;
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


        private readonly BackupService _backupService = new();

        [RelayCommand]
        private async Task CreateBackup()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "ZIP Archive (*.zip)|*.zip",
                    FileName = $"InvoiceApp_Backup_{DateTime.Now:yyyyMMdd}.zip"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _backupService.CreateBackupAsync(dialog.FileName);
                    MessageBox.Show($"Záloha byla úspěšně vytvořena:\n{dialog.FileName}", "Záloha dokončena", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při vytváření zálohy: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RestoreBackup()
        {
            try
            {
                var result = MessageBox.Show(
                    "Obnovení ze zálohy PŘEPÍŠE všechna aktuální data (faktury, klienty, nastavení).\n\nChcete pokračovat?",
                    "Potvrzení obnovy",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "ZIP Archive (*.zip)|*.zip"
                };

                if (dialog.ShowDialog() == true)
                {
                    await _backupService.RestoreBackupAsync(dialog.FileName);
                    
                    var restartResult = MessageBox.Show(
                        "Data byla úspěšně obnovena.\n\nPro správné načtení všech dat je nutné aplikaci restartovat.\nChcete aplikaci restartovat nyní?", 
                        "Obnova dokončena", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);

                    if (restartResult == MessageBoxResult.Yes)
                    {
                        // Restart application
                        var exePath = Environment.ProcessPath;
                        if (exePath != null)
                        {
                            System.Diagnostics.Process.Start(exePath);
                            Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        // Reload current view data at least
                        LoadCounters();
                        LoadConfig();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při obnově dat: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void FactoryReset()
        {
            var warning = MessageBox.Show(
                "Tato akce NEVRATNĚ SMAŽE VŠECHNA DATA APLIKACE!\n\n" +
                "Smažou se:\n" +
                "- Všechny faktury a objednávky\n" +
                "- Seznam klientů a dodavatelů\n" +
                "- Uložené položky\n" +
                "- Nastavení\n\n" +
                "Opravdu chcete pokračovat?",
                "NEBEZPEČNÁ AKCE",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (warning != MessageBoxResult.Yes) return;

            var dialog = new SecureInputWindow();
            if (dialog.ShowDialog() == true && dialog.InputText == "DELETE")
            {
                try
                {
                    // Smazat všechny .json soubory v adresáři, KROMĚ systémových
                    var dir = AppDomain.CurrentDomain.BaseDirectory;
                    var files = System.IO.Directory.GetFiles(dir, "*.json");
                    foreach (var file in files)
                    {
                        // Přeskočit kritické soubory .NET runtime
                        if (file.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase) || 
                            file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        try { System.IO.File.Delete(file); } catch { /* ignore locked */ }
                    }

                    // Restart aplikace
                    var exePath = Environment.ProcessPath;
                    if (exePath != null)
                    {
                        System.Diagnostics.Process.Start(exePath);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Chyba při mazání dat: {ex.Message}", "Chyba", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                if (dialog.DialogResult == true) // Jen pokud potvrdil, ale napsal špatně
                {
                    MessageBox.Show("Nesprávné potvrzovací heslo. Data nebyla smazána.", "Chyba", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
