using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InvoiceApp.Models
{
    public enum DocType
    {
        Invoice,
        Order
    }

    public partial class Party : ObservableObject
    {
        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string address = string.Empty;
        [ObservableProperty] private string city = string.Empty;
        [ObservableProperty] private string iCO = string.Empty;
        [ObservableProperty] private string dIC = string.Empty;

        [ObservableProperty] private string bank = string.Empty;
        [ObservableProperty] private string accountNumber = string.Empty;
        [ObservableProperty] private string iBAN = string.Empty;

        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string phone = string.Empty;

        [ObservableProperty] private string? country;

        // --- ZDE JE FINÁLNÍ OPRAVA: Manuální implementace vlastnosti SWIFT ---
        private string? swift;
        public string? SWIFT
        {
            get => swift;
            set => SetProperty(ref swift, value);
        }
    }

    public partial class Invoice : ObservableObject
    {
        [ObservableProperty] private DocType type = DocType.Invoice;
        [ObservableProperty] private string number = string.Empty;

        [ObservableProperty] private DateTime issueDate = DateTime.Today;
        [ObservableProperty] private DateTime dueDate = DateTime.Today.AddDays(14);

        [ObservableProperty] private Party supplier = new();
        [ObservableProperty] private Party customer = new();

        public ObservableCollection<InvoiceItem> Items { get; } = new();

        [ObservableProperty] private string variableSymbol = string.Empty;
        [ObservableProperty] private string currency = "CZK";
        [ObservableProperty] private string notes = string.Empty;

        [ObservableProperty] private string? paymentMethod;
        [ObservableProperty] private string? constantSymbol;
        [ObservableProperty] private DateTime? taxableSupplyDate;

        public string PaymentIban
        {
            get
            {
                var ibanRaw = Supplier?.IBAN?.Replace(" ", "");
                if (!string.IsNullOrWhiteSpace(ibanRaw))
                    return ibanRaw!;

                var acc = Supplier?.AccountNumber;
                var derived = BuildCzIbanFromAccount(acc);
                return derived ?? string.Empty;
            }
        }

        public decimal SubtotalNet => Items.Sum(i => i.Quantity * i.UnitPrice);
        public decimal VatTotal => Items.Sum(i => i.Quantity * i.UnitPrice * i.VatRate);
        public decimal Total => SubtotalNet + VatTotal;

        private static string? BuildCzIbanFromAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return null;

            account = account.Trim();

            var m = Regex.Match(account, @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
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
            var check = 98 - remainder;
            var checkStr = check.ToString("00");

            return "CZ" + checkStr + bban;
        }

        private static string ConvertIbanCharsToDigits(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length * 2);
            foreach (var ch in input)
            {
                if (char.IsLetter(ch))
                {
                    int val = char.ToUpperInvariant(ch) - 'A' + 10;
                    sb.Append(val.ToString());
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private static int Mod97(string digits)
        {
            int rem = 0;
            foreach (var ch in digits)
            {
                rem = (rem * 10 + (ch - '0')) % 97;
            }
            return rem;
        }
    }
}