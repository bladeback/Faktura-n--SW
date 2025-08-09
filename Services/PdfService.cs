using InvoiceApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;

namespace InvoiceApp.Services
{
    public class PdfService
    {
        public string SaveInvoicePdf(Invoice model, byte[]? qrPng = null, string? targetPath = null)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var fileName = targetPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{model.Number}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(model.Type == DocType.Invoice ? "Faktura" : "Objednávka").FontSize(20).SemiBold();
                            col.Item().Text($"Číslo: {model.Number}");
                            col.Item().Text($"Vystaveno: {model.IssueDate:dd.MM.yyyy}  Splatnost: {model.DueDate:dd.MM.yyyy}");
                        });
                        // Placeholder logo removed to avoid Placeholders.Icon API differences
                        // To add your logo later: read bytes and call .Image(logoBytes)
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Dodavatel").SemiBold();
                                c.Item().Text(model.Supplier.Name);
                                c.Item().Text(model.Supplier.Address);
                                c.Item().Text($"{model.Supplier.City}");
                                c.Item().Text($"IČO: {model.Supplier.ICO}  DIČ: {model.Supplier.DIC}");
                                c.Item().Text($"Bank: {model.Supplier.Bank}  IBAN: {model.Supplier.IBAN}");
                                c.Item().Text($"Email: {model.Supplier.Email}  Tel: {model.Supplier.Phone}");
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Odběratel").SemiBold();
                                c.Item().Text(model.Customer.Name);
                                c.Item().Text(model.Customer.Address);
                                c.Item().Text(model.Customer.City);
                                c.Item().Text($"IČO: {model.Customer.ICO}  DIČ: {model.Customer.DIC}");
                            });
                            if (qrPng != null)
                            {
                                row.ConstantItem(140).Column(c =>
                                {
                                    c.Item().Text("QR platba").SemiBold().AlignCenter();
                                    c.Item().Image(qrPng);
                                });
                            }
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(6);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).Text("Položka").SemiBold();
                                header.Cell().Element(CellStyle).Text("Množství").SemiBold();
                                header.Cell().Element(CellStyle).Text("Cena / ks").SemiBold();
                                header.Cell().Element(CellStyle).Text("DPH").SemiBold();
                                header.Cell().Element(CellStyle).Text("Celkem").SemiBold();

                                static IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                            });

                            foreach (var i in model.Items)
                            {
                                table.Cell().Text(i.Name);
                                table.Cell().Text($"{i.Quantity} {i.Unit}");
                                table.Cell().Text($"{i.UnitPrice:0.00} {model.Currency}");
                                table.Cell().Text($"{i.LineVat:0.00} {model.Currency}");
                                table.Cell().Text($"{i.LineTotal:0.00} {model.Currency}");
                            }
                        });

                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text(model.Notes);
                            r.ConstantItem(220).Column(c =>
                            {
                                c.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text("Základ");
                                    rr.ConstantItem(100).AlignRight().Text($"{model.TotalNet:0.00} {model.Currency}");
                                });
                                c.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text("DPH celkem");
                                    rr.ConstantItem(100).AlignRight().Text($"{model.TotalVat:0.00} {model.Currency}");
                                });
                                c.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text("Celkem k úhradě").SemiBold();
                                    rr.ConstantItem(100).AlignRight().Text($"{model.Total:0.00} {model.Currency}").SemiBold();
                                });
                            });
                        });
                    });

                    page.Footer().AlignCenter().Text("Vygenerováno v InvoiceApp");
                });
            }).GeneratePdf(fileName);

            return fileName;
        }
    }
}
