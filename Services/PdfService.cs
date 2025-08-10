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

        // ======================================================
        // Header
        // ======================================================
        private void ComposeHeader(IContainer container, Invoice inv, byte[]? qrPng)
        {
            bool isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);

            string title = inv.Type == DocType.Invoice
                ? (isVatPayer ? "Faktura - daňový doklad" : "Faktura")
                : "Objednávka";

            container.Row(row =>
            {
                // ---------- LEFT ----------
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(t => t.Span(title).FontSize(16).SemiBold());
                    col.Item().Text(t => t.Span(inv.Number).FontSize(16).SemiBold());

                    // víc zvýrazněná data
                    col.Item().PaddingTop(15).Text(text =>
                    {
                        text.Span("Datum vystavení: ").SemiBold().FontSize(11).FontColor(ThemeColor);
                        text.Span(FormatDate(inv.IssueDate)).FontSize(11);
                    });

                    if (inv.Type == DocType.Invoice && isVatPayer && inv.TaxableSupplyDate.HasValue)
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Datum zdan. plnění: ").SemiBold();
                            text.Span(FormatDate(inv.TaxableSupplyDate.Value));
                        });
                    }

                    if (inv.Type == DocType.Invoice)
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Datum splatnosti: ").SemiBold().FontSize(11).FontColor(ThemeColor);
                            text.Span(FormatDate(inv.DueDate)).FontSize(11);
                        });
                    }
                    // Pozn.: "Není plátce DPH" je nově v bloku Dodavatel (pod IČ)
                });

                // ---------- RIGHT ----------
                row.ConstantItem(150).Column(col =>
                {
                    // QR pouze na Faktuře
                    if (inv.Type == DocType.Invoice && qrPng is { Length: > 0 })
                    {
                        col.Item().AlignRight().Text("QR Platba+F").FontSize(8);
                        col.Item().Image(qrPng);
                    }

                    // K úhradě (zaokrouhleno na celé)
                    var gross = inv.Items.Sum(i => i.Quantity * i.UnitPrice * (1 + (isVatPayer ? i.VatRate : 0)));
                    var rounded = Math.Round(gross, 0, MidpointRounding.AwayFromZero);

                    col.Item()
                       .PaddingTop(10)
                       .Background(GreyColor)
                       .Padding(8)
                       .Border(1).BorderColor(ThemeColor)
                       .Column(summaryCol =>
                       {
                           summaryCol.Item().AlignCenter().Text("K úhradě").FontSize(10);
                           summaryCol.Item().AlignCenter().Text(t =>
                               t.Span(FormatMoney(rounded, inv.Currency)).Bold().FontSize(14));
                       });
                });
            });
        }

        // ======================================================
        // Body
        // ======================================================
        private void ComposeBody(IContainer container, Invoice inv)
        {
            bool isVatPayer = !string.IsNullOrWhiteSpace(inv.Supplier?.DIC);

            container.PaddingTop(20).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Dodavatel", inv.Supplier, showNonVatNote: true));
                    row.ConstantItem(20);
                    row.RelativeItem().Element(c => ComposePartyAddress(c, "Odběratel", inv.Customer, showNonVatNote: false));
                });

                // Platební údaje jen u FAKTURY, u OBJ jen kontakty
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

                col.Item().PaddingTop(20).Element(c => ItemsTable(c, inv, isVatPayer));

                // Souhrn / Totals
                if (isVatPayer)
                    col.Item().PaddingTop(10).Element(c => ComposeVatSummary(c, inv));
                else
                    col.Item().PaddingTop(10).Element(c => ComposeTotalsOnly(c, inv));
            });
        }

        // ======================================================
        // Footer
        // ======================================================
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

        // ======================================================
        // Blocks
        // ======================================================
        private void ComposePartyAddress(IContainer container, string title, Party? party, bool showNonVatNote)
        {
            if (party is null) return;
            bool isVatPayer = !string.IsNullOrWhiteSpace(party.DIC);

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
                {
                    col.Item().Text($"DIČ: {party.DIC ?? ""}");
                }
                else if (showNonVatNote)
                {
                    // požadavek: "Není plátce DPH" přímo pod IČ (v bloku Dodavatel)
                    col.Item().Text(t => t.Span("Není plátce DPH").FontColor(Colors.Grey.Darken2));
                }
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

        // ======================================================
        // Items table
        // ======================================================
        private void ItemsTable(IContainer container, Invoice inv, bool isVatPayer)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    if (isVatPayer)
                    {
                        columns.RelativeColumn(6.5f); // Označení dodávky
                        columns.RelativeColumn(1.6f); // Počet m.j.
                        columns.RelativeColumn(1.8f); // Cena za m.j.
                        columns.RelativeColumn(1.0f); // DPH %
                        columns.RelativeColumn(2.2f); // Bez DPH
                        columns.RelativeColumn(1.9f); // DPH
                        columns.RelativeColumn(2.8f); // Celkem
                    }
                    else
                    {
                        columns.RelativeColumn(7.8f); // Označení dodávky
                        columns.RelativeColumn(1.7f); // Počet m.j.
                        columns.RelativeColumn(2.1f); // Cena za m.j.
                        columns.RelativeColumn(3.4f); // Celkem
                    }
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Označení dodávky");

                    // zarovnání hlaviček dle číselných sloupců
                    header.Cell().Element(HeaderCellStyleRight).Text("Počet m.j.");
                    header.Cell().Element(HeaderCellStyleRight).Text("Cena za m.j.");

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

                // ---- helpers ----
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

        // ======================================================
        // VAT summary + totals (plátce)
        // ======================================================
        private void ComposeVatSummary(IContainer container, Invoice inv)
        {
            container.AlignRight().Width(360).Table(table =>
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
                        c.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4)
                         .DefaultTextStyle(x => x.SemiBold());
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

                foreach (var s in vatSummary)
                {
                    table.Cell().Element(SummaryBody).Text(FormatPercent(s.VatRate));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal, inv.Currency));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.VatTotal, inv.Currency));
                    table.Cell().Element(SummaryBody).AlignRight().Text(FormatMoney(s.BaseTotal + s.VatTotal, inv.Currency));
                }

                var totalBase = vatSummary.Sum(x => x.BaseTotal);
                var totalVat = vatSummary.Sum(x => x.VatTotal);
                var grandTotal = totalBase + totalVat;
                var rounded = Math.Round(grandTotal, 0, MidpointRounding.AwayFromZero);
                var rounding = rounded - grandTotal;

                table.Footer(footer =>
                {
                    footer.Cell().ColumnSpan(4).BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(6).Column(col =>
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

                        col.Item()
                           .PaddingTop(4)
                           .Border(1).BorderColor(ThemeColor).Background(GreyColor).Padding(6)
                           .Row(row =>
                           {
                               row.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                               row.RelativeItem()
                                  .AlignRight()
                                  .Text(t => t.Span(FormatMoney(rounded, inv.Currency))
                                  .Bold().FontSize(12).FontColor(ThemeColor));
                           });
                    });
                });

                static IContainer SummaryBody(IContainer c) => c.Padding(4);
            });
        }

        // Totals only (neplátce i OBJ neplátce)
        private void ComposeTotalsOnly(IContainer container, Invoice inv)
        {
            var baseTotal = inv.Items.Sum(i => i.Quantity * i.UnitPrice);
            var grandTotal = baseTotal;
            var rounded = Math.Round(grandTotal, 0, MidpointRounding.AwayFromZero);
            var rounding = rounded - grandTotal;

            container.AlignRight().Width(360).Column(col =>
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

                col.Item()
                   .PaddingTop(4)
                   .Border(1).BorderColor(ThemeColor).Background(GreyColor).Padding(6)
                   .Row(row =>
                   {
                       row.RelativeItem().Text(t => t.Span("Celkem k úhradě").Bold().FontSize(12));
                       row.RelativeItem()
                          .AlignRight()
                          .Text(t => t.Span(FormatMoney(rounded, inv.Currency))
                          .Bold().FontSize(12).FontColor(ThemeColor));
                   });
            });
        }

        // ======================================================
        // Helpers
        // ======================================================
        private static string FormatDate(DateTime dt) =>
            dt.ToString("dd.MM.yyyy", new CultureInfo("cs-CZ"));

        // NBSP: tisíce i mezera před měnou jsou nezalomitelné
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

            return string.Join(" ",
                Enumerable.Range(0, (compact.Length + 3) / 4)
                          .Select(i => i * 4)
                          .TakeWhile(i => i < compact.Length)
                          .Select(i => compact.Substring(i, Math.Min(4, compact.Length - i))));
        }
    }
}
