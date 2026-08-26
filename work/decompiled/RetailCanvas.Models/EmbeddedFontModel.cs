namespace RetailCanvas.Models;

public sealed class EmbeddedFontModel
{
	public string FamilyName { get; set; } = string.Empty;

	public string FileName { get; set; } = string.Empty;

	public string DataBase64 { get; set; } = string.Empty;

	public string Sha256 { get; set; } = string.Empty;
}
