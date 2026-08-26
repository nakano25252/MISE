using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class ImageCutoutSettings
{
	public string Mode { get; set; } = "自動";

	public double TolerancePercent { get; set; } = 18.0;

	public string SampleColor { get; set; } = string.Empty;

	public double EdgeExpandPixels { get; set; }

	public double FeatherPixels { get; set; } = 2.0;

	public double SmoothPixels { get; set; } = 1.0;

	public List<ShapePointModel> Polygon { get; set; } = new List<ShapePointModel>();

	public List<ImageMaskStroke> Strokes { get; set; } = new List<ImageMaskStroke>();
}
