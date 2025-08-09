using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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

        // Plátce DPH – řídí výpočet i zobrazení v UI (a QR částku)
        [ObservableProperty] private bool supplierIsVatPayer;

        // Souhrny pro UI
        public string SubtotalDisplay => FormatMoney(ComputeBaseTotal(), Current?.Currency ?? "CZK");
        public string VatTotalDisplay => FormatMoney(ComputeVatTotal(), Current?.Currency ?? "CZK");
        public string GrandTotalDisplay => FormatMoney(ComputeBaseTotal() + ComputeVatTotal(), Current?.Currency ?? "CZK");
        public string TotalDisplay => GrandTotalDisplay; // zpětná kompatibilita (pokud je někde ještě použité)

        public MainViewModel()
        {
            // výchozí hodnoty – jen pro pohodlné testování
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

            Items.CollectionChanged += Items_CollectionChanged;
        }

        // ======= přepínač plátce DPH =======
        partial void OnSupplierIsVatPayerChanged(bool value)
        {
            RaiseTotalsChanged();
        }

        // ======= přepočty =======
        private decimal ComputeBaseTotal()
        {
            decimal total = 0m;
            foreach (var it in Items)
                total += it.Quantity * it.UnitPrice;
            return total;
        }

        private decimal ComputeVatTotal()
        {
            if (!SupplierIsVatPayer) return 0m;
            decimal vat = 0m;
            foreach (var it in Items)
                vat += it.Quantity * it.UnitPrice * it.VatRate;
            return vat;
        }

        private static string FormatMoney(decimal value, string currency)
        {
            var ci = new CultureInfo("cs-CZ");
            return $"{string.Format(ci, "{0:N2}", value)} {currency}";
        }

        private void RaiseTotalsChanged()
        {
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(VatTotalDisplay));
            OnPropertyChanged(nameof(GrandTotalDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
        }

        // ======= reakce na změny položek =======
        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (InvoiceItem it in e.OldItems)
                    it.PropertyChanged -= Item_PropertyChanged;

            if (e.NewItems != null)
                foreach (InvoiceItem it in e.NewItems)
                    it.PropertyChanged += Item_PropertyChanged;

            RaiseTotalsChanged();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(InvoiceItem.Quantity)
                or nameof(InvoiceItem.UnitPrice)
                or nameof(InvoiceItem.VatRate)
                or nameof(InvoiceItem.Name))
            {
                RaiseTotalsChanged();
            }
        }

        // ======= příkazy (Commands) =======
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
            RaiseTotalsChanged();
        }

        [RelayCommand]
        private void RemoveSelected(object? param)
        {
            if (param is InvoiceItem it)
            {
                Items.Remove(it);
                Current.Items.Remove(it);
                RaiseTotalsChanged();
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
            SupplierIsVatPayer = false;
            RaiseTotalsChanged();
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
            SupplierIsVatPayer = false;
            RaiseTotalsChanged();
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                // Částka do QR = to, co vidíš jako Celkem (na 2 desetiny, tečka)
                var amountToPay = Math.Round(Current.Total, 2, MidpointRounding.AwayFromZero);

                // Zpráva do QR – krátká a užitečná
                var label = Current.Type == DocType.Invoice ? "Faktura" : "Objednávka";
                var msg = $"{label} {Current.Number}".Trim();

                // QR generujeme JEN u faktury a jen když je IBAN + částka > 0
                byte[]? qrPng = null;
                var iban = Current.Supplier?.IBAN?.Trim();
                if (Current.Type == DocType.Invoice &&
                    !string.IsNullOrWhiteSpace(iban) &&
                    amountToPay > 0m)
                {
                    var payload = _qr.BuildCzechQrPaymentPayload(
                        iban: iban,
                        amount: amountToPay,
                        currency: string.IsNullOrWhiteSpace(Current.Currency) ? "CZK" : Current.Currency,
                        variableSymbol: Current.VariableSymbol,
                        message: msg
                    );

                    qrPng = _qr.GenerateQrPng(payload);
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"{Current.Number}.pdf"
                };

                if (dialog.ShowDialog() == true)
                {
                    var path = _pdf.SaveInvoicePdf(Current, qrPng, dialog.FileName);
                    System.Windows.MessageBox.Show($"Uloženo: {path}");
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Chyba při exportu PDF: {ex.Message}");
            }
        }


        // ======= ARES: Odběratel =======
        [RelayCommand]
        private async Task LoadCustomerFromIco()
        {
            try
            {
                var ico = Current.Customer.ICO?.Trim();
                if (string.IsNullOrWhiteSpace(ico))
                {
                    MessageBox.Show("Zadejte prosím IČO odběratele.");
                    return;
                }

                var (name, address, city, dic) = await _ares.GetByIcoAsync(ico);
                if (name == null && address == null && city == null)
                {
                    MessageBox.Show("Subjekt s tímto IČO se v ARES nenašel.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(name)) Current.Customer.Name = name!;
                if (!string.IsNullOrWhiteSpace(address)) Current.Customer.Address = address!;
                if (!string.IsNullOrWhiteSpace(city)) Current.Customer.City = city!;
                if (!string.IsNullOrWhiteSpace(dic)) Current.Customer.DIC = dic!;
                OnPropertyChanged(nameof(Current));
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
        }

        // ======= ARES: Dodavatel =======
        [RelayCommand]
        private async Task LoadSupplierFromIco()
        {
            try
            {
                var ico = Current.Supplier.ICO?.Trim();
                if (string.IsNullOrWhiteSpace(ico))
                {
                    MessageBox.Show("Zadejte prosím IČO dodavatele.");
                    return;
                }

                var (name, address, city, dic) = await _ares.GetByIcoAsync(ico);
                if (name == null && address == null && city == null)
                {
                    MessageBox.Show("Subjekt s tímto IČO se v ARES nenašel.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(name)) Current.Supplier.Name = name!;
                if (!string.IsNullOrWhiteSpace(address)) Current.Supplier.Address = address!;
                if (!string.IsNullOrWhiteSpace(city)) Current.Supplier.City = city!;
                Current.Supplier.DIC = dic ?? string.Empty;

                // Automaticky označ plátce, pokud ARES vrátil DIČ
                SupplierIsVatPayer = !string.IsNullOrWhiteSpace(dic);

                OnPropertyChanged(nameof(Current));
                RaiseTotalsChanged();
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
        }
    }
}
