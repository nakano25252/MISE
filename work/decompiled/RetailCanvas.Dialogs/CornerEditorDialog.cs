using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class CornerEditorDialog : Window
{
	private sealed class CornerPreview : FrameworkElement
	{
		private readonly double _widthMm;

		private readonly double _heightMm;

		private readonly Brush _fill;

		private readonly Brush _stroke;

		private double _topLeft;

		private double _topRight;

		private double _bottomRight;

		private double _bottomLeft;

		private string _selected = "四隅すべて";

		public CornerPreview(CanvasElementModel model)
		{
			_widthMm = Math.Max(1.0, model.WidthMm);
			_heightMm = Math.Max(1.0, model.HeightMm);
			_fill = ParseBrush(model.FillColor, Color.FromRgb(242, 106, 33));
			_stroke = ParseBrush(model.StrokeColor, Color.FromRgb(23, 32, 51));
			base.MinWidth = 260.0;
			base.MinHeight = 260.0;
		}

		public void SetCorners(double topLeft, double topRight, double bottomRight, double bottomLeft, string selected)
		{
			_topLeft = Math.Max(0.0, topLeft);
			_topRight = Math.Max(0.0, topRight);
			_bottomRight = Math.Max(0.0, bottomRight);
			_bottomLeft = Math.Max(0.0, bottomLeft);
			_selected = selected;
			InvalidateVisual();
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);
			Rect rectangle = new Rect(12.0, 12.0, Math.Max(20.0, base.ActualWidth - 24.0), Math.Max(20.0, base.ActualHeight - 24.0));
			dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(247, 248, 250)), new Pen(new SolidColorBrush(Color.FromRgb(221, 226, 234)), 1.0), rectangle, 8.0, 8.0);
			Rect rect = new Rect(rectangle.Left + 24.0, rectangle.Top + 36.0, Math.Max(20.0, rectangle.Width - 48.0), Math.Max(20.0, rectangle.Height - 82.0));
			double num = Math.Min(rect.Width / _widthMm, rect.Height / _heightMm);
			double num2 = _widthMm * num;
			double num3 = _heightMm * num;
			Rect rect2 = new Rect(rect.Left + (rect.Width - num2) / 2.0, rect.Top + (rect.Height - num3) / 2.0, num2, num3);
			double val = Math.Min(rect2.Width, rect2.Height) / 2.0;
			double tl = Math.Min(_topLeft * num, val);
			double tr = Math.Min(_topRight * num, val);
			double br = Math.Min(_bottomRight * num, val);
			double bl = Math.Min(_bottomLeft * num, val);
			StreamGeometry geometry = BuildGeometry(rect2, tl, tr, br, bl);
			dc.DrawGeometry(_fill, new Pen(_stroke, 2.0), geometry);
			DrawCornerMarker(dc, new Point(rect2.Left, rect2.Top), IsSelected("左上"));
			DrawCornerMarker(dc, new Point(rect2.Right, rect2.Top), IsSelected("右上"));
			DrawCornerMarker(dc, new Point(rect2.Right, rect2.Bottom), IsSelected("右下"));
			DrawCornerMarker(dc, new Point(rect2.Left, rect2.Bottom), IsSelected("左下"));
			double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
			FormattedText formattedText = new FormattedText($"{_widthMm:0.#} × {_heightMm:0.#} mm\u3000変更中の角をオレンジで表示", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11.0, Brushes.SlateGray, pixelsPerDip);
			dc.DrawText(formattedText, new Point(rectangle.Left + 14.0, rectangle.Bottom - 27.0));
		}

		private bool IsSelected(string corner)
		{
			switch (_selected)
			{
			case "四隅すべて":
			case "四隅を個別":
				return true;
			case "上の2角":
				return (corner == "左上" || corner == "右上") ? true : false;
			case "下の2角":
				return (corner == "左下" || corner == "右下") ? true : false;
			case "左の2角":
				return (corner == "左上" || corner == "左下") ? true : false;
			case "右の2角":
				return (corner == "右上" || corner == "右下") ? true : false;
			default:
				return false;
			}
		}

		private static void DrawCornerMarker(DrawingContext dc, Point point, bool selected)
		{
			SolidColorBrush brush = (selected ? new SolidColorBrush(Color.FromRgb(242, 106, 33)) : Brushes.White);
			SolidColorBrush brush2 = (selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(139, 149, 166)));
			dc.DrawEllipse(brush, new Pen(brush2, 1.5), point, selected ? 5.5 : 4.5, selected ? 5.5 : 4.5);
		}

		private static StreamGeometry BuildGeometry(Rect rect, double tl, double tr, double br, double bl)
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(new Point(rect.Left + tl, rect.Top), isFilled: true, isClosed: true);
				streamGeometryContext.LineTo(new Point(rect.Right - tr, rect.Top), isStroked: true, isSmoothJoin: false);
				ArcOrLine(streamGeometryContext, new Point(rect.Right, rect.Top + tr), tr);
				streamGeometryContext.LineTo(new Point(rect.Right, rect.Bottom - br), isStroked: true, isSmoothJoin: false);
				ArcOrLine(streamGeometryContext, new Point(rect.Right - br, rect.Bottom), br);
				streamGeometryContext.LineTo(new Point(rect.Left + bl, rect.Bottom), isStroked: true, isSmoothJoin: false);
				ArcOrLine(streamGeometryContext, new Point(rect.Left, rect.Bottom - bl), bl);
				streamGeometryContext.LineTo(new Point(rect.Left, rect.Top + tl), isStroked: true, isSmoothJoin: false);
				ArcOrLine(streamGeometryContext, new Point(rect.Left + tl, rect.Top), tl);
			}
			streamGeometry.Freeze();
			return streamGeometry;
		}

		private static void ArcOrLine(StreamGeometryContext context, Point end, double radius)
		{
			if (radius <= 0.01)
			{
				context.LineTo(end, isStroked: true, isSmoothJoin: false);
			}
			else
			{
				context.ArcTo(end, new Size(radius, radius), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
			}
		}

		private static Brush ParseBrush(string value, Color fallback)
		{
			try
			{
				return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
			}
			catch
			{
				return new SolidColorBrush(fallback);
			}
		}
	}

	private readonly CanvasElementModel _model;

	private readonly ComboBox _where = new ComboBox
	{
		ItemsSource = new string[6] { "四隅すべて", "上の2角", "下の2角", "左の2角", "右の2角", "四隅を個別" },
		SelectedIndex = 0
	};

	private readonly ComboBox _how = new ComboBox
	{
		ItemsSource = new string[6] { "丸みなし（直角）", "控えめに丸める", "標準", "大きく丸める", "カプセル", "数値で指定" },
		SelectedIndex = 2
	};

	private readonly Slider _amount = new Slider
	{
		Minimum = 0.0,
		TickFrequency = 0.5,
		IsSnapToTickEnabled = true,
		Width = 250.0
	};

	private readonly TextBlock _amountText = new TextBlock
	{
		Width = 70.0,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly Grid _individual = new Grid();

	private readonly TextBox _tl = new TextBox();

	private readonly TextBox _tr = new TextBox();

	private readonly TextBox _br = new TextBox();

	private readonly TextBox _bl = new TextBox();

	private readonly CornerPreview _preview;

	private bool _applyingPreset;

	private double _pendingTopLeft;

	private double _pendingTopRight;

	private double _pendingBottomRight;

	private double _pendingBottomLeft;

	public double TopLeft { get; private set; }

	public double TopRight { get; private set; }

	public double BottomRight { get; private set; }

	public double BottomLeft { get; private set; }

	public CornerEditorDialog(CanvasElementModel model)
	{
		_model = model;
		base.Title = "角の形 － MISE";
		base.Width = 820.0;
		base.Height = 570.0;
		base.MinWidth = 680.0;
		base.MinHeight = 470.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 820.0, 570.0, 680.0, 470.0);
		double num = Math.Max(0.0, model.CornerRadiusMm);
		TopLeft = (_pendingTopLeft = ((model.CornerRadiusTopLeftMm >= 0.0) ? model.CornerRadiusTopLeftMm : num));
		TopRight = (_pendingTopRight = ((model.CornerRadiusTopRightMm >= 0.0) ? model.CornerRadiusTopRightMm : num));
		BottomRight = (_pendingBottomRight = ((model.CornerRadiusBottomRightMm >= 0.0) ? model.CornerRadiusBottomRightMm : num));
		BottomLeft = (_pendingBottomLeft = ((model.CornerRadiusBottomLeftMm >= 0.0) ? model.CornerRadiusBottomLeftMm : num));
		_preview = new CornerPreview(model);
		_amount.Maximum = Math.Max(0.5, Math.Min(model.WidthMm, model.HeightMm) / 2.0);
		_amount.Value = Math.Clamp(num, 0.0, _amount.Maximum);
		_how.SelectedItem = "数値指定";
		_tl.Text = TopLeft.ToString("0.##");
		_tr.Text = TopRight.ToString("0.##");
		_br.Text = BottomRight.ToString("0.##");
		_bl.Text = BottomLeft.ToString("0.##");
		base.Content = Build();
		_where.SelectionChanged += delegate
		{
			bool flag = _where.SelectedItem?.ToString() == "四隅を個別";
			_individual.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			_amount.IsEnabled = !flag;
			UpdatePreview();
		};
		_how.SelectionChanged += delegate
		{
			ApplyPreset();
		};
		_amount.ValueChanged += delegate
		{
			_amountText.Text = $"{_amount.Value:0.#} mm";
			if (!_applyingPreset)
			{
				_how.SelectedItem = "数値指定";
			}
			ApplyAmountToSelected(_amount.Value);
			UpdatePreview();
		};
		TextBox[] array = new TextBox[4] { _tl, _tr, _br, _bl };
		for (int num2 = 0; num2 < array.Length; num2++)
		{
			array[num2].TextChanged += delegate
			{
				UpdateIndividualValues();
			};
		}
		_amountText.Text = $"{_amount.Value:0.#} mm";
		UpdatePreview();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(20.0)
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
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "適用",
			MinWidth = 100.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button2.Click += delegate
		{
			Accept();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(1.08, GridUnitType.Star)
				},
				new ColumnDefinition
				{
					Width = new GridLength(18.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(0.92, GridUnitType.Star)
				}
			}
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = "角の形",
					FontSize = 22.0,
					FontWeight = FontWeights.Bold
				},
				(UIElement)new TextBlock
				{
					Text = "どこを変えるか → どう変えるか → どれくらい、の順に設定します。",
					Foreground = Brushes.SlateGray,
					Margin = new Thickness(0.0, 3.0, 0.0, 15.0),
					TextWrapping = TextWrapping.Wrap
				},
				Field("1. どこを変えるか", _where),
				Field("2. どのように変えるか", _how)
			}
		};
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel3.Children.Add(_amount);
		stackPanel3.Children.Add(_amountText);
		stackPanel2.Children.Add(Field("3. どれくらい変えるか", stackPanel3));
		BuildIndividual();
		_individual.Visibility = Visibility.Collapsed;
		stackPanel2.Children.Add(_individual);
		stackPanel2.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(245, 247, 249)),
			CornerRadius = new CornerRadius(7.0),
			Padding = new Thickness(10.0),
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
			Child = new TextBlock
			{
				Text = "カプセルは短辺の半分を自動設定します。四隅を個別にすると、左上・右上・右下・左下をそれぞれ数値入力できます。",
				TextWrapping = TextWrapping.Wrap,
				Foreground = Brushes.SlateGray
			}
		});
		grid.Children.Add(stackPanel2);
		DockPanel dockPanel = new DockPanel
		{
			Children = { (UIElement)new TextBlock
			{
				Text = "変更プレビュー",
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
			} }
		};
		DockPanel.SetDock(dockPanel.Children[0], Dock.Top);
		dockPanel.Children.Add(_preview);
		Grid.SetColumn(dockPanel, 2);
		grid.Children.Add(dockPanel);
		obj.Children.Add(grid);
		return obj;
	}

	private static UIElement Field(string label, UIElement control)
	{
		return new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 13.0),
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = label,
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
				},
				control
			}
		};
	}

	private void BuildIndividual()
	{
		_individual.ColumnDefinitions.Add(new ColumnDefinition());
		_individual.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(8.0)
		});
		_individual.ColumnDefinitions.Add(new ColumnDefinition());
		_individual.RowDefinitions.Add(new RowDefinition());
		_individual.RowDefinitions.Add(new RowDefinition());
		AddCorner(_tl, "左上", 0, 0);
		AddCorner(_tr, "右上", 2, 0);
		AddCorner(_bl, "左下", 0, 1);
		AddCorner(_br, "右下", 2, 1);
		void AddCorner(TextBox box, string label, int col, int row)
		{
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(0.0, 2.0, 0.0, 5.0)
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = label + " (mm)"
			});
			stackPanel.Children.Add(box);
			Grid.SetColumn(stackPanel, col);
			Grid.SetRow(stackPanel, row);
			_individual.Children.Add(stackPanel);
		}
	}

	private void ApplyPreset()
	{
		if (_how.SelectedItem?.ToString() == "数値指定")
		{
			UpdatePreview();
			return;
		}
		double maximum = _amount.Maximum;
		double value = _how.SelectedItem?.ToString() switch
		{
			"丸みなし（直角）" => 0.0, 
			"控えめに丸める" => maximum * 0.12, 
			"標準" => maximum * 0.28, 
			"大きく丸める" => maximum * 0.55, 
			"カプセル" => maximum, 
			_ => _amount.Value, 
		};
		_applyingPreset = true;
		_amount.Value = value;
		_applyingPreset = false;
		ApplyAmountToSelected(value);
		UpdatePreview();
	}

	private void ApplyAmountToSelected(double value)
	{
		switch (_where.SelectedItem?.ToString())
		{
		case "上の2角":
			_pendingTopLeft = (_pendingTopRight = value);
			break;
		case "下の2角":
			_pendingBottomLeft = (_pendingBottomRight = value);
			break;
		case "左の2角":
			_pendingTopLeft = (_pendingBottomLeft = value);
			break;
		case "右の2角":
			_pendingTopRight = (_pendingBottomRight = value);
			break;
		default:
			_pendingTopLeft = (_pendingTopRight = (_pendingBottomRight = (_pendingBottomLeft = value)));
			break;
		case "四隅を個別":
			break;
		}
	}

	private void UpdateIndividualValues()
	{
		if (Try(_tl, out var value))
		{
			_pendingTopLeft = value;
		}
		if (Try(_tr, out var value2))
		{
			_pendingTopRight = value2;
		}
		if (Try(_br, out var value3))
		{
			_pendingBottomRight = value3;
		}
		if (Try(_bl, out var value4))
		{
			_pendingBottomLeft = value4;
		}
		UpdatePreview();
	}

	private void UpdatePreview()
	{
		_preview.SetCorners(_pendingTopLeft, _pendingTopRight, _pendingBottomRight, _pendingBottomLeft, _where.SelectedItem?.ToString() ?? "四隅すべて");
	}

	private void Accept()
	{
		if (_where.SelectedItem?.ToString() == "四隅を個別" && (!Try(_tl, out var value) || !Try(_tr, out value) || !Try(_br, out value) || !Try(_bl, out value)))
		{
			MessageBox.Show("四隅の値を0以上で入力してください。", "角の形");
			return;
		}
		TopLeft = _pendingTopLeft;
		TopRight = _pendingTopRight;
		BottomRight = _pendingBottomRight;
		BottomLeft = _pendingBottomLeft;
		base.DialogResult = true;
	}

	private bool Try(TextBox box, out double value)
	{
		if (double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) && value >= 0.0)
		{
			return value <= _amount.Maximum;
		}
		return false;
	}
}
