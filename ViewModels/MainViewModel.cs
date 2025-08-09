using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace InvoiceApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PdfService _pdf = new();
        private readonly QrService _qr = new();
        private readonly InvoiceNumberService _num = new();
        private readonly AresService _ares = new();

        [ObservableProperty] private Invoice current = new();
        public ObservableCollection<InvoiceItem> Items { get; } = new();

        // ====== NOVÉ: plátcovství DPH dodavatele + stav ARES ======
        [ObservableProperty] private bool supplierIsVatPayer = true;
        [ObservableProperty] private bool isAresBusy;

        // Příkazy
        public IAsyncRelayCommand LoadCustomerFromIcoCommand { get; }
        public IAsyncRelayCommand LoadSupplierFromIcoCommand { get; }

        public MainViewModel()
        {
            // výchozí hodnoty dodavatele – můžeš si je pak uložit jako default
            Current.Supplier.Name = "Vaše firma s.r.o.";
            Current.Supplier.Address = "Ulice 1";
            Current.Supplier.City = "123 45 Město";
            Current.Supplier.IBAN = "CZ6508000000001234567899";
            Current.Supplier.Bank = "ČSOB";
            Current.Supplier.Email = "info@firma.cz";
            Current.Supplier.Phone = "+420123456789";

            Current.Customer.Name = "Zákazník a.s.";
            Current.Number = _num.NextInvoiceNumber();
            Current.VariableSymbol = DateTime.Now.ToString("yyyyMMdd");
            Current.Currency = "CZK";

            LoadCustomerFromIcoCommand = new AsyncRelayCommand(LoadCustomerFromIco, CanRunAres);
            LoadSupplierFromIcoCommand = new AsyncRelayCommand(LoadSupplierFromIco, CanRunAres);
        }

        private bool CanRunAres() => !IsAresBusy;

        private static bool IsValidIco(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = new string(s.Where(char.IsDigit).ToArray());
            return s.Length == 8;
        }

        // ====== NOVÉ: celková částka podle plátcovství ======
        public decimal TotalDisplay =>
            SupplierIsVatPayer ? CalcTotalWithVat() : CalcTotalNoVat();

        private decimal CalcTotalWithVat()
            => Current.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + i.VatRate));

        private decimal CalcTotalNoVat()
            => Current.Items.Sum(i => i.Quantity * i.UnitPrice);

        partial void OnSupplierIsVatPayerChanged(bool value)
        {
            // přepočítej zobrazenou celkovou cenu
            OnPropertyChanged(nameof(TotalDisplay));
        }

        private void BumpTotals()
        {
            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(Current)); // pro jistotu kvůli dalším vazbám
        }

        // ====== položky ======
        [RelayCommand]
        private void AddItem()
        {
            var item = new InvoiceItem
            {
                Name = "Nová položka",
                Quantity = 1,
                UnitPrice = 1000m,
                VatRate = 0.21m
            };
            Items.Add(item);
            Current.Items.Add(item);
            BumpTotals();
        }

        [RelayCommand]
        private void RemoveSelected(object? param)
        {
            if (param is InvoiceItem it)
            {
                Items.Remove(it);
                Current.Items.Remove(it);
                BumpTotals();
            }
        }

        [RelayCommand]
        private void NewInvoice()
        {
            Current = new Invoice
            {
                Type = DocType.Invoice,
                Number = _num.NextInvoiceNumber(),
                Currency = "CZK"
            };
            Items.Clear();
            BumpTotals();
        }

        [RelayCommand]
        private void NewOrder()
        {
            Current = new Invoice
            {
                Type = DocType.Order,
                Number = _num.NextOrderNumber(),
                Currency = "CZK"
            };
            Items.Clear();
            BumpTotals();
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                // QR zatím necháme, DPH zapojíme do PDF v dalším kroku
                string payload = _qr.BuildCzechQrPaymentPayload(
                    Current.PaymentIban,
                    TotalDisplay,                    // <<< použijeme zobrazenou celkovou částku
                    Current.Currency,
                    Current.VariableSymbol,
                    $"{Current.Type} {Current.Number}"
                );
                var png = _qr.GenerateQrPng(payload);

                var dialog = new SaveFileDialog
                {
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{Current.Number}.pdf"
                };
                if (dialog.ShowDialog() == true)
                {
                    var path = _pdf.SaveInvoicePdf(Current, png, dialog.FileName);
                    MessageBox.Show($"Uloženo: {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při exportu PDF: {ex.Message}");
            }
        }

        // ====== ARES – odběratel ======
        private async Task LoadCustomerFromIco()
        {
            try
            {
                var ico = Current.Customer.ICO?.Trim();
                if (!IsValidIco(ico))
                {
                    MessageBox.Show("Zadejte platné IČO (8 číslic) odběratele.");
                    return;
                }

                IsAresBusy = true;
                LoadCustomerFromIcoCommand.NotifyCanExecuteChanged();
                LoadSupplierFromIcoCommand.NotifyCanExecuteChanged();

                var (name, address, city, dic) = await _ares.GetByIcoAsync(ico!);

                if (name == null && address == null && city == null)
                {
                    MessageBox.Show("Subjekt s tímto IČO se v ARES nenašel.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(name)) Current.Customer.Name = NormalizeName(name);
                if (!string.IsNullOrWhiteSpace(address)) Current.Customer.Address = NormalizeAddress(address);
                if (!string.IsNullOrWhiteSpace(city)) Current.Customer.City = NormalizeCity(city);
                if (!string.IsNullOrWhiteSpace(dic)) Current.Customer.DIC = (dic ?? string.Empty).Trim();

                BumpTotals();
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Časový limit pro dotaz na ARES vypršel.");
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Nepodařilo se kontaktovat ARES: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při načítání z ARES: {ex.Message}");
            }
            finally
            {
                IsAresBusy = false;
                LoadCustomerFromIcoCommand.NotifyCanExecuteChanged();
                LoadSupplierFromIcoCommand.NotifyCanExecuteChanged();
            }
        }

        // ====== ARES – dodavatel ======
        private async Task LoadSupplierFromIco()
        {
            try
            {
                var ico = Current.Supplier.ICO?.Trim();
                if (!IsValidIco(ico))
                {
                    MessageBox.Show("Zadejte platné IČO (8 číslic) dodavatele.");
                    return;
                }

                IsAresBusy = true;
                LoadCustomerFromIcoCommand.NotifyCanExecuteChanged();
                LoadSupplierFromIcoCommand.NotifyCanExecuteChanged();

                var (name, address, city, dic) = await _ares.GetByIcoAsync(ico!);

                if (name == null && address == null && city == null)
                {
                    MessageBox.Show("Subjekt s tímto IČO se v ARES nenašel.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(name)) Current.Supplier.Name = NormalizeName(name);
                if (!string.IsNullOrWhiteSpace(address)) Current.Supplier.Address = NormalizeAddress(address);
                if (!string.IsNullOrWhiteSpace(city)) Current.Supplier.City = NormalizeCity(city);
                if (!string.IsNullOrWhiteSpace(dic)) Current.Supplier.DIC = (dic ?? string.Empty).Trim();

                // Heuristika: když máme DIČ, předpokládej plátce; bez DIČ neplátce (ručně lze přepnout)
                SupplierIsVatPayer = !string.IsNullOrWhiteSpace(Current.Supplier.DIC);

                BumpTotals();
            }
            catch (TaskCanceledException)
            {
                MessageBox.Show("Časový limit pro dotaz na ARES vypršel.");
            }
            catch (HttpRequestException ex)
            {
                MessageBox.Show($"Nepodařilo se kontaktovat ARES: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při načítání z ARES: {ex.Message}");
            }
            finally
            {
                IsAresBusy = false;
                LoadCustomerFromIcoCommand.NotifyCanExecuteChanged();
                LoadSupplierFromIcoCommand.NotifyCanExecuteChanged();
            }
        }

        // ====== drobné formátovací pomocníky ======
        private static string NormalizeName(string name)
            => name.Trim();

        private static string NormalizeAddress(string address)
            => System.Text.RegularExpressions.Regex.Replace(address.Trim(), @"\s+", " ");

        private static string NormalizeCity(string city)
            => System.Text.RegularExpressions.Regex.Replace(city.Trim(), @"\s+", " ");
    }
}
