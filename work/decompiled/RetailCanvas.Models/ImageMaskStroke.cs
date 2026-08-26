namespace RetailCanvas.Models;

public sealed class ImageMaskStroke
{
	public bool Keep { get; set; }

	public double XPercent { get; set; }

	public double YPercent { get; set; }

	public double RadiusPercent { get; set; } = 2.0;
}
