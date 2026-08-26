using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RetailCanvas.Services;

public static class TextureCatalogService
{
	private static readonly Dictionary<string, string> BuiltIns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["water-surface.png"] = "01_水面.png",
		["washi-paper.png"] = "02_和紙.png",
		["brushed-metal-dark.png"] = "03_ダークメタル.png",
		["gold-sparkle.png"] = "04_ゴールド粒子.png"
	};

	public static void EnsureInstalled()
	{
		Directory.CreateDirectory(AppPaths.Textures);
		Assembly executingAssembly = Assembly.GetExecutingAssembly();
		string[] manifestResourceNames = executingAssembly.GetManifestResourceNames();
		foreach (string resource in manifestResourceNames)
		{
			KeyValuePair<string, string> keyValuePair = BuiltIns.FirstOrDefault<KeyValuePair<string, string>>((KeyValuePair<string, string> x) => resource.EndsWith(x.Key, StringComparison.OrdinalIgnoreCase));
			if (string.IsNullOrEmpty(keyValuePair.Key))
			{
				continue;
			}
			string text = Path.Combine(AppPaths.Textures, keyValuePair.Value);
			if (File.Exists(text) && new FileInfo(text).Length > 1000)
			{
				continue;
			}
			using Stream stream = executingAssembly.GetManifestResourceStream(resource);
			if (stream != null)
			{
				using FileStream destination = File.Create(text);
				stream.CopyTo(destination);
			}
		}
	}

	public static Brush Blend(Brush baseBrush, string? base64, double opacity, double scale)
	{
		if (string.IsNullOrWhiteSpace(base64) || opacity <= 0.001)
		{
			return baseBrush;
		}
		try
		{
			using MemoryStream streamSource = new MemoryStream(Convert.FromBase64String(base64), writable: false);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			ImageBrush brush = new ImageBrush(bitmapImage)
			{
				Stretch = Stretch.UniformToFill,
				AlignmentX = AlignmentX.Center,
				AlignmentY = AlignmentY.Center,
				Opacity = Math.Clamp(opacity, 0.0, 1.0),
				RelativeTransform = new ScaleTransform(Math.Clamp(scale, 0.25, 4.0), Math.Clamp(scale, 0.25, 4.0), 0.5, 0.5)
			};
			return new DrawingBrush(new DrawingGroup
			{
				Children = 
				{
					(Drawing)new GeometryDrawing(baseBrush, null, new RectangleGeometry(new Rect(0.0, 0.0, 1.0, 1.0))),
					(Drawing)new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0.0, 0.0, 1.0, 1.0)))
				}
			})
			{
				Stretch = Stretch.Fill
			};
		}
		catch
		{
			return baseBrush;
		}
	}
}
