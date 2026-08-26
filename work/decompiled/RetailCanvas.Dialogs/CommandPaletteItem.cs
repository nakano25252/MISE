using System;

namespace RetailCanvas.Dialogs;

public sealed class CommandPaletteItem
{
	public string Name { get; init; } = string.Empty;

	public string Category { get; init; } = string.Empty;

	public string Shortcut { get; init; } = string.Empty;

	public string Keywords { get; init; } = string.Empty;

	public Action Execute { get; init; } = delegate
	{
	};

	public override string ToString()
	{
		if (!string.IsNullOrWhiteSpace(Shortcut))
		{
			return $"{Category}  ›  {Name}\u3000\u3000\u3000\u3000\u3000\u3000\u3000\u3000\u3000{Shortcut}";
		}
		return Category + "  ›  " + Name;
	}
}
