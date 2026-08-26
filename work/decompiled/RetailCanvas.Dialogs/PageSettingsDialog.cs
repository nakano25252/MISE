using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class PageSettingsDialog : Window
{
	private readonly ComboBox _preset = new ComboBox
	{
		MinWidth = 190.0
	};

	private readonly ComboBox _orientation = new ComboBox
	{
		ItemsSource = new string[3] { "縦", "横", "自由" }
	};

	private readonly TextBox _width = new TextBox();

	private readonly TextBox _height = new TextBox();

	private readonly TextBox _background = new TextBox();

	private readonly CheckBox _transparent = new CheckBox
	{
		Content = "背景を透明にする"
	};

	private readonly TextBox _safe = new TextBox();

	private readonly TextBox _bleed = new TextBox();

	private readonly TextBox _printMargin = new TextBox();

	private readonly TextBox _grid = new TextBox();

	private readonly CheckBox _showSafe = new CheckBox
	{
		Content = "安全領域を表示"
	};

	private readonly CheckBox _showBleed = new CheckBox
	{
		Content = "塗り足しを表示"
	};

	private readonly CheckBox _showGrid = new CheckBox
	{
		Content = "グリッドを表示"
	};

	private readonly ComboBox _resize = new ComboBox();

	private readonly ComboBox _rotation = new ComboBox();

	private readonly Border _livePreview = new Border
	{
		BorderBrush = new SolidColorBrush(Color.FromRgb(170, 178, 190)),
		BorderThickness = new Thickness(1.0),
		HorizontalAlignment = HorizontalAlignment.Center,
		VerticalAlignment = VerticalAlignment.Center
	};

	private string _textureName;

	private string? _textureData;

	private double _textureOpacity;

	private double _textureScale;

	public string PaperName => _preset.SelectedItem?.ToString() ?? "自由サイズ";

	public double WidthMm { get; private set; }

	public double HeightMm { get; private set; }

	public new string Background { get; private set; } = "#FFFFFFFF";

	public double SafeMarginMm { get; private set; }

	public double BleedMm { get; private set; }

	public double PrintMarginMm { get; private set; }

	public double GridSizeMm { get; private set; }

	public bool ShowSafeArea => _showSafe.IsChecked == true;

	public bool ShowBleed => _showBleed.IsChecked == true;

	public bool ShowGrid => _showGrid.IsChecked == true;

	public new string ResizeMode => _resize.SelectedItem?.ToString() ?? "現在位置を維持";

	public string RotationMode => _rotation.SelectedItem?.ToString() ?? "回転しない";

	public string TextureName => _textureName;

	public string? TextureDataBase64 => _textureData;

	public double TextureOpacity => _textureOpacity;

	public double TextureScale => _textureScale;

	public PageSettingsDialog(PageModel page, string paperName, AppSettings settings)
	{
		base.Title = "台紙の設定 － MISE";
		base.Width = 680.0;
		base.Height = 760.0;
		base.MinWidth = 540.0;
		base.MinHeight = 560.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 680.0, 760.0, 540.0, 560.0);
		_preset.ItemsSource = PaperCatalog.All.Select((PaperSizeDefinition p) => p.Name).Append("自由サイズ").Distinct()
			.ToList();
		_preset.SelectedItem = paperName;
		if (_preset.SelectedIndex < 0)
		{
			_preset.SelectedItem = "自由サイズ";
		}
		_orientation.SelectedItem = ((page.WidthMm > page.HeightMm) ? "横" : "縦");
		_width.Text = N(page.WidthMm);
		_height.Text = N(page.HeightMm);
		_background.Text = page.Background;
		_transparent.IsChecked = page.Background.StartsWith("#00", StringComparison.OrdinalIgnoreCase);
		_safe.Text = N(page.SafeMarginMm);
		_bleed.Text = N(page.BleedMm);
		_printMargin.Text = N(page.PrintMarginMm);
		_grid.Text = N(settings.GridSizeMm);
		_showSafe.IsChecked = page.ShowSafeArea;
		_showBleed.IsChecked = page.ShowBleed;
		_showGrid.IsChecked = page.ShowGrid;
		_textureName = page.BackgroundTextureName;
		_textureData = page.BackgroundTextureDataBase64;
		_textureOpacity = page.BackgroundTextureOpacity;
		_textureScale = page.BackgroundTextureScale;
		_resize.ItemsSource = new string[4] { "現在位置を維持", "中央位置を維持", "比率を維持して拡大縮小", "台紙と一緒に回転" };
		_resize.SelectedIndex = 0;
		_rotation.ItemsSource = new string[7] { "回転しない", "台紙だけ90°回転", "オブジェクトだけ90°回転", "台紙とオブジェクトを90°回転", "台紙だけ180°回転", "オブジェクトだけ180°回転", "台紙とオブジェクトを180°回転" };
		_rotation.SelectedIndex = 0;
		_preset.SelectionChanged += delegate
		{
			ApplyPreset();
		};
		_orientation.SelectionChanged += delegate
		{
			ApplyOrientation();
		};
		_transparent.Checked += delegate
		{
			_background.IsEnabled = false;
			RefreshLivePreview();
		};
		_transparent.Unchecked += delegate
		{
			_background.IsEnabled = true;
			RefreshLivePreview();
		};
		_background.IsEnabled = _transparent.IsChecked != true;
		_background.TextChanged += delegate
		{
			RefreshLivePreview();
		};
		_width.TextChanged += delegate
		{
			RefreshLivePreview();
		};
		_height.TextChanged += delegate
		{
			RefreshLivePreview();
		};
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(20.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "台紙を編集",
			FontSize = 24.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "上部の［台紙］ボタンからのみ開きます。変更時のオブジェクト処理も選べます。",
			Margin = new Thickness(0.0, 3.0, 0.0, 14.0),
			Foreground = Brushes.SlateGray
		});
		StackPanel stackPanel2 = Section("サイズ・向き");
		Add(stackPanel2, "用紙プリセット", _preset);
		Add(stackPanel2, "向き", _orientation);
		AddPair(stackPanel2, "幅 (mm)", _width, "高さ (mm)", _height);
		Add(stackPanel2, "サイズ変更時のオブジェクト", _resize);
		stackPanel.Children.Add(stackPanel2);
		StackPanel stackPanel3 = Section("色・質感");
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		_background.MinWidth = 180.0;
		stackPanel4.Children.Add(_background);
		Button button = new Button
		{
			Content = "カラーパレット…",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			string text = ColorPickerDialog.Show(this, _background.Text);
			if (text != null)
			{
				_background.Text = text;
			}
		};
		stackPanel4.Children.Add(button);
		Add(stackPanel3, "背景色", stackPanel4);
		stackPanel3.Children.Add(_transparent);
		Button button2 = new Button
		{
			Content = "台紙テクスチャを選ぶ…",
			HorizontalAlignment = HorizontalAlignment.Left
		};
		button2.Click += delegate
		{
			TexturePickerDialog texturePickerDialog = new TexturePickerDialog(_textureName, _textureData, _textureOpacity, _textureScale)
			{
				Owner = this
			};
			if (texturePickerDialog.ShowDialog() == true)
			{
				_textureName = texturePickerDialog.TextureName;
				_textureData = texturePickerDialog.TextureDataBase64;
				_textureOpacity = texturePickerDialog.TextureOpacity;
				_textureScale = texturePickerDialog.TextureScale;
				RefreshLivePreview();
			}
		};
		Add(stackPanel3, "テクスチャ", button2);
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "適用プレビュー",
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 4.0, 0.0, 5.0)
		});
		stackPanel3.Children.Add(new Border
		{
			Height = 190.0,
			Background = new SolidColorBrush(Color.FromRgb(232, 235, 240)),
			Padding = new Thickness(10.0),
			Child = _livePreview
		});
		stackPanel.Children.Add(stackPanel3);
		StackPanel stackPanel5 = Section("印刷領域・グリッド");
		AddPair(stackPanel5, "安全領域 (mm)", _safe, "塗り足し (mm)", _bleed);
		AddPair(stackPanel5, "印刷余白 (mm)", _printMargin, "グリッド間隔 (mm)", _grid);
		WrapPanel wrapPanel = new WrapPanel
		{
			Children = 
			{
				(UIElement)_showSafe,
				(UIElement)_showBleed,
				(UIElement)_showGrid
			}
		};
		foreach (CheckBox item in wrapPanel.Children.OfType<CheckBox>())
		{
			item.Margin = new Thickness(0.0, 2.0, 18.0, 2.0);
		}
		stackPanel5.Children.Add(wrapPanel);
		stackPanel.Children.Add(stackPanel5);
		StackPanel stackPanel6 = Section("ページ全体の回転");
		Add(stackPanel6, "対象と角度", _rotation);
		stackPanel6.Children.Add(new TextBlock
		{
			Text = "オブジェクトだけ／台紙だけ／両方を選択できます。",
			Foreground = Brushes.SlateGray
		});
		stackPanel.Children.Add(stackPanel6);
		StackPanel stackPanel7 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		Button button3 = new Button
		{
			Content = "キャンセル",
			MinWidth = 92.0
		};
		button3.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button4 = new Button
		{
			Content = "適用",
			MinWidth = 100.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsDefault = true
		};
		button4.Click += delegate
		{
			Apply();
		};
		stackPanel7.Children.Add(button3);
		stackPanel7.Children.Add(button4);
		stackPanel.Children.Add(stackPanel7);
		base.Content = new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		RefreshLivePreview();
	}

	private void RefreshLivePreview()
	{
		double result;
		double num = (double.TryParse(_width.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out result) ? Math.Max(10.0, result) : 210.0);
		double result2;
		double num2 = (double.TryParse(_height.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out result2) ? Math.Max(10.0, result2) : 297.0);
		double num3 = 168.0;
		double num4 = Math.Min(520.0 / num, num3 / num2);
		_livePreview.Width = Math.Max(24.0, num * num4);
		_livePreview.Height = Math.Max(24.0, num2 * num4);
		Brush baseBrush = Brushes.Transparent;
		if (_transparent.IsChecked != true)
		{
			try
			{
				baseBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_background.Text.Trim()));
			}
			catch
			{
				baseBrush = Brushes.White;
			}
		}
		_livePreview.Background = TextureCatalogService.Blend(baseBrush, _textureData, _textureOpacity, _textureScale);
	}

	private void ApplyPreset()
	{
		string text = _preset.SelectedItem?.ToString();
		if (text != null && !(text == "自由サイズ"))
		{
			PaperSizeDefinition paperSizeDefinition = PaperCatalog.Get(text);
			double num = paperSizeDefinition.WidthMm;
			double num2 = paperSizeDefinition.HeightMm;
			if (_orientation.SelectedItem?.ToString() == "横")
			{
				double num3 = num2;
				num2 = num;
				num = num3;
			}
			_width.Text = N(num);
			_height.Text = N(num2);
		}
	}

	private void ApplyOrientation()
	{
		if (double.TryParse(_width.Text, out var result) && double.TryParse(_height.Text, out var result2))
		{
			string text = _orientation.SelectedItem?.ToString() ?? "自由";
			if ((text == "縦" && result > result2) || (text == "横" && result < result2))
			{
				_width.Text = N(result2);
				_height.Text = N(result);
			}
		}
	}

	private void Apply()
	{
		if (!TryNumber(_width, out var value, 10.0) || !TryNumber(_height, out var value2, 10.0))
		{
			MessageBox.Show("幅と高さは10mm以上で入力してください。", "台紙");
			return;
		}
		if (!TryNumber(_safe, out var value3, 0.0) || !TryNumber(_bleed, out var value4, 0.0) || !TryNumber(_printMargin, out var value5, 0.0) || !TryNumber(_grid, out var value6, 0.1))
		{
			MessageBox.Show("印刷領域とグリッドの数値を確認してください。", "台紙");
			return;
		}
		string text = ((_transparent.IsChecked == true) ? "#00FFFFFF" : _background.Text.Trim());
		try
		{
			_ = (Color)ColorConverter.ConvertFromString(text);
		}
		catch
		{
			MessageBox.Show("台紙色を確認してください。", "台紙");
			return;
		}
		WidthMm = value;
		HeightMm = value2;
		Background = text;
		SafeMarginMm = value3;
		BleedMm = value4;
		PrintMarginMm = value5;
		GridSizeMm = value6;
		base.DialogResult = true;
	}

	private static bool TryNumber(TextBox box, out double value, double minimum)
	{
		if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
		{
			return value >= minimum;
		}
		return false;
	}

	private static string N(double value)
	{
		return value.ToString("0.##", CultureInfo.CurrentCulture);
	}

	private static StackPanel Section(string title)
	{
		return new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0),
			Children = { (UIElement)new TextBlock
			{
				Text = title,
				FontSize = 16.0,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 4.0, 0.0, 7.0)
			} }
		};
	}

	private static void Add(Panel root, string label, UIElement editor)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
		});
		stackPanel.Children.Add(editor);
		root.Children.Add(stackPanel);
	}

	private static void AddPair(Panel root, string leftLabel, UIElement left, string rightLabel, UIElement right)
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = leftLabel
		});
		stackPanel.Children.Add(left);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = rightLabel
		});
		stackPanel2.Children.Add(right);
		Grid.SetColumn(stackPanel2, 1);
		grid.Children.Add(stackPanel);
		grid.Children.Add(stackPanel2);
		root.Children.Add(grid);
	}
}
