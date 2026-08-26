namespace RetailCanvas.Models;

public sealed class ProductCsvImportOptions
{
	public ProductCsvDuplicateMode DuplicateMode { get; set; }

	public bool ClearExistingOnBlank { get; set; }
}
