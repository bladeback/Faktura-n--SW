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
        // Domácí číslo účtu (např. 12-3456789012/0100 nebo 2600456000/2010)
        [ObservableProperty] private string accountNumber = string.Empty;
        [ObservableProperty] private string iBAN = string.Empty;

        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string phone = string.Empty;
    }

    public partial class Invoice : ObservableObject
    {
        [ObservableProperty] private DocType type = DocType.Invoice;
        [ObservableProperty] private string number = string.Empty;

        [ObservableProperty] private DateTime issueDate = DateTime.Today;
        [ObservableProperty] private DateTime dueDate = DateTime.Today.AddDays(14);

        [ObservableProperty] private Party supplier = new();
        [ObservableProperty] private Party customer = new();

        // Položky dokladu
        public ObservableCollection<InvoiceItem> Items { get; } = new();

        // Platební a ostatní údaje
        [ObservableProperty] private string variableSymbol = string.Empty;
        [ObservableProperty] private string currency = "CZK";
        [ObservableProperty] private string notes = string.Empty;

        // IBAN pro platbu:
        // 1) pokud je vyplněn u dodavatele, použijeme ho,
        // 2) jinak se ho pokusíme dopočítat z domácího čísla účtu (prefix-/základ/bankovní kód).
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

        // Souhrny (bez ohledu na plátcovství; UI/PDF řeší zobrazení)
        public decimal SubtotalNet => Items.Sum(i => i.Quantity * i.UnitPrice);
        public decimal VatTotal => Items.Sum(i => i.Quantity * i.UnitPrice * i.VatRate);
        public decimal Total => SubtotalNet + VatTotal;

        /// <summary>
        /// Převod českého domácího čísla účtu (prefix?-základ/bank) na IBAN CZ.
        /// Podporuje tvary: "prefix-základ/bbbb", "základ/bbbb", mezery ignoruje.
        /// Příklad: 2600456000/2010 → CZkk 2010 0000 0000 2600 4560 00
        /// </summary>
        private static string? BuildCzIbanFromAccount(string? account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return null;

            // Odstraníme mezery
            account = account.Trim();

            // prefix (0–6 číslic) je volitelný, základ (1–10), bankovní kód (4)
            var m = Regex.Match(account, @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
            if (!m.Success) return null;

            var prefix = m.Groups[1].Value;           // může být prázdné
            var number = m.Groups[2].Value;
            var bank = m.Groups[3].Value;

            // CZ BBAN = bank (4) + prefix (6, s nulami) + number (10, s nulami) => 20 číslic
            var prefixPadded = (string.IsNullOrEmpty(prefix) ? "" : prefix).PadLeft(6, '0');
            var numberPadded = number.PadLeft(10, '0');
            var bban = bank + prefixPadded + numberPadded; // 4 + 6 + 10 = 20

            // IBAN checksum: 98 - mod97( (BBAN + "CZ00") převedeno na čísla )
            // Přeuspořádání se dělá jako BBAN + "CZ00", pak C=12, Z=35
            var rearranged = bban + "CZ00";
            var converted = ConvertIbanCharsToDigits(rearranged);
            var remainder = Mod97(converted);
            var check = 98 - remainder;
            var checkStr = check.ToString("00");

            return "CZ" + checkStr + bban;
        }

        // Pomocné pro IBAN výpočet
        private static string ConvertIbanCharsToDigits(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length * 2);
            foreach (var ch in input)
            {
                if (char.IsLetter(ch))
                {
                    int val = char.ToUpperInvariant(ch) - 'A' + 10; // A=10 ... Z=35
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
