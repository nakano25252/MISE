using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public static class QrOutputVerificationService
{
	private readonly record struct Sample(bool Expected, byte R, byte G, byte B);

	private const double DipPerMm = 3.7795275590551185;

	public static IReadOnlyList<QrOutputCheckResult> Verify(BitmapSource renderedPage, PageModel page)
	{
		List<CanvasElementModel> list = (from x in page.Elements
			where x.IsVisible && x.Kind == ElementKind.QrCode
			orderby x.ZIndex
			select x).ToList();
		if (list.Count == 0)
		{
			return Array.Empty<QrOutputCheckResult>();
		}
		BitmapSource source = ((renderedPage.Format == PixelFormats.Bgra32) ? renderedPage : new FormatConvertedBitmap(renderedPage, PixelFormats.Bgra32, null, 0.0));
		int stride = source.PixelWidth * 4;
		byte[] pixels = new byte[stride * source.PixelHeight];
		source.CopyPixels(pixels, stride, 0);
		return list.Select((CanvasElementModel qr) => VerifyOne(qr, page, source, pixels, stride)).ToList();
	}

	private static QrOutputCheckResult VerifyOne(CanvasElementModel qr, PageModel page, BitmapSource bitmap, byte[] pixels, int stride)
	{
		try
		{
			IReadOnlyList<BitArray> readOnlyList = QrService.CreateMatrix(qr.QrContent, qr.QrErrorCorrection);
			int count = readOnlyList.Count;
			int num = ((count != 0) ? readOnlyList[0].Length : 0);
			if (count == 0 || num == 0)
			{
				return Failed(qr, "QRパターンを生成できませんでした。");
			}
			double num2 = Math.Min(qr.WidthMm, qr.HeightMm);
			double num3 = (qr.WidthMm - num2) / 2.0;
			double num4 = (qr.HeightMm - num2) / 2.0;
			double num5 = num2 / Math.Max(page.WidthMm, 0.1) * (double)bitmap.PixelWidth / (double)num;
			List<Sample> list = new List<Sample>(count * num);
			for (int i = 0; i < count; i++)
			{
				BitArray bitArray = readOnlyList[i];
				for (int j = 0; j < num; j++)
				{
					double localXmm = num3 + ((double)j + 0.5) * num2 / (double)num;
					double localYmm = num4 + ((double)i + 0.5) * num2 / (double)count;
					Point point = TransformToPage(qr, localXmm, localYmm);
					int num6 = (int)Math.Round(point.X / (page.WidthMm * 3.7795275590551185) * (double)(bitmap.PixelWidth - 1));
					int num7 = (int)Math.Round(point.Y / (page.HeightMm * 3.7795275590551185) * (double)(bitmap.PixelHeight - 1));
					if (num6 >= 0 && num7 >= 0 && num6 < bitmap.PixelWidth && num7 < bitmap.PixelHeight)
					{
						int num8 = num7 * stride + num6 * 4;
						list.Add(new Sample(bitArray[j], pixels[num8 + 2], pixels[num8 + 1], pixels[num8]));
					}
				}
			}
			int num9 = count * num;
			if ((double)list.Count < (double)num9 * 0.94)
			{
				return Failed(qr, "QRコードの一部が台紙外にあり、最終画像から確認できません。", num5);
			}
			(double R, double G, double B) dark = Mean(list.Where((Sample x) => x.Expected));
			(double R, double G, double B) light = Mean(list.Where((Sample x) => !x.Expected));
			double num10 = Math.Sqrt(DistanceSquared(dark, light));
			double num11 = (double)list.Count((Sample sample) => DistanceSquared((R: (int)sample.R, G: (int)sample.G, B: (int)sample.B), dark) <= DistanceSquared((R: (int)sample.R, G: (int)sample.G, B: (int)sample.B), light) == sample.Expected) * 100.0 / (double)list.Count;
			bool flag = num5 >= 2.0 && num10 >= 38.0 && num11 >= 91.0;
			string detail = (flag ? $"出力画像の全モジュールを照合済み（{num11:0.0}%一致、{num5:0.0}px/セル）" : ((num5 < 2.0) ? $"QRが小さすぎます（{num5:0.0}px/セル）。サイズまたは書き出しDPIを上げてください。" : ((num10 < 38.0) ? $"QRの明暗差が不足しています（コントラスト {num10:0}）。色や背景を見直してください。" : $"QRパターンが最終画像で崩れています（一致率 {num11:0.0}%）。重なり・変形・ぼかしを確認してください。")));
			return new QrOutputCheckResult(qr.Id, qr.Name, qr.QrContent, flag, num11, num10, num5, detail);
		}
		catch (Exception ex)
		{
			return Failed(qr, "QR出力検査に失敗しました: " + ex.Message);
		}
	}

	private static Point TransformToPage(CanvasElementModel element, double localXmm, double localYmm)
	{
		Point point = new Point(element.WidthMm * 3.7795275590551185 / 2.0, element.HeightMm * 3.7795275590551185 / 2.0);
		Point point2 = new Point(localXmm * 3.7795275590551185 - point.X, localYmm * 3.7795275590551185 - point.Y);
		point2 = new SkewTransform(Math.Clamp(element.SkewX, -80.0, 80.0), Math.Clamp(element.SkewY, -80.0, 80.0)).Transform(point2);
		point2 = new RotateTransform(element.Rotation).Transform(point2);
		return new Point(point2.X + point.X + element.Xmm * 3.7795275590551185, point2.Y + point.Y + element.Ymm * 3.7795275590551185);
	}

	private static (double R, double G, double B) Mean(IEnumerable<Sample> source)
	{
		List<Sample> list = source.ToList();
		if (list.Count == 0)
		{
			return (R: 0.0, G: 0.0, B: 0.0);
		}
		return (R: ((IEnumerable<Sample>)list).Average((Func<Sample, double>)((Sample x) => (int)x.R)), G: ((IEnumerable<Sample>)list).Average((Func<Sample, double>)((Sample x) => (int)x.G)), B: ((IEnumerable<Sample>)list).Average((Func<Sample, double>)((Sample x) => (int)x.B)));
	}

	private static double DistanceSquared((double R, double G, double B) left, (double R, double G, double B) right)
	{
		double num = left.R - right.R;
		double num2 = left.G - right.G;
		double num3 = left.B - right.B;
		return num * num + num2 * num2 + num3 * num3;
	}

	private static QrOutputCheckResult Failed(CanvasElementModel qr, string detail, double pixelsPerModule = 0.0)
	{
		return new QrOutputCheckResult(qr.Id, qr.Name, qr.QrContent, Passed: false, 0.0, 0.0, pixelsPerModule, detail);
	}
}
