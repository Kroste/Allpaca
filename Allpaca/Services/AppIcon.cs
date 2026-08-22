using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Allpaca.Services;

/// <summary>
/// Rendert das Allpaca-Logo programmatisch via SkiaSharp (kommt transitiv über
/// Avalonia.Skia, keine Extra-Dep). Eine Quelle der Wahrheit für Window.Icon
/// und das InfoWindow-Logo - damit das XAML-Designer-Preview ohne Asset-Files
/// auskommt und das Packaging-SVG (packaging/linux/allpaca.svg) nur für
/// AppImage/.desktop existiert.
/// </summary>
public static class AppIcon
{
    private static Bitmap? _bitmap;
    private static WindowIcon? _windowIcon;

    /// <summary>256x256 RGBA-Bitmap für Image.Source-Bindings (z. B. InfoWindow).</summary>
    public static Bitmap Bitmap => _bitmap ??= Render(256);

    /// <summary>WindowIcon für Window.Icon - cached, sodass mehrere Fenster
    /// dieselbe Bitmap teilen.</summary>
    public static WindowIcon WindowIcon => _windowIcon ??= new WindowIcon(Bitmap);

    private static Bitmap Render(int size)
    {
        using var skBitmap = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(skBitmap))
        {
            canvas.Clear(SKColors.Transparent);
            DrawAlpaca(canvas, size);
        }

        // SKBitmap -> PNG -> Avalonia Bitmap. Umweg über PNG ist sauberer als
        // direkter Pixel-Copy, weil Avalonia Bitmap eine Stream-Quelle erwartet.
        using var image = SKImage.FromBitmap(skBitmap);
        using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream();
        pngData.SaveTo(stream);
        stream.Position = 0;
        return new Bitmap(stream);
    }

    private static void DrawAlpaca(SKCanvas canvas, int size)
    {
        var s = size / 256f;  // alle Koordinaten in 256x256-Designgröße

        using var bg = new SKPaint { Color = SKColor.Parse("#2BB673"), IsAntialias = true };
        using var bodyShadow = new SKPaint { Color = SKColor.Parse("#1F8E5A"), IsAntialias = true };
        using var fur = new SKPaint { Color = SKColor.Parse("#F5F5F5"), IsAntialias = true };
        using var ink = new SKPaint { Color = SKColor.Parse("#15171A"), IsAntialias = true };
        using var inkStroke = new SKPaint
        {
            Color = SKColor.Parse("#15171A"),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3 * s,
            StrokeCap = SKStrokeCap.Round,
        };

        // Hintergrund - abgerundetes Quadrat in Markenfarbe.
        canvas.DrawRoundRect(new SKRect(0, 0, size, size), 48 * s, 48 * s, bg);

        // Body-Andeutung als dunkleres Oval hinter dem Kopf.
        canvas.DrawOval(new SKRect(50 * s, 200 * s, 206 * s, 240 * s), bodyShadow);

        // Kopf als Bezier-Pfad.
        using var headBuilder = new SKPathBuilder();
        headBuilder.MoveTo(78 * s, 145 * s);
        headBuilder.CubicTo(78 * s, 100 * s, 95 * s, 70 * s, 128 * s, 70 * s);
        headBuilder.CubicTo(161 * s, 70 * s, 178 * s, 100 * s, 178 * s, 145 * s);
        headBuilder.LineTo(178 * s, 175 * s);
        headBuilder.CubicTo(178 * s, 200 * s, 158 * s, 220 * s, 128 * s, 220 * s);
        headBuilder.CubicTo(98 * s, 220 * s, 78 * s, 200 * s, 78 * s, 175 * s);
        headBuilder.Close();
        using var head = headBuilder.Detach();
        canvas.DrawPath(head, fur);

        // Wollbüsche oben (drei Locken).
        canvas.DrawCircle(115 * s, 70 * s, 12 * s, fur);
        canvas.DrawCircle(128 * s, 60 * s, 14 * s, fur);
        canvas.DrawCircle(141 * s, 70 * s, 12 * s, fur);

        // Ohren - links und rechts kleine Dreiecke.
        using var leftEarBuilder = new SKPathBuilder();
        leftEarBuilder.MoveTo(85 * s, 78 * s);
        leftEarBuilder.LineTo(70 * s, 50 * s);
        leftEarBuilder.LineTo(95 * s, 65 * s);
        leftEarBuilder.Close();
        using var leftEar = leftEarBuilder.Detach();
        canvas.DrawPath(leftEar, fur);

        using var rightEarBuilder = new SKPathBuilder();
        rightEarBuilder.MoveTo(171 * s, 78 * s);
        rightEarBuilder.LineTo(186 * s, 50 * s);
        rightEarBuilder.LineTo(161 * s, 65 * s);
        rightEarBuilder.Close();
        using var rightEar = rightEarBuilder.Detach();
        canvas.DrawPath(rightEar, fur);

        // Augen - leicht ovale schwarze Punkte.
        canvas.DrawOval(new SKRect(100 * s, 121 * s, 112 * s, 139 * s), ink);
        canvas.DrawOval(new SKRect(144 * s, 121 * s, 156 * s, 139 * s), ink);

        // Nase.
        canvas.DrawOval(new SKRect(115 * s, 161 * s, 141 * s, 179 * s), ink);

        // Mund - leicht abwärts gewölbter Bogen.
        using var smileBuilder = new SKPathBuilder();
        smileBuilder.MoveTo(120 * s, 185 * s);
        smileBuilder.QuadTo(128 * s, 192 * s, 136 * s, 185 * s);
        using var smile = smileBuilder.Detach();
        canvas.DrawPath(smile, inkStroke);
    }
}
