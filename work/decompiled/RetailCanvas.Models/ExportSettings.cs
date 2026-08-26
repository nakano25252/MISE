namespace RetailCanvas.Models;

public sealed class ExportSettings
{
	public int Dpi { get; set; } = 300;

	public string Format { get; set; } = "PDF";

	public int JpegQuality { get; set; } = 92;

	public bool TransparentBackground { get; set; }

	public bool ExportAllPages { get; set; } = true;

	public string NamingRule { get; set; } = "{Brand}_{Product}_{Store}_{Size}_{Date}";
}
