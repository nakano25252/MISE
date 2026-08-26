using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class TexturePickerDialog : Window
{
	private sealed record TextureEntry(string Path, string Name)
	{
		public override string ToString()
		{
			return Name;
		}
	}

	private readonly ListBox _files = new ListBox
	{
		MinWidth = 210.0
	};

	private readonly Border _preview = new Border
	{
		Background = Brushes.White
	};

	private readonly Slider _opacity = new Slider
	{
		Minimum = 0.0,
		Maximum = 100.0,
		TickFrequency = 1.0,
		Width = 190.0
	};

	private readonly Slider _scale = new Slider
	{
		Minimum = 25.0,
		Maximum = 400.0,
		TickFrequency = 5.0,
		Width = 190.0
	};

	private readonly TextBlock _details = new TextBlock
	{
		Foreground = Brushes.SlateGray,
		TextWrapping = TextWrapping.Wrap
	};

	private readonly List<TextureEntry> _entries = new List<TextureEntry>();

	private byte[]? _previewBytes;

	public string TextureName { get; private set; } = string.Empty;

	public string? TextureDataBase64 { get; private set; }

	public double TextureOpacity => _opacity.Value / 100.0;

	public double TextureScale => _scale.Value / 100.0;

	public TexturePickerDialog(string currentName, string? currentData, double opacity, double scale)
	{
		TexturePickerDialog texturePickerDialog = this;
		base.Title = "テクスチャ － MISE";
		base.Width = 760.0;
		base.Height = 590.0;
		base.MinWidth = 600.0;
		base.MinHeight = 430.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 760.0, 590.0, 600.0, 430.0);
		_opacity.Value = Math.Clamp(opacity * 100.0, 0.0, 100.0);
		_scale.Value = Math.Clamp(scale * 100.0, 25.0, 400.0);
		LoadEntries();
		base.Content = Build();
		_files.ItemsSource = _entries;
		_files.SelectionChanged += delegate
		{
			texturePickerDialog.PreviewSelected();
		};
		_opacity.ValueChanged += delegate
		{
			texturePickerDialog.UpdateLivePreview();
		};
		_scale.ValueChanged += delegate
		{
			texturePickerDialog.UpdateLivePreview();
		};
		TextureEntry textureEntry = _entries.FirstOrDefault((TextureEntry x) => x.Name == currentName);
		if (textureEntry != null)
		{
			_files.SelectedItem = textureEntry;
		}
		else if (_entries.Count > 0)
		{
			_files.SelectedIndex = 0;
		}
		if (_entries.Count == 0 && !string.IsNullOrWhiteSpace(currentData))
		{
			try
			{
				ShowPreview(Convert.FromBase64String(currentData), currentName);
			}
			catch
			{
			}
		}
	}

	private void LoadEntries()
	{
		TextureCatalogService.EnsureInstalled();
		HashSet<string> extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff" };
		if (!Directory.Exists(AppPaths.Textures))
		{
			return;
		}
		foreach (string item in from x in Directory.EnumerateFiles(AppPaths.Textures, "*.*", SearchOption.AllDirectories)
			where extensions.Contains(Path.GetExtension(x))
			select x)
		{
			_entries.Add(new TextureEntry(item, Path.GetFileNameWithoutExtension(item)));
		}
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
			Content = "なし",
			MinWidth = 80.0
		};
		button.Click += delegate
		{
			TextureName = string.Empty;
			TextureDataBase64 = null;
			base.DialogResult = true;
		};
		Button button2 = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button3 = new Button
		{
			Content = "適用",
			MinWidth = 100.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button3.Click += Apply;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "テクスチャ",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "図形や台紙へ質感を加えます。テクスチャはプロジェクト内へ埋め込みます。",
			Foreground = Brushes.SlateGray
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(230.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(12.0)
				},
				new ColumnDefinition()
			},
			Children = { (UIElement)_files }
		};
		StackPanel element = new StackPanel
		{
			Children = 
			{
				(UIElement)new Border
				{
					Height = 285.0,
					Background = new SolidColorBrush(Color.FromRgb(230, 232, 236)),
					BorderBrush = Brushes.LightGray,
					BorderThickness = new Thickness(1.0),
					Child = _preview
				},
				(UIElement)_details,
				Row("濃さ", _opacity, "%"),
				Row("大きさ", _scale, "%")
			}
		};
		Grid.SetColumn(element, 2);
		grid.Children.Add(element);
		obj.Children.Add(grid);
		return obj;
	}

	private static UIElement Row(string label, Slider slider, string suffix)
	{
		StackPanel obj = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0),
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = label,
					Width = 60.0,
					VerticalAlignment = VerticalAlignment.Center
				},
				(UIElement)slider
			}
		};
		TextBlock value = new TextBlock
		{
			Width = 55.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		value.Text = $"{slider.Value:0}{suffix}";
		slider.ValueChanged += delegate
		{
			value.Text = $"{slider.Value:0}{suffix}";
		};
		obj.Children.Add(value);
		return obj;
	}

	private void PreviewSelected()
	{
		if (!(_files.SelectedItem is TextureEntry textureEntry))
		{
			return;
		}
		try
		{
			ShowPreview(File.ReadAllBytes(textureEntry.Path), textureEntry.Name);
		}
		catch (Exception ex)
		{
			_details.Text = "プレビューできません: " + ex.Message;
			_preview.Background = Brushes.White;
		}
	}

	private void ShowPreview(byte[] data, string name)
	{
		using MemoryStream streamSource = new MemoryStream(data, writable: false);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		_previewBytes = data;
		UpdateLivePreview();
		_details.Text = $"{name}  /  {bitmapImage.PixelWidth}×{bitmapImage.PixelHeight}px  /  {(double)data.Length / 1024.0 / 1024.0:0.0}MB";
	}

	private void UpdateLivePreview()
	{
		if (_previewBytes == null)
		{
			_preview.Background = Brushes.White;
		}
		else
		{
			_preview.Background = TextureCatalogService.Blend(Brushes.White, Convert.ToBase64String(_previewBytes), _opacity.Value / 100.0, _scale.Value / 100.0);
		}
	}

	private void Apply(object? sender, RoutedEventArgs e)
	{
		if (!(_files.SelectedItem is TextureEntry textureEntry))
		{
			MessageBox.Show("テクスチャを選択してください。", "テクスチャ");
			return;
		}
		try
		{
			byte[] inArray = File.ReadAllBytes(textureEntry.Path);
			TextureName = textureEntry.Name;
			TextureDataBase64 = Convert.ToBase64String(inArray);
			base.DialogResult = true;
		}
		catch (Exception ex)
		{
			MessageBox.Show("読み込めませんでした。\n" + ex.Message, "テクスチャ", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}
}
