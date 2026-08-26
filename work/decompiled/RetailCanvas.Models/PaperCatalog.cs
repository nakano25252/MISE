using System;
using System.Collections.Generic;
using System.Linq;

namespace RetailCanvas.Models;

public static class PaperCatalog
{
	private static readonly List<PaperSizeDefinition> Sizes = new List<PaperSizeDefinition>
	{
		new PaperSizeDefinition("A3", 297.0, 420.0),
		new PaperSizeDefinition("A4", 210.0, 297.0),
		new PaperSizeDefinition("A5", 148.0, 210.0),
		new PaperSizeDefinition("A6", 105.0, 148.0),
		new PaperSizeDefinition("B4", 257.0, 364.0),
		new PaperSizeDefinition("B5", 182.0, 257.0),
		new PaperSizeDefinition("名刺", 91.0, 55.0),
		new PaperSizeDefinition("はがき", 100.0, 148.0),
		new PaperSizeDefinition("L判", 89.0, 127.0),
		new PaperSizeDefinition("2L判", 127.0, 178.0),
		new PaperSizeDefinition("プライスカード", 100.0, 65.0),
		new PaperSizeDefinition("棚帯", 300.0, 40.0),
		new PaperSizeDefinition("名刺2枚折り", 182.0, 55.0),
		new PaperSizeDefinition("自由サイズ", 210.0, 297.0)
	};

	public static IReadOnlyList<PaperSizeDefinition> All => Sizes;

	public static PaperSizeDefinition Get(string name)
	{
		PaperSizeDefinition result = Sizes.FirstOrDefault((PaperSizeDefinition x) => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
		if (!(result.WidthMm > 0.0))
		{
			return Sizes.First((PaperSizeDefinition x) => x.Name == "A4");
		}
		return result;
	}
}
