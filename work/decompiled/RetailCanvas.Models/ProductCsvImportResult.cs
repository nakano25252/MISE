using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class ProductCsvImportResult
{
	public int Added { get; set; }

	public int Updated { get; set; }

	public int Skipped { get; set; }

	public List<string> Warnings { get; } = new List<string>();
}
