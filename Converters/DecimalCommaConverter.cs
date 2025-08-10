using System;
using System.Globalization;
using System.Windows.Data;

namespace InvoiceApp.Converters
{
    /// <summary>
    /// Dovolí psát desetinnou čárku (CS locale) do TextBoxu/DataGridu.
    /// - Convert: decimal -> řetězec s čárkou
    /// - ConvertBack: řetězec -> decimal (tolerantní; tečka se převede na čárku,
    ///   mezistavy jako "6," nevyhazují chybu – vrací Binding.DoNothing)
    /// </summary>
    public class DecimalCommaConverter : IValueConverter
    {
        private static readonly CultureInfo Cs = new CultureInfo("cs-CZ");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            if (value is decimal d) return d.ToString("0.###", Cs);
            if (value is double db) return ((decimal)db).ToString("0.###", Cs);

            try
            {
                var dec = System.Convert.ToDecimal(value, Cs);
                return dec.ToString("0.###", Cs);
            }
            catch
            {
                return value.ToString() ?? string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = (value ?? string.Empty).ToString()!.Trim();

            if (string.IsNullOrEmpty(s))
                return Binding.DoNothing;

            // odstraň mezery, sjednoť oddělovač
            s = s.Replace(" ", string.Empty);
            s = s.Replace(".", ",");

            // uživatel dopsal čárku, ale ještě nepokračoval => nevracej chybu
            if (s.EndsWith(","))
                return Binding.DoNothing;

            if (decimal.TryParse(s, NumberStyles.Number, Cs, out var dec))
                return dec;

            // nevalidní vstup = nechávám bez změny zdroje, ale bez chyby
            return Binding.DoNothing;
        }
    }
}
