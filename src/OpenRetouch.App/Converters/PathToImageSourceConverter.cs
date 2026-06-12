using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OpenRetouch.App.Converters;

/// <summary>Converts a file path string to a BitmapImage (null/empty becomes null).</summary>
public sealed class PathToImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string language)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            return new BitmapImage(new Uri(path));
        }
        catch (UriFormatException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language) =>
        throw new NotSupportedException();
}
