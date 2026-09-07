using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class ValidationService
{
	public List<ValidationIssue> Validate(PageModel page, IEnumerable<string>? embeddedFontFamilies = null)
	{
		List<ValidationIssue> list = new List<ValidationIssue>();
		HashSet<string> availableEmbeddedFonts = new HashSet<string>(embeddedFontFamilies ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
		double safeMarginMm = page.SafeMarginMm;
		foreach (CanvasElementModel element in page.Elements.Where((CanvasElementModel x) => x.IsVisible))
		{
			bool flag = element.Kind == ElementKind.Shape && element.Xmm <= 0.0 && element.Ymm <= 0.0 && element.WidthMm >= page.WidthMm && element.HeightMm >= page.HeightMm;
			if (!element.IsDecoration && !flag && (element.Xmm < safeMarginMm || element.Ymm < safeMarginMm || element.Xmm + element.WidthMm > page.WidthMm - safeMarginMm || element.Ymm + element.HeightMm > page.HeightMm - safeMarginMm))
			{
				list.Add(Issue(IssueSeverity.Warning, "安全領域からはみ出しています", $"仕上がり端から{safeMarginMm:0.#}mm以上内側へ移動すると、印刷切れを防げます。", element));
			}
			if (element.WidthMm <= 0.5 || element.HeightMm <= 0.5)
			{
				list.Add(Issue(IssueSeverity.Error, "要素サイズが不正です", "幅・高さを1mm以上に設定してください。", element));
			}
			if (element.Kind == ElementKind.Text)
			{
				if (string.IsNullOrWhiteSpace(element.Text))
				{
					list.Add(Issue(IssueSeverity.Error, "空の文字要素があります", "文字を入力するか、要素を削除してください。", element));
				}
				bool flag2 = page.WidthMm <= 100.0 && page.HeightMm <= 150.0;
				double num = (flag2 ? 5.5 : 7.0);
				double num2 = ((element.Name.Contains("注意") || element.Name.Contains("補足") || element.Name.Contains("期間") || element.Name.Contains("URL") || element.Name.Contains("ラベル") || element.Name.Contains("資料種別") || element.Name.Contains("カテゴリ")) ? num : ((double)(flag2 ? 7 : 10)));
				if (element.FontSizePt < num)
				{
					list.Add(Issue(IssueSeverity.Error, "文字が小さすぎます", $"現在 {element.FontSizePt:0.#}pt。最低{num:0.#}pt以上を推奨します。", element));
				}
				else if (element.FontSizePt < num2)
				{
					list.Add(Issue(IssueSeverity.Warning, "文字サイズの確認を推奨", $"現在 {element.FontSizePt:0.#}pt。本文は{num2:0.#}pt以上が目安です。", element));
				}
				bool num3 = element.PlaceholderKey.Contains("製品画像", StringComparison.OrdinalIgnoreCase);
				Color second = ParseColor(VisibleBackground(page, element));
				double val = ContrastRatio(ParseColor(element.TextColor), second);
				double val2 = ((element.TextOutlineThicknessPt > 0.0) ? ContrastRatio(ParseColor(element.TextOutlineColor), second) : 0.0);
				if (!num3 && Math.Max(val, val2) < 3.0)
				{
					list.Add(Issue(IssueSeverity.Warning, "文字のコントラストが不足しています", "文字色または背景色を変更し、売場での視認性を高めてください。", element));
				}
				if (!availableEmbeddedFonts.Contains(element.FontFamily) && !Fonts.SystemFontFamilies.Any((FontFamily f) => string.Equals(f.Source, element.FontFamily, StringComparison.OrdinalIgnoreCase)))
				{
					list.Add(Issue(IssueSeverity.Error, "フォントが見つかりません", element.FontFamily + " を別のフォントへ置換してください。", element));
				}
			}
			else if (element.Kind == ElementKind.Image)
			{
				if (!string.IsNullOrWhiteSpace(element.PdfSourcePath) && !File.Exists(element.PdfSourcePath))
				{
					list.Add(Issue(IssueSeverity.Warning, "PDF／AI原本が見つかりません", "編集用プレビューは表示できますが、高品質な再描画には元のPDF／AIファイルを同じ場所へ戻してください。", element));
				}
				if (string.IsNullOrWhiteSpace(element.ImageDataBase64) && (string.IsNullOrWhiteSpace(element.ImageSourcePath) || !File.Exists(element.ImageSourcePath)))
				{
					list.Add(Issue(IssueSeverity.Error, "画像データが見つかりません", "画像を再リンクまたは差し替えてください。", element));
				}
				else if (element.ImageUsesLinkedOriginal && (string.IsNullOrWhiteSpace(element.ImageSourcePath) || !File.Exists(element.ImageSourcePath)))
				{
					list.Add(Issue(IssueSeverity.Warning, "大容量画像の元データが見つかりません", "編集用プレビューは表示できますが、高品質出力には元のTIFF／画像を再リンクしてください。", element));
				}
				else if (element.EffectiveDpi < 150.0)
				{
					list.Add(Issue(IssueSeverity.Error, "画像解像度が不足しています", $"実効DPIは約{element.EffectiveDpi:0}dpiです。画像を小さくするか、高解像度画像へ差し替えてください。", element));
				}
				else if (element.EffectiveDpi < 200.0)
				{
					list.Add(Issue(IssueSeverity.Warning, "画像解像度が低めです", $"実効DPIは約{element.EffectiveDpi:0}dpiです。200dpi以上を推奨します。", element));
				}
				else if (element.EffectiveDpi < 300.0)
				{
					list.Add(Issue(IssueSeverity.Suggestion, "画像品質は通常レベルです", $"実効DPIは約{element.EffectiveDpi:0}dpiです。高品質印刷は300dpi以上が目安です。", element));
				}
			}
			else if (element.Kind == ElementKind.QrCode)
			{
				if (element.WidthMm < 18.0 || element.HeightMm < 18.0)
				{
					list.Add(Issue(IssueSeverity.Error, "QRコードが小さすぎます", "読み取り安定性のため、18mm角以上を推奨します。", element));
				}
				if (string.IsNullOrWhiteSpace(element.QrContent))
				{
					list.Add(Issue(IssueSeverity.Error, "QRコードの内容が空です", "URLまたはテキストを入力してください。", element));
				}
				if (ContrastRatio(ParseColor(element.QrForeground), ParseColor(element.QrBackground)) < 4.5)
				{
					list.Add(Issue(IssueSeverity.Error, "QRコードのコントラストが不足しています", "前景色を濃く、背景色を明るくしてください。", element));
				}
				if (element.QrContent.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !Uri.TryCreate(element.QrContent, UriKind.Absolute, out Uri _))
				{
					list.Add(Issue(IssueSeverity.Warning, "URL形式を確認してください", "QRコードのURLを開けない可能性があります。", element));
				}
			}
		}
		List<CanvasElementModel> list2 = page.Elements.Where((CanvasElementModel x) => x.IsVisible && x.Kind == ElementKind.Text).ToList();
		List<CanvasElementModel> list3 = page.Elements.Where((CanvasElementModel x) => x.IsVisible && (x.Kind == ElementKind.Image || x.Kind == ElementKind.QrCode)).ToList();
		foreach (CanvasElementModel item in list2)
		{
			foreach (CanvasElementModel item2 in list3)
			{
				Rect rect = new Rect(item.Xmm, item.Ymm, item.WidthMm, item.HeightMm);
				Rect rect2 = new Rect(item2.Xmm, item2.Ymm, item2.WidthMm, item2.HeightMm);
				if (rect.IntersectsWith(rect2))
				{
					list.Add(Issue(IssueSeverity.Warning, "文字と重要画像が重なっています", "「" + item2.Name + "」との重なりを確認してください。意図した重なりであれば無視できます。", item));
				}
			}
		}
		if (list.Count == 0)
		{
			list.Add(new ValidationIssue
			{
				Severity = IssueSeverity.Ok,
				Title = "問題は見つかりませんでした",
				Detail = "書き出し可能な状態です。"
			});
		}
		return list;
	}

	private static ValidationIssue Issue(IssueSeverity severity, string title, string detail, CanvasElementModel element)
	{
		return new ValidationIssue
		{
			Severity = severity,
			Title = title,
			Detail = detail,
			ElementId = element.Id,
			ElementName = element.Name
		};
	}

	private static string VisibleBackground(PageModel page, CanvasElementModel text)
	{
		if (!text.TextBackground.StartsWith("#00", StringComparison.OrdinalIgnoreCase))
		{
			return text.TextBackground;
		}
		Point center = new Point(text.Xmm + text.WidthMm / 2.0, text.Ymm + text.HeightMm / 2.0);
		return (from x in page.Elements
			where x.IsVisible && x.Kind == ElementKind.Shape && x.ZIndex < text.ZIndex && center.X >= x.Xmm && center.X <= x.Xmm + x.WidthMm && center.Y >= x.Ymm && center.Y <= x.Ymm + x.HeightMm
			orderby x.ZIndex descending
			select x).FirstOrDefault()?.FillColor ?? page.Background;
	}

	private static Color ParseColor(string value)
	{
		try
		{
			return (Color)ColorConverter.ConvertFromString(value);
		}
		catch
		{
			return Colors.White;
		}
	}

	private static double ContrastRatio(Color first, Color second)
	{
		double val = L(first);
		double val2 = L(second);
		return (Math.Max(val, val2) + 0.05) / (Math.Min(val, val2) + 0.05);
		static double Channel(byte b)
		{
			double num = (double)(int)b / 255.0;
			if (!(num <= 0.03928))
			{
				return Math.Pow((num + 0.055) / 1.055, 2.4);
			}
			return num / 12.92;
		}
		static double L(Color c)
		{
			return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
		}
	}
}
