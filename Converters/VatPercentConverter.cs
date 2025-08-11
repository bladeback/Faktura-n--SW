using System;
using System.Globalization;
using System.Windows.Data;

namespace InvoiceApp.Converters
{
    /// <summary>
    /// Zobrazuje/sbírá sazbu DPH v PROCENTECH (např. 21),
    /// ale do modelu ukládá zlomkovou hodnotu (0.21).
    /// Toleruje vstup 21 i 0.21, čárku i tečku, ořeže rozsah na 0..100 %.
    /// </summary>
    public class VatPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0m;
            try
            {
                var dec = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return dec * 100m; // 0.21 -> 21
            }
            catch { return 0m; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Binding.DoNothing;
            var s = value.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return 0m;

            // zkuste podle aktuální kultury, pak invariant, pak prohoď čárku/tečku
            if (!decimal.TryParse(s, NumberStyles.Any, culture, out var parsed) &&
                !decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
            {
                s = s.Replace(",", ".");
                if (!decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    return Binding.DoNothing;
            }

            // přijmi 21 i 0.21
            if (parsed > 1m) parsed /= 100m;

            // clamp 0..1 (tj. 0..100 %)
            if (parsed < 0m) parsed = 0m;
            if (parsed > 1m) parsed = 1m;

            return parsed; // uloží se 0.21
        }
    }
}
