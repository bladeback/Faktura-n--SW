using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InvoiceApp.Services
{
    /// <summary>
    /// Generuje payload pro český standard QR Platba (SPD*1.0).
    /// ACC (IBAN bez mezer), AM (částka s tečkou), CC (měna),
    /// X-VS (variabilní symbol), X-KS (konstantní symbol), MSG (zpráva).
    /// </summary>
    public class QrService
    {
        public string BuildCzechQrPaymentPayload(
            string? iban,
            decimal amount,
            string currency,
            string? variableSymbol,
            string? constantSymbol,
            string? message)
        {
            if (string.IsNullOrWhiteSpace(iban))
                throw new ArgumentException("Pro QR Platbu je vyžadován IBAN příjemce.", nameof(iban));

            // IBAN bez mezer (SPD to vyžaduje)
            iban = iban.Replace(" ", string.Empty).ToUpperInvariant();

            string am = amount.ToString("0.##", CultureInfo.InvariantCulture);
            string vs = TrimToMax10Digits(variableSymbol);
            string ks = TrimToMax10Digits(constantSymbol);
            string msg = SanitizeMsg(message);

            var sb = new StringBuilder();
            sb.Append("SPD*1.0");
            sb.Append("*ACC:").Append(iban);
            sb.Append("*AM:").Append(am);
            sb.Append("*CC:").Append(string.IsNullOrWhiteSpace(currency) ? "CZK" : currency.Trim().ToUpperInvariant());

            if (!string.IsNullOrEmpty(vs))
                sb.Append("*X-VS:").Append(vs);

            if (!string.IsNullOrEmpty(ks))
                sb.Append("*X-KS:").Append(ks);

            if (!string.IsNullOrEmpty(msg))
                sb.Append("*MSG:").Append(msg);

            return sb.ToString();
        }

        /// <summary>
        /// PNG bajty QR – varianta bez GDI (PngByteQRCode), spolehlivá i v headless prostředí.
        /// </summary>
        public byte[] GenerateQrPng(string payload)
        {
            var gen = new QRCoder.QRCodeGenerator();
            var data = gen.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.M, forceUtf8: true);
            var png = new QRCoder.PngByteQRCode(data);
            return png.GetGraphic(pixelsPerModule: 7);
        }

        private static string TrimToMax10Digits(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
                if (char.IsDigit(ch)) sb.Append(ch);

            var digits = sb.ToString();
            return digits.Length > 10 ? digits.Substring(digits.Length - 10, 10) : digits;
        }

        private static string SanitizeMsg(string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return string.Empty;
            msg = msg.Replace("\r", " ").Replace("\n", " ");
            msg = Regex.Replace(msg, @"\s+", " ").Trim();
            if (msg.Length > 60) msg = msg[..60];
            return msg;
        }
    }
}
