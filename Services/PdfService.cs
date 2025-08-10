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

            // K úhradě = po zaokrouhlení na celé Kč
            decimal gross = inv.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + (isVatPayer ? i.VatRate : 0)));
            decimal pay = Math.Round(gross, 0, MidpointRounding.AwayFromZero);

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

                row.ConstantItem(150).Column(col =>
                {
                    if (inv.Type == DocType.Invoice && qrPng is { Length: > 0 })
                    {
                        col.Item().AlignRight().Text("QR Platba+F").FontSize(8);
                        col.Item().Image(qrPng);
                    }

                    col.Item().PaddingTop(10).Background(GreyColor).Padding(8).Column(s =>
                    {
                        s.Item().AlignCenter().Text("K úhradě").FontSize(10);
                        s.Item().AlignCenter().Text(t => t.Span(FormatMoney(pay, inv.Currency)).Bold().FontSize(14));
                    });
                });
            });
        }

        private void ComposeBody(IContainer container, Invoice inv)
        {
            container.PaddingTop(20).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Dodavatel", inv.Supplier));
                    row.ConstantItem(20);
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Odběratel", inv.Customer));
                });

                // OBJ = bez platebních údajů
                col.Item().PaddingTop(10).Row(row =>
                {
                    if (inv.Type == DocType.Invoice)
                    {
                        row.RelativeItem().Element(c => ComposePaymentDetails(c, inv));
                        row.ConstantItem(20);
                    }
                    row.RelativeItem().Element(c => ComposeContactDetails(c, inv.Supplier));
                });

                col.Item().PaddingTop(20).Element(c => ItemsTable(c, inv));

                var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);

                // *** FIX: souhrn DPH i u OBJ, pokud je plátce ***
                if (isVatPayer)
                    col.Item().PaddingTop(10).Element(c => ComposeVatSummary(c, inv));

                // u neplátce vykreslíme zvláštní souhrn s „Zaokrouhlení“
                col.Item().PaddingTop(10).Element(c => ComposePayableSummary(c, inv));
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
                        t.Span("Vystaveno v aplikaci InvoiceApp").FontSize(8).FontColor(Colors.Grey.Medium));

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

                col.Item().PaddingTop(5).Row(r =>
                {
                    r.ConstantItem(100).Text(t => t.Span("Způsob platby:").SemiBold());
                    r.RelativeItem().Text(inv.PaymentMethod ?? "Převodem");
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(100).Text(t => t.Span("Bankovní účet:").SemiBold());
                    r.RelativeItem().Text(supplier.AccountNumber ?? "");
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(100).Text(t => t.Span("IBAN:").SemiBold());
                    r.RelativeItem().Text(FormatIbanDisplay(inv.PaymentIban ?? supplier.IBAN ?? ""));
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(100).Text(t => t.Span("SWIFT:").SemiBold());
                    r.RelativeItem().Text(supplier.SWIFT ?? "");
                });
                col.Item().Row(r =>
                {
                    r.ConstantItem(100).Text(t => t.Span("Variabilní symbol:").SemiBold());
                    r.RelativeItem().Text(inv.VariableSymbol ?? "");
                });
                if (!string.IsNullOrWhiteSpace(inv.ConstantSymbol))
                {
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(100).Text(t => t.Span("Konstantní symbol:").SemiBold());
                        r.RelativeItem().Text(inv.ConstantSymbol);
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
                    col.Item().PaddingTop(5).Row(r =>
                    {
                        r.ConstantItem(50).Text(t => t.Span("E-mail:").SemiBold());
                        r.RelativeItem().Text(supplier.Email);
                    });
                }
                if (!string.IsNullOrWhiteSpace(supplier.Phone))
                {
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(50).Text(t => t.Span("Telefon:").SemiBold());
                        r.RelativeItem().Text(supplier.Phone);
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
                    // rozšířený 1. sloupec, zhuštěné číselné
                    columns.RelativeColumn(6.8f);  // Označení dodávky
                    columns.RelativeColumn(1.6f);  // Počet m.j.
                    columns.RelativeColumn(1.8f);  // Cena za m.j.
                    if (isVatPayer)
                    {
                        columns.RelativeColumn(1.0f);  // DPH %
                        columns.RelativeColumn(1.8f);  // Bez DPH
                        columns.RelativeColumn(1.8f);  // DPH
                    }
                    columns.RelativeColumn(2.0f);  // Celkem
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellL).Text("Označení dodávky");
                    header.Cell().Element(HeaderCellR).Text("Počet m.j.");
                    header.Cell().Element(HeaderCellR).Text("Cena za m.j.");
                    if (isVatPayer)
                    {
                        header.Cell().Element(HeaderCellR).Text("DPH %");
                        header.Cell().Element(HeaderCellR).Text("Bez DPH");
                        header.Cell().Element(HeaderCellR).Text("DPH");
                    }
                    header.Cell().Element(HeaderCellR).Text("Celkem");

                    static IContainer HeaderCellL(IContainer c)
                        => c.Background(GreyColor).Padding(5).DefaultTextStyle(x => x.SemiBold());

                    static IContainer HeaderCellR(IContainer c)
                        => c.Background(GreyColor).Padding(5).DefaultTextStyle(x => x.SemiBold()).AlignRight();
                });

                foreach (var item in inv.Items)
                {
                    var basePrice = item.Quantity * item.UnitPrice;
                    var vatAmount = isVatPayer ? basePrice * item.VatRate : 0;
                    var totalPrice = basePrice + vatAmount;

                    table.Cell().Element(BodyCell).Text(item.Name);
                    table.Cell().Element(BodyCellRight).Text($"{FormatNumber(item.Quantity)} {item.Unit}");
                    table.Cell().Element(BodyCellRight).Text(FormatMoney(item.UnitPrice, null));

                    if (isVatPayer)
                    {
                        table.Cell().Element(BodyCellRight).Text(FormatPercent(item.VatRate));
                        table.Cell().Element(BodyCellRight).Text(FormatMoney(basePrice, null));
                        table.Cell().Element(BodyCellRight).Text(FormatMoney(vatAmount, null));
                        table.Cell().Element(BodyCellRight).Background(GreyColor).Text(t => t.Span(FormatMoney(totalPrice, inv.Currency)).SemiBold());
                    }
                    else
                    {
                        table.Cell().Element(BodyCellRight).Background(GreyColor).Text(t => t.Span(FormatMoney(basePrice, inv.Currency)).SemiBold());
                    }
                }

                static IContainer BodyCell(IContainer c)
                    => c.BorderBottom(1).BorderColor(GreyColor).Padding(5);

                static IContainer BodyCellRight(IContainer c)
                    => c.BorderBottom(1).BorderColor(GreyColor).Padding(5).AlignRight();
            });
        }

        private void ComposeVatSummary(IContainer container, Invoice inv)
        {
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

                    static IContainer SummaryHeader(IContainer c)
                        => c.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4).DefaultTextStyle(x => x.SemiBold());
                });

                var vatSummary = inv.Items
                    .GroupBy(i => i.VatRate)
                    .Select(g => new
                    {
                        VatRate = g.Key,
                        BaseTotal = g.Sum(i => i.Quantity * i.UnitPrice),
                        VatTotal = g.Sum(i => i.Quantity * i.UnitPrice * i.VatRate)
                    }).OrderBy(g => g.VatRate);

                foreach (var s in vatSummary)
                {
                    table.Cell().Element(SummaryBody).Text(FormatPercent(s.VatRate));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal, inv.Currency));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.VatTotal, inv.Currency));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal + s.VatTotal, inv.Currency));
                }

                var totalBase = vatSummary.Sum(x => x.BaseTotal);
                var totalVat = vatSummary.Sum(x => x.VatTotal);
                var gross = totalBase + totalVat;
                var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
                var rounding = rounded - gross;

                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(4).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text(t => t.Span("Celkem (bez zaokrouhlení)").Bold());
                        r.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(gross, inv.Currency)).Bold());
                    });

                    footer.Cell().ColumnSpan(4).Row(r =>
                    {
                        r.RelativeItem().Text("Zaokrouhlení");
                        r.RelativeItem().AlignRight().Text(FormatSignedMoney(rounding, inv.Currency));
                    });

                    footer.Cell().ColumnSpan(4).PaddingTop(5).Row(r =>
                    {
                        r.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                        r.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(12));
                    });
                });

                static IContainer SummaryBody(IContainer c) => c.Padding(4);
            });
        }

        private void ComposePayableSummary(IContainer container, Invoice inv)
        {
            var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);
            var baseTotal = inv.Items.Sum(i => i.Quantity * i.UnitPrice);
            var vatTotal = isVatPayer ? inv.Items.Sum(i => i.Quantity * i.UnitPrice * i.VatRate) : 0m;
            var gross = baseTotal + vatTotal;
            var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);
            var rounding = rounded - gross;

            // pro plátce DPH nic – souhrn (včetně zaokrouhlení) je ve VAT tabulce
            if (isVatPayer) return;

            container.AlignRight().Width(350).Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Celkem (bez zaokrouhlení)").Bold();
                    r.RelativeItem().AlignRight().Text(FormatMoney(gross, inv.Currency));
                });
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text("Zaokrouhlení");
                    r.RelativeItem().AlignRight().Text(FormatSignedMoney(rounding, inv.Currency));
                });
                col.Item().PaddingTop(5).Row(r =>
                {
                    r.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                    r.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(12));
                });
            });
        }

        #region Helpers
        private static string FormatDate(DateTime dt) => dt.ToString("dd.MM.yyyy", new CultureInfo("cs-CZ"));

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
