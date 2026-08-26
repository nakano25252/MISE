using System;

namespace RetailCanvas.Models;

public sealed class RecentProjectInfo
{
	public string FilePath { get; set; } = string.Empty;

	public string ProjectName { get; set; } = string.Empty;

	public string PaperName { get; set; } = string.Empty;

	public string BrandName { get; set; } = string.Empty;

	public string StoreName { get; set; } = string.Empty;

	public DateTime LastOpenedAt { get; set; } = DateTime.Now;

	public bool IsAutoSave { get; set; }
}
