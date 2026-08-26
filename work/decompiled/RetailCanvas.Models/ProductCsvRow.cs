using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class ProductCsvRow
{
	public int RowNumber { get; set; }

	public ProductModel Product { get; set; } = new ProductModel();

	public long ExistingProductId { get; set; }

	public bool IsSkipped { get; set; }

	public List<string> Warnings { get; } = new List<string>();
}
