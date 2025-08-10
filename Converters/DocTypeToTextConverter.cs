using System;
using System.Globalization;
using System.Windows.Data;

namespace InvoiceApp.Converters
{
    /// <summary>
    /// Převádí hodnotu enumu DocType na čitelný český text a zpět.
    /// </summary>
    public class DocTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Models.DocType dt)
            {
                return dt switch
                {
                    Models.DocType.Invoice => "Faktura",
                    Models.DocType.Order => "Objednávka",
                    _ => dt.ToString()
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                s = s.Trim();
                if (string.Equals(s, "Faktura", StringComparison.OrdinalIgnoreCase))
                    return Models.DocType.Invoice;
                if (string.Equals(s, "Objednávka", StringComparison.OrdinalIgnoreCase))
                    return Models.DocType.Order;
            }
            return Binding.DoNothing;
        }
    }
}
