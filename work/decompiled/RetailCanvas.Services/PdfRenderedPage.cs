namespace RetailCanvas.Services;

public sealed record PdfRenderedPage(byte[] PngBytes, double Width, double Height, int PageIndex);
