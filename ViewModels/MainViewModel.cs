using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoiceApp.Models;
using InvoiceApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
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

        // >>> Přidané: ručně deklarovaný command (nečekáme na generator)
        public IAsyncRelayCommand LoadCustomerFromIcoCommand { get; }

        public MainViewModel()
        {
            // výchozí hodnoty dodavatele
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

            // >>> Přidané: explicitní napojení tlačítka na metodu
            LoadCustomerFromIcoCommand = new AsyncRelayCommand(LoadCustomerFromIco);
        }

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
            OnPropertyChanged(nameof(Current));
        }

        [RelayCommand]
        private void RemoveSelected(object? param)
        {
            if (param is InvoiceItem it)
            {
                Items.Remove(it);
                Current.Items.Remove(it);
                OnPropertyChanged(nameof(Current));
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
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                string payload = _qr.BuildCzechQrPaymentPayload(
                    Current.PaymentIban,
                    Current.Total,
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

        // ===== ARES: načtení odběratele podle IČO =====
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

                if (!string.IsNullOrWhiteSpace(name)) Current.Customer.Name = name;
                if (!string.IsNullOrWhiteSpace(address)) Current.Customer.Address = address;
                if (!string.IsNullOrWhiteSpace(city)) Current.Customer.City = city;
                if (!string.IsNullOrWhiteSpace(dic)) Current.Customer.DIC = dic;

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
    }
}
