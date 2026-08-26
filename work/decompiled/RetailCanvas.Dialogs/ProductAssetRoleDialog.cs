using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ProductAssetRoleDialog : Window
{
	private sealed record AssetItem(string Path, string Role, BitmapSource? Thumbnail)
	{
		public string Display => "[" + Role + "] " + System.IO.Path.GetFileName(Path);

		public override string ToString()
		{
			return Display;
		}
	}

	private static readonly string[] Extensions = new string[7] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp" };

	private readonly ProductModel _product;

	private readonly ListBox _files = new ListBox();

	private readonly Image _preview = new Image
	{
		Stretch = Stretch.Uniform
	};

	private readonly ComboBox _role = new ComboBox
	{
		ItemsSource = new string[7] { "メイン画像", "色違い", "パッケージ", "背面", "装着イメージ", "機能説明", "その他" },
		SelectedIndex = 0
	};

	private readonly TextBlock _status = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap
	};

	private readonly Dictionary<string, string> _roles;

	public string ResultJson { get; private set; } = string.Empty;

	public string MainImagePath { get; private set; } = string.Empty;

	public ProductAssetRoleDialog(ProductModel product)
	{
		_product = product;
		_roles = Parse(product.AssetRoleData);
		base.Title = "素材画像の役割 － MISE";
		base.Width = 900.0;
		base.Height = 620.0;
		base.MinWidth = 680.0;
		base.MinHeight = 440.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 900.0, 620.0, 680.0, 440.0);
		base.Content = Build();
		LoadFiles();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "保存",
			MinWidth = 100.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsDefault = true
		};
		button2.Click += delegate
		{
			ResultJson = JsonSerializer.Serialize(_roles);
			MainImagePath = _roles.FirstOrDefault<KeyValuePair<string, string>>((KeyValuePair<string, string> pair) => pair.Value == "メイン画像").Key ?? string.Empty;
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "素材画像に役割を割り当て",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = _product.AssetFolderPath,
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(320.0)
				},
				new ColumnDefinition()
			}
		};
		DataTemplate dataTemplate = new DataTemplate();
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(StackPanel));
		frameworkElementFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Image));
		frameworkElementFactory2.SetBinding(Image.SourceProperty, new Binding("Thumbnail"));
		frameworkElementFactory2.SetValue(FrameworkElement.WidthProperty, 62.0);
		frameworkElementFactory2.SetValue(FrameworkElement.HeightProperty, 46.0);
		frameworkElementFactory2.SetValue(Image.StretchProperty, Stretch.Uniform);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(2.0, 2.0, 8.0, 2.0));
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory3.SetBinding(TextBlock.TextProperty, new Binding("Display"));
		frameworkElementFactory3.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
		frameworkElementFactory3.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		frameworkElementFactory.AppendChild(frameworkElementFactory3);
		dataTemplate.VisualTree = frameworkElementFactory;
		_files.ItemTemplate = dataTemplate;
		_files.SelectionChanged += delegate
		{
			Preview();
		};
		grid.Children.Add(_files);
		StackPanel stackPanel3 = new StackPanel
		{
			Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
		};
		stackPanel3.Children.Add(new Border
		{
			Height = 360.0,
			Background = Brushes.Gainsboro,
			Child = _preview
		});
		stackPanel3.Children.Add(_status);
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		stackPanel4.Children.Add(_role);
		Button button3 = new Button
		{
			Content = "この役割を設定",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button3.Click += delegate
		{
			SetRole();
		};
		stackPanel4.Children.Add(button3);
		stackPanel3.Children.Add(stackPanel4);
		Grid.SetColumn(stackPanel3, 1);
		grid.Children.Add(stackPanel3);
		obj.Children.Add(grid);
		return obj;
	}

	private void LoadFiles()
	{
		if (!Directory.Exists(_product.AssetFolderPath))
		{
			_status.Text = "素材フォルダが見つかりません。";
			return;
		}
		_files.ItemsSource = (from path in Directory.EnumerateFiles(_product.AssetFolderPath, "*", SearchOption.AllDirectories)
			where Extensions.Contains<string>(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
			select new AssetItem(path, _roles.GetValueOrDefault(path, "未設定"), Thumbnail(path))).ToList();
	}

	private void SetRole()
	{
		object selectedItem = _files.SelectedItem;
		AssetItem item = selectedItem as AssetItem;
		if ((object)item == null)
		{
			return;
		}
		string text = _role.SelectedItem?.ToString() ?? "その他";
		if (text == "メイン画像")
		{
			foreach (string item2 in (from pair in _roles
				where pair.Value == "メイン画像"
				select pair.Key).ToList())
			{
				_roles.Remove(item2);
			}
		}
		_roles[item.Path] = text;
		LoadFiles();
		_files.SelectedItem = (_files.ItemsSource as IEnumerable<AssetItem>)?.FirstOrDefault((AssetItem x) => x.Path == item.Path);
	}

	private void Preview()
	{
		if (!(_files.SelectedItem is AssetItem assetItem))
		{
			return;
		}
		FileInfo fileInfo = new FileInfo(assetItem.Path);
		if (fileInfo.Length >= 31457280 && MessageBox.Show($"{(double)fileInfo.Length / 1024.0 / 1024.0:0.0}MBの素材です。プレビューしますか？", "大容量素材", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			using FileStream streamSource = File.OpenRead(assetItem.Path);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			_preview.Source = bitmapImage;
			_status.Text = $"{assetItem.Display}\n{bitmapImage.PixelWidth} × {bitmapImage.PixelHeight}px";
		}
		catch (Exception ex)
		{
			_status.Text = "プレビューできません: " + ex.Message;
		}
	}

	private static Dictionary<string, string> Parse(string value)
	{
		try
		{
			return JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
		catch
		{
			return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		}
	}

	private static BitmapSource? Thumbnail(string path)
	{
		try
		{
			if (new FileInfo(path).Length >= 31457280 || IsOnlineOnly(path))
			{
				return null;
			}
			using FileStream streamSource = File.OpenRead(path);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = 100;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsOnlineOnly(string path)
	{
		try
		{
			return (File.GetAttributes(path) & (FileAttributes)4460544) != 0;
		}
		catch
		{
			return false;
		}
	}
}
