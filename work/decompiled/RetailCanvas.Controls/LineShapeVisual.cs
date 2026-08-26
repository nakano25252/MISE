using System;
using System.Windows;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Controls;

public sealed class LineShapeVisual : FrameworkElement
{
	private readonly CanvasElementModel _model;

	private readonly Brush _brush;

	private readonly double _thickness;

	public LineShapeVisual(CanvasElementModel model, Brush brush, double thickness)
	{
		_model = model;
		_brush = brush;
		_thickness = Math.Max(1.0, thickness);
		base.IsHitTestVisible = false;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		double y = base.ActualHeight / 2.0;
		Point point = new Point(Math.Max(1.0, _thickness), y);
		Point point2 = new Point(Math.Max(point.X + 1.0, base.ActualWidth - _thickness), y);
		Pen pen = new Pen(_brush, _thickness)
		{
			StartLineCap = PenLineCap.Round,
			EndLineCap = PenLineCap.Round
		};
		if (_model.LineStyle == "破線")
		{
			pen.DashStyle = DashStyles.Dash;
		}
		else if (_model.LineStyle == "点線")
		{
			pen.DashStyle = DashStyles.Dot;
		}
		pen.Freeze();
		dc.DrawLine(pen, point, point2);
		DrawCap(dc, point, new Vector(1.0, 0.0), _model.LineStartCap);
		DrawCap(dc, point2, new Vector(-1.0, 0.0), _model.LineEndCap);
	}

	private void DrawCap(DrawingContext dc, Point tip, Vector direction, string cap)
	{
		if (cap == "なし")
		{
			return;
		}
		double num = Math.Clamp(_model.ArrowSize, 3.0, 30.0);
		switch (cap)
		{
		case "丸":
			dc.DrawEllipse(_brush, null, tip, num / 2.5, num / 2.5);
			return;
		case "四角":
			dc.DrawRectangle(_brush, null, new Rect(tip.X - num / 2.5, tip.Y - num / 2.5, num / 1.25, num / 1.25));
			return;
		case "ひし形":
		{
			Point point = tip + direction * (num * 0.55);
			Point point2 = tip + direction * num;
			Vector vector = new Vector(0.0 - direction.Y, direction.X) * (num * 0.38);
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				streamGeometryContext.BeginFigure(tip, isFilled: true, isClosed: true);
				streamGeometryContext.LineTo(point + vector, isStroked: true, isSmoothJoin: false);
				streamGeometryContext.LineTo(point2, isStroked: true, isSmoothJoin: false);
				streamGeometryContext.LineTo(point - vector, isStroked: true, isSmoothJoin: false);
			}
			dc.DrawGeometry(_brush, null, streamGeometry);
			return;
		}
		}
		Point point3 = tip + direction * num;
		double num2 = ((cap == "細型矢印") ? 0.3 : ((cap == "幅広矢印") ? 0.85 : 0.55));
		Vector vector2 = new Vector(0.0 - direction.Y, direction.X) * (num * num2);
		StreamGeometry streamGeometry2 = new StreamGeometry();
		bool flag;
		switch (cap)
		{
		case "開き矢印":
		case "V字":
		case "山形":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		bool flag2 = flag;
		bool flag3 = cap == "中抜き矢印";
		using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
		{
			streamGeometryContext2.BeginFigure(tip, !flag2 && !flag3, !flag2);
			streamGeometryContext2.LineTo(point3 + vector2, isStroked: true, isSmoothJoin: false);
			if (!flag2)
			{
				streamGeometryContext2.LineTo(point3 - vector2, isStroked: true, isSmoothJoin: false);
			}
			else
			{
				streamGeometryContext2.BeginFigure(tip, isFilled: false, isClosed: false);
				streamGeometryContext2.LineTo(point3 - vector2, isStroked: true, isSmoothJoin: false);
			}
		}
		Pen pen = ((flag2 || flag3) ? new Pen(_brush, _thickness) : null);
		dc.DrawGeometry((flag2 || flag3) ? null : _brush, pen, streamGeometry2);
		if (cap == "山形")
		{
			Point point4 = tip + direction * (num * 0.38);
			Point point5 = point4 + direction * (num * 0.75);
			Vector vector3 = new Vector(0.0 - direction.Y, direction.X) * (num * 0.42);
			StreamGeometry streamGeometry3 = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext3 = streamGeometry3.Open())
			{
				streamGeometryContext3.BeginFigure(point4, isFilled: false, isClosed: false);
				streamGeometryContext3.LineTo(point5 + vector3, isStroked: true, isSmoothJoin: false);
				streamGeometryContext3.BeginFigure(point4, isFilled: false, isClosed: false);
				streamGeometryContext3.LineTo(point5 - vector3, isStroked: true, isSmoothJoin: false);
			}
			dc.DrawGeometry(null, new Pen(_brush, _thickness), streamGeometry3);
		}
	}
}
