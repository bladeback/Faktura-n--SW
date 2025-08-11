using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace InvoiceApp.Converters
{
    /// <summary>
    /// Zobrazuje IBAN s mezerami po 4 znacích (např. "CZ65 0800 0000 1920 0014 5399"),
    /// ale do modelu ukládá IBAN bez mezer a VELKÝMI písmeny (např. "CZ6508000000192000145399").
    /// </summary>
    public class IbanDisplayConverter : IValueConverter
    {
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string) ?? string.Empty;
            s = Spaces.Replace(s, "").ToUpperInvariant();

            if (s.Length == 0) return string.Empty;

            var sb = new StringBuilder(s.Length + s.Length / 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value as string) ?? string.Empty;
            s = Spaces.Replace(s, "").ToUpperInvariant();
            return s;
        }
    }
}
