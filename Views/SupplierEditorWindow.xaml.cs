using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using InvoiceApp.Models;
using InvoiceApp.Services;

namespace InvoiceApp.Views
{
    public partial class SupplierEditorWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly Company _target;
        public Company Editable { get; }
        public List<Bank> Banks { get; }
        private Bank? _selectedBank;
        public Bank? SelectedBank
        {
            get => _selectedBank;
            set
            {
                if (_selectedBank == value) return;
                _selectedBank = value;
                Editable.Bank = _selectedBank?.Name ?? "";
                RecalcIban();
                PropertyChanged?.Invoke(this, new(nameof(SelectedBank)));
            }
        }

        public SupplierEditorWindow(Company model, IEnumerable<Bank> banks)
        {
            InitializeComponent();

            _target = model;
            Editable = CloneCompany(model);
            Banks = banks.ToList();

            // předvyber banku podle účtu nebo názvu
            if (TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                _selectedBank = Banks.FirstOrDefault(b => b.Code == code);
            if (_selectedBank == null && !string.IsNullOrWhiteSpace(Editable.Bank))
                _selectedBank = Banks.FirstOrDefault(b => string.Equals(b.Name, Editable.Bank, StringComparison.CurrentCultureIgnoreCase));

            DataContext = this;
            RecalcIban(); // spočti a ulož bez mezer (zobrazení řeší converter)
        }

        private static Company CloneCompany(Company c) => new Company
        {
            Name = c.Name,
            Address = c.Address,
            City = c.City,
            PostalCode = c.PostalCode,
            ICO = c.ICO,
            DIC = c.DIC,
            Bank = c.Bank,
            AccountNumber = c.AccountNumber,
            IBAN = c.IBAN,
            Email = c.Email,
            Phone = c.Phone,
            IsVatPayer = c.IsVatPayer
        };

        private static void CopyTo(Company src, Company dst)
        {
            dst.Name = src.Name; dst.Address = src.Address; dst.City = src.City; dst.PostalCode = src.PostalCode;
            dst.ICO = src.ICO; dst.DIC = src.DIC; dst.Bank = src.Bank; dst.AccountNumber = src.AccountNumber;
            dst.IBAN = src.IBAN; dst.Email = src.Email; dst.Phone = src.Phone; dst.IsVatPayer = src.IsVatPayer;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            CopyTo(Editable, _target);
            DialogResult = true;
        }

        // === ARES: načtení + naplnění polí, včetně parsování PSČ ===
        private async void LoadFromAres_Click(object sender, RoutedEventArgs e)
        {
            var ico = (Editable.ICO ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ico))
            {
                MessageBox.Show("Zadej IČO, které mám hledat.", "ARES", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var ares = new AresService();

                // tvá služba vrací (string? Name, string? Address, string? City, string? Dic)
                var data = await ares.GetByIcoAsync(ico);

                var allEmpty = string.IsNullOrWhiteSpace(data.Name)
                               && string.IsNullOrWhiteSpace(data.Address)
                               && string.IsNullOrWhiteSpace(data.City)
                               && string.IsNullOrWhiteSpace(data.Dic);

                if (allEmpty)
                {
                    MessageBox.Show("V ARES nebyl nalezen žádný záznam.", "ARES", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(data.Name)) Editable.Name = data.Name;
                if (!string.IsNullOrWhiteSpace(data.Address)) Editable.Address = data.Address;

                // City může přijít jako "67521 Okříšky" – vytáhneme PSČ a město
                if (!string.IsNullOrWhiteSpace(data.City))
                {
                    var (psc, cityName) = ExtractPscAndCity(data.City);
                    if (!string.IsNullOrWhiteSpace(psc)) Editable.PostalCode = NormalizePsc(psc);
                    if (!string.IsNullOrWhiteSpace(cityName)) Editable.City = cityName;
                    else Editable.City = data.City?.Trim();
                }

                if (!string.IsNullOrWhiteSpace(data.Dic)) Editable.DIC = data.Dic;

                // z účtu odvodíme banku + IBAN
                if (TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                    SelectedBank = Banks.FirstOrDefault(b => b.Code == code);
                RecalcIban(); // IBAN uložíme bez mezer; zobrazení řeší converter

                PropertyChanged?.Invoke(this, new(nameof(Editable)));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Načtení z ARES selhalo:\n{ex.Message}", "ARES", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Account_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                SelectedBank = Banks.FirstOrDefault(b => b.Code == code);
            RecalcIban();
        }

        private void Account_LostFocus(object sender, RoutedEventArgs e) => RecalcIban();

        // === Pomocné metody ===

        private static bool TryGetBankCodeFromAccount(string? account, out string code)
        {
            code = "";
            if (string.IsNullOrWhiteSpace(account)) return false;
            var m = Regex.Match(account.Trim(), @"/(?<code>\d{4})\b");
            if (m.Success) { code = m.Groups["code"].Value; return true; }
            return false;
        }

        private void RecalcIban()
        {
            var code = SelectedBank?.Code;
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(Editable.AccountNumber))
            {
                Editable.IBAN = "";
                PropertyChanged?.Invoke(this, new(nameof(Editable)));
                return;
            }

            // "123456-7890123456/0800" nebo "7890123456/0800"
            var acc = (Editable.AccountNumber ?? "").Trim();
            var m = Regex.Match(acc, @"^(?:(?<pre>\d+)-)?(?<num>\d+)\s*/\s*(?<code>\d{4})$");
            if (!m.Success) { Editable.IBAN = ""; PropertyChanged?.Invoke(this, new(nameof(Editable))); return; }

            var prefix = m.Groups["pre"].Success ? m.Groups["pre"].Value : "";
            var number = m.Groups["num"].Value;
            var bank = m.Groups["code"].Value;

            var prePad = string.IsNullOrEmpty(prefix) ? "000000" : prefix.PadLeft(6, '0');
            var numPad = number.PadLeft(10, '0');
            var bban = $"{bank}{prePad}{numPad}";

            // IBAN kontrolní čísla
            string rearranged = bban + "CZ00";
            string numeric = "";
            foreach (char ch in rearranged) numeric += char.IsLetter(ch) ? (ch - 'A' + 10).ToString() : ch.ToString();

            int mod = 0;
            foreach (char d in numeric) mod = (mod * 10 + (d - '0')) % 97;

            int check = 98 - mod;

            // Do modelu uložíme bez mezer (converter se postará o hezké zobrazení)
            Editable.IBAN = $"CZ{check:00}{bban}";
            PropertyChanged?.Invoke(this, new(nameof(Editable)));
        }

        private static (string? psc, string? city) ExtractPscAndCity(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return (null, null);

            // formáty: "67521 Okříšky" / "110 00 Praha 1" / "Praha 1 110 00"
            var s = source.Trim();

            // PSČ na začátku
            var m1 = Regex.Match(s, @"^(?<psc>\d{3}\s?\d{2})\s+(?<city>.+)$");
            if (m1.Success)
                return (m1.Groups["psc"].Value, m1.Groups["city"].Value.Trim());

            // PSČ na konci
            var m2 = Regex.Match(s, @"^(?<city>.+?)\s+(?<psc>\d{3}\s?\d{2})$");
            if (m2.Success)
                return (m2.Groups["psc"].Value, m2.Groups["city"].Value.Trim());

            // žádné PSČ – vrať celé jako město
            return (null, s);
        }

        private static string NormalizePsc(string psc)
        {
            var digits = new string((psc ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 5)
                return $"{digits.Substring(0, 3)} {digits.Substring(3)}";
            return (psc ?? string.Empty).Trim();
        }

    }
}
