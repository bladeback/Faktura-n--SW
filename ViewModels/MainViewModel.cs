using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly BankService _bankService = new();

        [ObservableProperty]
        private Invoice _current = new();

        public ObservableCollection<InvoiceItem> Items { get; } = new();
        [ObservableProperty] private bool supplierIsVatPayer;
        public List<string> PaymentMethods { get; } = new() { "Převodem", "Hotově", "Kartou" };
        public ObservableCollection<Bank> Banks { get; } = new();
        [ObservableProperty] private Bank? _selectedBank;

        public string SubtotalDisplay => FormatMoney(ComputeBaseTotal(), Current?.Currency ?? "CZK");
        public string VatTotalDisplay => FormatMoney(ComputeVatTotal(), Current?.Currency ?? "CZK");

        // Součet bez zaokrouhlení
        public string GrandTotalDisplay => FormatMoney(ComputeBaseTotal() + ComputeVatTotal(), Current?.Currency ?? "CZK");

        // Zaokrouhlení (může být + nebo −)
        public string RoundingDisplay => FormatSignedMoney(ComputeRounding(), Current?.Currency ?? "CZK");

        // Částka k úhradě po zaokrouhlení
        public string PayableDisplay => FormatMoney(ComputeRoundedTotal(), Current?.Currency ?? "CZK");

        // Pro UI necháme TotalDisplay směřovat na „k úhradě“
        public string TotalDisplay => PayableDisplay;

        // Viditelnost řádku „Zaokrouhlení“
        public bool ShowRounding => Math.Round(ComputeRounding(), 2) != 0m;

        // Zobrazení čísla dokladu pro UI – vrací to, co je v Current.Number
        public string DisplayNumber => Current?.Number ?? string.Empty;

        public MainViewModel()
        {
            Current.PropertyChanged += Current_PropertyChanged;
            NewInvoice();
            Items.CollectionChanged += Items_CollectionChanged;
            HookSupplierWatcher(Current.Supplier);
            LoadBanks();
        }

        private void Current_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Invoice.Number))
            {
                // VS = posledních 10 číslic
                var digits = new string(Current.Number.Where(char.IsDigit).ToArray());
                Current.VariableSymbol = digits.Length > 10 ? digits[^10..] : digits;
                OnPropertyChanged(nameof(DisplayNumber));
            }
            else if (e.PropertyName == nameof(Invoice.Type))
            {
                OnPropertyChanged(nameof(DisplayNumber));
            }
        }

        private void LoadBanks()
        {
            var banksList = _bankService.GetBanks();
            Banks.Clear();
            foreach (var bank in banksList)
                Banks.Add(bank);
        }

        partial void OnSelectedBankChanged(Bank? value)
        {
            if (value != null && Current.Supplier != null)
            {
                Current.Supplier.Bank = value.Name;
                Current.Supplier.SWIFT = value.Swift;
            }
        }

        partial void OnSupplierIsVatPayerChanged(bool value) => RaiseTotalsChanged();

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

        private decimal ComputeGrossTotal() => ComputeBaseTotal() + ComputeVatTotal();

        private decimal ComputeRoundedTotal() =>
            Math.Round(ComputeGrossTotal(), 0, MidpointRounding.AwayFromZero);

        private decimal ComputeRounding() => ComputeRoundedTotal() - ComputeGrossTotal();

        private static string FormatMoney(decimal value, string currency)
        {
            var ci = new CultureInfo("cs-CZ");
            return $"{string.Format(ci, "{0:N2}", value)} {currency}";
        }

        private static string FormatSignedMoney(decimal value, string currency)
        {
            if (value == 0m) return FormatMoney(0m, currency);
            var sign = value > 0 ? "+" : "-";
            var ci = new CultureInfo("cs-CZ");
            var abs = Math.Abs(value);
            return $"{sign}{string.Format(ci, "{0:N2}", abs)} {currency}";
        }

        private void RaiseTotalsChanged()
        {
            OnPropertyChanged(nameof(SubtotalDisplay));
            OnPropertyChanged(nameof(VatTotalDisplay));
            OnPropertyChanged(nameof(GrandTotalDisplay));
            OnPropertyChanged(nameof(RoundingDisplay));
            OnPropertyChanged(nameof(PayableDisplay));
            OnPropertyChanged(nameof(TotalDisplay));
            OnPropertyChanged(nameof(ShowRounding));
        }

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

        private void HookSupplierWatcher(Party? supplier)
        {
            if (supplier == null) return;
            supplier.PropertyChanged -= Supplier_PropertyChanged;
            supplier.PropertyChanged += Supplier_PropertyChanged;
        }

        private void Supplier_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not Party sup) return;
            if (e.PropertyName == nameof(Party.AccountNumber))
            {
                var acc = sup.AccountNumber;
                var iban = TryBuildCzIbanFromAccount(acc);
                if (!string.IsNullOrWhiteSpace(iban))
                {
                    sup.IBAN = iban.Replace(" ", "").ToUpperInvariant();
                    OnPropertyChanged(nameof(Current));
                }
            }
        }

        private static string? TryBuildCzIbanFromAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account)) return null;
            var m = Regex.Match(account.Trim(), @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
            if (!m.Success) return null;
            var prefix = m.Groups[1].Value;
            var number = m.Groups[2].Value;
            var bank = m.Groups[3].Value;
            var prefixPadded = (string.IsNullOrEmpty(prefix) ? "" : prefix).PadLeft(6, '0');
            var numberPadded = number.PadLeft(10, '0');
            var bban = bank + prefixPadded + numberPadded;
            var rearranged = bban + "CZ00";
            var converted = ConvertIbanCharsToDigits(rearranged);
            var remainder = Mod97(converted);
            var check = (98 - remainder).ToString("00");
            return "CZ" + check + bban;
        }

        private static string ConvertIbanCharsToDigits(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length * 2);
            foreach (var ch in input)
            {
                if (char.IsLetter(ch)) { sb.Append((int)char.ToUpperInvariant(ch) - 'A' + 10); }
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static int Mod97(string digits)
        {
            int rem = 0;
            foreach (var ch in digits)
                rem = (rem * 10 + (ch - '0')) % 97;
            return rem;
        }

        private static bool IsValidIban(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return false;
            iban = iban.Replace(" ", "").ToUpperInvariant();

            // Délka 15–34, alfanumerická
            if (!Regex.IsMatch(iban, "^[A-Z0-9]{15,34}$")) return false;

            // Přesun prvních 4 znaků na konec a převod písmen na číslice
            var rearranged = iban[4..] + iban[..4];
            var converted = ConvertIbanCharsToDigits(rearranged);

            // Modulo 97 musí být 1
            return Mod97(converted) == 1;
        }

        [RelayCommand]
        private void AddItem()
        {
            var item = new InvoiceItem { Name = "Nová položka", Quantity = 1m, UnitPrice = 1000m, VatRate = 0.21m };
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
            var newNumber = _num.ReserveInvoiceNumber();
            Current.PropertyChanged -= Current_PropertyChanged;
            Current = new Invoice
            {
                Type = DocType.Invoice,
                Number = $"FA-{newNumber}",
                Currency = "CZK",
                PaymentMethod = "Převodem",
                TaxableSupplyDate = DateTime.Today
            };
            Current.PropertyChanged += Current_PropertyChanged;

            Current.VariableSymbol = new string(newNumber.Where(char.IsDigit).ToArray());
            if (Current.VariableSymbol.Length > 10)
                Current.VariableSymbol = Current.VariableSymbol[^10..];

            Items.Clear();
            SupplierIsVatPayer = false;
            SelectedBank = null;
            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));
        }

        [RelayCommand]
        private void NewOrder()
        {
            var newNumber = _num.ReserveOrderNumber();
            Current.PropertyChanged -= Current_PropertyChanged;
            Current = new Invoice
            {
                Type = DocType.Order,
                Number = $"OBJ-{newNumber}",
                Currency = "CZK",
                PaymentMethod = "Převodem",
                TaxableSupplyDate = DateTime.Today
            };
            Current.PropertyChanged += Current_PropertyChanged;

            Current.VariableSymbol = new string(newNumber.Where(char.IsDigit).ToArray());
            if (Current.VariableSymbol.Length > 10)
                Current.VariableSymbol = Current.VariableSymbol[^10..];

            Items.Clear();
            SupplierIsVatPayer = false;
            SelectedBank = null;
            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));
        }

        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            try
            {
                // QR i PDF používají částku po ZAOKROUHLENÍ
                var amountToPay = ComputeRoundedTotal();

                var label = Current.Type == DocType.Invoice ? "Faktura" : "Objednávka";
                var msg = $"{label} {Current.Number}".Trim();

                // Normalizuj IBAN (odstraň mezery, upper)
                var paymentIban = (Current.PaymentIban ?? string.Empty).Replace(" ", "").ToUpperInvariant();

                // Pokud je IBAN vyplněný, ověř jeho platnost – jinak zastav export
                if (!string.IsNullOrWhiteSpace(paymentIban) && !IsValidIban(paymentIban))
                {
                    MessageBox.Show("Zadaný IBAN není platný. Opravte ho prosím, než budete exportovat PDF.", "Neplatný IBAN", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                byte[]? qrPng = null;
                if (Current.Type == DocType.Invoice && !string.IsNullOrWhiteSpace(paymentIban) && amountToPay > 0m)
                {
                    var payload = _qr.BuildCzechQrPaymentPayload(
                        iban: paymentIban,
                        amount: amountToPay,
                        currency: string.IsNullOrWhiteSpace(Current.Currency) ? "CZK" : Current.Currency,
                        variableSymbol: Current.VariableSymbol,
                        constantSymbol: Current.ConstantSymbol,
                        message: msg
                    );
                    qrPng = _qr.GenerateQrPng(payload);
                }

                var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"{Current.Number}.pdf" };
                if (dialog.ShowDialog() == true)
                {
                    var path = await Task.Run(() => _pdf.SaveInvoicePdf(Current, qrPng, dialog.FileName));

                    // úspěšný export -> potvrdit číslo
                    if (Current.Type == DocType.Invoice) _num.CommitInvoice();
                    else _num.CommitOrder();

                    MessageBox.Show($"Uloženo: {path}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Chyba při exportu PDF: {ex.Message}");
            }
        }

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
                Current.Customer.Country = "Česká republika";
                OnPropertyChanged(nameof(Current.Customer));
            }
            catch (Exception ex) { MessageBox.Show($"Chyba při načítání z ARES: {ex.Message}"); }
        }

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
                Current.Supplier.Country = "Česká republika";
                SupplierIsVatPayer = !string.IsNullOrWhiteSpace(dic);

                OnPropertyChanged(nameof(Current.Supplier));
                RaiseTotalsChanged();
            }
            catch (Exception ex) { MessageBox.Show($"Chyba při načítání z ARES: {ex.Message}"); }
        }
    }
}
