using System;
using System.Text.Json.Serialization;

namespace RetailCanvas.Models;

public sealed class ProductModel
{
	public long ProductId { get; set; }

	public string Manufacturer { get; set; } = string.Empty;

	public string BrandName { get; set; } = string.Empty;

	public string Category { get; set; } = "その他";

	public string ProductName { get; set; } = string.Empty;

	public string ModelNumber { get; set; } = string.Empty;

	public string JanCode { get; set; } = string.Empty;

	public DateTime? ReleaseDate { get; set; }

	public decimal? Price { get; set; }

	public string Colors { get; set; } = string.Empty;

	public string ImagePath { get; set; } = string.Empty;

	public string CatchCopy { get; set; } = string.Empty;

	public string Features { get; set; } = string.Empty;

	public string Specifications { get; set; } = string.Empty;

	public string Notes { get; set; } = string.Empty;

	public string Codec { get; set; } = string.Empty;

	public string Waterproof { get; set; } = string.Empty;

	public string Battery { get; set; } = string.Empty;

	public string Weight { get; set; } = string.Empty;

	public string Url { get; set; } = string.Empty;

	public string Tags { get; set; } = string.Empty;

	public string SalesTalk { get; set; } = string.Empty;

	public string SalesPointData { get; set; } = string.Empty;

	public string AssetFolderPath { get; set; } = string.Empty;

	public string SourceStatus { get; set; } = string.Empty;

	public string AssetRoleData { get; set; } = string.Empty;

	public bool IsDeleted { get; set; }

	public DateTime? DeletedAt { get; set; }

	[JsonIgnore]
	public bool IsSelectedForBatch { get; set; }

	public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
