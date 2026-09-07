using RetailCanvas.Controls;
using RetailCanvas.Models;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class TextRenderingTests
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { CheckRendering(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) throw new InvalidOperationException("Text rendering regression", failure);
    }

    private static void CheckRendering()
    {
        var font = new FontFamily("Arial");
        foreach (string text in new[] { "I", ".", "MISE", "日本語", "AB\nCD" })
        {
            var model = new CanvasElementModel { Kind = ElementKind.Text, Text = text,
                TextFrameTight = true, FontSizePt = 30, TextOutlineThicknessPt = 0,
                TextExtrusionDepthPt = 0, CharacterSpacing = 0, LineSpacingPt = 0, LineHeight = 0 };
            Size large = OutlinedTextVisual.MeasureTightSize(model, font);
            model.FontSizePt = 3;
            Size small = OutlinedTextVisual.MeasureTightSize(model, font);
            Require(Math.Abs(small.Width / large.Width - 0.1) < 0.002, "3pt width scales with font: " + text);
            Require(Math.Abs(small.Height / large.Height - 0.1) < 0.002, "3pt height scales with font: " + text);
            var visual = new OutlinedTextVisual(model, font, Brushes.Black, Brushes.Black, Brushes.Black)
                { Width = small.Width, Height = small.Height };
            visual.Measure(small);
            visual.Arrange(new Rect(small));
            visual.UpdateLayout();
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
                dc.DrawRectangle(new VisualBrush(visual), null, new Rect(4, 4, small.Width, small.Height));
            const int scale = 8;
            int width = (int)Math.Ceiling((small.Width + 8) * scale);
            int height = (int)Math.Ceiling((small.Height + 8) * scale);
            var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(drawing);
            byte[] pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            int left = width, top = height, right = -1, bottom = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[(y * width + x) * 4 + 3] > 8)
                { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
            Require(right >= left && bottom >= top, "3pt glyph is visible: " + text);
            Require(Math.Abs(left - 4 * scale) <= 2 && Math.Abs(top - 4 * scale) <= 2, "Glyph starts at tight frame: " + text);
            Require(Math.Abs((right + 1) - (4 + small.Width) * scale) <= 2 &&
                Math.Abs((bottom + 1) - (4 + small.Height) * scale) <= 2, "Glyph ends at tight frame: " + text);
        }
        Console.WriteLine("PASS: WPF 3pt glyph rendering, proportional measurement and tight frame edges");
    }

    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
