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
using System.Windows.Threading;

namespace InvoiceApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly PdfService _pdf = new();
        private readonly QrService _qr = new();
        private readonly InvoiceNumberService _num = InvoiceNumberService.Instance;
        private readonly AresService _ares = new();

        private readonly BankService _bankService = new();
        private readonly DocumentService _docService = new();

        private readonly SuppliersService _suppliersService = new();
        private readonly CustomersService _customersService = new();

        private bool _isNewDocument = true;

        [ObservableProperty]
        private Invoice _current = new();

        public ObservableCollection<InvoiceItem> Items { get; } = new();

        [ObservableProperty] private bool supplierIsVatPayer;

        public List<string> PaymentMethods { get; } = new() { "Převodem", "Hotově", "Kartou" };
        public ObservableCollection<Bank> Banks { get; } = new();
        [ObservableProperty] private Bank? _selectedBank;

        // Uložené seznamy pro rychlý výběr
        public ObservableCollection<Company> SavedSuppliers { get; } = new();

        public ObservableCollection<Company> SavedCustomers { get; } = new();
        public ObservableCollection<SavedItem> SavedItems { get; } = new();

        private readonly SavedItemsService _savedItemsService = new();

        [ObservableProperty] private Company? _selectedSavedSupplier;
        [ObservableProperty] private Company? _selectedSavedCustomer;

        // Text pro vyhledávání v ComboBoxech (aby se dal resetovat na "Vyberte ze seznamu...")
        [ObservableProperty] private string supplierSearchText = "Vyberte ze seznamu...";
        [ObservableProperty] private string customerSearchText = "Vyberte ze seznamu...";

        // Uživatelsky nastavitelná doba splatnosti (dny) – výchozí 14
        [ObservableProperty]
        private int paymentTermDays = 14;


        public string SubtotalDisplay => FormatMoney(ComputeBaseTotal(), Current?.Currency ?? "Kč");
        public string VatTotalDisplay => FormatMoney(ComputeVatTotal(), Current?.Currency ?? "Kč");

        // Součet bez zaokrouhlení
        public string GrandTotalDisplay => FormatMoney(ComputeBaseTotal() + ComputeVatTotal(), Current?.Currency ?? "Kč");

        // Zaokroulení (může být + nebo −)
        public string RoundingDisplay => FormatSignedMoney(ComputeRounding(), Current?.Currency ?? "Kč");

        // Částka k úhradě po zaokrouhlení
        public string PayableDisplay => FormatMoney(ComputeRoundedTotal(), Current?.Currency ?? "Kč");

        // Pro UI necháme TotalDisplay směřovat na „k úhradě“
        public string TotalDisplay => PayableDisplay;

        // Viditelnost řádku „Zaokrouhlení“ - zobrazit jen pokud je >= 0.50 (jinak je v N0 formátu 0)
        public bool ShowRounding => Math.Abs(ComputeRounding()) >= 0.5m;

        // Zobrazení čísla dokladu pro UI – vrací to, co je v Current.Number
        public string DisplayNumber
        {
            get => Current?.Number ?? string.Empty;
            set
            {
                if (Current != null && Current.Number != value)
                {
                    Current.Number = value;
                    OnPropertyChanged();
                    
                    // Pokusíme se vyparsovat VariableSymbol, pokud to vypadá jako číslo
                    var digits = new string(value.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(digits))
                    {
                        Current.VariableSymbol = digits.Length > 10 ? digits[^10..] : digits;
                        OnPropertyChanged(nameof(Current));
                    }
                }
            }
        }

        public MainViewModel()
        {
            Current.PropertyChanged += Current_PropertyChanged;
            NewInvoice();
            Items.CollectionChanged += Items_CollectionChanged;
            HookSupplierWatcher(Current.Supplier);
            LoadBanks();
            _ = LoadSavedParties();
        }

        public async Task LoadSavedParties()
        {
            try
            {
                var suppliers = await _suppliersService.LoadAsync();
                SavedSuppliers.Clear();
                foreach (var s in suppliers) SavedSuppliers.Add(s);

                var customers = await _customersService.LoadAsync();
                SavedCustomers.Clear();

                SavedCustomers.Clear();
                foreach (var c in customers) SavedCustomers.Add(c);

                var items = await _savedItemsService.LoadAsync();
                SavedItems.Clear();
                foreach (var i in items) SavedItems.Add(i);
            }
            catch { /* ignore */ }
        }

        partial void OnSelectedSavedSupplierChanged(Company? value)
        {
            if (value == null) return;
            if (Current?.Supplier == null) return;

            Current.Supplier.Name = value.Name ?? "";
            Current.Supplier.Address = value.Address ?? "";
            Current.Supplier.City = $"{value.City} {value.PostalCode}".Trim();
            Current.Supplier.ICO = value.ICO ?? "";
            Current.Supplier.DIC = value.DIC ?? "";
            Current.Supplier.Bank = value.Bank ?? "";
            Current.Supplier.AccountNumber = value.AccountNumber ?? "";
            Current.Supplier.IBAN = value.IBAN ?? "";
            Current.Supplier.Email = value.Email ?? "";
            Current.Supplier.Phone = value.Phone ?? "";
            Current.Supplier.Country = "Česká republika";
            
            SupplierIsVatPayer = value.IsVatPayer;
            
            // Trigger update
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(Current.Supplier));
            RaiseTotalsChanged();
        }

        partial void OnSelectedSavedCustomerChanged(Company? value)
        {
            if (value == null) return;
            if (Current?.Customer == null) return;

            Current.Customer.Name = value.Name ?? "";
            Current.Customer.Address = value.Address ?? "";
            Current.Customer.City = $"{value.City} {value.PostalCode}".Trim();
            Current.Customer.ICO = value.ICO ?? "";
            Current.Customer.DIC = value.DIC ?? "";
            // Current.Customer.Bank ... u odběratele obvykle neřešíme, ale model to má
            Current.Customer.Email = value.Email ?? "";
            Current.Customer.Phone = value.Phone ?? "";
            Current.Customer.Country = "Česká republika";

            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(Current.Customer));
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
            else if (e.PropertyName == nameof(Invoice.Type) || e.PropertyName == nameof(Invoice.IssueDate))
            {
                UpdateDueDate();
                OnPropertyChanged(nameof(DisplayNumber));

                // SMART DATE LOGIC:
                // Pokud je to nový doklad (ne načtený z historie) a změní se rok vystavení,
                // automaticky přečíslujeme doklad pro daný rok.
                if (_isNewDocument && e.PropertyName == nameof(Invoice.IssueDate))
                {
                    var newYear = Current.IssueDate != default ? Current.IssueDate.Year : DateTime.Now.Year;
                    var currentNum = Current.Number;
                    
                    // Zjednodušený regex bez \b pro jistotu
                    var match = Regex.Match(currentNum, @"(20\d{2})");
                    
                    if (match.Success)
                    {
                        if (int.TryParse(match.Value, out int currentYearVal))
                        {
                            if (currentYearVal != newYear)
                            {
                                if (Current.Type == DocType.Invoice)
                                {
                                    var newNum = _num.ReserveInvoiceNumber(newYear);
                                    Current.Number = $"FA-{newNum}"; 
                                }
                                else
                                {
                                    var newNum = _num.ReserveOrderNumber(newYear);
                                    Current.Number = $"OBJ-{newNum}";
                                }
                                
                                var digits = new string(Current.Number.Where(char.IsDigit).ToArray());
                                Current.VariableSymbol = digits.Length > 10 ? digits[^10..] : digits;
                                OnPropertyChanged(nameof(Current));
                                OnPropertyChanged(nameof(DisplayNumber)); // Force UI update
                            }
                        }
                    }
                }
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
            var sup = Current?.Supplier;
            if (sup == null) return;

            // 1) nejdřív promítni aktuální výběr (dočasně)
            if (value != null) { sup.Bank = value.Name; sup.SWIFT = value.Swift; }
            else { sup.Bank = string.Empty; sup.SWIFT = string.Empty; }

            // 2) zjisti kód banky z účtu
            var codeFromAcc = GetBankCodeFromAccount(sup.AccountNumber);
            if (string.IsNullOrEmpty(codeFromAcc)) return;

            var must = Banks.FirstOrDefault(b =>
                string.Equals((b.Code ?? "").Trim(), codeFromAcc, StringComparison.Ordinal));

            if (must == null) return;

            // 3) pokud neodpovídá, přepiš výběr banky *po* skončení aktuálního UI cyklu
            if (!ReferenceEquals(SelectedBank, must))
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        SelectedBank = must;
                        sup.Bank = must.Name;
                        sup.SWIFT = must.Swift;
                    }),
                    DispatcherPriority.ApplicationIdle
                );
            }
        }

        private static string? GetBankCodeFromAccount(string? acc)
        {
            if (string.IsNullOrWhiteSpace(acc)) return null;
            var m = Regex.Match(acc.Trim(), @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
            return m.Success ? m.Groups[3].Value : null;
        }

        partial void OnSupplierIsVatPayerChanged(bool value) => RaiseTotalsChanged();

        // Změna PaymentTermDays -> přepočítej splatnost (omezíme 0..365)
        partial void OnPaymentTermDaysChanged(int value)
        {
            var clamped = Math.Max(0, Math.Min(365, value));
            if (clamped != value)
            {
                PaymentTermDays = clamped; // jednorázové narovnání, rekurze skončí hned
                return;
            }
            UpdateDueDate();
        }

        private void UpdateDueDate()
        {
            if (Current == null) return;

            var issue = Current.IssueDate == default ? DateTime.Today : Current.IssueDate;

            if (Current.Type == DocType.Invoice)
            {
                Current.DueDate = issue.AddDays(PaymentTermDays);
            }
            else
            {
                // U objednávky pole v UI schováváme, ale hodnotu držíme na rozumném datu
                Current.DueDate = issue;
            }
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

        private decimal ComputeGrossTotal() => ComputeBaseTotal() + ComputeVatTotal();

        private decimal ComputeRoundedTotal() =>
            Math.Round(ComputeGrossTotal(), 0, MidpointRounding.AwayFromZero);

        private decimal ComputeRounding() => ComputeRoundedTotal() - ComputeGrossTotal();

        private static string FormatMoney(decimal value, string currency)
        {
            var culture = new System.Globalization.CultureInfo("cs-CZ");
            var formatted = value.ToString("N0", culture);
            var curr = string.IsNullOrWhiteSpace(currency) ? "Kč" : currency;
            // Vynutit zobrazení Kč, pokud je v datech CZK (case-insensitive)
            if (string.Equals(curr.Trim(), "CZK", StringComparison.OrdinalIgnoreCase)) curr = "Kč";
            return $"{formatted} {curr}";
        }

        private static string FormatSignedMoney(decimal value, string currency)
        {
            if (value == 0m) return FormatMoney(0m, currency);
            var sign = value > 0 ? "+" : "-";
            var ci = new CultureInfo("cs-CZ");
            var abs = Math.Abs(value);
            return $"{sign}{string.Format(ci, "{0:N0}", abs)} {currency}";
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

                // 1) přepočti/normalizuj IBAN z účtu
                var iban = TryBuildCzIbanFromAccount(acc);
                sup.IBAN = string.IsNullOrWhiteSpace(iban)
                    ? string.Empty
                    : iban.Replace(" ", "").ToUpperInvariant();

                // 2) podle kódu banky za lomítkem automaticky vyber banku v combu
                if (!string.IsNullOrWhiteSpace(acc))
                {
                    var m = Regex.Match(acc.Trim(), @"^\s*(?:(\d{0,6})-)?(\d{1,10})/(\d{4})\s*$");
                    if (m.Success)
                    {
                        var bankCode = m.Groups[3].Value; // poslední 4 číslice
                        var match = Banks.FirstOrDefault(b =>
                            string.Equals((b.Code ?? "").Trim(), bankCode, StringComparison.Ordinal));

                        if (match != null && !ReferenceEquals(SelectedBank, match))
                            SelectedBank = match; // vyvolá OnSelectedBankChanged -> doplní název/SWIFT
                    }
                    // když formát neodpovídá, banku neměníme
                }
                else
                {
                    // prázdný účet -> zruš volbu banky
                    SelectedBank = null;
                }

                OnPropertyChanged(nameof(Current));
            }
            else if (e.PropertyName == nameof(Party.IBAN))
            {
                // ruční změna IBANu -> normalizovat
                var normalized = (sup.IBAN ?? string.Empty).Replace(" ", "").ToUpperInvariant();
                sup.IBAN = normalized;
                OnPropertyChanged(nameof(Current));
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
            _isNewDocument = true;
            var newNumber = _num.ReserveInvoiceNumber();
            Current.PropertyChanged -= Current_PropertyChanged;
            Current = new Invoice
            {
                Type = DocType.Invoice,
                Number = $"FA-{newNumber}",
                Currency = "Kč",
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
            SelectedSavedSupplier = null;
            SelectedSavedCustomer = null;
            SupplierSearchText = "Vyberte ze seznamu...";
            CustomerSearchText = "Vyberte ze seznamu...";
            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));

            UpdateDueDate(); // použij PaymentTermDays
        }

        [RelayCommand]
        private void NewOrder()
        {
            _isNewDocument = true;
            var newNumber = _num.ReserveOrderNumber();
            Current.PropertyChanged -= Current_PropertyChanged;
            Current = new Invoice
            {
                Type = DocType.Order,
                Number = $"OBJ-{newNumber}",
                Currency = "Kč",
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
            SelectedSavedSupplier = null;
            SelectedSavedCustomer = null;
            SupplierSearchText = "Vyberte ze seznamu...";
            CustomerSearchText = "Vyberte ze seznamu...";
            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));

            UpdateDueDate(); // u OBJ nastaví na issue date
        }

        public void LoadDocument(Invoice doc)
        {
            _isNewDocument = false;
            Current.PropertyChanged -= Current_PropertyChanged;
            
            Current = doc;
            Current.PropertyChanged += Current_PropertyChanged;

            Items.Clear();
            foreach (var item in doc.Items) Items.Add(item);

            SupplierIsVatPayer = !string.IsNullOrEmpty(doc.Supplier.DIC);
            if (!string.IsNullOrEmpty(doc.Supplier.Bank))
                SelectedBank = Banks.FirstOrDefault(b => b.Name == doc.Supplier.Bank);

            SupplierSearchText = doc.Supplier.Name;
            CustomerSearchText = doc.Customer.Name;

            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));
        }

        public void CreateFromOrder(Invoice order)
        {
            _isNewDocument = true;
            Current.PropertyChanged -= Current_PropertyChanged;

            var newNumber = _num.ReserveInvoiceNumber(); 
            // Vytvoříme novou instanci faktury na základě objednávky
            Current = new Invoice
            {
                Type = DocType.Invoice,
                Number = $"FA-{newNumber}",
                IssueDate = DateTime.Today,
                TaxableSupplyDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(PaymentTermDays),
                Currency = order.Currency,
                PaymentMethod = order.PaymentMethod,
                Notes = order.Notes,
                
                // Hluboká kopie objektů Party, aby změna na faktuře neměnila zpětně objednávku
                Supplier = new Party 
                { 
                    Name = order.Supplier.Name, Address = order.Supplier.Address, City = order.Supplier.City,
                    ICO = order.Supplier.ICO, DIC = order.Supplier.DIC, Bank = order.Supplier.Bank,
                    AccountNumber = order.Supplier.AccountNumber, SWIFT = order.Supplier.SWIFT, IBAN = order.Supplier.IBAN,
                    Email = order.Supplier.Email, Phone = order.Supplier.Phone, Country = order.Supplier.Country
                },
                Customer = new Party 
                { 
                    Name = order.Customer.Name, Address = order.Customer.Address, City = order.Customer.City,
                    ICO = order.Customer.ICO, DIC = order.Customer.DIC, Email = order.Customer.Email, 
                    Phone = order.Customer.Phone, Country = order.Customer.Country
                }
            };
            
            Current.VariableSymbol = new string(newNumber.Where(char.IsDigit).ToArray());
            if (Current.VariableSymbol.Length > 10) Current.VariableSymbol = Current.VariableSymbol[^10..];

            Current.PropertyChanged += Current_PropertyChanged;

            // Zkopírovat položky (nové instance)
            Items.Clear();
            foreach (var existing in order.Items)
            {
                var newItem = new InvoiceItem
                {
                    Name = existing.Name,
                    Quantity = existing.Quantity,
                    Unit = existing.Unit,
                    UnitPrice = existing.UnitPrice,
                    VatRate = existing.VatRate
                };
                Items.Add(newItem);
                Current.Items.Add(newItem);
            }

            // UI stav
            SupplierIsVatPayer = !string.IsNullOrEmpty(Current.Supplier.DIC);
            if (!string.IsNullOrEmpty(Current.Supplier.Bank))
                SelectedBank = Banks.FirstOrDefault(b => b.Name == Current.Supplier.Bank);

            SupplierSearchText = Current.Supplier.Name;
            CustomerSearchText = Current.Customer.Name;

            HookSupplierWatcher(Current.Supplier);
            RaiseTotalsChanged();
            OnPropertyChanged(nameof(DisplayNumber));
        }

        private readonly ConfigService _configService = new();

        // ... (existing fields)

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
                    var config = await _configService.LoadAsync();
                    var path = await Task.Run(() => _pdf.SaveInvoicePdf(Current, qrPng, dialog.FileName, config));

                    // úspěšný export -> potvrdit číslo
                    if (Current.Type == DocType.Invoice) _num.CommitInvoice();
                    else _num.CommitOrder();

                    // Uložit do historie
                    await _docService.SaveDocumentAsync(Current);

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
