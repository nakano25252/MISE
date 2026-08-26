using System;
using System.IO;

namespace RetailCanvas.Services;

public sealed class AssetTrashEntry
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public string OriginalPath { get; set; } = string.Empty;

	public string TrashPath { get; set; } = string.Empty;

	public DateTime DeletedAt { get; set; } = DateTime.Now;

	public string Label => $"{Path.GetFileName(OriginalPath)}  —  {DeletedAt:yyyy/MM/dd HH:mm}";
}
