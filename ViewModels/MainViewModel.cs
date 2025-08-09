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

        // Plátce DPH – vazba na CheckBox v UI + na DPH sloupec a přepočet celkové částky
        [ObservableProperty]
        private bool supplierIsVatPayer;

        // Zobrazení součtu vpravo dole
        public string TotalDisplay => FormatMoney(ComputeTotal(), Current?.Currency ?? "CZK");

        public MainViewModel()
        {
            // výchozí dodavatel – můžeš přepsat
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

            // Vazby na změny položek – přepočítáme Celkem při každé editaci
            Items.CollectionChanged += Items_CollectionChanged;
        }

        // Reaguj, když uživatel přepne plátce DPH
        partial void OnSupplierIsVatPayerChanged(bool value)
        {
            OnPropertyChanged(nameof(TotalDisplay));
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (InvoiceItem it in e.OldItems)
                    it.PropertyChanged -= Item_PropertyChanged;

            if (e.NewItems != null)
                foreach (InvoiceItem it in e.NewItems)
                    it.PropertyChanged += Item_PropertyChanged;

            // přepočet celku při přidání/odebrání
            OnPropertyChanged(nameof(TotalDisplay));
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(InvoiceItem.Quantity)
                or nameof(InvoiceItem.UnitPrice)
                or nameof(InvoiceItem.VatRate)
                or nameof(InvoiceItem.Name))
            {
                OnPropertyChanged(nameof(TotalDisplay));
            }
        }

        private decimal ComputeTotal()
        {
            if (Current == null) return 0m;
            decimal total = 0m;
            foreach (var it in Items)
            {
                var line = it.Quantity * it.UnitPrice;
                if (SupplierIsVatPayer)
                    line *= (1 + it.VatRate);
                total += line;
            }
            return total;
        }

        private static string FormatMoney(decimal value, string currency)
        {
            var ci = new CultureInfo("cs-CZ");
            return $"{string.Format(ci, "{0:N2}", value)} {currency}";
        }

        // ===== Položky =====
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
            OnPropertyChanged(nameof(TotalDisplay));
        }

        [RelayCommand]
        private void RemoveSelected(object? param)
        {
            if (param is InvoiceItem it)
            {
                Items.Remove(it);
                Current.Items.Remove(it);
                OnPropertyChanged(nameof(TotalDisplay));
            }
        }

        // ===== Nový doklad =====
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
            SupplierIsVatPayer = false;    // výchozí
            OnPropertyChanged(nameof(TotalDisplay));
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
            SupplierIsVatPayer = false;    // objednávka = spíš bez DPH zobrazení
            OnPropertyChanged(nameof(TotalDisplay));
        }

        // ===== Export PDF =====
        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                // QR platba generujeme jen pro Fakturu
                byte[]? png = null;
                if (Current.Type == DocType.Invoice)
                {
                    string payload = _qr.BuildCzechQrPaymentPayload(
                        Current.PaymentIban,
                        ComputeTotal(),                // vezmeme částku stejně jako v UI
                        Current.Currency,
                        Current.VariableSymbol,
                        $"{Current.Type} {Current.Number}"
                    );
                    png = _qr.GenerateQrPng(payload);
                }

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

        // ===== ARES: Načtení podle IČO (Odběratel) =====
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

        // ===== ARES: Načtení podle IČO (Dodavatel) =====
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

                // Automaticky zaškrtni plátce, když má DIČ
                SupplierIsVatPayer = !string.IsNullOrWhiteSpace(dic);

                OnPropertyChanged(nameof(Current));
                OnPropertyChanged(nameof(TotalDisplay));
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
