using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Controls;

public sealed class OutlinedTextVisual : FrameworkElement
{
	private sealed record SpacedGlyph(FormattedText Text, double Width, bool IsWhitespace);

	private sealed class SpacedLine
	{
		public List<SpacedGlyph> Glyphs { get; } = new List<SpacedGlyph>();

		public double Width { get; set; }
	}

	private readonly CanvasElementModel _model;

	private readonly FontFamily _fontFamily;

	private readonly Brush _face;

	private readonly Brush _outline;

	private readonly Brush _extrusion;

	public OutlinedTextVisual(CanvasElementModel model, FontFamily fontFamily, Brush face, Brush outline, Brush extrusion)
	{
		_model = model;
		_fontFamily = fontFamily;
		_face = face;
		_outline = outline;
		_extrusion = extrusion;
		base.SnapsToDevicePixels = false;
		base.IsHitTestVisible = false;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		// Keep rendering at very small font sizes; a 2px guard caused 1–2pt
		// text to disappear even though the element itself was valid.
		if (string.IsNullOrEmpty(_model.Text) || base.ActualWidth < 0.25 || base.ActualHeight < 0.25)
		{
			return;
		}
		FontStyle style = (_model.Italic ? FontStyles.Italic : FontStyles.Normal);
		int num = Math.Clamp(_model.FontWeightValue, 100, 900);
		if (_model.Bold && num < 700)
		{
			num = 700;
		}
		FontWeight weight = FontWeight.FromOpenTypeWeight(num);
		Typeface typeface = new Typeface(_fontFamily, style, weight, FontStretches.Normal);
		// Keep sub-pixel text visible at editor zoom levels; the model value is
		// unchanged, only the preview rasterization gets a tiny display floor.
		double num2 = Math.Max(3.0, _model.FontSizePt * 96.0 / 72.0);
		double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		double maxTextWidth = Math.Max(1.0, base.ActualWidth);
		double num3 = Math.Max(1.0, base.ActualHeight);
		TextAlignment result;
		TextAlignment textAlignment = (Enum.TryParse<TextAlignment>(_model.TextAlignment, out result) ? result : TextAlignment.Center);
		VerticalAlignment result2;
		VerticalAlignment vertical = ((!Enum.TryParse<VerticalAlignment>(_model.VerticalAlignment, out result2)) ? VerticalAlignment.Center : result2);
		double num4 = Math.Clamp(_model.CharacterSpacing, -100.0, 300.0) * 96.0 / 72.0;
		double num5 = Math.Clamp(_model.LineSpacingPt, -100.0, 300.0) * 96.0 / 72.0;
		Geometry geometry;
		if (Math.Abs(num4) < 0.01)
		{
			FormattedText formattedText = CreateFormattedText(_model.Text, typeface, num2, pixelsPerDip);
			formattedText.MaxTextWidth = maxTextWidth;
			formattedText.MaxTextHeight = num3;
			formattedText.TextAlignment = textAlignment;
			formattedText.Trimming = TextTrimming.None;
			if (Math.Abs(num5) >= 0.01)
			{
				double num6 = MeasureDefaultLineHeight(typeface, num2, pixelsPerDip);
				formattedText.LineHeight = Math.Max(num2 * 0.35, num6 + num5);
			}
			else if (_model.LineHeight > 0.0)
			{
				formattedText.LineHeight = _model.LineHeight * 96.0 / 72.0;
			}
			if (_model.Underline)
			{
				formattedText.SetTextDecorations(TextDecorations.Underline);
			}
			double y = ResolveVerticalOrigin(vertical, formattedText.Height, num3);
			geometry = formattedText.BuildGeometry(new Point(0.0, y));
		}
		else
		{
			geometry = BuildSpacedTextGeometry(typeface, num2, pixelsPerDip, maxTextWidth, num3, num4, num5, textAlignment, vertical);
		}
		geometry.Freeze();
		double num7 = Math.Clamp(_model.TextOutlineThicknessPt, 0.0, 24.0) * 96.0 / 72.0;
		Pen pen = null;
		if (num7 > 0.01)
		{
			double thickness = ((_model.TextOutlinePosition == "中央") ? num7 : (num7 * 2.0));
			pen = new Pen(_outline, thickness)
			{
				LineJoin = PenLineJoin.Round,
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round
			};
			pen.Freeze();
		}
		double num8 = Math.Clamp(_model.TextExtrusionDepthPt, 0.0, 48.0) * 96.0 / 72.0;
		if (num8 > 0.01)
		{
			double num9 = _model.TextExtrusionAngle * Math.PI / 180.0;
			int num10 = Math.Clamp((int)Math.Ceiling(num8), 1, 64);
			Pen pen2 = null;
			if (_model.TextExtrudeOutline && num7 > 0.01)
			{
				pen2 = new Pen(_extrusion, num7 * 2.0)
				{
					LineJoin = PenLineJoin.Round
				};
				pen2.Freeze();
			}
			for (int num11 = num10; num11 >= 1; num11--)
			{
				double num12 = num8 * (double)num11 / (double)num10;
				dc.PushTransform(new TranslateTransform(Math.Cos(num9) * num12, Math.Sin(num9) * num12));
				dc.DrawGeometry(_extrusion, pen2, geometry);
				dc.Pop();
			}
		}
		if (_model.TextOutlinePosition == "内側" && pen != null)
		{
			dc.PushClip(geometry);
			dc.DrawGeometry(null, pen, geometry);
			dc.Pop();
		}
		else if (pen != null)
		{
			dc.DrawGeometry(null, pen, geometry);
		}
		dc.DrawGeometry(_face, null, geometry);
	}

	private FormattedText CreateFormattedText(string value, Typeface typeface, double fontSizeDip, double pixelsPerDip)
	{
		return new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSizeDip, _face, pixelsPerDip);
	}

	private double MeasureDefaultLineHeight(Typeface typeface, double fontSizeDip, double pixelsPerDip)
	{
		return CreateFormattedText("Agあ", typeface, fontSizeDip, pixelsPerDip).Height;
	}

	private static double ResolveVerticalOrigin(VerticalAlignment vertical, double textHeight, double availableHeight)
	{
		return vertical switch
		{
			VerticalAlignment.Bottom => 2.0 + Math.Max(0.0, availableHeight - textHeight), 
			VerticalAlignment.Center => 2.0 + Math.Max(0.0, (availableHeight - textHeight) / 2.0), 
			_ => 2.0, 
		};
	}

	private Geometry BuildSpacedTextGeometry(Typeface typeface, double fontSizeDip, double pixelsPerDip, double maxTextWidth, double maxTextHeight, double characterSpacingDip, double lineSpacingDip, TextAlignment alignment, VerticalAlignment vertical)
	{
		double num = MeasureDefaultLineHeight(typeface, fontSizeDip, pixelsPerDip);
		double num2 = Math.Max(fontSizeDip * 0.35, num + lineSpacingDip);
		if (Math.Abs(lineSpacingDip) < 0.01 && _model.LineHeight > 0.0)
		{
			num2 = Math.Max(fontSizeDip * 0.35, _model.LineHeight * 96.0 / 72.0);
		}
		List<SpacedLine> list = LayoutLines(typeface, fontSizeDip, pixelsPerDip, maxTextWidth, characterSpacingDip);
		int num3 = Math.Max(1, (int)Math.Floor(Math.Max(0.0, maxTextHeight - num) / num2) + 1);
		if (list.Count > num3)
		{
			list.RemoveRange(num3, list.Count - num3);
		}
		double textHeight = ((list.Count == 0) ? num : (num + (double)Math.Max(0, list.Count - 1) * num2));
		double num4 = ResolveVerticalOrigin(vertical, textHeight, maxTextHeight);
		GeometryGroup geometryGroup = new GeometryGroup
		{
			FillRule = FillRule.Nonzero
		};
		for (int i = 0; i < list.Count; i++)
		{
			SpacedLine spacedLine = list[i];
			double num5 = alignment switch
			{
				TextAlignment.Right => Math.Max(0.0, maxTextWidth - spacedLine.Width), 
				TextAlignment.Center => Math.Max(0.0, (maxTextWidth - spacedLine.Width) / 2.0), 
				_ => 0.0, 
			};
			double y = num4 + (double)i * num2;
			foreach (SpacedGlyph glyph in spacedLine.Glyphs)
			{
				Geometry geometry = glyph.Text.BuildGeometry(new Point(num5, y));
				if (!geometry.IsEmpty())
				{
					geometryGroup.Children.Add(geometry);
				}
				num5 += glyph.Width + characterSpacingDip;
			}
		}
		return geometryGroup;
	}

	private List<SpacedLine> LayoutLines(Typeface typeface, double fontSizeDip, double pixelsPerDip, double maxTextWidth, double characterSpacingDip)
	{
		List<SpacedLine> list = new List<SpacedLine>();
		string[] array = _model.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
		foreach (string value in array)
		{
			List<SpacedGlyph> list2 = MeasureTextElements(value, typeface, fontSizeDip, pixelsPerDip);
			if (list2.Count == 0)
			{
				list.Add(new SpacedLine());
				continue;
			}
			SpacedLine spacedLine = new SpacedLine();
			foreach (SpacedGlyph item in list2)
			{
				double num = ((spacedLine.Glyphs.Count == 0) ? item.Width : (spacedLine.Width + characterSpacingDip + item.Width));
				if (spacedLine.Glyphs.Count > 0 && num > maxTextWidth)
				{
					list.Add(spacedLine);
					spacedLine = new SpacedLine();
					if (item.IsWhitespace)
					{
						continue;
					}
					num = item.Width;
				}
				spacedLine.Glyphs.Add(item);
				spacedLine.Width = Math.Max(0.0, num);
			}
			list.Add(spacedLine);
		}
		return list;
	}

	private List<SpacedGlyph> MeasureTextElements(string value, Typeface typeface, double fontSizeDip, double pixelsPerDip)
	{
		List<SpacedGlyph> list = new List<SpacedGlyph>();
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(value);
		while (textElementEnumerator.MoveNext())
		{
			string textElement = textElementEnumerator.GetTextElement();
			FormattedText formattedText = CreateFormattedText(textElement, typeface, fontSizeDip, pixelsPerDip);
			if (_model.Underline)
			{
				formattedText.SetTextDecorations(TextDecorations.Underline);
			}
			list.Add(new SpacedGlyph(formattedText, Math.Max(0.0, formattedText.WidthIncludingTrailingWhitespace), string.IsNullOrWhiteSpace(textElement)));
		}
		return list;
	}
}
