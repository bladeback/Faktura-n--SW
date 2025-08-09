using QRCoder;
using System;

namespace InvoiceApp.Services
{
    public class QrService
    {
        // Czech QR Platba payload (SPD 1.0)
        public string BuildCzechQrPaymentPayload(string iban, decimal amount, string currency, string variableSymbol, string message)
        {
            string acc = $"ACC:{iban}";
            string am = amount > 0 ? $"*AM:{amount:0.00}" : string.Empty;
            string cc = !string.IsNullOrWhiteSpace(currency) ? $"*CC:{currency}" : "";
            string vs = !string.IsNullOrWhiteSpace(variableSymbol) ? $"*X-VS:{variableSymbol}" : "";
            string msg = !string.IsNullOrWhiteSpace(message) ? $"*MSG:{message}" : "";
            return $"SPD*1.0*{acc}{am}{cc}{vs}{msg}";
        }

        public byte[] GenerateQrPng(string payload, int pixelsPerModule = 8)
        {
            using var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var qr = new PngByteQRCode(data);
            return qr.GetGraphic(pixelsPerModule);
        }
    }
}
