using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace RetailCanvas.Services;

public sealed record EditableDesignDocument(IReadOnlyList<EditableDesignPage> Pages);

public sealed record EditableDesignPage(int PageIndex, double WidthPt, double HeightPt, IReadOnlyList<EditableTextBlock> TextBlocks, IReadOnlyList<EditableImageBlock> Images);

public sealed record EditableTextBlock(string Text, double LeftPt, double BottomPt, double WidthPt, double HeightPt, double FontSizePt, string FontName, string Color, double Rotation, int FontWeight, bool IsItalic);

public sealed record EditableImageBlock(byte[] Data, double LeftPt, double BottomPt, double WidthPt, double HeightPt, double Rotation, int PixelWidth, int PixelHeight);

public static class EditableDesignImportService
{
	public static bool IsPdfCompatible(string path)
	{
		try
		{
			using FileStream stream = File.OpenRead(path);
			Span<byte> header = stackalloc byte[5];
			return stream.Read(header) == header.Length && header.SequenceEqual("%PDF-"u8);
		}
		catch
		{
			return false;
		}
	}

	public static Task<EditableDesignDocument> ReadAsync(string path)
	{
		return Task.Run(() => Read(path));
	}

	private static EditableDesignDocument Read(string path)
	{
		if (!IsPdfCompatible(path))
		{
			throw new InvalidDataException("このAIファイルにはPDF互換データがありません。Illustratorで『PDF互換ファイルを作成』を有効にして保存し直してください。");
		}
		List<EditableDesignPage> pages = new List<EditableDesignPage>();
		using PdfDocument document = PdfDocument.Open(path);
		foreach (Page page in document.GetPages())
		{
			List<EditableTextBlock> blocks = new List<EditableTextBlock>();
			List<EditableImageBlock> images = new List<EditableImageBlock>();
			IEnumerable<Word> words = page.GetWords(NearestNeighbourWordExtractor.Instance);
			foreach (Word word in words)
			{
				string text = word.Text?.Trim() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(text) || word.Letters.Count == 0)
				{
					continue;
				}
				var box = word.BoundingBox;
				Letter first = word.Letters[0];
				double fontSize = word.Letters.Where(letter => double.IsFinite(letter.PointSize) && letter.PointSize > 0.0).Select(letter => letter.PointSize).DefaultIfEmpty(Math.Max(6.0, box.Height)).Median();
				string fontName = NormalizeFontName(word.FontName ?? first.FontName);
				string color = ToColor(first);
				double rotation = word.TextOrientation switch
				{
					TextOrientation.Rotate90 => 90.0,
					TextOrientation.Rotate180 => 180.0,
					TextOrientation.Rotate270 => 270.0,
					_ => 0.0
				};
				int fontWeight = first.FontDetails.IsBold ? 700 : Math.Clamp(first.FontDetails.Weight, 100, 900);
				blocks.Add(new EditableTextBlock(text, box.Left, box.Bottom, Math.Max(1.0, box.Width), Math.Max(1.0, box.Height), Math.Clamp(fontSize, 1.0, 300.0), fontName, color, rotation, fontWeight, first.FontDetails.IsItalic));
			}
			foreach (IPdfImage image in page.GetImages())
			{
				if (image.IsImageMask || !TryGetDisplayBytes(image, out byte[] bytes))
				{
					continue;
				}
				var box = image.BoundingBox;
				images.Add(new EditableImageBlock(bytes, box.Left, box.Bottom, Math.Max(1.0, box.Width), Math.Max(1.0, box.Height), box.Rotation, image.WidthInSamples, image.HeightInSamples));
			}
			pages.Add(new EditableDesignPage(page.Number - 1, page.Width, page.Height, blocks, images));
		}
		return new EditableDesignDocument(pages);
	}

	private static bool TryGetDisplayBytes(IPdfImage image, out byte[] bytes)
	{
		if (image.TryGetPng(out byte[]? png) && png != null && png.Length > 0)
		{
			bytes = png;
			return true;
		}
		byte[] raw = image.RawMemory.ToArray();
		if (raw.Length > 3 && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF)
		{
			bytes = raw;
			return true;
		}
		if (raw.Length > 8 && raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47)
		{
			bytes = raw;
			return true;
		}
		bytes = Array.Empty<byte>();
		return false;
	}

	private static string NormalizeFontName(string? value)
	{
		string name = string.IsNullOrWhiteSpace(value) ? "Yu Gothic UI" : value.Trim();
		int subset = name.IndexOf('+');
		if (subset >= 0 && subset + 1 < name.Length)
		{
			name = name[(subset + 1)..];
		}
		return name.Replace(',', ' ').Replace('-', ' ').Trim();
	}

	private static string ToColor(Letter letter)
	{
		try
		{
			(double red, double green, double blue) = letter.Color.ToRGBValues();
			byte r = (byte)Math.Clamp((int)Math.Round(red * 255.0), 0, 255);
			byte g = (byte)Math.Clamp((int)Math.Round(green * 255.0), 0, 255);
			byte b = (byte)Math.Clamp((int)Math.Round(blue * 255.0), 0, 255);
			return $"#FF{r:X2}{g:X2}{b:X2}";
		}
		catch
		{
			return "#FF172033";
		}
	}

	private static double Median(this IEnumerable<double> values)
	{
		double[] ordered = values.OrderBy(value => value).ToArray();
		if (ordered.Length == 0)
		{
			return 12.0;
		}
		int middle = ordered.Length / 2;
		return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) / 2.0 : ordered[middle];
	}
}
