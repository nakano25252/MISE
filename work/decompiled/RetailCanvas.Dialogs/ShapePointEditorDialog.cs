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

public sealed class ShapePointEditorDialog : Window
{
	private sealed class PointPreview : FrameworkElement
	{
		private readonly double _widthMm;

		private readonly double _heightMm;

		private readonly double _originXmm;

		private readonly double _originYmm;

		private int _dragIndex = -1;

		public List<ShapePointModel> Points { get; private set; }

		public bool Closed { get; set; }

		public bool SnapEnabled { get; set; }

		public double GridMm { get; set; } = 1.0;

		public string SnapMode { get; set; } = "交点のみ";

		public event EventHandler? PointsChanged;

		public PointPreview(double widthMm, double heightMm, double originXmm, double originYmm, IEnumerable<ShapePointModel> points)
		{
			_widthMm = widthMm;
			_heightMm = heightMm;
			_originXmm = originXmm;
			_originYmm = originYmm;
			Points = points.Select((ShapePointModel x) => new ShapePointModel
			{
				X = x.X,
				Y = x.Y
			}).ToList();
			base.Cursor = Cursors.Cross;
			base.Focusable = true;
		}

		public void Reset(IEnumerable<ShapePointModel> points)
		{
			Points = points.Select((ShapePointModel x) => new ShapePointModel
			{
				X = x.X,
				Y = x.Y
			}).ToList();
			InvalidateVisual();
			this.PointsChanged?.Invoke(this, EventArgs.Empty);
		}

		public void ResnapAll()
		{
			if (SnapEnabled && !(GridMm <= 0.0))
			{
				Points = Points.Select(Snap).ToList();
				InvalidateVisual();
				this.PointsChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);
			Focus();
			Point position = e.GetPosition(this);
			_dragIndex = HitTestPoint(position);
			if (_dragIndex < 0 && e.ClickCount >= 2 && ShapeRect().Contains(position))
			{
				ShapePointModel shapePointModel = Normalize(position);
				int num = ClosestSegment(shapePointModel);
				Points.Insert(num + 1, shapePointModel);
				_dragIndex = num + 1;
				this.PointsChanged?.Invoke(this, EventArgs.Empty);
			}
			if (_dragIndex >= 0)
			{
				CaptureMouse();
				e.Handled = true;
				InvalidateVisual();
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (_dragIndex >= 0 && e.LeftButton == MouseButtonState.Pressed)
			{
				ShapePointModel shapePointModel = Normalize(e.GetPosition(this));
				if (SnapEnabled && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
				{
					shapePointModel = Snap(shapePointModel);
				}
				Points[_dragIndex] = shapePointModel;
				InvalidateVisual();
				this.PointsChanged?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);
			if (_dragIndex >= 0)
			{
				_dragIndex = -1;
				ReleaseMouseCapture();
				e.Handled = true;
			}
		}

		protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseRightButtonDown(e);
			int num = HitTestPoint(e.GetPosition(this));
			if (num >= 0 && Points.Count > 2)
			{
				Points.RemoveAt(num);
				InvalidateVisual();
				this.PointsChanged?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}

		private Rect ShapeRect()
		{
			double num = Math.Max(10.0, base.ActualWidth - 32.0);
			double num2 = Math.Min(val2: Math.Max(10.0, base.ActualHeight - 32.0) / _heightMm, val1: num / _widthMm);
			double num3 = _widthMm * num2;
			double num4 = _heightMm * num2;
			return new Rect((base.ActualWidth - num3) / 2.0, (base.ActualHeight - num4) / 2.0, num3, num4);
		}

		private ShapePointModel Normalize(Point point)
		{
			Rect rect = ShapeRect();
			return new ShapePointModel
			{
				X = Math.Clamp((point.X - rect.Left) / rect.Width * 100.0, 0.0, 100.0),
				Y = Math.Clamp((point.Y - rect.Top) / rect.Height * 100.0, 0.0, 100.0)
			};
		}

		private ShapePointModel Snap(ShapePointModel point)
		{
			double num = point.X / 100.0 * _widthMm;
			double num2 = point.Y / 100.0 * _heightMm;
			double num3 = Math.Clamp((Math.Round((_originXmm + num) / GridMm, MidpointRounding.AwayFromZero) * GridMm - _originXmm) / _widthMm * 100.0, 0.0, 100.0);
			double num4 = Math.Clamp((Math.Round((_originYmm + num2) / GridMm, MidpointRounding.AwayFromZero) * GridMm - _originYmm) / _heightMm * 100.0, 0.0, 100.0);
			if (SnapMode == "線上も許可")
			{
				double num5 = Math.Abs(num3 - point.X) / 100.0 * _widthMm;
				double num6 = Math.Abs(num4 - point.Y) / 100.0 * _heightMm;
				if (!(num5 <= num6))
				{
					return new ShapePointModel
					{
						X = point.X,
						Y = num4
					};
				}
				return new ShapePointModel
				{
					X = num3,
					Y = point.Y
				};
			}
			return new ShapePointModel
			{
				X = num3,
				Y = num4
			};
		}

		private Point ToScreen(ShapePointModel point)
		{
			Rect rect = ShapeRect();
			return new Point(rect.Left + point.X / 100.0 * rect.Width, rect.Top + point.Y / 100.0 * rect.Height);
		}

		private int HitTestPoint(Point point)
		{
			for (int num = Points.Count - 1; num >= 0; num--)
			{
				if ((ToScreen(Points[num]) - point).Length <= 11.0)
				{
					return num;
				}
			}
			return -1;
		}

		private int ClosestSegment(ShapePointModel point)
		{
			if (Points.Count < 2)
			{
				return Math.Max(0, Points.Count - 1);
			}
			double num = double.MaxValue;
			int result = 0;
			int num2 = (Closed ? Points.Count : (Points.Count - 1));
			for (int i = 0; i < num2; i++)
			{
				ShapePointModel shapePointModel = Points[i];
				ShapePointModel shapePointModel2 = Points[(i + 1) % Points.Count];
				double num3 = shapePointModel2.X - shapePointModel.X;
				double num4 = shapePointModel2.Y - shapePointModel.Y;
				double num5 = num3 * num3 + num4 * num4;
				double num6 = ((num5 <= 0.001) ? 0.0 : Math.Clamp(((point.X - shapePointModel.X) * num3 + (point.Y - shapePointModel.Y) * num4) / num5, 0.0, 1.0));
				double num7 = Math.Pow(point.X - (shapePointModel.X + num3 * num6), 2.0) + Math.Pow(point.Y - (shapePointModel.Y + num4 * num6), 2.0);
				if (num7 < num)
				{
					num = num7;
					result = i;
				}
			}
			return result;
		}

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);
			Rect rectangle = ShapeRect();
			dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(byte.MaxValue, 246, 239)), new Pen(new SolidColorBrush(Color.FromRgb(242, 106, 33)), 1.0), rectangle);
			Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(48, 60, 75, 95)), 0.5);
			double num = Math.Ceiling(_originXmm / GridMm) * GridMm;
			double num2 = 0.0;
			while (num <= _originXmm + _widthMm + 0.0001 && num2 < 500.0)
			{
				double num3 = num - _originXmm;
				if (!(num3 <= 0.0001) && !(num3 >= _widthMm - 0.0001))
				{
					double x = rectangle.Left + rectangle.Width * num3 / _widthMm;
					dc.DrawLine(pen, new Point(x, rectangle.Top), new Point(x, rectangle.Bottom));
				}
				num += GridMm;
				num2 += 1.0;
			}
			double num4 = Math.Ceiling(_originYmm / GridMm) * GridMm;
			double num5 = 0.0;
			while (num4 <= _originYmm + _heightMm + 0.0001 && num5 < 500.0)
			{
				double num6 = num4 - _originYmm;
				if (!(num6 <= 0.0001) && !(num6 >= _heightMm - 0.0001))
				{
					double y = rectangle.Top + rectangle.Height * num6 / _heightMm;
					dc.DrawLine(pen, new Point(rectangle.Left, y), new Point(rectangle.Right, y));
				}
				num4 += GridMm;
				num5 += 1.0;
			}
			if (Points.Count >= 2)
			{
				StreamGeometry streamGeometry = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
				{
					streamGeometryContext.BeginFigure(ToScreen(Points[0]), Closed, Closed);
					streamGeometryContext.PolyLineTo(Points.Skip(1).Select(ToScreen).ToList(), isStroked: true, isSmoothJoin: true);
				}
				dc.DrawGeometry(Closed ? new SolidColorBrush(Color.FromArgb(40, 242, 106, 33)) : null, new Pen(new SolidColorBrush(Color.FromRgb(242, 106, 33)), 2.0), streamGeometry);
				for (int i = 0; i < Points.Count; i++)
				{
					Point center = ToScreen(Points[i]);
					dc.DrawEllipse((i == _dragIndex) ? Brushes.Orange : Brushes.White, new Pen(Brushes.DarkCyan, 1.5), center, 6.0, 6.0);
					FormattedText formattedText = new FormattedText((i + 1).ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10.0, Brushes.DarkSlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
					dc.DrawText(formattedText, new Point(center.X + 7.0, center.Y - 15.0));
				}
			}
		}
	}

	private readonly TextBox _grid = new TextBox
	{
		Text = "1",
		Width = 72.0
	};

	private readonly CheckBox _snap = new CheckBox
	{
		Content = "グリッドへ吸着",
		IsChecked = true,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly CheckBox _closed = new CheckBox
	{
		Content = "パスを閉じて塗りつぶす"
	};

	private readonly ComboBox _snapMode = new ComboBox
	{
		Width = 130.0,
		ItemsSource = new string[2] { "交点のみ", "線上も許可" }
	};

	private readonly PointPreview _preview;

	private readonly List<ShapePointModel> _originalPoints;

	private readonly bool _initialClosed;

	private bool _restoreOriginalShape = true;

	public List<ShapePointModel>? Result { get; private set; }

	public bool IsClosedPath => _closed.IsChecked == true;

	public bool RestoreOriginalShape => _restoreOriginalShape;

	public string OriginalShapeType { get; }

	public bool InitialClosedPath => _initialClosed;

	public bool HadOriginalPoints { get; }

	public ShapePointEditorDialog(CanvasElementModel model, double defaultGridMm, string defaultSnapMode = "交点のみ")
	{
		base.Title = "頂点・精密編集";
		base.Width = 840.0;
		base.Height = 650.0;
		base.MinWidth = 600.0;
		base.MinHeight = 460.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 840.0, 650.0, 600.0, 460.0);
		OriginalShapeType = model.ShapeType;
		HadOriginalPoints = model.ShapePoints.Count > 0;
		List<ShapePointModel> list = ((model.ShapePoints.Count > 0) ? model.ShapePoints : DefaultPoints(model.ShapeType));
		_originalPoints = list.Select((ShapePointModel x) => new ShapePointModel
		{
			X = x.X,
			Y = x.Y
		}).ToList();
		_initialClosed = model.ShapePoints.Count <= 0 || model.ShapeClosed;
		_preview = new PointPreview(Math.Max(1.0, model.WidthMm), Math.Max(1.0, model.HeightMm), model.Xmm, model.Ymm, list);
		_grid.Text = Math.Clamp(defaultGridMm, 0.1, 10.0).ToString("0.#");
		_closed.IsChecked = _initialClosed;
		_preview.Closed = _initialClosed;
		_preview.SnapEnabled = true;
		_preview.GridMm = Math.Clamp(defaultGridMm, 0.1, 10.0);
		_snapMode.SelectedItem = defaultSnapMode;
		if (_snapMode.SelectedIndex < 0)
		{
			_snapMode.SelectedIndex = 0;
		}
		_preview.SnapMode = _snapMode.SelectedItem?.ToString() ?? "交点のみ";
		_preview.PointsChanged += delegate
		{
			_restoreOriginalShape = false;
		};
		_snap.Click += delegate
		{
			_preview.SnapEnabled = _snap.IsChecked == true;
			if (_preview.SnapEnabled)
			{
				_preview.ResnapAll();
			}
		};
		_snapMode.SelectionChanged += delegate
		{
			_preview.SnapMode = _snapMode.SelectedItem?.ToString() ?? "交点のみ";
			if (_preview.SnapEnabled)
			{
				_preview.ResnapAll();
			}
		};
		_closed.Click += delegate
		{
			_preview.Closed = _closed.IsChecked == true;
			_restoreOriginalShape = _preview.Closed == _initialClosed && SamePoints(_preview.Points, _originalPoints);
			_preview.InvalidateVisual();
		};
		_grid.TextChanged += delegate
		{
			if (double.TryParse(_grid.Text, out var result) && result > 0.0)
			{
				_preview.GridMm = Math.Clamp(result, 0.1, 100.0);
				if (_preview.SnapEnabled)
				{
					_preview.ResnapAll();
				}
			}
		};
		Build();
		_preview.ResnapAll();
	}

	private void Build()
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition());
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "頂点・精密編集",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "点をドラッグして変形／線上をダブルクリックして点を追加／点を右クリックして削除。Shift中は吸着を解除します。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
		});
		grid.Children.Add(stackPanel);
		Border element = new Border
		{
			Background = Brushes.White,
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(12.0),
			Child = _preview
		};
		Grid.SetRow(element, 1);
		grid.Children.Add(element);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		wrapPanel.Children.Add(_snap);
		wrapPanel.Children.Add(new TextBlock
		{
			Text = "間隔 (mm)",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(16.0, 0.0, 5.0, 0.0)
		});
		wrapPanel.Children.Add(_grid);
		wrapPanel.Children.Add(new TextBlock
		{
			Text = "吸着先",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(16.0, 0.0, 5.0, 0.0)
		});
		wrapPanel.Children.Add(_snapMode);
		_closed.Margin = new Thickness(18.0, 0.0, 0.0, 0.0);
		wrapPanel.Children.Add(_closed);
		Button button = new Button
		{
			Content = "元の形に戻す",
			ToolTip = "精密編集を開いた時点の形へ戻します",
			Margin = new Thickness(18.0, 0.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			_preview.Reset(_originalPoints);
			_closed.IsChecked = _initialClosed;
			_preview.Closed = _initialClosed;
			_restoreOriginalShape = true;
			_preview.InvalidateVisual();
		};
		wrapPanel.Children.Add(button);
		Grid.SetRow(wrapPanel, 2);
		grid.Children.Add(wrapPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
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
			MinWidth = 100.0
		};
		button3.Click += delegate
		{
			if (_preview.Points.Count < 2)
			{
				MessageBox.Show("頂点を2点以上残してください。", "頂点編集");
			}
			else
			{
				Result = _preview.Points.Select((ShapePointModel x) => new ShapePointModel
				{
					X = x.X,
					Y = x.Y
				}).ToList();
				base.DialogResult = true;
			}
		};
		stackPanel2.Children.Add(button2);
		stackPanel2.Children.Add(button3);
		Grid.SetRow(stackPanel2, 3);
		grid.Children.Add(stackPanel2);
		base.Content = grid;
	}

	private static List<ShapePointModel> DefaultPoints(string type)
	{
		bool flag;
		switch (type)
		{
		case "Triangle":
			return new List<ShapePointModel>
			{
				new ShapePointModel
				{
					X = 50.0,
					Y = 0.0
				},
				new ShapePointModel
				{
					X = 100.0,
					Y = 100.0
				},
				new ShapePointModel
				{
					X = 0.0,
					Y = 100.0
				}
			};
		case "Star":
		{
			List<ShapePointModel> list = new List<ShapePointModel>();
			for (int i = 0; i < 10; i++)
			{
				double num = -Math.PI / 2.0 + (double)i * Math.PI / 5.0;
				double num2 = ((i % 2 == 0) ? 50.0 : 22.0);
				list.Add(new ShapePointModel
				{
					X = 50.0 + Math.Cos(num) * num2,
					Y = 50.0 + Math.Sin(num) * num2
				});
			}
			return list;
		}
		case "Ellipse":
		case "Circle":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			List<ShapePointModel> list2 = new List<ShapePointModel>();
			for (int j = 0; j < 32; j++)
			{
				double num3 = -Math.PI / 2.0 + (double)j * Math.PI * 2.0 / 32.0;
				list2.Add(new ShapePointModel
				{
					X = 50.0 + Math.Cos(num3) * 50.0,
					Y = 50.0 + Math.Sin(num3) * 50.0
				});
			}
			return list2;
		}
		switch (type)
		{
		case "SemiCircle":
		{
			List<ShapePointModel> list3 = new List<ShapePointModel>();
			for (int k = 0; k <= 20; k++)
			{
				double num4 = Math.PI + (double)k * Math.PI / 20.0;
				list3.Add(new ShapePointModel
				{
					X = 50.0 + Math.Cos(num4) * 50.0,
					Y = 100.0 + Math.Sin(num4) * 100.0
				});
			}
			return list3;
		}
		case "Heart":
		{
			List<ShapePointModel> result = new List<ShapePointModel>();
			AddBezier(result, (x: 50.0, y: 92.0), (x: 42.0, y: 80.0), (x: 5.0, y: 58.0), (x: 8.0, y: 30.0));
			AddBezier(result, (x: 8.0, y: 30.0), (x: 10.0, y: 8.0), (x: 38.0, y: 3.0), (x: 50.0, y: 24.0), skipFirst: true);
			AddBezier(result, (x: 50.0, y: 24.0), (x: 62.0, y: 3.0), (x: 90.0, y: 8.0), (x: 92.0, y: 30.0), skipFirst: true);
			AddBezier(result, (x: 92.0, y: 30.0), (x: 95.0, y: 58.0), (x: 58.0, y: 80.0), (x: 50.0, y: 92.0), skipFirst: true);
			return result;
		}
		case "SpeechBubble":
			return new List<ShapePointModel>
			{
				new ShapePointModel
				{
					X = 5.0,
					Y = 5.0
				},
				new ShapePointModel
				{
					X = 95.0,
					Y = 5.0
				},
				new ShapePointModel
				{
					X = 95.0,
					Y = 75.0
				},
				new ShapePointModel
				{
					X = 62.0,
					Y = 75.0
				},
				new ShapePointModel
				{
					X = 48.0,
					Y = 96.0
				},
				new ShapePointModel
				{
					X = 48.0,
					Y = 75.0
				},
				new ShapePointModel
				{
					X = 5.0,
					Y = 75.0
				}
			};
		case "Polygon":
			return new List<ShapePointModel>
			{
				new ShapePointModel
				{
					X = 50.0,
					Y = 0.0
				},
				new ShapePointModel
				{
					X = 97.0,
					Y = 35.0
				},
				new ShapePointModel
				{
					X = 80.0,
					Y = 95.0
				},
				new ShapePointModel
				{
					X = 20.0,
					Y = 95.0
				},
				new ShapePointModel
				{
					X = 3.0,
					Y = 35.0
				}
			};
		case "Line":
			return new List<ShapePointModel>
			{
				new ShapePointModel
				{
					X = 0.0,
					Y = 50.0
				},
				new ShapePointModel
				{
					X = 100.0,
					Y = 50.0
				}
			};
		default:
			return new List<ShapePointModel>
			{
				new ShapePointModel
				{
					X = 0.0,
					Y = 0.0
				},
				new ShapePointModel
				{
					X = 100.0,
					Y = 0.0
				},
				new ShapePointModel
				{
					X = 100.0,
					Y = 100.0
				},
				new ShapePointModel
				{
					X = 0.0,
					Y = 100.0
				}
			};
		}
		static void AddBezier(List<ShapePointModel> list4, (double x, double y) p0, (double x, double y) p1, (double x, double y) p2, (double x, double y) p3, bool skipFirst = false)
		{
			for (int l = (skipFirst ? 1 : 0); l <= 8; l++)
			{
				double num5 = (double)l / 8.0;
				double num6 = 1.0 - num5;
				list4.Add(new ShapePointModel
				{
					X = num6 * num6 * num6 * p0.x + 3.0 * num6 * num6 * num5 * p1.x + 3.0 * num6 * num5 * num5 * p2.x + num5 * num5 * num5 * p3.x,
					Y = num6 * num6 * num6 * p0.y + 3.0 * num6 * num6 * num5 * p1.y + 3.0 * num6 * num5 * num5 * p2.y + num5 * num5 * num5 * p3.y
				});
			}
		}
	}

	private static bool SamePoints(IReadOnlyList<ShapePointModel> left, IReadOnlyList<ShapePointModel> right)
	{
		if (left.Count != right.Count)
		{
			return false;
		}
		for (int i = 0; i < left.Count; i++)
		{
			if (Math.Abs(left[i].X - right[i].X) > 0.0001 || Math.Abs(left[i].Y - right[i].Y) > 0.0001)
			{
				return false;
			}
		}
		return true;
	}
}
