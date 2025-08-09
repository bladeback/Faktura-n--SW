using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using InvoiceApp.Models;

namespace InvoiceApp.Services
{
    public class PdfService
    {
        public string SaveInvoicePdf(Invoice invoice, byte[]? qrPng, string filePath)
        {
            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            bool supplierIsVatPayer = !string.IsNullOrWhiteSpace(invoice.Supplier?.DIC);

            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.PageColor(Colors.White);

                    page.Header().Element(c => ComposeHeader(c, invoice));
                    page.Content().Element(c => ComposeBody(c, invoice, supplierIsVatPayer, qrPng));
                    page.Footer().AlignRight().Text(txt =>
                    {
                        txt.Span("Vygenerováno aplikací InvoiceApp • ").FontColor(Colors.Grey.Darken1);
                        txt.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            doc.GeneratePdf(filePath);
            return filePath;
        }

        private void ComposeHeader(IContainer container, Invoice inv)
        {
            var title = inv.Type == DocType.Invoice ? "FAKTURA (daňový doklad)" : "OBJEDNÁVKA";

            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(title).FontSize(18).SemiBold().FontColor(Colors.Black);
                    col.Item().Text($"Číslo: {inv.Number}").FontSize(11);
                    col.Item().Text($"Vystaveno: {FormatDate(inv.IssueDate)}").FontSize(11);
                    if (inv.Type == DocType.Invoice)
                    {
                        col.Item().Text($"Splatnost: {FormatDate(inv.DueDate)}").FontSize(11);
                        if (inv.TaxableSupplyDate.HasValue)
                        {
                            col.Item().Text($"Datum zd. plnění: {FormatDate(inv.TaxableSupplyDate.Value)}").FontSize(11);
                        }
                    }
                });

                row.ConstantItem(160).AlignRight().Column(col =>
                {
                    col.Item().Border(0.5f).Padding(6).AlignCenter().Text("LOGO").FontSize(12).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        private void ComposeBody(IContainer container, Invoice inv, bool supplierIsVatPayer, byte[]? qrPng)
        {
            container.PaddingTop(10).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => PartyBox(c, inv, isSupplier: true, supplierIsVatPayer));
                    row.ConstantItem(18);
                    row.RelativeItem().Element(c => PartyBox(c, inv, isSupplier: false, supplierIsVatPayer: null));
                });

                if (inv.Type == DocType.Invoice)
                {
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Element(c => PaymentBox(c, inv));
                        if (qrPng is { Length: > 0 })
                        {
                            row.ConstantItem(150).PaddingLeft(10).Column(q =>
                            {
                                q.Item().Text("QR platba").SemiBold().FontSize(10);
                                q.Item().Height(4);
                                q.Item().Border(0.5f).Padding(4).AlignCenter().Image(qrPng);
                            });
                        }
                    });
                }

                col.Item().PaddingTop(12).Element(c => ItemsTable(c, inv, supplierIsVatPayer));

                col.Item().PaddingTop(10).Element(c => TotalsBox(c, inv, supplierIsVatPayer));

                if (!string.IsNullOrWhiteSpace(inv.Notes))
                    col.Item().PaddingTop(8).Text($"Poznámka: {inv.Notes}").FontSize(10);
            });
        }

        private void PartyBox(IContainer container, Invoice inv, bool isSupplier, bool? supplierIsVatPayer)
        {
            var party = isSupplier ? inv.Supplier : inv.Customer;
            if (party is null) return;

            container.Border(0.5f).Padding(8).Column(col =>
            {
                col.Item().Text(isSupplier ? "Dodavatel" : "Odběratel").SemiBold().FontSize(11);
                col.Item().PaddingTop(2).Text(party.Name ?? "").SemiBold().FontSize(11);
                col.Item().Text(party.Address ?? "");
                col.Item().Text(party.City ?? "");
                if (!string.IsNullOrWhiteSpace(party.Country))
                    col.Item().Text(party.Country);

                col.Item().PaddingTop(2).Text(txt =>
                {
                    txt.Span("IČO: ").SemiBold();
                    txt.Span(party.ICO ?? "");
                });
                col.Item().Text(txt =>
                {
                    txt.Span("DIČ: ").SemiBold();
                    txt.Span(party.DIC ?? "");
                });

                if (!string.IsNullOrWhiteSpace(party.Email))
                {
                    col.Item().PaddingTop(2).Text(txt =>
                    {
                        txt.Span("E-mail: ").SemiBold();
                        txt.Span(party.Email);
                    });
                }
                if (!string.IsNullOrWhiteSpace(party.Phone))
                {
                    col.Item().PaddingTop(2).Text(txt =>
                    {
                        txt.Span("Telefon: ").SemiBold();
                        txt.Span(party.Phone);
                    });
                }

                if (isSupplier && supplierIsVatPayer.HasValue)
                {
                    col.Item().PaddingTop(4).Text(supplierIsVatPayer.Value
                        ? "Dodavatel je plátce DPH."
                        : "Dodavatel není plátce DPH.").FontColor(Colors.Grey.Darken1);
                }
            });
        }

        private void PaymentBox(IContainer container, Invoice inv)
        {
            var supplier = inv.Supplier;
            if (supplier is null) return;

            container.Border(0.5f).Padding(8).Column(col =>
            {
                col.Item().Text("Platební údaje").SemiBold().FontSize(11);

                if (!string.IsNullOrWhiteSpace(inv.PaymentMethod))
                {
                    col.Item().PaddingTop(2).Text(txt =>
                    {
                        txt.Span("Způsob úhrady: ").SemiBold();
                        txt.Span(inv.PaymentMethod);
                    });
                }

                if (!string.IsNullOrWhiteSpace(supplier.AccountNumber))
                {
                    col.Item().PaddingTop(2).Text(txt =>
                    {
                        txt.Span("Účet: ").SemiBold();
                        txt.Span(supplier.AccountNumber);
                    });
                }

                var displayIban = FormatIbanDisplay(inv.PaymentIban ?? supplier.IBAN ?? "");
                col.Item().PaddingTop(2).Text(txt =>
                {
                    txt.Span("IBAN: ").SemiBold();
                    txt.Span(displayIban);
                });

                if (!string.IsNullOrWhiteSpace(supplier.SWIFT))
                {
                    col.Item().Text(txt =>
                    {
                        txt.Span("SWIFT: ").SemiBold();
                        txt.Span(supplier.SWIFT);
                    });
                }

                col.Item().Text(txt =>
                {
                    txt.Span("Variabilní symbol: ").SemiBold();
                    txt.Span(inv.VariableSymbol ?? "");
                });

                if (!string.IsNullOrWhiteSpace(inv.ConstantSymbol))
                {
                    col.Item().Text(txt =>
                    {
                        txt.Span("Konstantní symbol: ").SemiBold();
                        txt.Span(inv.ConstantSymbol);
                    });
                }

                col.Item().Text(txt =>
                {
                    txt.Span("Měna: ").SemiBold();
                    txt.Span(inv.Currency ?? "CZK");
                });
            });
        }

        private void ItemsTable(IContainer container, Invoice inv, bool supplierIsVatPayer)
        {
            // --- ZDE JE OPRAVA ---
            var items = inv.Items;

            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(5);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                    if (supplierIsVatPayer) cols.RelativeColumn(2);
                    cols.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Položka");
                    header.Cell().Element(HeaderCell).Text("Mj");
                    header.Cell().Element(HeaderCell).Text("Množství");
                    header.Cell().Element(HeaderCell).Text("Cena/ks");
                    if (supplierIsVatPayer) header.Cell().Element(HeaderCell).Text("DPH (%)");
                    header.Cell().Element(HeaderCell).Text(supplierIsVatPayer ? "Celkem s DPH" : "Celkem");
                });

                foreach (var it in items)
                {
                    table.Cell().Element(BodyCell).Text(it.Name);
                    table.Cell().Element(BodyCell).Text(it.Unit);
                    table.Cell().Element(BodyCell).Text(FormatNumber(it.Quantity));
                    table.Cell().Element(BodyCell).Text(FormatMoney(it.UnitPrice, inv.Currency));
                    if (supplierIsVatPayer) table.Cell().Element(BodyCell).Text(FormatPercent(it.VatRate));
                    var lineTotal = supplierIsVatPayer ? it.Quantity * it.UnitPrice * (1 + it.VatRate) : it.Quantity * it.UnitPrice;
                    table.Cell().Element(BodyCell).Text(FormatMoney(lineTotal, inv.Currency));
                }
            });

            static IContainer HeaderCell(IContainer c) => c.BorderBottom(1).PaddingVertical(4).DefaultTextStyle(x => x.SemiBold());
            static IContainer BodyCell(IContainer c) => c.PaddingVertical(4);
        }

        private void TotalsBox(IContainer container, Invoice inv, bool supplierIsVatPayer)
        {
            // --- ZDE JE DRUHÁ OPRAVA ---
            var items = inv.Items;

            decimal baseTotal = items.Sum(i => i.Quantity * i.UnitPrice);
            decimal vatTotal = supplierIsVatPayer ? items.Sum(i => i.Quantity * i.UnitPrice * i.VatRate) : 0m;
            decimal grandTotal = baseTotal + vatTotal;

            container.Row(row =>
            {
                row.RelativeItem().Text(" ");
                row.ConstantItem(260).Border(0.5f).Padding(8).Column(stack =>
                {
                    if (supplierIsVatPayer)
                    {
                        stack.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Základ bez DPH:");
                            r.ConstantItem(120).AlignRight().Text(FormatMoney(baseTotal, inv.Currency));
                        });
                        stack.Item().Row(r =>
                        {
                            r.RelativeItem().Text("DPH celkem:");
                            r.ConstantItem(120).AlignRight().Text(FormatMoney(vatTotal, inv.Currency));
                        });
                        stack.Item().BorderTop(0.5f).PaddingTop(6).Row(r =>
                        {
                            r.RelativeItem().Text("Celkem k úhradě:");
                            r.ConstantItem(120).AlignRight().Text(FormatMoney(grandTotal, inv.Currency)).SemiBold();
                        });
                    }
                    else
                    {
                        stack.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Celkem k úhradě:");
                            r.ConstantItem(120).AlignRight().Text(FormatMoney(baseTotal, inv.Currency)).SemiBold();
                        });
                    }
                });
            });
        }

        private static string FormatDate(DateTime dt) => dt.ToString("dd.MM.yyyy", new CultureInfo("cs-CZ"));

        private static string FormatMoney(decimal value, string? currency)
        {
            var ci = new CultureInfo("cs-CZ");
            var formatted = string.Format(ci, "{0:N2}", value);
            return string.IsNullOrWhiteSpace(currency) ? formatted : $"{formatted} {currency}";
        }

        private static string FormatNumber(decimal value)
        {
            var ci = new CultureInfo("cs-CZ");
            return string.Format(ci, "{0:N2}", value);
        }

        private static string FormatPercent(decimal rate)
        {
            decimal pct = rate * 100m;
            var ci = new CultureInfo("cs-CZ");
            return string.Format(ci, "{0:N0} %", pct);
        }

        private static string FormatIbanDisplay(string? iban)
        {
            if (string.IsNullOrWhiteSpace(iban)) return string.Empty;
            var compact = iban.Replace(" ", "").ToUpperInvariant();

            return string.Join(" ", Enumerable.Range(0, (compact.Length + 3) / 4)
                                             .Select(i => i * 4)
                                             .TakeWhile(i => i < compact.Length)
                                             .Select(i => compact.Substring(i, Math.Min(4, compact.Length - i))));
        }
    }
}