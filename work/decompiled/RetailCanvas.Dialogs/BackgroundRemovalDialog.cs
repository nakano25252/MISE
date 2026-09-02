using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class BackgroundRemovalDialog : Window
{
	private sealed class CutoutSurface : FrameworkElement
	{
		private readonly BitmapSource _bitmap;

		private readonly byte[] _pixels;

		private readonly int _stride;

		private int _dragPoint = -1;

		private bool _painting;

		public ImageCutoutSettings Settings { get; }

		public string Tool { get; set; } = "色を採る";

		public double BrushRadiusPercent { get; set; } = 2.0;

		public event EventHandler? Changed;

		public event Action<Color>? ColorSampled;

		public CutoutSurface(BitmapSource bitmap, ImageCutoutSettings settings)
		{
			_bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0.0);
			Settings = settings;
			_stride = _bitmap.PixelWidth * 4;
			_pixels = new byte[_stride * _bitmap.PixelHeight];
			_bitmap.CopyPixels(_pixels, _stride, 0);
			base.Cursor = Cursors.Cross;
			base.Focusable = true;
			base.MinWidth = 300.0;
			base.MinHeight = 280.0;
		}

		public void ClearEdits()
		{
			Settings.Polygon.Clear();
			Settings.Strokes.Clear();
			InvalidateVisual();
			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);
			Rect rectangle = ImageRect();
			dc.DrawRectangle(Checker(), null, new Rect(0.0, 0.0, base.ActualWidth, base.ActualHeight));
			dc.DrawImage(_bitmap, rectangle);
			if (Settings.Polygon.Count > 0)
			{
				List<Point> list = Settings.Polygon.Select(ToScreen).ToList();
				if (list.Count >= 2)
				{
					StreamGeometry streamGeometry = new StreamGeometry();
					using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
					streamGeometryContext.BeginFigure(list[0], isFilled: false, list.Count >= 3);
					streamGeometryContext.PolyLineTo(list.Skip(1).ToList(), isStroked: true, isSmoothJoin: true);
					dc.DrawGeometry((list.Count >= 3) ? new SolidColorBrush(Color.FromArgb(32, 43, 182, 200)) : null, new Pen(new SolidColorBrush(Color.FromRgb(43, 182, 200)), 2.0), streamGeometry);
				}
				for (int i = 0; i < list.Count; i++)
				{
					bool flag = i == _dragPoint;
					dc.DrawEllipse(flag ? Brushes.Orange : Brushes.White, new Pen(Brushes.DarkCyan, 1.5), list[i], flag ? 6 : 5, flag ? 6 : 5);
				}
			}
			foreach (ImageMaskStroke stroke in Settings.Strokes)
			{
				Point center = ToScreen(new ShapePointModel
				{
					X = stroke.XPercent,
					Y = stroke.YPercent
				});
				double num = stroke.RadiusPercent / 100.0 * Math.Min(rectangle.Width, rectangle.Height);
				SolidColorBrush brush = (stroke.Keep ? new SolidColorBrush(Color.FromArgb(70, 30, 180, 90)) : new SolidColorBrush(Color.FromArgb(70, 220, 60, 60)));
				dc.DrawEllipse(brush, null, center, num, num);
			}
			dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(175, 182, 194)), 1.0), rectangle);
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			Focus();
			Point position = e.GetPosition(this);
			if (!ImageRect().Contains(position))
			{
				return;
			}
			ShapePointModel shapePointModel = Normalize(position);
			if (Tool == "色を採る")
			{
				int num = Math.Clamp((int)Math.Round(shapePointModel.X / 100.0 * (double)(_bitmap.PixelWidth - 1)), 0, _bitmap.PixelWidth - 1);
				int num2 = Math.Clamp((int)Math.Round(shapePointModel.Y / 100.0 * (double)(_bitmap.PixelHeight - 1)), 0, _bitmap.PixelHeight - 1) * _stride + num * 4;
				this.ColorSampled?.Invoke(Color.FromRgb(_pixels[num2 + 2], _pixels[num2 + 1], _pixels[num2]));
			}
			else if (Tool == "多角形・頂点")
			{
				_dragPoint = HitPoint(position);
				if (_dragPoint < 0)
				{
					Settings.Polygon.Add(shapePointModel);
					_dragPoint = Settings.Polygon.Count - 1;
				}
				CaptureMouse();
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				_painting = true;
				AddStroke(shapePointModel);
				CaptureMouse();
			}
			InvalidateVisual();
			e.Handled = true;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (e.LeftButton != MouseButtonState.Pressed)
			{
				return;
			}
			Point position = e.GetPosition(this);
			if (ImageRect().Contains(position))
			{
				ShapePointModel shapePointModel = Normalize(position);
				if (_dragPoint >= 0 && Tool == "多角形・頂点")
				{
					Settings.Polygon[_dragPoint] = shapePointModel;
					this.Changed?.Invoke(this, EventArgs.Empty);
					InvalidateVisual();
					e.Handled = true;
				}
				else if (_painting)
				{
					AddStroke(shapePointModel);
					InvalidateVisual();
					e.Handled = true;
				}
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			if (_dragPoint >= 0 || _painting)
			{
				_dragPoint = -1;
				_painting = false;
				ReleaseMouseCapture();
				e.Handled = true;
			}
		}

		protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseRightButtonDown(e);
			int num = HitPoint(e.GetPosition(this));
			if (num >= 0)
			{
				Settings.Polygon.RemoveAt(num);
				this.Changed?.Invoke(this, EventArgs.Empty);
				InvalidateVisual();
				e.Handled = true;
			}
		}

		private void AddStroke(ShapePointModel point)
		{
			bool flag = Tool == "残すブラシ";
			ImageMaskStroke imageMaskStroke = Settings.Strokes.LastOrDefault();
			if (imageMaskStroke == null || imageMaskStroke.Keep != flag || !(Math.Abs(imageMaskStroke.XPercent - point.X) + Math.Abs(imageMaskStroke.YPercent - point.Y) < BrushRadiusPercent * 0.35))
			{
				Settings.Strokes.Add(new ImageMaskStroke
				{
					Keep = flag,
					XPercent = point.X,
					YPercent = point.Y,
					RadiusPercent = BrushRadiusPercent
				});
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		private int HitPoint(Point point)
		{
			for (int num = Settings.Polygon.Count - 1; num >= 0; num--)
			{
				if ((ToScreen(Settings.Polygon[num]) - point).Length <= 11.0)
				{
					return num;
				}
			}
			return -1;
		}

		private ShapePointModel Normalize(Point point)
		{
			Rect rect = ImageRect();
			return new ShapePointModel
			{
				X = Math.Clamp((point.X - rect.Left) / rect.Width * 100.0, 0.0, 100.0),
				Y = Math.Clamp((point.Y - rect.Top) / rect.Height * 100.0, 0.0, 100.0)
			};
		}

		private Point ToScreen(ShapePointModel point)
		{
			Rect rect = ImageRect();
			return new Point(rect.Left + point.X / 100.0 * rect.Width, rect.Top + point.Y / 100.0 * rect.Height);
		}

		private Rect ImageRect()
		{
			double num = Math.Min(Math.Max(1.0, base.ActualWidth - 12.0) / (double)_bitmap.PixelWidth, Math.Max(1.0, base.ActualHeight - 12.0) / (double)_bitmap.PixelHeight);
			double num2 = (double)_bitmap.PixelWidth * num;
			double num3 = (double)_bitmap.PixelHeight * num;
			return new Rect((base.ActualWidth - num2) / 2.0, (base.ActualHeight - num3) / 2.0, num2, num3);
		}

		private static Brush Checker()
		{
			return new DrawingBrush
			{
				TileMode = TileMode.Tile,
				Viewport = new Rect(0.0, 0.0, 20.0, 20.0),
				ViewportUnits = BrushMappingMode.Absolute,
				Drawing = new DrawingGroup
				{
					Children = 
					{
						(Drawing)new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0.0, 0.0, 20.0, 20.0))),
						(Drawing)new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(0.0, 0.0, 10.0, 10.0))),
						(Drawing)new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(10.0, 10.0, 10.0, 10.0)))
					}
				}
			};
		}
	}

	private readonly byte[] _source;

	private readonly ImageCutoutSettings _settings;

	private readonly CutoutSurface _editor;

	private readonly Image _after = new Image
	{
		Stretch = Stretch.Uniform
	};

	private readonly ComboBox _baseMode = Combo("自動", "色をクリック");

	private readonly ComboBox _tool = Combo("色を採る", "残すブラシ", "消すブラシ", "多角形・頂点");

	private readonly Slider _tolerance = Slider(2.0, 55.0, 18.0);

	private readonly Slider _brushSize = Slider(0.3, 12.0, 2.0);

	private readonly Slider _expand = Slider(-10.0, 10.0, 0.0);

	private readonly Slider _feather = Slider(0.0, 15.0, 2.0);

	private readonly Slider _smooth = Slider(0.0, 10.0, 1.0);

	private readonly TextBlock _sample = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
	};

	private int _previewRequest;

	public byte[]? ResultBytes { get; private set; }

	public string? ResultSettingsJson { get; private set; }

	public BackgroundRemovalDialog(byte[] source, string? settingsJson = null)
	{
		_source = source;
		_settings = LoadSettings(settingsJson);
		base.Title = "画像のパス抜き（非破壊）－ MISE";
		base.Width = 1120.0;
		base.Height = 760.0;
		base.MinWidth = 760.0;
		base.MinHeight = 520.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 1120.0, 760.0, 760.0, 520.0);
		BitmapSource bitmap = Decode(source, 850);
		_editor = new CutoutSurface(bitmap, _settings);
		base.Content = Build();
		_baseMode.SelectedItem = _settings.Mode;
		if (_baseMode.SelectedIndex < 0)
		{
			_baseMode.SelectedIndex = 0;
		}
		_tolerance.Value = Math.Clamp(_settings.TolerancePercent, _tolerance.Minimum, _tolerance.Maximum);
		_expand.Value = Math.Clamp(_settings.EdgeExpandPixels, _expand.Minimum, _expand.Maximum);
		_feather.Value = Math.Clamp(_settings.FeatherPixels, _feather.Minimum, _feather.Maximum);
		_smooth.Value = Math.Clamp(_settings.SmoothPixels, _smooth.Minimum, _smooth.Maximum);
		_sample.Text = (string.IsNullOrWhiteSpace(_settings.SampleColor) ? "未採取" : _settings.SampleColor);
		_tool.SelectionChanged += delegate
		{
			_editor.Tool = _tool.SelectedItem?.ToString() ?? "色を採る";
		};
		_brushSize.ValueChanged += delegate
		{
			_editor.BrushRadiusPercent = _brushSize.Value;
		};
		_editor.ColorSampled += delegate(Color color)
		{
			_settings.SampleColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
			_settings.Mode = "色をクリック";
			_baseMode.SelectedItem = "色をクリック";
			_sample.Text = _settings.SampleColor;
			_ = UpdatePreviewAsync();
		};
		_editor.Changed += delegate
		{
		};
		_editor.Tool = _tool.SelectedItem?.ToString() ?? "色を採る";
		_editor.BrushRadiusPercent = _brushSize.Value;
		_ = UpdatePreviewAsync();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(16.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 11.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "編集点・ブラシを消去"
		};
		button.Click += delegate
		{
			_editor.ClearEdits();
		};
		Button button2 = new Button
		{
			Content = "プレビュー更新"
		};
		button2.Click += async delegate
		{
			await UpdatePreviewAsync();
		};
		Button button3 = new Button
		{
			Content = "透明PNGとして保存"
		};
		button3.Click += async delegate
		{
			await SavePngAsync();
		};
		Button button4 = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button4.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button5 = new Button
		{
			Content = "非破壊で適用",
			MinWidth = 115.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button5.Click += async delegate
		{
			await AcceptAsync();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		stackPanel.Children.Add(button4);
		stackPanel.Children.Add(button5);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 9.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "画像のパス抜き",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "元画像は変更せず、削除色・ブラシ・多角形・境界設定を編集可能な状態でプロジェクトに保存します。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap
		});
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 7.0, 0.0, 0.0)
		};
		wrapPanel.Children.Add(Labeled("背景", _baseMode));
		wrapPanel.Children.Add(Labeled("編集ツール", _tool));
		wrapPanel.Children.Add(Labeled("許容色幅", _tolerance));
		wrapPanel.Children.Add(Labeled("ブラシ", _brushSize));
		wrapPanel.Children.Add(Labeled("境界拡張/縮小", _expand));
		wrapPanel.Children.Add(Labeled("ぼかし", _feather));
		wrapPanel.Children.Add(Labeled("滑らかさ", _smooth));
		wrapPanel.Children.Add(_sample);
		stackPanel2.Children.Add(wrapPanel);
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "多角形：クリックで点追加／点をドラッグで調整／点を右クリックで削除\u3000\u3000ブラシ：ドラッグで残す・消す",
			Foreground = Brushes.SlateGray,
			Margin = new Thickness(0.0, 5.0, 0.0, 0.0)
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = new GridLength(12.0)
				},
				new ColumnDefinition()
			},
			Children = { PreviewBox("元画像／編集マスク", _editor) }
		};
		UIElement element = PreviewBox("切り抜き結果（市松は透明）", _after);
		Grid.SetColumn(element, 2);
		grid.Children.Add(element);
		obj.Children.Add(grid);
		return obj;
	}

	private async Task UpdatePreviewAsync()
	{
		ReadControls();
		int request = ++_previewRequest;
		try
		{
			Mouse.OverrideCursor = Cursors.Wait;
			byte[] data = await Task.Run(() => BackgroundRemovalService.Apply(_source, _settings, 700));
			if (request == _previewRequest)
			{
				_after.Source = Decode(data);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("プレビューできません。\n" + ex.Message, "パス抜き");
		}
		finally
		{
			if (request == _previewRequest)
			{
				Mouse.OverrideCursor = null;
			}
		}
	}

	private async Task AcceptAsync()
	{
		ReadControls();
		try
		{
			Mouse.OverrideCursor = Cursors.Wait;
			ResultBytes = await Task.Run(() => BackgroundRemovalService.Apply(_source, _settings));
			ResultSettingsJson = JsonSerializer.Serialize(_settings, ProjectService.JsonOptions);
			base.DialogResult = true;
		}
		catch (Exception ex)
		{
			MessageBox.Show("背景を抜けませんでした。\n" + ex.Message, "パス抜き");
		}
		finally
		{
			Mouse.OverrideCursor = null;
		}
	}

	private async Task SavePngAsync()
	{
		SaveFileDialog save = new SaveFileDialog
		{
			Filter = "透明PNG (*.png)|*.png",
			DefaultExt = ".png",
			AddExtension = true,
			FileName = "切り抜き画像.png"
		};
		if (save.ShowDialog(this) != true)
		{
			return;
		}
		ReadControls();
		try
		{
			Mouse.OverrideCursor = Cursors.Wait;
			byte[] bytes = await Task.Run(() => BackgroundRemovalService.Apply(_source, _settings));
			await File.WriteAllBytesAsync(save.FileName, bytes);
		}
		catch (Exception ex)
		{
			MessageBox.Show("保存できませんでした。\n" + ex.Message, "透明PNG");
		}
		finally
		{
			Mouse.OverrideCursor = null;
		}
	}

	private void ReadControls()
	{
		_settings.Mode = _baseMode.SelectedItem?.ToString() ?? "自動";
		_settings.TolerancePercent = _tolerance.Value;
		_settings.EdgeExpandPixels = _expand.Value;
		_settings.FeatherPixels = _feather.Value;
		_settings.SmoothPixels = _smooth.Value;
	}

	private static ImageCutoutSettings LoadSettings(string? json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new ImageCutoutSettings();
		}
		try
		{
			return JsonSerializer.Deserialize<ImageCutoutSettings>(json, ProjectService.JsonOptions) ?? new ImageCutoutSettings();
		}
		catch
		{
			return new ImageCutoutSettings();
		}
	}

	private static ComboBox Combo(params string[] values)
	{
		return new ComboBox
		{
			ItemsSource = values,
			SelectedIndex = 0,
			MinWidth = 105.0
		};
	}

	private static Slider Slider(double min, double max, double value)
	{
		return new Slider
		{
			Minimum = min,
			Maximum = max,
			Value = value,
			Width = 105.0,
			TickFrequency = 1.0,
			IsSnapToTickEnabled = true
		};
	}

	private static UIElement Labeled(string label, Control control)
	{
		return new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 10.0, 4.0),
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = label,
					FontSize = 11.0,
					Foreground = Brushes.SlateGray
				},
				(UIElement)control
			}
		};
	}

	private static UIElement PreviewBox(string title, UIElement content)
	{
		DockPanel dockPanel = new DockPanel();
		TextBlock element = new TextBlock
		{
			Text = title,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 5.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		dockPanel.Children.Add(new Border
		{
			Background = Checker(),
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0),
			Child = content
		});
		return dockPanel;
	}

	private static Brush Checker()
	{
		return new DrawingBrush
		{
			TileMode = TileMode.Tile,
			Viewport = new Rect(0.0, 0.0, 20.0, 20.0),
			ViewportUnits = BrushMappingMode.Absolute,
			Drawing = new DrawingGroup
			{
				Children = 
				{
					(Drawing)new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0.0, 0.0, 20.0, 20.0))),
					(Drawing)new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(0.0, 0.0, 10.0, 10.0))),
					(Drawing)new GeometryDrawing(Brushes.LightGray, null, new RectangleGeometry(new Rect(10.0, 10.0, 10.0, 10.0)))
				}
			}
		};
	}

	private static BitmapSource Decode(byte[] data, int maxWidth = 0)
	{
		using MemoryStream streamSource = new MemoryStream(data, writable: false);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		if (maxWidth > 0)
		{
			bitmapImage.DecodePixelWidth = maxWidth;
		}
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}
}
