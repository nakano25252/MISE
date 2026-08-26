using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class PanelDividerEditorDialog : Window
{
	private sealed class DividerSurface : FrameworkElement
	{
		private readonly double _widthMm;

		private readonly double _heightMm;

		private readonly double _gridMm;

		private readonly double _originXmm;

		private readonly double _originYmm;

		private (bool row, int index)? _dragging;

		private (bool row, int index)? _hovered;

		private (bool row, int index)? _selected;

		private (bool row, int index)? _previewRemoval;

		public List<double> RowSplits { get; }

		public List<double> ColumnSplits { get; }

		public string DividerColor { get; set; } = "#FF172033";

		public double DividerThicknessPt { get; set; } = 1.0;

		public double DividerOpacity { get; set; } = 1.0;

		public string DividerStyle { get; set; } = "実線";

		public DividerSurface(CanvasElementModel model, double gridMm)
		{
			_widthMm = Math.Max(1.0, model.WidthMm);
			_heightMm = Math.Max(1.0, model.HeightMm);
			_gridMm = Math.Clamp(gridMm, 0.1, 20.0);
			_originXmm = model.Xmm;
			_originYmm = model.Ymm;
			RowSplits = Initial(model.PanelRowSplits, model.PanelRows);
			ColumnSplits = Initial(model.PanelColumnSplits, model.PanelColumns);
			ResnapAll();
			DividerColor = model.PanelDividerColor;
			DividerThicknessPt = model.PanelDividerThicknessPt;
			DividerOpacity = model.PanelDividerOpacity;
			DividerStyle = model.PanelDividerStyle;
			base.MinWidth = 300.0;
			base.MinHeight = 260.0;
			base.Cursor = Cursors.Cross;
			base.MouseLeftButtonDown += Down;
			base.MouseMove += Move;
			base.MouseLeftButtonUp += Up;
			base.MouseRightButtonDown += RightDown;
		}

		private static List<double> Initial(List<double> values, int count)
		{
			List<double> list = (from x in values
				where x > 0.0 && x < 100.0
				orderby x
				select x).ToList();
			if (list.Count == 0)
			{
				for (int num = 1; num < Math.Clamp(count, 1, 12); num++)
				{
					list.Add(100.0 * (double)num / (double)count);
				}
			}
			return list;
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);
			Rect rectangle = PanelRect();
			dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(23, 32, 51)), 2.0), rectangle, 14.0, 14.0);
			Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(48, 60, 75, 95)), 0.5);
			DrawPageGrid(dc, rectangle, pen);
			Brush brush;
			try
			{
				Color color = (Color)ColorConverter.ConvertFromString(DividerColor);
				color.A = (byte)Math.Clamp(Math.Round((double)(int)color.A * DividerOpacity), 0.0, 255.0);
				brush = new SolidColorBrush(color);
			}
			catch
			{
				brush = new SolidColorBrush(Color.FromRgb(16, 24, 39));
			}
			Pen pen2 = new Pen(brush, Math.Max(0.5, DividerThicknessPt * 96.0 / 72.0));
			if (DividerStyle == "破線")
			{
				pen2.DashStyle = DashStyles.Dash;
			}
			else if (DividerStyle == "点線")
			{
				pen2.DashStyle = DashStyles.Dot;
			}
			double num;
			double num2;
			Pen pen3;
			Pen pen4;
			for (int i = 0; i < RowSplits.Count; pen4 = pen3, dc.DrawLine(pen4, new Point(rectangle.Left, num2), new Point(rectangle.Right, num2)), DrawLabel(dc, $"{num:0.#}% / {_heightMm * num / 100.0:0.#}mm", new Point(rectangle.Left + 6.0, num2 - 18.0)), i++)
			{
				num = RowSplits[i];
				num2 = rectangle.Top + rectangle.Height * num / 100.0;
				(bool, int)? hovered = _hovered;
				int num3 = i;
				bool hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_01bf;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (!valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_01bf;
					}
				}
				goto IL_024c;
				IL_024c:
				pen3 = new Pen(Brushes.OrangeRed, Math.Max(2.0, pen2.Thickness + 1.0));
				continue;
				IL_0249:
				pen3 = pen2;
				continue;
				IL_0204:
				hovered = _previewRemoval;
				num3 = i;
				hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_0249;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (!valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_0249;
					}
				}
				goto IL_024c;
				IL_01bf:
				hovered = _selected;
				num3 = i;
				hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_0204;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (!valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_0204;
					}
				}
				goto IL_024c;
			}
			double num4;
			double num5;
			Pen pen5;
			Pen pen6;
			for (int j = 0; j < ColumnSplits.Count; pen6 = pen5, dc.DrawLine(pen6, new Point(num5, rectangle.Top), new Point(num5, rectangle.Bottom)), DrawLabel(dc, $"{num4:0.#}%\n{_widthMm * num4 / 100.0:0.#}mm", new Point(num5 + 4.0, rectangle.Top + 6.0)), j++)
			{
				num4 = ColumnSplits[j];
				num5 = rectangle.Left + rectangle.Width * num4 / 100.0;
				(bool, int)? hovered = _hovered;
				int num3 = j;
				bool hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_03ac;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_03ac;
					}
				}
				goto IL_0439;
				IL_0439:
				pen5 = new Pen(Brushes.OrangeRed, Math.Max(2.0, pen2.Thickness + 1.0));
				continue;
				IL_0436:
				pen5 = pen2;
				continue;
				IL_03f1:
				hovered = _previewRemoval;
				num3 = j;
				hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_0436;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_0436;
					}
				}
				goto IL_0439;
				IL_03ac:
				hovered = _selected;
				num3 = j;
				hasValue = hovered.HasValue;
				if (!hasValue)
				{
					goto IL_03f1;
				}
				if (hasValue)
				{
					(bool, int) valueOrDefault = hovered.GetValueOrDefault();
					if (valueOrDefault.Item1 || valueOrDefault.Item2 != num3)
					{
						goto IL_03f1;
					}
				}
				goto IL_0439;
			}
		}

		private Rect PanelRect()
		{
			double num = _widthMm / _heightMm;
			double num2 = Math.Min(base.ActualWidth - 20.0, (base.ActualHeight - 20.0) * num);
			double num3 = num2 / num;
			if (num3 > base.ActualHeight - 20.0)
			{
				num3 = base.ActualHeight - 20.0;
				num2 = num3 * num;
			}
			return new Rect((base.ActualWidth - num2) / 2.0, (base.ActualHeight - num3) / 2.0, num2, num3);
		}

		private void DrawPageGrid(DrawingContext dc, Rect rectangle, Pen pen)
		{
			double num = Math.Ceiling(_originXmm / _gridMm) * _gridMm;
			double num2 = _originXmm + _widthMm;
			double num3 = num;
			double num4 = 0.0;
			while (num3 <= num2 + 0.0001 && num4 < 500.0)
			{
				double num5 = num3 - _originXmm;
				if (!(num5 <= 0.0001) && !(num5 >= _widthMm - 0.0001))
				{
					double x = rectangle.Left + rectangle.Width * num5 / _widthMm;
					dc.DrawLine(pen, new Point(x, rectangle.Top), new Point(x, rectangle.Bottom));
				}
				num3 += _gridMm;
				num4 += 1.0;
			}
			double num6 = Math.Ceiling(_originYmm / _gridMm) * _gridMm;
			double num7 = _originYmm + _heightMm;
			double num8 = num6;
			double num9 = 0.0;
			while (num8 <= num7 + 0.0001 && num9 < 500.0)
			{
				double num10 = num8 - _originYmm;
				if (!(num10 <= 0.0001) && !(num10 >= _heightMm - 0.0001))
				{
					double y = rectangle.Top + rectangle.Height * num10 / _heightMm;
					dc.DrawLine(pen, new Point(rectangle.Left, y), new Point(rectangle.Right, y));
				}
				num8 += _gridMm;
				num9 += 1.0;
			}
		}

		private static void DrawLabel(DrawingContext dc, string text, Point point)
		{
			FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11.0, Brushes.DarkSlateGray, 1.0);
			dc.DrawText(formattedText, point);
		}

		private void Down(object sender, MouseButtonEventArgs e)
		{
			Point position = e.GetPosition(this);
			(bool, int)? tuple = Hit(position);
			if (e.ClickCount == 2 && !tuple.HasValue)
			{
				Rect rect = PanelRect();
				if (rect.Contains(position))
				{
					double num = Math.Min(Math.Abs(position.Y - rect.Top), Math.Abs(position.Y - rect.Bottom));
					double num2 = Math.Min(Math.Abs(position.X - rect.Left), Math.Abs(position.X - rect.Right));
					if (num < num2)
					{
						double num3 = Math.Clamp((position.Y - rect.Top) / rect.Height * 100.0, 0.5, 99.5);
						RowSplits.Add(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? num3 : SnapPercent(row: true, num3));
					}
					else
					{
						double num4 = Math.Clamp((position.X - rect.Left) / rect.Width * 100.0, 0.5, 99.5);
						ColumnSplits.Add(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? num4 : SnapPercent(row: false, num4));
					}
					Sort();
					InvalidateVisual();
				}
			}
			else
			{
				_dragging = tuple;
				_selected = tuple;
				InvalidateVisual();
				if (tuple.HasValue)
				{
					CaptureMouse();
				}
			}
		}

		private void Move(object sender, MouseEventArgs e)
		{
			if (_dragging.HasValue && e.LeftButton == MouseButtonState.Pressed)
			{
				Rect rect = PanelRect();
				Point position = e.GetPosition(this);
				(bool, int) value = _dragging.Value;
				double num = (value.Item1 ? ((position.Y - rect.Top) / rect.Height * 100.0) : ((position.X - rect.Left) / rect.Width * 100.0));
				if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					num = SnapPercent(value.Item1, num);
				}
				num = Math.Clamp(num, 0.5, 99.5);
				if (value.Item1)
				{
					RowSplits[value.Item2] = num;
				}
				else
				{
					ColumnSplits[value.Item2] = num;
				}
				InvalidateVisual();
				return;
			}
			(bool, int)? tuple = Hit(e.GetPosition(this));
			(bool, int)? tuple2 = tuple;
			(bool, int)? hovered = _hovered;
			bool hasValue = tuple2.HasValue;
			if (hasValue == hovered.HasValue)
			{
				if (!hasValue)
				{
					return;
				}
				(bool, int) valueOrDefault = tuple2.GetValueOrDefault();
				(bool, int) valueOrDefault2 = hovered.GetValueOrDefault();
				if (valueOrDefault.Item1 == valueOrDefault2.Item1 && valueOrDefault.Item2 == valueOrDefault2.Item2)
				{
					return;
				}
			}
			_hovered = tuple;
			base.Cursor = (tuple.HasValue ? Cursors.SizeAll : Cursors.Cross);
			InvalidateVisual();
		}

		private void Up(object sender, MouseButtonEventArgs e)
		{
			if (_dragging.HasValue)
			{
				_dragging = null;
				ReleaseMouseCapture();
				Sort();
				InvalidateVisual();
			}
		}

		private void RightDown(object sender, MouseButtonEventArgs e)
		{
			(bool, int)? tuple = Hit(e.GetPosition(this));
			if (tuple.HasValue)
			{
				if (tuple.Value.Item1)
				{
					RowSplits.RemoveAt(tuple.Value.Item2);
				}
				else
				{
					ColumnSplits.RemoveAt(tuple.Value.Item2);
				}
				InvalidateVisual();
				e.Handled = true;
			}
		}

		private (bool row, int index)? Hit(Point point)
		{
			Rect rect = PanelRect();
			for (int i = 0; i < RowSplits.Count; i++)
			{
				if (Math.Abs(point.Y - (rect.Top + rect.Height * RowSplits[i] / 100.0)) <= 9.0 && point.X >= rect.Left && point.X <= rect.Right)
				{
					return (true, i);
				}
			}
			for (int j = 0; j < ColumnSplits.Count; j++)
			{
				if (Math.Abs(point.X - (rect.Left + rect.Width * ColumnSplits[j] / 100.0)) <= 9.0 && point.Y >= rect.Top && point.Y <= rect.Bottom)
				{
					return (false, j);
				}
			}
			return null;
		}

		private void Sort()
		{
			List<double> collection = (from value in RowSplits.Distinct()
				orderby value
				select value).ToList();
			List<double> collection2 = (from value in ColumnSplits.Distinct()
				orderby value
				select value).ToList();
			RowSplits.Clear();
			RowSplits.AddRange(collection);
			ColumnSplits.Clear();
			ColumnSplits.AddRange(collection2);
		}

		public void AddDivider(bool row)
		{
			List<double> list = (row ? RowSplits : ColumnSplits);
			if (list.Count >= 11)
			{
				return;
			}
			List<double> list2 = new List<double> { 0.0 };
			list2.AddRange(list);
			list2.Add(100.0);
			double num = 0.0;
			double num2 = 100.0;
			double num3 = -1.0;
			for (int i = 0; i < list2.Count - 1; i++)
			{
				double num4 = list2[i + 1] - list2[i];
				if (num4 > num3)
				{
					num3 = num4;
					num = list2[i];
					num2 = list2[i + 1];
				}
			}
			list.Add(SnapPercent(row, (num + num2) / 2.0));
			Sort();
			InvalidateVisual();
		}

		public void RemoveDivider(bool row)
		{
			List<double> list = (row ? RowSplits : ColumnSplits);
			if (list.Count > 0)
			{
				list.RemoveAt(list.Count - 1);
				InvalidateVisual();
			}
		}

		public void RemoveSelectedDivider()
		{
			(bool, int)? tuple = _selected ?? _hovered;
			if (!tuple.HasValue)
			{
				return;
			}
			if (tuple.Value.Item1)
			{
				if (tuple.Value.Item2 >= 0 && tuple.Value.Item2 < RowSplits.Count)
				{
					RowSplits.RemoveAt(tuple.Value.Item2);
				}
			}
			else if (tuple.Value.Item2 >= 0 && tuple.Value.Item2 < ColumnSplits.Count)
			{
				ColumnSplits.RemoveAt(tuple.Value.Item2);
			}
			_selected = null;
			_hovered = null;
			InvalidateVisual();
		}

		public void PreviewLast(bool row, bool enabled)
		{
			List<double> list = (row ? RowSplits : ColumnSplits);
			_previewRemoval = ((enabled && list.Count > 0) ? new(bool, int)?((row, list.Count - 1)) : (((bool, int)?)null));
			InvalidateVisual();
		}

		public void ApplyPreset(string preset)
		{
			RowSplits.Clear();
			ColumnSplits.Clear();
			switch (preset)
			{
			case "上下2分割":
				RowSplits.Add(50.0);
				break;
			case "上下3分割":
				RowSplits.AddRange(new double[2] { 33.333333333333336, 66.66666666666667 });
				break;
			case "上下4分割":
				RowSplits.AddRange(new double[3] { 25.0, 50.0, 75.0 });
				break;
			case "左右2分割":
				ColumnSplits.Add(50.0);
				break;
			case "左右3分割":
				ColumnSplits.AddRange(new double[2] { 33.333333333333336, 66.66666666666667 });
				break;
			case "見出し＋本文":
				RowSplits.Add(25.0);
				break;
			case "見出し＋本文2列":
				RowSplits.Add(25.0);
				ColumnSplits.Add(50.0);
				break;
			}
			ResnapAll();
			InvalidateVisual();
		}

		private double SnapPercent(bool row, double percent)
		{
			double num = (row ? _heightMm : _widthMm);
			double num2 = (row ? _originYmm : _originXmm);
			double num3 = Math.Clamp(percent, 0.0, 100.0) / 100.0 * num;
			double num4 = Math.Round((num2 + num3) / _gridMm, MidpointRounding.AwayFromZero) * _gridMm - num2;
			if (num4 <= 0.0 && num > _gridMm)
			{
				num4 += _gridMm;
			}
			if (num4 >= num && num > _gridMm)
			{
				num4 -= _gridMm;
			}
			return Math.Clamp(num4 / num * 100.0, 0.5, 99.5);
		}

		private void ResnapAll()
		{
			for (int i = 0; i < RowSplits.Count; i++)
			{
				RowSplits[i] = SnapPercent(row: true, RowSplits[i]);
			}
			for (int j = 0; j < ColumnSplits.Count; j++)
			{
				ColumnSplits[j] = SnapPercent(row: false, ColumnSplits[j]);
			}
			Sort();
		}
	}

	private readonly DividerSurface _surface;

	private readonly TextBox _color = new TextBox
	{
		Width = 118.0
	};

	private readonly TextBox _thickness = new TextBox
	{
		Width = 72.0
	};

	private readonly Slider _opacity = new Slider
	{
		Minimum = 0.0,
		Maximum = 100.0,
		Width = 125.0,
		TickFrequency = 1.0
	};

	private readonly ComboBox _style = new ComboBox
	{
		Width = 92.0,
		ItemsSource = new string[3] { "実線", "破線", "点線" }
	};

	public List<double> RowSplits => _surface.RowSplits;

	public List<double> ColumnSplits => _surface.ColumnSplits;

	public string DividerColor => _surface.DividerColor;

	public double DividerThicknessPt => _surface.DividerThicknessPt;

	public double DividerOpacity => _surface.DividerOpacity;

	public string DividerStyle => _surface.DividerStyle;

	public PanelDividerEditorDialog(CanvasElementModel model, double gridMm)
	{
		base.Title = "パネル分割線の編集";
		base.Width = 780.0;
		base.Height = 600.0;
		base.MinWidth = 580.0;
		base.MinHeight = 430.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 780.0, 600.0, 580.0, 430.0);
		_surface = new DividerSurface(model, gridMm);
		_color.Text = model.PanelDividerColor;
		_thickness.Text = model.PanelDividerThicknessPt.ToString("0.##", CultureInfo.CurrentCulture);
		_opacity.Value = Math.Clamp(model.PanelDividerOpacity * 100.0, 0.0, 100.0);
		_style.SelectedItem = model.PanelDividerStyle;
		if (_style.SelectedIndex < 0)
		{
			_style.SelectedIndex = 0;
		}
		_color.LostFocus += delegate
		{
			UpdateAppearance();
		};
		_thickness.TextChanged += delegate
		{
			UpdateAppearance();
		};
		_opacity.ValueChanged += delegate
		{
			UpdateAppearance();
		};
		_style.SelectionChanged += delegate
		{
			UpdateAppearance();
		};
		DockPanel dockPanel = new DockPanel
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
			Content = "適用",
			MinWidth = 100.0
		};
		button2.Click += delegate
		{
			if (!UpdateAppearance())
			{
				MessageBox.Show("線の色または太さを確認してください。", "パネル分割線");
			}
			else
			{
				base.DialogResult = true;
			}
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "パネル分割線の編集",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "1. 分割方法を選ぶ\u30002. 線をクリックして選ぶ\u30003. ドラッグまたは数値で位置を調整します。Shiftで自由移動できます。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		dockPanel.Children.Add(stackPanel2);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		wrapPanel.Children.Add(Label("プリセット"));
		ComboBox preset = new ComboBox
		{
			Width = 135.0,
			ItemsSource = new string[7] { "上下2分割", "上下3分割", "上下4分割", "左右2分割", "左右3分割", "見出し＋本文", "見出し＋本文2列" },
			SelectedIndex = 0
		};
		Button button3 = new Button
		{
			Content = "適用",
			MinWidth = 52.0
		};
		button3.Click += delegate
		{
			_surface.ApplyPreset(preset.SelectedItem?.ToString() ?? "上下2分割");
		};
		wrapPanel.Children.Add(preset);
		wrapPanel.Children.Add(button3);
		wrapPanel.Children.Add(Label("線色"));
		wrapPanel.Children.Add(_color);
		Button button4 = new Button
		{
			Content = "色…",
			MinWidth = 58.0
		};
		button4.Click += delegate
		{
			string text = ColorPickerDialog.Show(this, _color.Text);
			if (text != null)
			{
				_color.Text = text;
				UpdateAppearance();
			}
		};
		wrapPanel.Children.Add(button4);
		wrapPanel.Children.Add(Label("太さ (pt)"));
		wrapPanel.Children.Add(_thickness);
		wrapPanel.Children.Add(Label("透明度"));
		wrapPanel.Children.Add(_opacity);
		wrapPanel.Children.Add(Label("線種"));
		wrapPanel.Children.Add(_style);
		Button button5 = new Button
		{
			Content = "上下に分割",
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0),
			ToolTip = "最も広い区画へ横方向の分割線を追加"
		};
		Button button6 = new Button
		{
			Content = "左右に分割",
			ToolTip = "最も広い区画へ縦方向の分割線を追加"
		};
		Button button7 = new Button
		{
			Content = "選択線を削除",
			ToolTip = "プレビューでオレンジ表示されている線を削除"
		};
		button5.Click += delegate
		{
			_surface.AddDivider(row: true);
		};
		button6.Click += delegate
		{
			_surface.AddDivider(row: false);
		};
		button7.Click += delegate
		{
			_surface.RemoveSelectedDivider();
		};
		wrapPanel.Children.Add(button5);
		wrapPanel.Children.Add(button6);
		wrapPanel.Children.Add(button7);
		DockPanel.SetDock(wrapPanel, Dock.Top);
		dockPanel.Children.Add(wrapPanel);
		dockPanel.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(235, 238, 243)),
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(35.0),
			Child = _surface
		});
		base.Content = dockPanel;
	}

	private static TextBlock Label(string text)
	{
		return new TextBlock
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(9.0, 0.0, 4.0, 0.0)
		};
	}

	private bool UpdateAppearance()
	{
		try
		{
			_ = (Color)ColorConverter.ConvertFromString(_color.Text.Trim());
		}
		catch
		{
			return false;
		}
		if (!double.TryParse(_thickness.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var result) || result < 0.0 || result > 30.0)
		{
			return false;
		}
		_surface.DividerColor = _color.Text.Trim();
		_surface.DividerThicknessPt = result;
		_surface.DividerOpacity = _opacity.Value / 100.0;
		_surface.DividerStyle = _style.SelectedItem?.ToString() ?? "実線";
		_surface.InvalidateVisual();
		return true;
	}
}
