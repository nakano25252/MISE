using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public static class BackgroundRemovalService
{
	public static byte[] RemoveCornerBackground(byte[] source, double tolerancePercent, int previewMaxWidth = 0)
	{
		return Apply(source, new ImageCutoutSettings
		{
			Mode = "自動",
			TolerancePercent = tolerancePercent,
			FeatherPixels = 2.0
		}, previewMaxWidth);
	}

	public static byte[] Apply(byte[] source, ImageCutoutSettings settings, int previewMaxWidth = 0)
	{
		using MemoryStream streamSource = new MemoryStream(source, writable: false);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		if (previewMaxWidth > 0)
		{
			bitmapImage.DecodePixelWidth = previewMaxWidth;
		}
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapImage, PixelFormats.Bgra32, null, 0.0);
		int pixelWidth = formatConvertedBitmap.PixelWidth;
		int pixelHeight = formatConvertedBitmap.PixelHeight;
		int num = pixelWidth * 4;
		byte[] array = new byte[num * pixelHeight];
		formatConvertedBitmap.CopyPixels(array, num, 0);
		byte[] array2 = new byte[pixelWidth * pixelHeight];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = array[i * 4 + 3];
		}
		string mode = settings.Mode;
		if ((mode == "自動" || mode == "色をクリック") ? true : false)
		{
			Color color;
			(double, double, double) tuple = ((!(settings.Mode == "色をクリック") || !TryColor(settings.SampleColor, out color)) ? CornerAverage(array, pixelWidth, pixelHeight, num) : ((int)color.B, (int)color.G, (int)color.R));
			double num2 = Math.Clamp(settings.TolerancePercent, 0.0, 100.0) / 100.0 * 441.7;
			double num3 = Math.Max(1.0, num2 * 0.12);
			for (int j = 0; j < array2.Length; j++)
			{
				int num4 = j * 4;
				double num5 = (double)(int)array[num4] - tuple.Item1;
				double num6 = (double)(int)array[num4 + 1] - tuple.Item2;
				double num7 = (double)(int)array[num4 + 2] - tuple.Item3;
				double num8 = Math.Sqrt(num5 * num5 + num6 * num6 + num7 * num7);
				if (num8 <= num2)
				{
					array2[j] = 0;
				}
				else if (num8 < num2 + num3)
				{
					double num9 = (num8 - num2) / num3;
					array2[j] = (byte)Math.Clamp(Math.Round((double)(int)array2[j] * num9), 0.0, 255.0);
				}
			}
		}
		if (settings.Polygon.Count >= 3)
		{
			ApplyPolygon(array2, pixelWidth, pixelHeight, settings.Polygon);
		}
		ApplyStrokes(array2, pixelWidth, pixelHeight, settings.Strokes);
		int num10 = (int)Math.Round(Math.Clamp(settings.EdgeExpandPixels, -20.0, 20.0) * ((previewMaxWidth > 0) ? ((double)pixelWidth / Math.Max(1.0, previewMaxWidth)) : 1.0));
		if (num10 != 0)
		{
			array2 = Morph(array2, pixelWidth, pixelHeight, Math.Abs(num10), num10 > 0);
		}
		int num11 = (int)Math.Round(Math.Clamp(settings.SmoothPixels + settings.FeatherPixels, 0.0, 20.0));
		if (num11 > 0)
		{
			array2 = Blur(array2, pixelWidth, pixelHeight, num11);
		}
		for (int k = 0; k < array2.Length; k++)
		{
			array[k * 4 + 3] = array2[k];
		}
		BitmapSource source2 = BitmapSource.Create(pixelWidth, pixelHeight, formatConvertedBitmap.DpiX, formatConvertedBitmap.DpiY, PixelFormats.Bgra32, null, array, num);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source2));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}

	private static bool TryColor(string value, out Color color)
	{
		try
		{
			color = (Color)ColorConverter.ConvertFromString(value);
			return true;
		}
		catch
		{
			color = Colors.White;
			return false;
		}
	}

	private static void ApplyPolygon(byte[] alpha, int width, int height, IReadOnlyList<ShapePointModel> polygon)
	{
		for (int i = 0; i < height; i++)
		{
			double num = ((double)i + 0.5) / (double)height * 100.0;
			List<double> list = new List<double>();
			for (int j = 0; j < polygon.Count; j++)
			{
				ShapePointModel shapePointModel = polygon[j];
				ShapePointModel shapePointModel2 = polygon[(j + 1) % polygon.Count];
				if ((shapePointModel.Y <= num && shapePointModel2.Y > num) || (shapePointModel2.Y <= num && shapePointModel.Y > num))
				{
					list.Add(shapePointModel.X + (num - shapePointModel.Y) * (shapePointModel2.X - shapePointModel.X) / (shapePointModel2.Y - shapePointModel.Y));
				}
			}
			list.Sort();
			bool flag = false;
			int k = 0;
			for (int l = 0; l < width; l++)
			{
				for (double num2 = ((double)l + 0.5) / (double)width * 100.0; k < list.Count && num2 >= list[k]; k++)
				{
					flag = !flag;
				}
				if (!flag)
				{
					alpha[i * width + l] = 0;
				}
			}
		}
	}

	private static void ApplyStrokes(byte[] alpha, int width, int height, IEnumerable<ImageMaskStroke> strokes)
	{
		foreach (ImageMaskStroke stroke in strokes)
		{
			double num = stroke.XPercent / 100.0 * (double)width;
			double num2 = stroke.YPercent / 100.0 * (double)height;
			double num3 = Math.Max(1.0, stroke.RadiusPercent / 100.0 * (double)Math.Min(width, height));
			int num4 = Math.Max(0, (int)Math.Floor(num - num3));
			int num5 = Math.Min(width - 1, (int)Math.Ceiling(num + num3));
			int num6 = Math.Max(0, (int)Math.Floor(num2 - num3));
			int num7 = Math.Min(height - 1, (int)Math.Ceiling(num2 + num3));
			for (int i = num6; i <= num7; i++)
			{
				for (int j = num4; j <= num5; j++)
				{
					if (((double)j - num) * ((double)j - num) + ((double)i - num2) * ((double)i - num2) <= num3 * num3)
					{
						alpha[i * width + j] = (byte)(stroke.Keep ? byte.MaxValue : 0);
					}
				}
			}
		}
	}

	private static byte[] Morph(byte[] source, int width, int height, int radius, bool expand)
	{
		radius = Math.Clamp(radius, 1, 20);
		byte[] array = new byte[source.Length];
		byte[] array2 = new byte[source.Length];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				byte b = (byte)((!expand) ? byte.MaxValue : 0);
				for (int k = -radius; k <= radius; k++)
				{
					byte val = source[i * width + Math.Clamp(j + k, 0, width - 1)];
					b = (expand ? Math.Max(b, val) : Math.Min(b, val));
				}
				array[i * width + j] = b;
			}
		}
		for (int l = 0; l < height; l++)
		{
			for (int m = 0; m < width; m++)
			{
				byte b2 = (byte)((!expand) ? byte.MaxValue : 0);
				for (int n = -radius; n <= radius; n++)
				{
					byte val2 = array[Math.Clamp(l + n, 0, height - 1) * width + m];
					b2 = (expand ? Math.Max(b2, val2) : Math.Min(b2, val2));
				}
				array2[l * width + m] = b2;
			}
		}
		return array2;
	}

	private static byte[] Blur(byte[] source, int width, int height, int radius)
	{
		radius = Math.Clamp(radius, 1, 20);
		byte[] array = new byte[source.Length];
		byte[] array2 = new byte[source.Length];
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num = 0;
				int num2 = 0;
				for (int k = -radius; k <= radius; k++)
				{
					num += source[i * width + Math.Clamp(j + k, 0, width - 1)];
					num2++;
				}
				array[i * width + j] = (byte)(num / num2);
			}
		}
		for (int l = 0; l < height; l++)
		{
			for (int m = 0; m < width; m++)
			{
				int num3 = 0;
				int num4 = 0;
				for (int n = -radius; n <= radius; n++)
				{
					num3 += array[Math.Clamp(l + n, 0, height - 1) * width + m];
					num4++;
				}
				array2[l * width + m] = (byte)(num3 / num4);
			}
		}
		return array2;
	}

	private static (double, double, double) CornerAverage(byte[] pixels, int width, int height, int stride)
	{
		long num = 0L;
		long num2 = 0L;
		long num3 = 0L;
		long num4 = 0L;
		int num5 = Math.Clamp(Math.Min(width, height) / 50, 2, 12);
		(int, int)[] array = new(int, int)[4]
		{
			(0, 0),
			(Math.Max(0, width - num5), 0),
			(0, Math.Max(0, height - num5)),
			(Math.Max(0, width - num5), Math.Max(0, height - num5))
		};
		for (int i = 0; i < array.Length; i++)
		{
			(int, int) tuple = array[i];
			for (int j = tuple.Item2; j < Math.Min(height, tuple.Item2 + num5); j++)
			{
				var (k, _) = tuple;
				for (; k < Math.Min(width, tuple.Item1 + num5); k++)
				{
					int num6 = j * stride + k * 4;
					num += pixels[num6];
					num2 += pixels[num6 + 1];
					num3 += pixels[num6 + 2];
					num4++;
				}
			}
		}
		return ((double)num / (double)Math.Max(1L, num4), (double)num2 / (double)Math.Max(1L, num4), (double)num3 / (double)Math.Max(1L, num4));
	}
}
