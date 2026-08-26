using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public static class ColorPickerDialog
{
	private sealed class PaletteWindow : Window
	{
		private static readonly Color[] BrandPalette = new Color[15]
		{
			Color.FromRgb(16, 24, 39),
			Color.FromRgb(byte.MaxValue, 107, 74),
			Color.FromRgb(50, 199, 201),
			Color.FromRgb(246, 243, 238),
			Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue),
			Color.FromRgb(0, 0, 0),
			Color.FromRgb(23, 32, 51),
			Color.FromRgb(242, 106, 33),
			Color.FromRgb(43, 182, 200),
			Color.FromRgb(byte.MaxValue, 214, 10),
			Color.FromRgb(229, 57, 53),
			Color.FromRgb(30, 136, 229),
			Color.FromRgb(67, 160, 71),
			Color.FromRgb(142, 36, 170),
			Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue)
		};

		private readonly Border _preview = new Border
		{
			Width = 72.0,
			Height = 54.0,
			CornerRadius = new CornerRadius(7.0),
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0)
		};

		private readonly Border _beforePreview = new Border
		{
			Width = 72.0,
			Height = 54.0,
			CornerRadius = new CornerRadius(7.0),
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0)
		};

		private readonly TextBox _hex = new TextBox
		{
			Width = 132.0
		};

		private readonly Slider _alpha = new Slider
		{
			Minimum = 0.0,
			Maximum = 255.0,
			Width = 190.0,
			TickFrequency = 1.0,
			IsSnapToTickEnabled = true
		};

		private readonly Slider _value = new Slider
		{
			Minimum = 0.0,
			Maximum = 100.0,
			Width = 190.0,
			TickFrequency = 1.0
		};

		private readonly TextBlock _valueText = new TextBlock
		{
			Width = 48.0,
			VerticalAlignment = VerticalAlignment.Center
		};

		private readonly TextBox _r = NumberBox();

		private readonly TextBox _g = NumberBox();

		private readonly TextBox _b = NumberBox();

		private readonly TextBox _h = NumberBox();

		private readonly TextBox _s = NumberBox();

		private readonly TextBox _v = NumberBox();

		private readonly TextBlock _alphaText = new TextBlock
		{
			Width = 48.0,
			VerticalAlignment = VerticalAlignment.Center
		};

		private readonly TextBlock _sampleHelp = new TextBlock
		{
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap
		};

		private readonly ColorWheel _wheel = new ColorWheel();

		private Color _color;

		private readonly Color _initial;

		private bool _updating;

		public Color SelectedColor => _color;

		public bool Accepted { get; private set; }

		public PaletteWindow(Color initial)
		{
			base.Title = "色を選択 － MISE";
			base.Width = 570.0;
			base.Height = 540.0;
			base.MinWidth = 500.0;
			base.MinHeight = 430.0;
			base.ResizeMode = ResizeMode.CanResize;
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			WindowSizing.Attach(this, 570.0, 540.0, 500.0, 430.0);
			_color = initial;
			_initial = initial;
			_beforePreview.Background = new SolidColorBrush(initial);
			base.Content = Build();
			_alpha.ValueChanged += delegate
			{
				if (!_updating)
				{
					_color.A = (byte)Math.Round(_alpha.Value);
					RefreshFields(updateHex: true);
				}
			};
			_value.ValueChanged += delegate
			{
				if (!_updating)
				{
					RgbToHsv(_color, out var h, out var s, out var _);
					Color color = HsvToRgb(h, s, _value.Value / 100.0);
					_color = Color.FromArgb(_color.A, color.R, color.G, color.B);
					RefreshFields(updateHex: true);
				}
			};
			_hex.LostKeyboardFocus += delegate
			{
				ApplyHex();
			};
			_hex.KeyDown += delegate(object _, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					ApplyHex();
					e.Handled = true;
				}
			};
			TextBox[] array = new TextBox[6] { _r, _g, _b, _h, _s, _v };
			foreach (TextBox obj in array)
			{
				obj.LostKeyboardFocus += delegate
				{
					ApplyNumericFields();
				};
				obj.KeyDown += delegate(object _, KeyEventArgs e)
				{
					if (e.Key == Key.Return)
					{
						ApplyNumericFields();
						e.Handled = true;
					}
				};
			}
			RefreshFields(updateHex: true);
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
				Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
			};
			Button button = new Button
			{
				Content = "キャンセル",
				MinWidth = 90.0
			};
			button.Click += delegate
			{
				Close();
			};
			Button button2 = new Button
			{
				Content = "この色を使う",
				MinWidth = 120.0,
				Style = (TryFindResource("PrimaryButton") as Style)
			};
			button2.Click += delegate
			{
				ApplyHex();
				Accepted = true;
				Close();
			};
			stackPanel.Children.Add(button);
			stackPanel.Children.Add(button2);
			DockPanel.SetDock(stackPanel, Dock.Bottom);
			obj.Children.Add(stackPanel);
			StackPanel stackPanel2 = new StackPanel
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
			};
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "カラーパレット",
				FontSize = 22.0,
				FontWeight = FontWeights.Bold
			});
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "色・透明度・スポイトをここでまとめて調整できます。",
				Foreground = Brushes.SlateGray
			});
			DockPanel.SetDock(stackPanel2, Dock.Top);
			obj.Children.Add(stackPanel2);
			ScrollViewer scrollViewer = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};
			StackPanel stackPanel3 = (StackPanel)(scrollViewer.Content = new StackPanel());
			obj.Children.Add(scrollViewer);
			Grid grid = new Grid
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			StackPanel element = new StackPanel
			{
				Children = 
				{
					(UIElement)new TextBlock
					{
						Text = "変更前",
						FontWeight = FontWeights.SemiBold
					},
					(UIElement)_beforePreview
				}
			};
			grid.Children.Add(element);
			StackPanel stackPanel5 = new StackPanel
			{
				Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
			};
			stackPanel5.Children.Add(new TextBlock
			{
				Text = "変更後",
				FontWeight = FontWeights.SemiBold
			});
			stackPanel5.Children.Add(_preview);
			Grid.SetColumn(stackPanel5, 1);
			grid.Children.Add(stackPanel5);
			StackPanel stackPanel6 = new StackPanel
			{
				Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
			};
			stackPanel6.Children.Add(new TextBlock
			{
				Text = "カラーコード（#AARRGGBB）",
				FontWeight = FontWeights.SemiBold
			});
			stackPanel6.Children.Add(_hex);
			Grid.SetColumn(stackPanel6, 2);
			grid.Children.Add(stackPanel6);
			stackPanel3.Children.Add(grid);
			stackPanel3.Children.Add(Heading("カラーサークル"));
			_wheel.HorizontalAlignment = HorizontalAlignment.Left;
			_wheel.ColorSelected += delegate(Color color)
			{
				_color = Color.FromArgb(_color.A, color.R, color.G, color.B);
				RefreshFields(updateHex: true);
			};
			stackPanel3.Children.Add(_wheel);
			StackPanel stackPanel7 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0.0, 4.0, 0.0, 6.0)
			};
			stackPanel7.Children.Add(new TextBlock
			{
				Text = "明度",
				Width = 45.0,
				VerticalAlignment = VerticalAlignment.Center
			});
			stackPanel7.Children.Add(_value);
			stackPanel7.Children.Add(_valueText);
			stackPanel3.Children.Add(stackPanel7);
			WrapPanel wrapPanel = new WrapPanel
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			};
			AddNumber(wrapPanel, "R", _r);
			AddNumber(wrapPanel, "G", _g);
			AddNumber(wrapPanel, "B", _b);
			AddNumber(wrapPanel, "H", _h);
			AddNumber(wrapPanel, "S", _s);
			AddNumber(wrapPanel, "V", _v);
			stackPanel3.Children.Add(wrapPanel);
			stackPanel3.Children.Add(Heading("MISE配色・標準色"));
			stackPanel3.Children.Add(SwatchPanel(BrandPalette));
			if (_brandColors.Count > 0)
			{
				stackPanel3.Children.Add(Heading("ブランド色"));
				stackPanel3.Children.Add(SwatchPanel(_brandColors));
			}
			if (_designColors.Count > 0)
			{
				stackPanel3.Children.Add(Heading("このデザインで使用中"));
				stackPanel3.Children.Add(SwatchPanel(_designColors));
			}
			stackPanel3.Children.Add(Heading("最近使用した色"));
			stackPanel3.Children.Add((RecentColors.Count == 0) ? ((UIElement)new TextBlock
			{
				Text = "まだありません",
				Foreground = Brushes.SlateGray,
				Margin = new Thickness(2.0, 4.0, 2.0, 8.0)
			}) : ((UIElement)SwatchPanel(RecentColors)));
			stackPanel3.Children.Add(Heading("透明度"));
			StackPanel stackPanel8 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
			};
			stackPanel8.Children.Add(_alpha);
			stackPanel8.Children.Add(_alphaText);
			Button button3 = new Button
			{
				Content = "透明",
				MinWidth = 72.0,
				ToolTip = "完全に透明にする"
			};
			button3.Click += delegate
			{
				_color.A = 0;
				RefreshFields(updateHex: true);
			};
			stackPanel8.Children.Add(button3);
			stackPanel3.Children.Add(stackPanel8);
			stackPanel3.Children.Add(Heading("スポイト"));
			Button button4 = new Button
			{
				Content = "⌾  画面から色を取得",
				MinWidth = 190.0,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			button4.Click += BeginSampling;
			stackPanel3.Children.Add(button4);
			_sampleHelp.Text = "ボタンを押した後、画面上の欲しい色を左クリックします。右クリックで中止します。";
			_sampleHelp.Margin = new Thickness(2.0, 4.0, 2.0, 0.0);
			stackPanel3.Children.Add(_sampleHelp);
			return obj;
		}

		private static TextBlock Heading(string text)
		{
			return new TextBlock
			{
				Text = text,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(Color.FromRgb(16, 24, 39)),
				Margin = new Thickness(0.0, 7.0, 0.0, 4.0)
			};
		}

		private WrapPanel SwatchPanel(IEnumerable<Color> colors)
		{
			WrapPanel wrapPanel = new WrapPanel
			{
				Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
			};
			foreach (Color item in colors.Distinct())
			{
				Color swatchColor = item;
				Button button = new Button
				{
					Width = 34.0,
					Height = 34.0,
					Margin = new Thickness(2.0),
					Padding = new Thickness(0.0),
					Background = new SolidColorBrush(swatchColor),
					BorderBrush = new SolidColorBrush(Color.FromArgb(100, 30, 30, 30)),
					ToolTip = $"#{swatchColor.A:X2}{swatchColor.R:X2}{swatchColor.G:X2}{swatchColor.B:X2}"
				};
				button.Click += delegate
				{
					_color = swatchColor;
					RefreshFields(updateHex: true);
				};
				wrapPanel.Children.Add(button);
			}
			return wrapPanel;
		}

		private void ApplyHex()
		{
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(_hex.Text.Trim());
				_color = color;
				RefreshFields(updateHex: true);
			}
			catch
			{
				_hex.Text = $"#{_color.A:X2}{_color.R:X2}{_color.G:X2}{_color.B:X2}";
			}
		}

		private void ApplyNumericFields()
		{
			if (!_updating)
			{
				if (byte.TryParse(_r.Text, out var result) && byte.TryParse(_g.Text, out var result2) && byte.TryParse(_b.Text, out var result3))
				{
					_color = Color.FromArgb(_color.A, result, result2, result3);
				}
				if (double.TryParse(_h.Text, out var result4) && double.TryParse(_s.Text, out var result5) && double.TryParse(_v.Text, out var result6))
				{
					Color color = HsvToRgb(Math.Clamp(result4, 0.0, 360.0), Math.Clamp(result5, 0.0, 100.0) / 100.0, Math.Clamp(result6, 0.0, 100.0) / 100.0);
					_color = Color.FromArgb(_color.A, color.R, color.G, color.B);
				}
				RefreshFields(updateHex: true);
			}
		}

		private void RefreshFields(bool updateHex)
		{
			_updating = true;
			_preview.Background = new SolidColorBrush(_color);
			_wheel.SetColor(_color);
			_alpha.Value = (int)_color.A;
			_alphaText.Text = $"{(double)(int)_color.A / 255.0:P0}";
			RgbToHsv(_color, out var h, out var s, out var v);
			_value.Value = v * 100.0;
			_valueText.Text = $"{v:P0}";
			_r.Text = _color.R.ToString();
			_g.Text = _color.G.ToString();
			_b.Text = _color.B.ToString();
			_h.Text = h.ToString("0");
			_s.Text = (s * 100.0).ToString("0");
			_v.Text = (v * 100.0).ToString("0");
			if (updateHex)
			{
				_hex.Text = $"#{_color.A:X2}{_color.R:X2}{_color.G:X2}{_color.B:X2}";
			}
			_updating = false;
		}

		private void BeginSampling(object? sender, RoutedEventArgs e)
		{
			_sampleHelp.Text = "スポイトを起動しています…";
			double left = base.Left;
			double top = base.Top;
			WindowStartupLocation windowStartupLocation = base.WindowStartupLocation;
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth + 200.0;
			base.Top = SystemParameters.VirtualScreenTop;
			Color? color = null;
			try
			{
				ScreenColorSamplerWindow screenColorSamplerWindow = new ScreenColorSamplerWindow(_color.A);
				if (screenColorSamplerWindow.ShowDialog() == true)
				{
					color = screenColorSamplerWindow.SelectedColor;
				}
			}
			finally
			{
				if (color.HasValue)
				{
					_color = color.Value;
				}
				base.Left = left;
				base.Top = top;
				base.WindowStartupLocation = windowStartupLocation;
				Activate();
				Focus();
				_sampleHelp.Text = (color.HasValue ? "画面から色を取得しました。" : "スポイトを中止しました。");
				RefreshFields(updateHex: true);
			}
		}

		private static TextBox NumberBox()
		{
			return new TextBox
			{
				Width = 47.0,
				Margin = new Thickness(2.0, 0.0, 7.0, 0.0)
			};
		}

		private static void AddNumber(Panel panel, string label, TextBox box)
		{
			panel.Children.Add(new TextBlock
			{
				Text = label,
				VerticalAlignment = VerticalAlignment.Center
			});
			panel.Children.Add(box);
		}

		private static Color HsvToRgb(double h, double s, double v)
		{
			double num = v * s;
			double num2 = num * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
			double num3 = v - num;
			var (num4, num5, num6) = ((h < 60.0) ? (num, num2, 0.0) : ((h < 120.0) ? (num2, num, 0.0) : ((h < 180.0) ? (0.0, num, num2) : ((h < 240.0) ? (0.0, num2, num) : ((!(h < 300.0)) ? (num, 0.0, num2) : (num2, 0.0, num))))));
			return Color.FromRgb((byte)Math.Round((num4 + num3) * 255.0), (byte)Math.Round((num5 + num3) * 255.0), (byte)Math.Round((num6 + num3) * 255.0));
		}

		private static void RgbToHsv(Color color, out double h, out double s, out double v)
		{
			double num = (double)(int)color.R / 255.0;
			double num2 = (double)(int)color.G / 255.0;
			double num3 = (double)(int)color.B / 255.0;
			double num4 = Math.Max(num, Math.Max(num2, num3));
			double num5 = Math.Min(num, Math.Min(num2, num3));
			double num6 = num4 - num5;
			h = ((num6 == 0.0) ? 0.0 : ((num4 == num) ? (60.0 * ((num2 - num3) / num6 % 6.0)) : ((num4 == num2) ? (60.0 * ((num3 - num) / num6 + 2.0)) : (60.0 * ((num - num2) / num6 + 4.0)))));
			if (h < 0.0)
			{
				h += 360.0;
			}
			s = ((num4 == 0.0) ? 0.0 : (num6 / num4));
			v = num4;
		}
	}

	private sealed class ColorWheel : FrameworkElement
	{
		private const int WheelSize = 210;

		private WriteableBitmap? _bitmap;

		private Color _selected = Colors.Red;

		private double _value = 1.0;

		public event Action<Color>? ColorSelected;

		public ColorWheel()
		{
			base.Width = 210.0;
			base.Height = 210.0;
			base.Cursor = Cursors.Cross;
			base.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
			{
				CaptureMouse();
				Pick(e.GetPosition(this));
			};
			base.MouseMove += delegate(object _, MouseEventArgs e)
			{
				if (e.LeftButton == MouseButtonState.Pressed)
				{
					Pick(e.GetPosition(this));
				}
			};
			base.MouseLeftButtonUp += delegate
			{
				ReleaseMouseCapture();
			};
		}

		public void SetColor(Color color)
		{
			_selected = color;
			RgbToHsv(color, out var _, out var _, out _value);
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);
			if (_bitmap == null)
			{
				_bitmap = CreateBitmap();
			}
			dc.DrawImage(_bitmap, new Rect(0.0, 0.0, 210.0, 210.0));
			RgbToHsv(_selected, out var h, out var s, out var _);
			double num = 102.0;
			double num2 = h * Math.PI / 180.0;
			Point center = new Point(105.0 + Math.Cos(num2) * s * num, 105.0 + Math.Sin(num2) * s * num);
			dc.DrawEllipse(null, new Pen(Brushes.White, 2.0), center, 5.0, 5.0);
			dc.DrawEllipse(null, new Pen(Brushes.Black, 1.0), center, 6.0, 6.0);
		}

		private void Pick(Point point)
		{
			double num = point.X - 105.0;
			double num2 = point.Y - 105.0;
			double num3 = 102.0;
			double num4 = Math.Sqrt(num * num + num2 * num2) / num3;
			if (!(num4 > 1.0))
			{
				double h = (Math.Atan2(num2, num) * 180.0 / Math.PI + 360.0) % 360.0;
				_selected = HsvToRgb(h, num4, _value);
				InvalidateVisual();
				this.ColorSelected?.Invoke(_selected);
			}
		}

		private static WriteableBitmap CreateBitmap()
		{
			int num = 840;
			byte[] array = new byte[num * 210];
			double num2 = 102.0;
			for (int i = 0; i < 210; i++)
			{
				for (int j = 0; j < 210; j++)
				{
					double num3 = (double)j - 105.0;
					double num4 = (double)i - 105.0;
					double num5 = Math.Sqrt(num3 * num3 + num4 * num4) / num2;
					if (!(num5 > 1.0))
					{
						Color color = HsvToRgb((Math.Atan2(num4, num3) * 180.0 / Math.PI + 360.0) % 360.0, num5, 1.0);
						int num6 = i * num + j * 4;
						array[num6] = color.B;
						array[num6 + 1] = color.G;
						array[num6 + 2] = color.R;
						array[num6 + 3] = byte.MaxValue;
					}
				}
			}
			WriteableBitmap writeableBitmap = new WriteableBitmap(210, 210, 96.0, 96.0, PixelFormats.Bgra32, null);
			writeableBitmap.WritePixels(new Int32Rect(0, 0, 210, 210), array, num, 0);
			writeableBitmap.Freeze();
			return writeableBitmap;
		}

		private static Color HsvToRgb(double h, double s, double v)
		{
			double num = v * s;
			double num2 = num * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
			double num3 = v - num;
			var (num4, num5, num6) = ((h < 60.0) ? (num, num2, 0.0) : ((h < 120.0) ? (num2, num, 0.0) : ((h < 180.0) ? (0.0, num, num2) : ((h < 240.0) ? (0.0, num2, num) : ((!(h < 300.0)) ? (num, 0.0, num2) : (num2, 0.0, num))))));
			return Color.FromRgb((byte)Math.Round((num4 + num3) * 255.0), (byte)Math.Round((num5 + num3) * 255.0), (byte)Math.Round((num6 + num3) * 255.0));
		}

		private static void RgbToHsv(Color color, out double h, out double s, out double v)
		{
			double num = (double)(int)color.R / 255.0;
			double num2 = (double)(int)color.G / 255.0;
			double num3 = (double)(int)color.B / 255.0;
			double num4 = Math.Max(num, Math.Max(num2, num3));
			double num5 = Math.Min(num, Math.Min(num2, num3));
			double num6 = num4 - num5;
			h = ((num6 == 0.0) ? 0.0 : ((num4 == num) ? (60.0 * ((num2 - num3) / num6 % 6.0)) : ((num4 == num2) ? (60.0 * ((num3 - num) / num6 + 2.0)) : (60.0 * ((num - num2) / num6 + 4.0)))));
			if (h < 0.0)
			{
				h += 360.0;
			}
			s = ((num4 == 0.0) ? 0.0 : (num6 / num4));
			v = num4;
		}
	}

	private sealed class ScreenColorSamplerWindow : Window
	{
		private const string EyedropperCursorBase64 = "AAACAAEAICAAAAUAHABeAQAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAASVJREFUeJztlb0NwjAQRr9DDEFLnxKJGQijZAcKZoBRAjMgUaanZYujQI6Cf5K7s0EI8XW5JH7PZzsB/vmVMDNb3puVhFsksgUcdLFcvVx/RMCHWyTMAj7cRSuR1QEfbpHIErjfrsl7UgmTADc1V+dLEQm1ADc1A0DX7lFCQiXg4C4lJFQCdDyRX8uV0HWAmavtLqjnSJg2oVXC1Ymo76RYwG+dViIGVwkA6AfXSqTgYoGxcyyViMEBIFqMCfizj0GDHNok2GWyA9KfStAJAVwkoEkvIYQDE0vgZj/Vfpdus34OKoQDwFz6oASshQMjSyCdfQ58VECSXDgQWQLpri8BDwT88z6EvAMOGJagJBwQnIJUF0rAgch3YLgHiIhie6IU/CvyAEQD3McCgZaPAAAAAElFTkSuQmCC";

		private readonly byte _alpha;

		private readonly MemoryStream? _cursorStream;

		public Color? SelectedColor { get; private set; }

		public ScreenColorSamplerWindow(byte alpha)
		{
			_alpha = alpha;
			base.Title = "スポイト － MISE";
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;
			base.AllowsTransparency = true;
			base.Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
			base.ShowInTaskbar = false;
			base.Topmost = true;
			base.Left = SystemParameters.VirtualScreenLeft;
			base.Top = SystemParameters.VirtualScreenTop;
			base.Width = SystemParameters.VirtualScreenWidth;
			base.Height = SystemParameters.VirtualScreenHeight;
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Focusable = true;
			try
			{
				_cursorStream = new MemoryStream(Convert.FromBase64String("AAACAAEAICAAAAUAHABeAQAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAAgAAAAIAgGAAAAc3p69AAAASVJREFUeJztlb0NwjAQRr9DDEFLnxKJGQijZAcKZoBRAjMgUaanZYujQI6Cf5K7s0EI8XW5JH7PZzsB/vmVMDNb3puVhFsksgUcdLFcvVx/RMCHWyTMAj7cRSuR1QEfbpHIErjfrsl7UgmTADc1V+dLEQm1ADc1A0DX7lFCQiXg4C4lJFQCdDyRX8uV0HWAmavtLqjnSJg2oVXC1Ymo76RYwG+dViIGVwkA6AfXSqTgYoGxcyyViMEBIFqMCfizj0GDHNok2GWyA9KfStAJAVwkoEkvIYQDE0vgZj/Vfpdus34OKoQDwFz6oASshQMjSyCdfQ58VECSXDgQWQLpri8BDwT88z6EvAMOGJagJBwQnIJUF0rAgch3YLgHiIhie6IU/CvyAEQD3McCgZaPAAAAAElFTkSuQmCC"));
				base.Cursor = new Cursor(_cursorStream);
			}
			catch
			{
				base.Cursor = Cursors.Cross;
			}
			Grid grid = new Grid
			{
				Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
			};
			Border element = new Border
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0.0, 24.0, 0.0, 0.0),
				Padding = new Thickness(16.0, 10.0, 16.0, 10.0),
				CornerRadius = new CornerRadius(7.0),
				Background = new SolidColorBrush(Color.FromArgb(225, 16, 24, 39)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(150, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
				BorderThickness = new Thickness(1.0),
				IsHitTestVisible = false,
				Child = new TextBlock
				{
					Text = "スポイト：取得したい場所を左クリック\u3000\u3000Esc／右クリックで中止",
					Foreground = Brushes.White,
					FontWeight = FontWeights.SemiBold
				}
			};
			grid.Children.Add(element);
			base.Content = grid;
			base.PreviewMouseLeftButtonDown += Sample;
			base.PreviewMouseRightButtonDown += Cancel;
			base.PreviewKeyDown += KeyDownHandler;
			base.Loaded += delegate
			{
				Activate();
				Focus();
				Keyboard.Focus(this);
			};
			base.Closed += delegate
			{
				_cursorStream?.Dispose();
			};
		}

		private void Sample(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			if (!GetCursorPos(out var point))
			{
				return;
			}
			nint dC = GetDC(IntPtr.Zero);
			try
			{
				if (dC != IntPtr.Zero)
				{
					uint pixel = GetPixel(dC, point.X, point.Y);
					if (pixel != uint.MaxValue)
					{
						SelectedColor = Color.FromArgb(_alpha, (byte)(pixel & 0xFF), (byte)((pixel >> 8) & 0xFF), (byte)((pixel >> 16) & 0xFF));
						base.DialogResult = true;
					}
				}
			}
			finally
			{
				if (dC != IntPtr.Zero)
				{
					ReleaseDC(IntPtr.Zero, dC);
				}
			}
		}

		private void Cancel(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
			base.DialogResult = false;
		}

		private void KeyDownHandler(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				base.DialogResult = false;
			}
		}
	}

	private struct NativePoint
	{
		public int X;

		public int Y;
	}

	private static readonly List<Color> RecentColors = LoadRecentColors();

	private static List<Color> _brandColors = new List<Color>();

	private static List<Color> _designColors = new List<Color>();

	private static string RecentColorFile => Path.Combine(AppPaths.Root, "recent-colors.json");

	public static void SetContext(IEnumerable<string>? brandColors, IEnumerable<string>? designColors)
	{
		_brandColors = ParseColors(brandColors);
		_designColors = ParseColors(designColors);
	}

	public static string? Show(Window owner, string initialColor)
	{
		PaletteWindow paletteWindow = new PaletteWindow(Parse(initialColor))
		{
			Owner = owner
		};
		paletteWindow.ShowDialog();
		if (!paletteWindow.Accepted)
		{
			return null;
		}
		Color color = paletteWindow.SelectedColor;
		RecentColors.RemoveAll((Color x) => x == color);
		RecentColors.Insert(0, color);
		if (RecentColors.Count > 12)
		{
			RecentColors.RemoveRange(12, RecentColors.Count - 12);
		}
		SaveRecentColors();
		return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	private static List<Color> ParseColors(IEnumerable<string>? values)
	{
		if (values != null)
		{
			return values.Select(Parse).Distinct().ToList();
		}
		return new List<Color>();
	}

	private static List<Color> LoadRecentColors()
	{
		try
		{
			if (!File.Exists(RecentColorFile))
			{
				return new List<Color>();
			}
			return (JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentColorFile)) ?? new List<string>()).Select(Parse).Distinct().Take(12)
				.ToList();
		}
		catch
		{
			return new List<Color>();
		}
	}

	private static void SaveRecentColors()
	{
		try
		{
			AppPaths.EnsureCreated();
			File.WriteAllText(RecentColorFile, JsonSerializer.Serialize(RecentColors.Select((Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}")));
		}
		catch
		{
		}
	}

	private static Color Parse(string value)
	{
		try
		{
			return (Color)ColorConverter.ConvertFromString(value);
		}
		catch
		{
			return Colors.Black;
		}
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out NativePoint point);

	[DllImport("user32.dll")]
	private static extern nint GetDC(nint hwnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(nint hwnd, nint dc);

	[DllImport("gdi32.dll")]
	private static extern uint GetPixel(nint dc, int x, int y);
}
