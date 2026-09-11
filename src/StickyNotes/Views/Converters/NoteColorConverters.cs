using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace StickyNotes.Views.Converters;

public static class NoteColorThemeHelper
{
    public static readonly Dictionary<string, (Color Bg, Color Border, Color Header, Color Accent)> Themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["yellow"] = (Color.FromArgb(255, 0x29, 0x24, 0x16), Color.FromArgb(255, 0xF5, 0xC5, 0x42), Color.FromArgb(255, 0x33, 0x2D, 0x1C), Color.FromArgb(255, 0xF5, 0xC5, 0x42)),
        ["blue"]   = (Color.FromArgb(255, 0x13, 0x23, 0x33), Color.FromArgb(255, 0x60, 0xCD, 0xFF), Color.FromArgb(255, 0x1A, 0x2F, 0x42), Color.FromArgb(255, 0x60, 0xCD, 0xFF)),
        ["green"]  = (Color.FromArgb(255, 0x14, 0x28, 0x1F), Color.FromArgb(255, 0x6C, 0xCF, 0x8E), Color.FromArgb(255, 0x1B, 0x35, 0x29), Color.FromArgb(255, 0x6C, 0xCF, 0x8E)),
        ["pink"]   = (Color.FromArgb(255, 0x2E, 0x17, 0x24), Color.FromArgb(255, 0xF4, 0x8F, 0xB1), Color.FromArgb(255, 0x3D, 0x1F, 0x30), Color.FromArgb(255, 0xF4, 0x8F, 0xB1)),
        ["purple"] = (Color.FromArgb(255, 0x26, 0x19, 0x33), Color.FromArgb(255, 0xCE, 0x93, 0xD8), Color.FromArgb(255, 0x33, 0x22, 0x44), Color.FromArgb(255, 0xCE, 0x93, 0xD8)),
        ["gray"]   = (Color.FromArgb(255, 0x20, 0x20, 0x22), Color.FromArgb(255, 0xD2, 0xD2, 0xD7), Color.FromArgb(255, 0x28, 0x28, 0x2B), Color.FromArgb(255, 0xD2, 0xD2, 0xD7)),
    };

    public static SolidColorBrush GetBackgroundBrush(string? theme)
    {
        var key = theme?.ToLowerInvariant() ?? "yellow";
        var col = Themes.TryGetValue(key, out var t) ? t.Bg : Themes["yellow"].Bg;
        return new SolidColorBrush(col);
    }

    public static SolidColorBrush GetBorderBrush(string? theme)
    {
        var key = theme?.ToLowerInvariant() ?? "yellow";
        var col = Themes.TryGetValue(key, out var t) ? t.Border : Themes["yellow"].Border;
        return new SolidColorBrush(Color.FromArgb(100, col.R, col.G, col.B));
    }

    public static SolidColorBrush GetHeaderBrush(string? theme)
    {
        var key = theme?.ToLowerInvariant() ?? "yellow";
        var col = Themes.TryGetValue(key, out var t) ? t.Header : Themes["yellow"].Header;
        return new SolidColorBrush(col);
    }

    public static SolidColorBrush GetAccentBrush(string? theme)
    {
        var key = theme?.ToLowerInvariant() ?? "yellow";
        var col = Themes.TryGetValue(key, out var t) ? t.Accent : Themes["yellow"].Accent;
        return new SolidColorBrush(col);
    }
}

public sealed class NoteBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NoteColorThemeHelper.GetBackgroundBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public sealed class NoteBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NoteColorThemeHelper.GetBorderBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public sealed class NoteHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NoteColorThemeHelper.GetHeaderBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public sealed class NoteAccentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NoteColorThemeHelper.GetAccentBrush(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
                return b ? Visibility.Collapsed : Visibility.Visible;
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
