namespace RetailCanvas.Services;

public sealed record RenderedPage(byte[] PngBytes, double WidthMm, double HeightMm, string Name);
