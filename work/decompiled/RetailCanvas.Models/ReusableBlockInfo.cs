using System;

namespace RetailCanvas.Models;

public sealed class ReusableBlockInfo
{
	public string Name { get; init; } = string.Empty;

	public string FilePath { get; init; } = string.Empty;

	public int ElementCount { get; init; }

	public DateTime UpdatedAt { get; init; }

	public override string ToString()
	{
		return $"{Name}\u3000{ElementCount}要素\u3000{UpdatedAt:yyyy/MM/dd HH:mm}";
	}
}
