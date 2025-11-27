using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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
                RecalcIban(); // funguje i bez banky
                PropertyChanged?.Invoke(this, new(nameof(SelectedBank)));
            }
        }

        public SupplierEditorWindow(Company model, IEnumerable<Bank> banks)
        {
            InitializeComponent();

            _target = model;
            Editable = CloneCompany(model);
            Banks = banks?.ToList() ?? new List<Bank>();

            // předvyber banku z účtu / z názvu
            if (TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                _selectedBank = Banks.FirstOrDefault(b => b.Code == code);
            if (_selectedBank == null && !string.IsNullOrWhiteSpace(Editable.Bank))
                _selectedBank = Banks.FirstOrDefault(b =>
                    string.Equals(b.Name, Editable.Bank, StringComparison.CurrentCultureIgnoreCase));

            DataContext = this;
            RecalcIban();
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
            dst.Name = src.Name;
            dst.Address = src.Address;
            dst.City = src.City;
            dst.PostalCode = src.PostalCode;
            dst.ICO = src.ICO;
            dst.DIC = src.DIC;
            dst.Bank = src.Bank;
            dst.AccountNumber = src.AccountNumber;
            dst.IBAN = src.IBAN;
            dst.Email = src.Email;
            dst.Phone = src.Phone;
            dst.IsVatPayer = src.IsVatPayer;
        }

        // === HANDLERY ===

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            CopyTo(Editable, _target);
            DialogResult = true;
        }

        private void Dic_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                var caret = tb.CaretIndex;
                var cleaned = (tb.Text ?? string.Empty).ToUpperInvariant().Replace(" ", "");
                if (tb.Text != cleaned)
                {
                    tb.Text = cleaned;
                    tb.CaretIndex = Math.Min(caret, tb.Text.Length);
                }
            }
        }

        private void Account_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Banks.Count > 0 && TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                SelectedBank = Banks.FirstOrDefault(b => b.Code == code);
            RecalcIban();
        }

        private void Account_LostFocus(object sender, RoutedEventArgs e) => RecalcIban();

        private async void LoadFromAres_Click(object sender, RoutedEventArgs e)
        {
            var ico = (Editable.ICO ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ico))
            {
                MessageBox.Show("Zadej IČO, které mám hledat.", "ARES",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var ares = new AresService();

                // najdi volatelnou metodu
                var m = ares.GetType().GetMethod("GetByIcoAsync")
                        ?? ares.GetType().GetMethod("GetCompanyAsync")
                        ?? ares.GetType().GetMethod("FindByIcoAsync");

                if (m == null)
                {
                    MessageBox.Show("V AresService chybí metoda pro načtení firmy podle IČO.",
                        "ARES", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // zavolej metodu
                object? ret = m.Invoke(ares, new object[] { ico });

                // může to být Task<T> nebo přímo výsledek
                object? result;
                if (ret is System.Threading.Tasks.Task task)
                {
                    await task.ConfigureAwait(true);
                    var prop = task.GetType().GetProperty("Result");
                    result = prop?.GetValue(task);
                }
                else
                {
                    result = ret;
                }

                if (result == null)
                {
                    MessageBox.Show("V ARES nebyl nalezen žádný záznam.", "ARES",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // varianta: rovnou Company
                if (result is Company c)
                {
                    FillFromCompany(c);
                    return;
                }

                // varianta: ValueTuple<string...> (1 až 8 položek)
                var t = result.GetType();
                if (t.IsValueType && t.FullName != null && t.FullName.StartsWith("System.ValueTuple"))
                {
                    // vyzobeme Item1..Item8, co existují
                    var items = new List<string>();
                    for (int i = 1; i <= 8; i++)
                    {
                        var p = t.GetProperty($"Item{i}");
                        if (p == null) break;
                        items.Add(p.GetValue(result)?.ToString() ?? "");
                    }

                    // mapování tolerantně podle obsahu
                    // 1: Název
                    if (items.Count >= 1 && !string.IsNullOrWhiteSpace(items[0]))
                        Editable.Name = items[0].Trim();

                    // 2: Ulice a č.p.
                    if (items.Count >= 2 && !string.IsNullOrWhiteSpace(items[1]))
                        Editable.Address = items[1].Trim();

                    // 3: Město + PSČ v jednom
                    if (items.Count >= 3 && !string.IsNullOrWhiteSpace(items[2]))
                    {
                        var s = items[2].Trim();
                        var mP = Regex.Match(s, @"\b(\d{3})\s?(\d{2})\b");
                        if (mP.Success)
                        {
                            Editable.PostalCode = $"{mP.Groups[1].Value}{mP.Groups[2].Value}";
                            Editable.City = Regex.Replace(s, @"\b\d{3}\s?\d{2}\b", "")
                                                 .Trim(new[] { ',', ' ', '-' });
                        }
                        else
                        {
                            Editable.City = s;
                        }
                    }

                    // 4: DIČ (CZ…)
                    if (items.Count >= 4 && Regex.IsMatch(items[3] ?? "", @"^\s*CZ\d+", RegexOptions.IgnoreCase))
                        Editable.DIC = items[3].Replace(" ", "").ToUpperInvariant();

                    // 5: E-mail (obsahuje @)
                    if (items.Count >= 5 && (items[4]?.Contains("@") ?? false))
                        Editable.Email = items[4].Trim();

                    // 6: Telefon (nějaké číslo s mezerami)
                    if (items.Count >= 6 && !string.IsNullOrWhiteSpace(items[5]))
                        Editable.Phone = items[5].Trim();

                    // 7: Účet (obsahuje '/')
                    if (items.Count >= 7 && (items[6]?.Contains("/") ?? false))
                        Editable.AccountNumber = items[6].Trim();

                    // 8: IBAN (začíná CZ)
                    if (items.Count >= 8 && Regex.IsMatch(items[7] ?? "", @"^\s*CZ\d+"))
                        Editable.IBAN = items[7].Replace(" ", "").ToUpperInvariant();

                    // z účtu vyber banku (pokud máme seznam)
                    if (Banks.Count > 0 && TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                        SelectedBank = Banks.FirstOrDefault(b => b.Code == code);

                    // když IBAN nepřišel hotový, spočítej
                    if (string.IsNullOrWhiteSpace(Editable.IBAN))
                        RecalcIban();

                    PropertyChanged?.Invoke(this, new(nameof(Editable)));
                    return;
                }

                MessageBox.Show("ARES vrátil neočekávaný typ výsledku. Pošli mi prosím signaturu metody, upravím to natvrdo.",
                    "ARES", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Načtení z ARES selhalo:\n{ex.Message}", "ARES",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // === Pomocné ===

        private static bool TryGetBankCodeFromAccount(string? account, out string code)
        {
            code = "";
            if (string.IsNullOrWhiteSpace(account)) return false;
            var m = Regex.Match(account.Trim(), @"/(?<code>\d{4})\b");
            if (!m.Success) return false;
            code = m.Groups["code"].Value;
            return true;
        }

        private void RecalcIban()
        {
            var acc = (Editable.AccountNumber ?? "").Trim();
            if (string.IsNullOrEmpty(acc))
            {
                Editable.IBAN = "";
                PropertyChanged?.Invoke(this, new(nameof(Editable)));
                return;
            }

            // povolené tvary: "123-4567890123/0800" nebo "4567890123/0800"
            var mainMatch = Regex.Match(acc, @"^(?:(?<pre>\d+)-)?(?<num>\d+)\s*/\s*(?<code>\d{4})$");
            if (!mainMatch.Success)
            {
                Editable.IBAN = "";
                PropertyChanged?.Invoke(this, new(nameof(Editable)));
                return;
            }

            var prefix = mainMatch.Groups["pre"].Success ? mainMatch.Groups["pre"].Value : "";
            var number = mainMatch.Groups["num"].Value;
            var bank = mainMatch.Groups["code"].Value;

            var prePad = string.IsNullOrEmpty(prefix) ? "000000" : prefix.PadLeft(6, '0');
            var numPad = number.PadLeft(10, '0');
            var bban = $"{bank}{prePad}{numPad}";

            string rearranged = bban + "CZ00";
            string numeric = "";
            foreach (char ch in rearranged)
                numeric += char.IsLetter(ch) ? (ch - 'A' + 10).ToString() : ch.ToString();

            int mod = 0;
            foreach (char d in numeric)
                mod = (mod * 10 + (d - '0')) % 97;

            int check = 98 - mod;
            Editable.IBAN = $"CZ{check:00}{bban}";
            PropertyChanged?.Invoke(this, new(nameof(Editable)));
        }

        private void FillFromCompany(Company data)
        {
            Editable.Name = data.Name;
            Editable.Address = data.Address;
            Editable.City = data.City;
            Editable.PostalCode = data.PostalCode;
            Editable.DIC = data.DIC;
            Editable.IsVatPayer = data.IsVatPayer;

            if (!string.IsNullOrWhiteSpace(data.AccountNumber))
                Editable.AccountNumber = data.AccountNumber;
            if (!string.IsNullOrWhiteSpace(data.IBAN))
                Editable.IBAN = data.IBAN;

            if (Banks.Count > 0 && TryGetBankCodeFromAccount(Editable.AccountNumber, out var code))
                SelectedBank = Banks.FirstOrDefault(b => b.Code == code);

            RecalcIban();
            PropertyChanged?.Invoke(this, new(nameof(Editable)));
        }
    }
}
