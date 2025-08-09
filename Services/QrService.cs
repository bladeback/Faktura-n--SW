using System;
using System.Globalization;
using System.Text;

namespace InvoiceApp.Services
{
    /// <summary>
    /// Generuje payload pro český standard QR Platba (SPD*1.0).
    /// Specifikace: ACC (IBAN), AM (částka s desetinnou tečkou), CC (měna),
    /// X-VS (variabilní symbol), X-SS (specifický), X-KS (konstantní), MSG (zpráva).
    /// </summary>
    public class QrService
    {
        /// <summary>
        /// Vytvoří payload pro QR Platba (SPD 1.0).
        /// Minimální sada: ACC, AM, CC; doporučené: X-VS, MSG.
        /// </summary>
        /// <param name="iban">IBAN příjemce (bez mezer)</param>
        /// <param name="amount">Částka k úhradě – doporučuji už předat po zaokrouhlení</param>
        /// <param name="currency">Měna, např. "CZK"</param>
        /// <param name="variableSymbol">Variabilní symbol (jen číslice, max 10)</param>
        /// <param name="message">Krátká zpráva pro příjemce (např. "Faktura INV-...")</param>
        public string BuildCzechQrPaymentPayload(
            string? iban,
            decimal amount,
            string currency,
            string? variableSymbol,
            string? message)
        {
            if (string.IsNullOrWhiteSpace(iban))
                throw new ArgumentException("Pro QR Platbu je vyžadován IBAN příjemce.", nameof(iban));

            // IBAN: bez mezer, velká písmena
            iban = iban.Replace(" ", string.Empty).ToUpperInvariant();

            // Částka musí mít tečku jako desetinný oddělovač (max 2 desetinná místa)
            string am = amount.ToString("0.##", CultureInfo.InvariantCulture);

            // Variabilní symbol – jen číslice (norma)
            string vs = SanitizeVs(variableSymbol);

            // Krátká zpráva (doporučení: max ~60 znaků)
            string msg = SanitizeMsg(message);

            var sb = new StringBuilder();
            sb.Append("SPD*1.0");
            sb.Append("*ACC:").Append(iban);
            sb.Append("*AM:").Append(am);
            sb.Append("*CC:").Append(string.IsNullOrWhiteSpace(currency) ? "CZK" : currency.Trim().ToUpperInvariant());

            if (!string.IsNullOrEmpty(vs))
                sb.Append("*X-VS:").Append(vs);

            if (!string.IsNullOrEmpty(msg))
                sb.Append("*MSG:").Append(msg);

            return sb.ToString();
        }

        /// <summary>
        /// Vrátí PNG bajty QR kódu pro dodaný payload (používáme QRCoder).
        /// </summary>
        public byte[] GenerateQrPng(string payload)
        {
            // Pozn.: projekt už obsahuje QRCoder.
            using var qrGen = new QRCoder.QRCodeGenerator();
            var data = qrGen.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.M, true);
            using var qr = new QRCoder.QRCode(data);
            using var bmp = qr.GetGraphic(pixelsPerModule: 6);
            using var ms = new System.IO.MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        private static string SanitizeVs(string? vs)
        {
            if (string.IsNullOrWhiteSpace(vs)) return string.Empty;
            var digits = new StringBuilder();
            foreach (var ch in vs)
                if (char.IsDigit(ch)) digits.Append(ch);
            // VS bývá do 10 číslic, nebudeme ale striktně ořezávat – banky většinou tolerují delší.
            return digits.ToString();
        }

        private static string SanitizeMsg(string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return string.Empty;

            // odebereme CR/LF a přebytečné mezery
            msg = msg.Replace("\r", " ").Replace("\n", " ");
            msg = System.Text.RegularExpressions.Regex.Replace(msg, @"\s+", " ").Trim();

            // prakticky stačí kolem 60 znaků (některé banky omezují), ale není to tvrdá povinnost
            if (msg.Length > 60) msg = msg.Substring(0, 60);
            return msg;
        }
    }
}
