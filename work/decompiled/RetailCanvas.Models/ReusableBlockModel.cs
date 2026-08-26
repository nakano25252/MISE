using System;
using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class ReusableBlockModel
{
	public string Name { get; set; } = "再利用ブロック";

	public DateTime CreatedAt { get; set; } = DateTime.Now;

	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public double WidthMm { get; set; }

	public double HeightMm { get; set; }

	public List<CanvasElementModel> Elements { get; set; } = new List<CanvasElementModel>();
}
