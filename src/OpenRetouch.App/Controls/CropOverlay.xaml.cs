using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;

namespace OpenRetouch.App.Controls;

/// <summary>Normalized coordinates of the crop rectangle (0-1).</summary>
public sealed record CropRectChangedEventArgs(double X, double Y, double Width, double Height);

/// <summary>
/// Crop editing overlay. Place it so it fills the displayed image area.
/// Edits the crop rectangle in normalized coordinates and reports changes via CropChanged.
/// </summary>
public sealed partial class CropOverlay : UserControl
{
    private const double MinSizeNormalized = 0.05;

    private double _x;
    private double _y = 0;
    private double _w = 1;
    private double _h = 1;

    /// <summary>Aspect ratio lock (pixel ratio of the crop rectangle; null = free).</summary>
    private double? _aspectRatio;

    /// <summary>Pixel dimensions of the displayed image (for aspect calculations).</summary>
    private double _imagePixelWidth = 1;
    private double _imagePixelHeight = 1;

    public CropOverlay()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateVisuals();
    }

    public event EventHandler<CropRectChangedEventArgs>? CropChanged;

    /// <summary>Sets the crop state from outside (does not raise the change event).</summary>
    public void SetCrop(double x, double y, double width, double height)
    {
        _x = Math.Clamp(x, 0, 1);
        _y = Math.Clamp(y, 0, 1);
        _w = Math.Clamp(width, MinSizeNormalized, 1 - _x);
        _h = Math.Clamp(height, MinSizeNormalized, 1 - _y);
        UpdateVisuals();
    }

    public void SetImagePixelSize(double width, double height)
    {
        _imagePixelWidth = Math.Max(1, width);
        _imagePixelHeight = Math.Max(1, height);
    }

    /// <summary>Sets the aspect ratio lock and adjusts the current rectangle to match the ratio.</summary>
    public void SetAspectRatio(double? ratio)
    {
        _aspectRatio = ratio;
        if (ratio is { } r)
        {
            // Fit the ratio while keeping the center (accounting for the normalized-to-pixel ratio conversion)
            var centerX = _x + _w / 2;
            var centerY = _y + _h / 2;
            var pixelW = _w * _imagePixelWidth;
            var pixelH = _h * _imagePixelHeight;
            if (pixelW / pixelH > r)
            {
                pixelW = pixelH * r;
            }
            else
            {
                pixelH = pixelW / r;
            }

            _w = pixelW / _imagePixelWidth;
            _h = pixelH / _imagePixelHeight;
            _x = Math.Clamp(centerX - _w / 2, 0, 1 - _w);
            _y = Math.Clamp(centerY - _h / 2, 0, 1 - _h);
            UpdateVisuals();
            RaiseChanged();
        }
    }

    private void OnRectManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _x = Math.Clamp(_x + e.Delta.Translation.X / ActualWidth, 0, 1 - _w);
        _y = Math.Clamp(_y + e.Delta.Translation.Y / ActualHeight, 0, 1 - _h);
        UpdateVisuals();
        RaiseChanged();
        e.Handled = true;
    }

    private void OnHandleManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0 || sender is not Rectangle handle)
        {
            return;
        }

        var dx = e.Delta.Translation.X / ActualWidth;
        var dy = e.Delta.Translation.Y / ActualHeight;
        var corner = (string)handle.Tag;

        // Resize relative to the fixed (diagonal) corner
        var left = _x;
        var top = _y;
        var right = _x + _w;
        var bottom = _y + _h;

        switch (corner)
        {
            case "TL":
                left = Math.Clamp(left + dx, 0, right - MinSizeNormalized);
                top = Math.Clamp(top + dy, 0, bottom - MinSizeNormalized);
                break;
            case "TR":
                right = Math.Clamp(right + dx, left + MinSizeNormalized, 1);
                top = Math.Clamp(top + dy, 0, bottom - MinSizeNormalized);
                break;
            case "BL":
                left = Math.Clamp(left + dx, 0, right - MinSizeNormalized);
                bottom = Math.Clamp(bottom + dy, top + MinSizeNormalized, 1);
                break;
            default: // BR
                right = Math.Clamp(right + dx, left + MinSizeNormalized, 1);
                bottom = Math.Clamp(bottom + dy, top + MinSizeNormalized, 1);
                break;
        }

        _x = left;
        _y = top;
        _w = right - left;
        _h = bottom - top;

        // Aspect ratio lock: adjust the height to match the ratio (relative to the fixed corner)
        if (_aspectRatio is { } r)
        {
            var pixelW = _w * _imagePixelWidth;
            var targetPixelH = pixelW / r;
            var targetH = Math.Min(targetPixelH / _imagePixelHeight, 1);
            if (corner is "TL" or "TR")
            {
                _y = Math.Clamp(bottom - targetH, 0, bottom - MinSizeNormalized);
                _h = bottom - _y;
            }
            else
            {
                _h = Math.Min(targetH, 1 - _y);
            }
        }

        UpdateVisuals();
        RaiseChanged();
        e.Handled = true;
    }

    private void RaiseChanged() => CropChanged?.Invoke(this, new CropRectChangedEventArgs(_x, _y, _w, _h));

    private void UpdateVisuals()
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var rectX = _x * w;
        var rectY = _y * h;
        var rectW = _w * w;
        var rectH = _h * h;

        Canvas.SetLeft(CropRect, rectX);
        Canvas.SetTop(CropRect, rectY);
        CropRect.Width = rectW;
        CropRect.Height = rectH;

        // Shades (dimmed areas)
        ShadeTop.Width = w;
        ShadeTop.Height = rectY;
        Canvas.SetLeft(ShadeTop, 0);
        Canvas.SetTop(ShadeTop, 0);

        ShadeBottom.Width = w;
        ShadeBottom.Height = Math.Max(0, h - rectY - rectH);
        Canvas.SetLeft(ShadeBottom, 0);
        Canvas.SetTop(ShadeBottom, rectY + rectH);

        ShadeLeft.Width = rectX;
        ShadeLeft.Height = rectH;
        Canvas.SetLeft(ShadeLeft, 0);
        Canvas.SetTop(ShadeLeft, rectY);

        ShadeRight.Width = Math.Max(0, w - rectX - rectW);
        ShadeRight.Height = rectH;
        Canvas.SetLeft(ShadeRight, rectX + rectW);
        Canvas.SetTop(ShadeRight, rectY);

        // Handles
        PositionHandle(HandleTopLeft, rectX, rectY);
        PositionHandle(HandleTopRight, rectX + rectW, rectY);
        PositionHandle(HandleBottomLeft, rectX, rectY + rectH);
        PositionHandle(HandleBottomRight, rectX + rectW, rectY + rectH);
    }

    private static void PositionHandle(Rectangle handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - handle.Width / 2);
        Canvas.SetTop(handle, y - handle.Height / 2);
    }
}
