using InvoiceApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace InvoiceApp.Services
{
    public class PdfService
    {
        private static readonly string ThemeColor = "#009A8D";
        private static readonly string GreyColor = "#F0F0F0";

        public string SaveInvoicePdf(Invoice invoice, byte[]? qrPng, string filePath)
        {
            if (invoice == null)
                throw new ArgumentNullException(nameof(invoice));

            QuestPDF.Settings.License = LicenseType.Community;

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Calibri));
                    page.PageColor(Colors.White);

                    page.Header().Element(c => ComposeHeader(c, invoice, qrPng));
                    page.Content().Element(c => ComposeBody(c, invoice));
                    page.Footer().Element(c => ComposeFooter(c, invoice));
                });
            });

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            doc.GeneratePdf(filePath);
            return filePath;
        }

        private void ComposeHeader(IContainer container, Invoice inv, byte[]? qrPng)
        {
            var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);
            var title = inv.Type == DocType.Invoice
                ? (isVatPayer ? "Faktura - daňový doklad" : "Faktura")
                : "Objednávka";

            container.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(t => t.Span(title).FontSize(16).SemiBold());
                    col.Item().Text(t => t.Span(inv.Number).FontSize(16).SemiBold());

                    col.Item().PaddingTop(15).Text(t =>
                    {
                        t.Span("Datum vystavení: ").SemiBold();
                        t.Span(FormatDate(inv.IssueDate));
                    });

                    if (inv.Type == DocType.Invoice)
                    {
                        if (inv.TaxableSupplyDate.HasValue)
                        {
                            col.Item().Text(t =>
                            {
                                t.Span("Datum zdan. plnění: ").SemiBold();
                                t.Span(FormatDate(inv.TaxableSupplyDate.Value));
                            });
                        }

                        col.Item().Text(t =>
                        {
                            t.Span("Datum splatnosti: ").SemiBold();
                            t.Span(FormatDate(inv.DueDate));
                        });
                    }
                });

                // QR + box „K úhradě“ jen u faktury
                row.ConstantItem(150).Column(col =>
                {
                    if (inv.Type == DocType.Invoice && qrPng is { Length: > 0 })
                    {
                        col.Item().AlignRight().Text("QR Platba+F").FontSize(8);
                        col.Item().Image(qrPng);
                    }

                    if (inv.Type == DocType.Invoice)
                    {
                        col.Item().PaddingTop(10).Background(GreyColor).Padding(8).Column(summaryCol =>
                        {
                            summaryCol.Item().AlignCenter().Text("K úhradě").FontSize(10);
                            decimal total = inv.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + (isVatPayer ? i.VatRate : 0)));
                            summaryCol.Item().AlignCenter().Text(t => t.Span(FormatMoney(total, inv.Currency)).Bold().FontSize(14));
                        });
                    }
                });
            });
        }

        private void ComposeBody(IContainer container, Invoice inv)
        {
            container.PaddingTop(20).Column(col =>
            {
                // Dodavatel / Odběratel
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Dodavatel", inv.Supplier));
                    row.ConstantItem(20);
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Odběratel", inv.Customer));
                });

                // Platební údaje vlevo – jen u Faktury. U OBJ zobrazíme jen kontakty přes celou šířku.
                col.Item().PaddingTop(10).Row(row =>
                {
                    if (inv.Type == DocType.Invoice)
                    {
                        row.RelativeItem().Element(c => ComposePaymentDetails(c, inv));
                        row.ConstantItem(20);
                        row.RelativeItem().Element(c => ComposeContactDetails(c, inv.Supplier));
                    }
                    else
                    {
                        row.RelativeItem().Element(c => ComposeContactDetails(c, inv.Supplier));
                    }
                });

                // Položky
                col.Item().PaddingTop(20).Element(c => ItemsTable(c, inv));

                // Souhrn (včetně zaokrouhlení) – necháme pro oba typy dokumentů.
                col.Item().PaddingTop(10).Element(c => ComposeVatSummary(c, inv));
            });
        }

        private void ComposeFooter(IContainer container, Invoice inv)
        {
            container.Column(col =>
            {
                if (inv.Type == DocType.Invoice)
                {
                    col.Item().PaddingBottom(5).Text(t =>
                    {
                        t.Span("Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře Vám můžeme účtovat zákonný úrok z prodlení.")
                         .FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                }

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(t =>
                    {
                        t.Span("Vystaveno v aplikaci InvoiceApp").FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium));
                        t.Span("Strana ");
                        t.CurrentPageNumber();
                        t.Span(" / ");
                        t.TotalPages();
                    });
                });
            });
        }

        private void ComposePartyAddress(IContainer container, string title, Party? party)
        {
            if (party is null) return;
            var isVatPayer = !string.IsNullOrWhiteSpace(party.DIC);

            container.Column(col =>
            {
                col.Item().Text(t => t.Span(title).SemiBold().FontColor(ThemeColor).FontSize(10));
                col.Item().PaddingBottom(5).BorderBottom(1).BorderColor(ThemeColor);

                col.Item().PaddingTop(5).Text(t => t.Span(party.Name ?? "").Bold());
                col.Item().Text(party.Address ?? "");
                col.Item().Text(party.City ?? "");

                if (!string.IsNullOrWhiteSpace(party.Country))
                    col.Item().Text(party.Country);

                col.Item().PaddingTop(8).Text($"IČ: {party.ICO ?? ""}");
                if (isVatPayer)
                    col.Item().Text($"DIČ: {party.DIC ?? ""}");
            });
        }

        private void ComposePaymentDetails(IContainer container, Invoice inv)
        {
            var supplier = inv.Supplier;
            if (supplier is null) return;

            container.Column(col =>
            {
                col.Item().Text(t => t.Span("Platební údaje").SemiBold().FontColor(ThemeColor).FontSize(10));
                col.Item().PaddingBottom(5).BorderBottom(1).BorderColor(ThemeColor);

                col.Item().PaddingTop(5).Row(row =>
                {
                    row.ConstantItem(100).Text(t => t.Span("Způsob platby:").SemiBold());
                    row.RelativeItem().Text(inv.PaymentMethod ?? "Převodem");
                });
                col.Item().Row(row =>
                {
                    row.ConstantItem(100).Text(t => t.Span("Bankovní účet:").SemiBold());
                    row.RelativeItem().Text(supplier.AccountNumber ?? "");
                });
                col.Item().Row(row =>
                {
                    row.ConstantItem(100).Text(t => t.Span("IBAN:").SemiBold());
                    row.RelativeItem().Text(FormatIbanDisplay(inv.PaymentIban ?? supplier.IBAN ?? ""));
                });
                col.Item().Row(row =>
                {
                    row.ConstantItem(100).Text(t => t.Span("SWIFT:").SemiBold());
                    row.RelativeItem().Text(supplier.SWIFT ?? "");
                });
                col.Item().Row(row =>
                {
                    row.ConstantItem(100).Text(t => t.Span("Variabilní symbol:").SemiBold());
                    row.RelativeItem().Text(inv.VariableSymbol ?? "");
                });
                if (!string.IsNullOrWhiteSpace(inv.ConstantSymbol))
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(100).Text(t => t.Span("Konstantní symbol:").SemiBold());
                        row.RelativeItem().Text(inv.ConstantSymbol);
                    });
                }
            });
        }

        private void ComposeContactDetails(IContainer container, Party? supplier)
        {
            if (supplier is null) return;

            container.Column(col =>
            {
                col.Item().Text(t => t.Span("Kontaktní údaje").SemiBold().FontColor(ThemeColor).FontSize(10));
                col.Item().PaddingBottom(5).BorderBottom(1).BorderColor(ThemeColor);

                if (!string.IsNullOrWhiteSpace(supplier.Email))
                {
                    col.Item().PaddingTop(5).Row(row =>
                    {
                        row.ConstantItem(50).Text(t => t.Span("E-mail:").SemiBold());
                        row.RelativeItem().Text(supplier.Email);
                    });
                }
                if (!string.IsNullOrWhiteSpace(supplier.Phone))
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(50).Text(t => t.Span("Telefon:").SemiBold());
                        row.RelativeItem().Text(supplier.Phone);
                    });
                }
            });
        }

        private void ItemsTable(IContainer container, Invoice inv)
        {
            var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);    // Označení dodávky
                    columns.RelativeColumn(1.5f); // Počet m.j.
                    columns.RelativeColumn(2);    // Cena za m.j.
                    if (isVatPayer)
                    {
                        columns.RelativeColumn(1.2f); // DPH %
                        columns.RelativeColumn(2);    // Bez DPH
                        columns.RelativeColumn(2);    // DPH
                    }
                    columns.RelativeColumn(2.5f); // Celkem
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderLeft).Text("Označení dodávky");
                    header.Cell().Element(HeaderRight).Text("Počet m.j.");
                    header.Cell().Element(HeaderRight).Text("Cena za m.j.");
                    if (isVatPayer)
                    {
                        header.Cell().Element(HeaderRight).Text("DPH %");
                        header.Cell().Element(HeaderRight).Text("Bez DPH");
                        header.Cell().Element(HeaderRight).Text("DPH");
                    }
                    header.Cell().Element(HeaderRight).Text("Celkem");

                    static IContainer HeaderLeft(IContainer c)
                        => c.Background(GreyColor).Padding(5).DefaultTextStyle(x => x.SemiBold());

                    static IContainer HeaderRight(IContainer c)
                        => c.Background(GreyColor).Padding(5).DefaultTextStyle(x => x.SemiBold()).AlignRight();
                });

                foreach (var item in inv.Items)
                {
                    var basePrice = item.Quantity * item.UnitPrice;
                    var vatAmount = isVatPayer ? basePrice * item.VatRate : 0;
                    var totalPrice = basePrice + vatAmount;

                    table.Cell().Element(BodyCell).Text(item.Name);
                    table.Cell().Element(BodyCell).AlignRight().Text($"{FormatNumber(item.Quantity)} {item.Unit}");
                    table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(item.UnitPrice, null));

                    if (isVatPayer)
                    {
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatPercent(item.VatRate));
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(basePrice, null));
                        table.Cell().Element(BodyCell).AlignRight().Text(FormatMoney(vatAmount, null));
                        table.Cell().Element(BodyCell).AlignRight().Background(GreyColor)
                             .Text(t => t.Span(FormatMoney(totalPrice, inv.Currency)).SemiBold());
                    }
                    else
                    {
                        table.Cell().Element(BodyCell).AlignRight().Background(GreyColor)
                             .Text(t => t.Span(FormatMoney(basePrice, inv.Currency)).SemiBold());
                    }
                }

                static IContainer BodyCell(IContainer c)
                    => c.BorderBottom(1).BorderColor(GreyColor).Padding(5);
            });
        }

        private void ComposeVatSummary(IContainer container, Invoice inv)
        {
            var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);

            container.AlignRight().Width(350).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(SummaryHeader).Text("Sazba DPH");
                    header.Cell().Element(SummaryHeader).AlignRight().Text("Základ");
                    header.Cell().Element(SummaryHeader).AlignRight().Text("Výše DPH");
                    header.Cell().Element(SummaryHeader).AlignRight().Text("Celkem");
                });

                var vatSummary = inv.Items
                    .GroupBy(i => i.VatRate)
                    .Select(g => new
                    {
                        VatRate = g.Key,
                        BaseTotal = g.Sum(i => i.Quantity * i.UnitPrice),
                        VatTotal = g.Sum(i => i.Quantity * i.UnitPrice * i.VatRate)
                    })
                    .OrderBy(g => g.VatRate);

                if (isVatPayer)
                {
                    foreach (var s in vatSummary)
                    {
                        table.Cell().Element(SummaryBody).Text(FormatPercent(s.VatRate));
                        table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal, inv.Currency));
                        table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.VatTotal, inv.Currency));
                        table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal + s.VatTotal, inv.Currency));
                    }
                }

                var totalBase = vatSummary.Sum(s => s.BaseTotal);
                var totalVat = isVatPayer ? vatSummary.Sum(s => s.VatTotal) : 0m;
                var gross = totalBase + totalVat;

                var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
                var rounding = rounded - gross;

                // Footer – součty a zaokrouhlení
                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(4).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => t.Span("Celkem (bez zaokrouhlení)").SemiBold());
                            row.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(gross, inv.Currency)).SemiBold());
                        });

                        if (rounding != 0m)
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Zaokrouhlení");
                                row.RelativeItem().AlignRight().Text(FormatSignedMoney(rounding, inv.Currency));
                            });
                        }

                        col.Item().PaddingTop(2).Row(row =>
                        {
                            row.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                            row.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(12));
                        });
                    });
                });

                static IContainer SummaryHeader(IContainer c)
                    => c.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4).DefaultTextStyle(x => x.SemiBold());

                static IContainer SummaryBody(IContainer c)
                    => c.Padding(4);
            });
        }

        #region Helpers

        private static string FormatDate(DateTime dt)
            => dt.ToString("dd.MM.yyyy", new CultureInfo("cs-CZ"));

        private static string FormatMoney(decimal value, string? currency)
        {
            var ci = new CultureInfo("cs-CZ");
            var formatted = string.Format(ci, "{0:N2}", value);
            return string.IsNullOrWhiteSpace(currency) ? formatted : $"{formatted} {currency}";
        }

        private static string FormatSignedMoney(decimal value, string currency)
        {
            if (value == 0m) return FormatMoney(0m, currency);
            var sign = value > 0 ? "+" : "-";
            var ci = new CultureInfo("cs-CZ");
            var abs = Math.Abs(value);
            return $"{sign}{string.Format(ci, "{0:N2}", abs)} {currency}";
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

        #endregion
    }
}
