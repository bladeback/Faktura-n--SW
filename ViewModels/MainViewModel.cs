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
using System.Net.Http;
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
        public string GrandTotalDisplay => FormatMoney(ComputeBaseTotal() + ComputeVatTotal(), Current?.Currency ?? "CZK");
        public string TotalDisplay => GrandTotalDisplay;

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
                Current.VariableSymbol = new string(Current.Number.Where(char.IsDigit).ToArray());
            }
        }

        private void LoadBanks()
        {
            var banksList = _bankService.GetBanks();
            Banks.Clear();
            foreach (var bank in banksList)
            {
                Banks.Add(bank);
            }
        }

        partial void OnSelectedBankChanged(Bank? value)
        {
            if (Current?.Supplier == null) return;

            if (value != null)
            {
                // Při výběru banky doplníme název a SWIFT
                Current.Supplier.Bank = value.Name;
                Current.Supplier.SWIFT = value.Swift;
            }
            else
            {
                // Při nulování výběru banku v modelu vymažeme
                Current.Supplier.Bank = string.Empty;
                Current.Supplier.SWIFT = string.Empty;
            }

            OnPropertyChanged(nameof(Current));
        }

        partial void OnSupplierIsVatPayerChanged(bool value)
        {
            RaiseTotalsChanged();
        }

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

        [RelayCommand]
        private void AddItem()
        {
            var item = new InvoiceItem { Name = "Nová položka", Quantity = 1, UnitPrice = 1000m, VatRate = 0.21m };
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
            var newNumber = _num.NextInvoiceNumber();
            Current.PropertyChanged -= Current_PropertyChanged; // Odpojíme starý handler
            Current = new Invoice
            {
                Type = DocType.Invoice,
                Number = newNumber,
                VariableSymbol = new string(newNumber.Where(char.IsDigit).ToArray()),
                Currency = "CZK",
                PaymentMethod = "Převodem",
                TaxableSupplyDate = DateTime.Today
            };
            Current.PropertyChanged += Current_PropertyChanged; // Připojíme nový
            Items.Clear();
            SupplierIsVatPayer = false;

            // 🚿 Reset vybrané banky i polí v modelu
            SelectedBank = null;

            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
        }

        [RelayCommand]
        private void NewOrder()
        {
            var newNumber = _num.NextOrderNumber();
            Current.PropertyChanged -= Current_PropertyChanged; // Odpojíme starý handler
            Current = new Invoice
            {
                Type = DocType.Order,
                Number = newNumber,
                VariableSymbol = new string(newNumber.Where(char.IsDigit).ToArray()),
                Currency = "CZK",
                PaymentMethod = "Převodem",
                TaxableSupplyDate = DateTime.Today
            };
            Current.PropertyChanged += Current_PropertyChanged; // Připojíme nový
            Items.Clear();
            SupplierIsVatPayer = false;

            // 🚿 Reset vybrané banky i polí v modelu
            SelectedBank = null;

            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                var amountToPay = Math.Round(ComputeBaseTotal() + ComputeVatTotal(), 2, MidpointRounding.AwayFromZero);
                var label = Current.Type == DocType.Invoice ? "Faktura" : "Objednávka";
                var msg = $"{label} {Current.Number}".Trim();
                var paymentIban = (Current.PaymentIban ?? string.Empty).Replace(" ", "").ToUpperInvariant();

                byte[]? qrPng = null;
                if (Current.Type == DocType.Invoice && !string.IsNullOrWhiteSpace(paymentIban) && amountToPay > 0m)
                {
                    var payload = _qr.BuildCzechQrPaymentPayload(iban: paymentIban, amount: amountToPay, currency: string.IsNullOrWhiteSpace(Current.Currency) ? "CZK" : Current.Currency, variableSymbol: Current.VariableSymbol, message: msg);
                    qrPng = _qr.GenerateQrPng(payload);
                }

                var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = $"{Current.Number}.pdf" };
                if (dialog.ShowDialog() == true)
                {
                    var path = _pdf.SaveInvoicePdf(Current, qrPng, dialog.FileName);
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
                OnPropertyChanged(nameof(Current));
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
                OnPropertyChanged(nameof(Current));
                RaiseTotalsChanged();
            }
            catch (Exception ex) { MessageBox.Show($"Chyba při načítání z ARES: {ex.Message}"); }
        }
    }
}
