namespace RetailCanvas.Dialogs;

public sealed class NewProjectOptions
{
	public string ProjectName { get; set; } = "無題の販促物";

	public string Purpose { get; set; } = "製品単品POP";

	public string PaperName { get; set; } = "A4";

	public bool Landscape { get; set; }

	public int PageCount { get; set; } = 1;

	public string Brand { get; set; } = string.Empty;

	public string Store { get; set; } = string.Empty;

	public string Author { get; set; } = string.Empty;

	public string PrintMode { get; set; } = "家庭用プリンタ";

	public string Background { get; set; } = "#FFFFFFFF";

	public double? CustomWidthMm { get; set; }

	public double? CustomHeightMm { get; set; }
}
