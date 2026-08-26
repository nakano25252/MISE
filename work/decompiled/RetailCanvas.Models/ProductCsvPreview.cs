using System.Collections.Generic;
using System.Linq;

namespace RetailCanvas.Models;

public sealed class ProductCsvPreview
{
	public string SourcePath { get; set; } = string.Empty;

	public string EncodingName { get; set; } = string.Empty;

	public List<ProductCsvRow> Rows { get; } = new List<ProductCsvRow>();

	public List<string> RecognizedHeaders { get; } = new List<string>();

	public List<string> UnknownHeaders { get; } = new List<string>();

	public int ImportableCount => Rows.Count((ProductCsvRow row) => !row.IsSkipped);

	public int NewCount => Rows.Count((ProductCsvRow row) => !row.IsSkipped && row.ExistingProductId == 0);

	public int UpdateCount => Rows.Count((ProductCsvRow row) => !row.IsSkipped && row.ExistingProductId != 0);

	public int SkipCount => Rows.Count((ProductCsvRow row) => row.IsSkipped);

	public int WarningCount => Rows.Sum((ProductCsvRow row) => row.Warnings.Count);
}
