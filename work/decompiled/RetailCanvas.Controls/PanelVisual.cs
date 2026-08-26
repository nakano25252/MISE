using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Controls;

public sealed class PanelVisual : FrameworkElement
{
	private readonly CanvasElementModel _model;

	private readonly Brush _fill;

	private readonly Brush _stroke;

	private readonly IReadOnlyList<Brush> _cells;

	private readonly Brush _divider;

	private readonly double _frameDip;

	private readonly double _dividerDip;

	public PanelVisual(CanvasElementModel model, Brush fill, Brush stroke, IReadOnlyList<Brush> cells, Brush divider, double frameDip)
	{
		_model = model;
		_fill = fill;
		_stroke = stroke;
		_cells = cells;
		_divider = divider;
		_frameDip = Math.Max(0.0, frameDip);
		_dividerDip = Math.Max(0.0, model.PanelDividerThicknessPt * 96.0 / 72.0);
		base.IsHitTestVisible = false;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (base.ActualWidth <= 0.0 || base.ActualHeight <= 0.0)
		{
			return;
		}
		Rect rect = new Rect(_frameDip / 2.0, _frameDip / 2.0, Math.Max(0.0, base.ActualWidth - _frameDip), Math.Max(0.0, base.ActualHeight - _frameDip));
		Geometry geometry = Outline(rect);
		geometry.Freeze();
		dc.PushClip(geometry);
		dc.DrawRectangle(_fill, null, rect);
		int num = Math.Clamp(_model.PanelRows, 1, 12);
		int num2 = Math.Clamp(_model.PanelColumns, 1, 12);
		List<double> list = Stops(_model.PanelRowSplits, num);
		List<double> list2 = Stops(_model.PanelColumnSplits, num2);
		for (int i = 0; i < num; i++)
		{
			for (int j = 0; j < num2; j++)
			{
				int num3 = i * num2 + j;
				Brush brush = ((num3 < _cells.Count) ? _cells[num3] : _fill);
				double num4 = rect.Left + rect.Width * list2[j] / 100.0;
				double num5 = rect.Top + rect.Height * list[i] / 100.0;
				double num6 = rect.Left + rect.Width * list2[j + 1] / 100.0;
				double num7 = rect.Top + rect.Height * list[i + 1] / 100.0;
				dc.DrawRectangle(brush, null, new Rect(num4, num5, Math.Max(0.0, num6 - num4), Math.Max(0.0, num7 - num5)));
			}
		}
		if (_dividerDip > 0.01 && _model.PanelDividerOpacity > 0.001)
		{
			Brush brush2 = _divider.Clone();
			brush2.Opacity *= Math.Clamp(_model.PanelDividerOpacity, 0.0, 1.0);
			brush2.Freeze();
			Pen pen = new Pen(brush2, _dividerDip)
			{
				LineJoin = PenLineJoin.Round
			};
			if (_model.PanelDividerStyle == "点線")
			{
				pen.DashStyle = DashStyles.Dot;
			}
			else if (_model.PanelDividerStyle == "破線")
			{
				pen.DashStyle = DashStyles.Dash;
			}
			pen.Freeze();
			foreach (double item in list.Skip(1).SkipLast(1))
			{
				double y = rect.Top + rect.Height * item / 100.0;
				dc.DrawLine(pen, new Point(rect.Left, y), new Point(rect.Right, y));
			}
			foreach (double item2 in list2.Skip(1).SkipLast(1))
			{
				double x = rect.Left + rect.Width * item2 / 100.0;
				dc.DrawLine(pen, new Point(x, rect.Top), new Point(x, rect.Bottom));
			}
		}
		dc.Pop();
		if (_frameDip > 0.01)
		{
			Pen pen2 = new Pen(_stroke, _frameDip)
			{
				LineJoin = PenLineJoin.Round
			};
			pen2.Freeze();
			dc.DrawGeometry(null, pen2, geometry);
		}
	}

	private (double tl, double tr, double br, double bl) CornerRadii(Rect rect)
	{
		double fallback = Math.Max(0.0, _model.CornerRadiusMm * 3.7795275590551185);
		double max = Math.Min(rect.Width, rect.Height) / 2.0;
		return (tl: R(_model.CornerRadiusTopLeftMm), tr: R(_model.CornerRadiusTopRightMm), br: R(_model.CornerRadiusBottomRightMm), bl: R(_model.CornerRadiusBottomLeftMm));
		double R(double value)
		{
			return Math.Clamp((value >= 0.0) ? (value * 3.7795275590551185) : fallback, 0.0, max);
		}
	}

	private Geometry Outline(Rect rect)
	{
		if (_model.ShapePoints.Count >= 3)
		{
			return Polygon(rect, _model.ShapePoints.Select((ShapePointModel point) => new Point(rect.Left + rect.Width * point.X / 100.0, rect.Top + rect.Height * point.Y / 100.0)));
		}
		string shapeType = _model.ShapeType;
		if ((shapeType == "Ellipse" || shapeType == "Circle") ? true : false)
		{
			return new EllipseGeometry(rect);
		}
		if (_model.ShapeType == "SemiCircle")
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
			streamGeometryContext.BeginFigure(new Point(rect.Left, rect.Bottom), isFilled: true, isClosed: true);
			streamGeometryContext.ArcTo(new Point(rect.Right, rect.Bottom), new Size(rect.Width / 2.0, rect.Height), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
			streamGeometryContext.LineTo(new Point(rect.Left, rect.Bottom), isStroked: true, isSmoothJoin: false);
			return streamGeometry;
		}
		if (_model.ShapeType == "Triangle")
		{
			return Polygon(rect, new Point[3]
			{
				new Point(rect.Left + rect.Width / 2.0, rect.Top),
				new Point(rect.Right, rect.Bottom),
				new Point(rect.Left, rect.Bottom)
			});
		}
		if (_model.ShapeType == "Star")
		{
			List<Point> list = new List<Point>();
			for (int num = 0; num < 10; num++)
			{
				double num2 = -Math.PI / 2.0 + (double)num * Math.PI / 5.0;
				double num3 = ((num % 2 == 0) ? 0.5 : 0.22) * rect.Width;
				double num4 = ((num % 2 == 0) ? 0.5 : 0.22) * rect.Height;
				list.Add(new Point(rect.Left + rect.Width / 2.0 + Math.Cos(num2) * num3, rect.Top + rect.Height / 2.0 + Math.Sin(num2) * num4));
			}
			return Polygon(rect, list);
		}
		if (_model.ShapeType == "Heart")
		{
			StreamGeometry streamGeometry2 = new StreamGeometry();
			using StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open();
			streamGeometryContext2.BeginFigure(P(50.0, 92.0), isFilled: true, isClosed: true);
			streamGeometryContext2.BezierTo(P(42.0, 80.0), P(5.0, 58.0), P(8.0, 30.0), isStroked: true, isSmoothJoin: true);
			streamGeometryContext2.BezierTo(P(10.0, 8.0), P(38.0, 3.0), P(50.0, 24.0), isStroked: true, isSmoothJoin: true);
			streamGeometryContext2.BezierTo(P(62.0, 3.0), P(90.0, 8.0), P(92.0, 30.0), isStroked: true, isSmoothJoin: true);
			streamGeometryContext2.BezierTo(P(95.0, 58.0), P(58.0, 80.0), P(50.0, 92.0), isStroked: true, isSmoothJoin: true);
			return streamGeometry2;
		}
		if (_model.ShapeType == "Ring")
		{
			return new GeometryGroup
			{
				FillRule = FillRule.EvenOdd,
				Children = 
				{
					(Geometry)new EllipseGeometry(rect),
					(Geometry)new EllipseGeometry(new Rect(rect.Left + rect.Width * 0.25, rect.Top + rect.Height * 0.25, rect.Width * 0.5, rect.Height * 0.5))
				}
			};
		}
		if (_model.ShapeType == "Diamond")
		{
			return Polygon(rect, new Point[4]
			{
				new Point(rect.Left + rect.Width / 2.0, rect.Top),
				new Point(rect.Right, rect.Top + rect.Height / 2.0),
				new Point(rect.Left + rect.Width / 2.0, rect.Bottom),
				new Point(rect.Left, rect.Top + rect.Height / 2.0)
			});
		}
		if (_model.ShapeType == "Badge")
		{
			List<Point> list2 = new List<Point>();
			for (int num5 = 0; num5 < 24; num5++)
			{
				double num6 = -Math.PI / 2.0 + (double)num5 * Math.PI / 12.0;
				double num7 = ((num5 % 2 == 0) ? 0.49 : 0.41);
				list2.Add(new Point(rect.Left + rect.Width / 2.0 + Math.Cos(num6) * rect.Width * num7, rect.Top + rect.Height / 2.0 + Math.Sin(num6) * rect.Height * num7));
			}
			return Polygon(rect, list2);
		}
		if (_model.ShapeType == "SpeechBubble")
		{
			return Polygon(rect, new Point[7]
			{
				new Point(rect.Left + rect.Width * 0.05, rect.Top + rect.Height * 0.05),
				new Point(rect.Left + rect.Width * 0.95, rect.Top + rect.Height * 0.05),
				new Point(rect.Left + rect.Width * 0.95, rect.Top + rect.Height * 0.75),
				new Point(rect.Left + rect.Width * 0.62, rect.Top + rect.Height * 0.75),
				new Point(rect.Left + rect.Width * 0.48, rect.Top + rect.Height * 0.96),
				new Point(rect.Left + rect.Width * 0.48, rect.Top + rect.Height * 0.75),
				new Point(rect.Left + rect.Width * 0.05, rect.Top + rect.Height * 0.75)
			});
		}
		if (_model.ShapeType == "Label")
		{
			return Polygon(rect, new Point[6]
			{
				new Point(rect.Left, rect.Top + rect.Height * 0.08),
				new Point(rect.Left + rect.Width * 0.82, rect.Top + rect.Height * 0.08),
				new Point(rect.Right, rect.Top + rect.Height * 0.5),
				new Point(rect.Left + rect.Width * 0.82, rect.Top + rect.Height * 0.92),
				new Point(rect.Left, rect.Top + rect.Height * 0.92),
				new Point(rect.Left + rect.Width * 0.12, rect.Top + rect.Height * 0.5)
			});
		}
		if (_model.ShapeType == "Polygon")
		{
			return Polygon(rect, new Point[5]
			{
				new Point(rect.Left + rect.Width * 0.5, rect.Top),
				new Point(rect.Left + rect.Width * 0.97, rect.Top + rect.Height * 0.35),
				new Point(rect.Left + rect.Width * 0.8, rect.Top + rect.Height * 0.95),
				new Point(rect.Left + rect.Width * 0.2, rect.Top + rect.Height * 0.95),
				new Point(rect.Left + rect.Width * 0.03, rect.Top + rect.Height * 0.35)
			});
		}
		return RoundedRect(rect, CornerRadii(rect));
		Point P(double x, double y)
		{
			return new Point(rect.Left + rect.Width * x / 100.0, rect.Top + rect.Height * y / 100.0);
		}
	}

	private static StreamGeometry Polygon(Rect rect, IEnumerable<Point> input)
	{
		List<Point> list = input.ToList();
		if (list.Count < 3)
		{
			return RoundedRect(rect, (tl: 0.0, tr: 0.0, br: 0.0, bl: 0.0));
		}
		StreamGeometry streamGeometry = new StreamGeometry();
		using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
		streamGeometryContext.BeginFigure(list[0], isFilled: true, isClosed: true);
		streamGeometryContext.PolyLineTo(list.Skip(1).ToList(), isStroked: true, isSmoothJoin: true);
		return streamGeometry;
	}

	private static StreamGeometry RoundedRect(Rect r, (double tl, double tr, double br, double bl) c)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
		streamGeometryContext.BeginFigure(new Point(r.Left + c.tl, r.Top), isFilled: true, isClosed: true);
		streamGeometryContext.LineTo(new Point(r.Right - c.tr, r.Top), isStroked: true, isSmoothJoin: false);
		if (c.tr > 0.0)
		{
			streamGeometryContext.QuadraticBezierTo(new Point(r.Right, r.Top), new Point(r.Right, r.Top + c.tr), isStroked: true, isSmoothJoin: false);
		}
		streamGeometryContext.LineTo(new Point(r.Right, r.Bottom - c.br), isStroked: true, isSmoothJoin: false);
		if (c.br > 0.0)
		{
			streamGeometryContext.QuadraticBezierTo(new Point(r.Right, r.Bottom), new Point(r.Right - c.br, r.Bottom), isStroked: true, isSmoothJoin: false);
		}
		streamGeometryContext.LineTo(new Point(r.Left + c.bl, r.Bottom), isStroked: true, isSmoothJoin: false);
		if (c.bl > 0.0)
		{
			streamGeometryContext.QuadraticBezierTo(new Point(r.Left, r.Bottom), new Point(r.Left, r.Bottom - c.bl), isStroked: true, isSmoothJoin: false);
		}
		streamGeometryContext.LineTo(new Point(r.Left, r.Top + c.tl), isStroked: true, isSmoothJoin: false);
		if (c.tl > 0.0)
		{
			streamGeometryContext.QuadraticBezierTo(new Point(r.Left, r.Top), new Point(r.Left + c.tl, r.Top), isStroked: true, isSmoothJoin: false);
		}
		return streamGeometry;
	}

	private static List<double> Stops(List<double> input, int count)
	{
		List<double> list = (from x in input
			where x > 0.0 && x < 100.0
			orderby x
			select x).Take(count - 1).ToList();
		while (list.Count < count - 1)
		{
			list.Add(100.0 * (double)(list.Count + 1) / (double)count);
		}
		list.Sort();
		list.Insert(0, 0.0);
		list.Add(100.0);
		return list;
	}
}
