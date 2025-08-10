using System;
using System.Globalization;
using System.Windows.Data;

namespace InvoiceApp.Converters
{
    /// <summary>
    /// Vstup i výstup jako CZ celé číslo:
    /// - Convert: zobrazí "N0" (skupiny, bez desetin)
    /// - ConvertBack: akceptuje tečku/čárku, mezery; zaokrouhlí na celé (AwayFromZero)
    /// Vrací decimal, takže se hodí k vlastnosti typu decimal.
    /// </summary>
    public class WholeNumberConverter : IValueConverter
    {
        private static readonly CultureInfo Cs = new CultureInfo("cs-CZ");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null) return string.Empty;

            try
            {
                var d = System.Convert.ToDecimal(value, Cs);
                return d.ToString("N0", Cs); // skupiny, bez desetin
            }
            catch
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value?.ToString() ?? string.Empty;
            s = s.Trim();
            if (string.IsNullOrEmpty(s)) return 0m;

            // odstraníme mezery (oddělovače tisíců)
            s = s.Replace(" ", string.Empty);

            // normalizace – dovolíme i tečku
            s = s.Replace(".", ",");

            if (decimal.TryParse(s, NumberStyles.Number, Cs, out var d))
            {
                d = Math.Round(d, 0, MidpointRounding.AwayFromZero);
                return d;
            }

            // nevalidní vstup – ponecháme hodnotu v editoru
            return Binding.DoNothing;
        }
    }
}
