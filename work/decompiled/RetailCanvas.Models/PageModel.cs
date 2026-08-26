using System;
using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class PageModel
{
	public Guid PageId { get; set; } = Guid.NewGuid();

	public string Name { get; set; } = "ページ 1";

	public double WidthMm { get; set; } = 210.0;

	public double HeightMm { get; set; } = 297.0;

	public string Background { get; set; } = "#FFFFFFFF";

	public string BackgroundTextureName { get; set; } = string.Empty;

	public string? BackgroundTextureDataBase64 { get; set; }

	public double BackgroundTextureOpacity { get; set; } = 0.45;

	public double BackgroundTextureScale { get; set; } = 1.0;

	public double SafeMarginMm { get; set; } = 5.0;

	public double BleedMm { get; set; } = 3.0;

	public double PrintMarginMm { get; set; } = 5.0;

	public bool ShowSafeArea { get; set; } = true;

	public bool ShowBleed { get; set; }

	public bool ShowGrid { get; set; } = true;

	public List<CanvasElementModel> Elements { get; set; } = new List<CanvasElementModel>();

	public static PageModel Create(string paperName, bool landscape)
	{
		PaperSizeDefinition paperSizeDefinition = PaperCatalog.Get(paperName);
		return new PageModel
		{
			WidthMm = (landscape ? paperSizeDefinition.HeightMm : paperSizeDefinition.WidthMm),
			HeightMm = (landscape ? paperSizeDefinition.WidthMm : paperSizeDefinition.HeightMm),
			Name = "ページ 1"
		};
	}
}
