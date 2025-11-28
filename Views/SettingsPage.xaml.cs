using System.Windows.Controls;
using System.Windows.Media;

namespace InvoiceApp.Views
{
    public partial class SettingsPage : UserControl
    {
        private TextBox? _lastFocusedTextBox;

        public SettingsPage()
        {
            InitializeComponent();
            
            // Track last focused textbox
            FooterInvoiceBox.GotFocus += (s, e) => _lastFocusedTextBox = FooterInvoiceBox;
            FooterOrderBox.GotFocus += (s, e) => _lastFocusedTextBox = FooterOrderBox;

            // Initial preview update
            Loaded += (s, e) => 
            {
                UpdatePreview(FooterInvoiceBox.Text, PreviewInvoice);
                UpdatePreview(FooterOrderBox.Text, PreviewOrder);
            };
        }

        private void InsertBold_Click(object sender, System.Windows.RoutedEventArgs e) => InsertTag("<b>", "</b>");
        private void InsertItalic_Click(object sender, System.Windows.RoutedEventArgs e) => InsertTag("<i>", "</i>");
        
        private void InsertSize_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
            else
            {
                InsertTag("<size:15>", "</size>");
            }
        }

        private void SizeItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string size)
            {
                InsertTag($"<size:{size}>", "</size>");
            }
        }

        private void InsertColor_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // If sender is Button, open ContextMenu
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
            else
            {
                // Fallback for "Custom..." menu item or direct call
                InsertTag("<color:#FF0000>", "</color>");
            }
        }

        private void InsertBg_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
            else
            {
                InsertTag("<bg:#FFFF00>", "</bg>");
            }
        }

        private void ColorItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string colorCode)
            {
                InsertTag($"<color:{colorCode}>", "</color>");
            }
        }

        private void BgItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string colorCode)
            {
                InsertTag($"<bg:{colorCode}>", "</bg>");
            }
        }

        private void InsertTag(string startTag, string endTag)
        {
            // Find focused TextBox or use last focused
            var focusedControl = System.Windows.Input.Keyboard.FocusedElement as TextBox ?? _lastFocusedTextBox;
            
            if (focusedControl == null) return;

            var txt = focusedControl;
            var selStart = txt.SelectionStart;
            var selLen = txt.SelectionLength;
            var text = txt.Text;

            var before = text.Substring(0, selStart);
            var selected = text.Substring(selStart, selLen);
            var after = text.Substring(selStart + selLen);

            txt.Text = before + startTag + selected + endTag + after;
            
            // Restore selection/cursor
            if (selLen > 0)
            {
                txt.SelectionStart = selStart + startTag.Length;
                txt.SelectionLength = selLen;
            }
            else
            {
                txt.SelectionStart = selStart + startTag.Length;
                txt.SelectionLength = 0;
            }
            txt.Focus();
        }

        private void FooterInvoiceBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview(FooterInvoiceBox.Text, PreviewInvoice);
        }

        private void FooterOrderBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview(FooterOrderBox.Text, PreviewOrder);
        }

        private void UpdatePreview(string text, TextBlock target)
        {
            if (target == null) return;
            target.Inlines.Clear();

            if (string.IsNullOrEmpty(text)) return;

            // Simple parser for <b>...</b>, <i>...</i>, <size:15>...</size>, <color:#RRGGBB>...</color>, <bg:#RRGGBB>...</bg>
            // Regex to find tags: <[^>]+>
            
            var matches = System.Text.RegularExpressions.Regex.Matches(text, @"(<[^>]+>)|([^<]+)");
            
            bool isBold = false;
            bool isItalic = false;
            double? currentSize = null;
            Brush? currentColor = null;
            Brush? currentBg = null;

            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var val = m.Value;
                if (val.StartsWith("<"))
                {
                    // Tag processing
                    var tag = val.ToLowerInvariant();
                    if (tag == "<b>") isBold = true;
                    else if (tag == "</b>") isBold = false;
                    else if (tag == "<i>") isItalic = true;
                    else if (tag == "</i>") isItalic = false;
                    else if (tag.StartsWith("<size:"))
                    {
                        var sizePart = tag.Substring(6, tag.Length - 7).Trim();
                        if (double.TryParse(sizePart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var s))
                            currentSize = s;
                    }
                    else if (tag == "</size>") currentSize = null;
                    else if (tag.StartsWith("<color:"))
                    {
                        try
                        {
                            var colorPart = tag.Substring(7, tag.Length - 8).Trim();
                            currentColor = new BrushConverter().ConvertFromString(colorPart) as Brush;
                        }
                        catch { /* ignore invalid color */ }
                    }
                    else if (tag == "</color>") currentColor = null;
                    else if (tag.StartsWith("<bg:"))
                    {
                        try
                        {
                            var bgPart = tag.Substring(4, tag.Length - 5).Trim();
                            currentBg = new BrushConverter().ConvertFromString(bgPart) as Brush;
                        }
                        catch { /* ignore invalid color */ }
                    }
                    else if (tag == "</bg>") currentBg = null;
                }
                else
                {
                    // Text content
                    var run = new System.Windows.Documents.Run(val);
                    if (isBold) run.FontWeight = System.Windows.FontWeights.Bold;
                    if (isItalic) run.FontStyle = System.Windows.FontStyles.Italic;
                    if (currentSize.HasValue) run.FontSize = currentSize.Value;
                    if (currentColor != null) run.Foreground = currentColor;
                    if (currentBg != null) run.Background = currentBg;
                    
                    target.Inlines.Add(run);
                }
            }
        }
    }
}
