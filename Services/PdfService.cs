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
                // Levý blok – titulek, číslo a data
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(t => t.Span(title).FontSize(16).SemiBold());
                    col.Item().Text(t => t.Span(inv.Number).FontSize(16).SemiBold());

                    // Datum vystavení – zvýraznit
                    col.Item().PaddingTop(12).Text(t =>
                    {
                        t.Span("Datum vystavení: ").SemiBold().FontColor(ThemeColor).FontSize(11);
                        t.Span(FormatDate(inv.IssueDate)).FontSize(11);
                    });

                    if (inv.Type == DocType.Invoice)
                    {
                        // Datum zdan. plnění – ponecháme standardní
                        if (inv.TaxableSupplyDate.HasValue)
                        {
                            col.Item().Text(t =>
                            {
                                t.Span("Datum zdan. plnění: ").SemiBold();
                                t.Span(FormatDate(inv.TaxableSupplyDate.Value));
                            });
                        }

                        // Datum splatnosti – zvýraznit
                        col.Item().Text(t =>
                        {
                            t.Span("Datum splatnosti: ").SemiBold().FontColor(ThemeColor).FontSize(11);
                            t.Span(FormatDate(inv.DueDate)).FontSize(11);
                        });
                    }
                });

                // Pravý blok – QR + „K úhradě“
                row.ConstantItem(150).Column(col =>
                {
                    if (inv.Type == DocType.Invoice && qrPng is { Length: > 0 })
                    {
                        col.Item().AlignRight().Text("QR Platba+F").FontSize(8);
                        col.Item().Image(qrPng);
                    }

                    // K úhradě (zaokrouhlená částka) v zeleně orámovaném boxu
                    var gross = inv.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + (isVatPayer ? i.VatRate : 0)));
                    var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);

                    col.Item().PaddingTop(8).Element(e =>
                    {
                        e.Border(1).BorderColor(ThemeColor).Padding(8).Column(sumCol =>
                        {
                            sumCol.Item().AlignCenter().Text("K úhradě").FontSize(10);
                            sumCol.Item().AlignCenter().Text(t => t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(14));
                        });
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

                // Platební údaje jen u faktury, u objednávky pouze kontakty
                if (inv.Type == DocType.Invoice)
                {
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Element(c => ComposePaymentDetails(c, inv));
                        row.ConstantItem(20);
                        row.RelativeItem().Element(c => ComposeContactDetails(c, inv.Supplier));
                    });
                }
                else
                {
                    col.Item().PaddingTop(10).Element(c => ComposeContactDetails(c, inv.Supplier));
                }

                col.Item().PaddingTop(20).Element(c => ItemsTable(c, inv));

                var isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);
                if (isVatPayer)
                    col.Item().PaddingTop(10).Element(c => ComposeVatSummary(c, inv));
            });
        }

        private void ComposeFooter(IContainer container, Invoice inv)
        {
            container.Column(col =>
            {
                if (inv.Type == DocType.Invoice)
                {
                    col.Item().PaddingBottom(5).Text(text =>
                    {
                        text.Span("Dovolujeme si Vás upozornit, že v případě nedodržení data splatnosti uvedeného na faktuře Vám můžeme účtovat zákonný úrok z prodlení.")
                            .FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                }

                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span("Vystaveno v aplikaci InvoiceApp").FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium));
                        text.Span("Strana ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
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
                    // popis – víc místa; „Celkem“ širší, aby se vešly velké částky s měnou
                    columns.RelativeColumn(6.5f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.8f);

                    if (isVatPayer)
                    {
                        columns.RelativeColumn(1.0f);
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(1.9f);
                    }

                    columns.RelativeColumn(2.8f);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Označení dodávky");
                    header.Cell().Element(HeaderCellStyle).Text("Počet m.j.");
                    header.Cell().Element(HeaderCellStyle).Text("Cena za m.j.");

                    if (isVatPayer)
                    {
                        header.Cell().Element(HeaderCellStyleCenter).Text("DPH %");
                        header.Cell().Element(HeaderCellStyleRight).Text("Bez DPH");
                        header.Cell().Element(HeaderCellStyleRight).Text("DPH");
                    }

                    header.Cell().Element(HeaderCellStyleRight).Text("Celkem");

                    static IContainer HeaderCellStyle(IContainer c) =>
                        c.Background(GreyColor).PaddingVertical(5).PaddingHorizontal(4)
                         .DefaultTextStyle(x => x.SemiBold());

                    static IContainer HeaderCellStyleRight(IContainer c) =>
                        HeaderCellStyle(c).AlignRight();

                    static IContainer HeaderCellStyleCenter(IContainer c) =>
                        HeaderCellStyle(c).AlignCenter();
                });

                foreach (var item in inv.Items)
                {
                    var basePrice = item.Quantity * item.UnitPrice;
                    var vatAmount = isVatPayer ? basePrice * item.VatRate : 0;
                    var totalPrice = basePrice + vatAmount;

                    table.Cell().Element(BodyCellStyle).Text(item.Name);

                    table.Cell().Element(BodyCellStyleRightSmall)
                        .Text(t => t.Span($"{FormatNumber(item.Quantity)} {item.Unit}"));

                    table.Cell().Element(BodyCellStyleRightSmall)
                        .Text(t => t.Span(FormatMoney(item.UnitPrice, null)));

                    if (isVatPayer)
                    {
                        table.Cell().Element(BodyCellStyleCenterSmall)
                            .Text(t => t.Span(FormatPercent(item.VatRate)));

                        table.Cell().Element(BodyCellStyleRightSmall)
                            .Text(t => t.Span(FormatMoney(basePrice, null)));

                        table.Cell().Element(BodyCellStyleRightSmall)
                            .Text(t => t.Span(FormatMoney(vatAmount, null)));
                    }

                    table.Cell().Element(BodyCellStyleRightGreySmall)
                        .Text(t => t.Span(FormatMoney(totalPrice, inv.Currency)).SemiBold());
                }

                static IContainer BodyCellStyle(IContainer c) =>
                    c.BorderBottom(1).BorderColor(GreyColor).PaddingVertical(5).PaddingHorizontal(4);

                static IContainer BodyCellStyleRightSmall(IContainer c) =>
                    BodyCellStyle(c).AlignRight().DefaultTextStyle(x => x.FontSize(8.5f));

                static IContainer BodyCellStyleCenterSmall(IContainer c) =>
                    BodyCellStyle(c).AlignCenter().DefaultTextStyle(x => x.FontSize(8.5f));

                static IContainer BodyCellStyleRightGreySmall(IContainer c) =>
                    BodyCellStyleRightSmall(c).Background(GreyColor);
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
                    header.Cell().Element(SummaryHeaderStyle).Text("Sazba DPH");
                    header.Cell().Element(SummaryHeaderStyle).AlignRight().Text("Základ");
                    header.Cell().Element(SummaryHeaderStyle).AlignRight().Text("Výše DPH");
                    header.Cell().Element(SummaryHeaderStyle).AlignRight().Text("Celkem");

                    static IContainer SummaryHeaderStyle(IContainer c) =>
                        c.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4).DefaultTextStyle(x => x.SemiBold());
                });

                var vatSummary = inv.Items
                    .GroupBy(i => i.VatRate)
                    .Select(g => new
                    {
                        VatRate = g.Key,
                        BaseTotal = g.Sum(i => i.Quantity * i.UnitPrice),
                        VatTotal = g.Sum(i => i.Quantity * i.UnitPrice * i.VatRate)
                    }).OrderBy(g => g.VatRate);

                foreach (var summary in vatSummary)
                {
                    table.Cell().Element(SummaryBodyStyle).Text(FormatPercent(summary.VatRate));
                    table.Cell().Element(SummaryBodyStyle).AlignRight().Text(FormatMoney(summary.BaseTotal, inv.Currency));
                    table.Cell().Element(SummaryBodyStyle).AlignRight().Text(FormatMoney(summary.VatTotal, inv.Currency));
                    table.Cell().Element(SummaryBodyStyle).AlignRight().Text(FormatMoney(summary.BaseTotal + summary.VatTotal, inv.Currency));
                }

                var totalBase = vatSummary.Sum(s => s.BaseTotal);
                var totalVat = vatSummary.Sum(s => s.VatTotal);
                var grandTotal = totalBase + totalVat;
                var rounded = Math.Round(grandTotal, 0, MidpointRounding.AwayFromZero);
                var rounding = rounded - grandTotal;

                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(4).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(5).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text(t => t.Span("Celkem (bez zaokrouhlení)").Bold());
                            row.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(grandTotal, inv.Currency)).Bold());
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("Zaokrouhlení");
                            var sign = rounding >= 0 ? "+" : "-";
                            row.RelativeItem().AlignRight().Text($"{sign}{FormatMoney(Math.Abs(rounding), inv.Currency)}");
                        });

                        // finální částka – zeleně orámovaný řádek
                        col.Item().PaddingTop(4).Element(e =>
                        {
                            e.Border(1).BorderColor(ThemeColor).Padding(6).Row(row =>
                            {
                                row.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                                row.RelativeItem().AlignRight().Text(t => t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(12));
                            });
                        });
                    });
                });

                static IContainer SummaryBodyStyle(IContainer c) => c.Padding(4);
            });
        }

        // ---------------- helpers ----------------

        private static string FormatDate(DateTime dt) => dt.ToString("dd.MM.yyyy", new CultureInfo("cs-CZ"));

        // NBSP fix: tisíce i mezera před měnou jsou nezalomitelné
        private static string FormatMoney(decimal value, string? currency)
        {
            var ci = new CultureInfo("cs-CZ");
            var formatted = string.Format(ci, "{0:N2}", value).Replace(' ', '\u00A0');
            return string.IsNullOrWhiteSpace(currency) ? formatted : $"{formatted}\u00A0{currency}";
        }

        private static string FormatNumber(decimal value)
        {
            var ci = new CultureInfo("cs-CZ");
            return string.Format(ci, "{0:N2}", value).Replace(' ', '\u00A0');
        }

        private static string FormatPercent(decimal rate)
        {
            decimal pct = rate * 100m;
            var ci = new CultureInfo("cs-CZ");
            return string.Format(ci, "{0:N0} %", pct).Replace(' ', '\u00A0');
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
