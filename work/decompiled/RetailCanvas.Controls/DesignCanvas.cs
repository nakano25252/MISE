using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetailCanvas.Controls;

public sealed class DesignCanvas : Canvas
{
	public double MmToDip { get; } = 3.7795275590551185;

	public double SafeMarginMm { get; set; } = 5.0;

	public double BleedMm { get; set; } = 3.0;

	public double PrintMarginMm { get; set; } = 5.0;

	public double GridMm { get; set; } = 5.0;

	public bool ShowGrid { get; set; } = true;

	public bool ShowSafeArea { get; set; } = true;

	public bool ShowBleed { get; set; }

	public bool ShowPrintMargin { get; set; } = true;

	public bool ShowCenterGuides { get; set; } = true;

	public bool ShowVerticalCenterGuide { get; set; } = true;

	public bool ShowHorizontalCenterGuide { get; set; } = true;

	public bool ExportMode { get; set; }

	public DesignCanvas()
	{
		base.ClipToBounds = false;
		base.SnapsToDevicePixels = true;
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		if (ExportMode)
		{
			return;
		}
		if (ShowGrid && GridMm > 0.0)
		{
			double num = GridMm * MmToDip;
			Pen pen = new Pen(new SolidColorBrush(Color.FromArgb(42, 82, 96, 118)), 0.55);
			pen.Freeze();
			for (double num2 = num; num2 < base.ActualWidth; num2 += num)
			{
				dc.DrawLine(pen, new Point(num2, 0.0), new Point(num2, base.ActualHeight));
			}
			for (double num3 = num; num3 < base.ActualHeight; num3 += num)
			{
				dc.DrawLine(pen, new Point(0.0, num3), new Point(base.ActualWidth, num3));
			}
		}
		if (ShowCenterGuides && (ShowVerticalCenterGuide || ShowHorizontalCenterGuide))
		{
			double x = base.ActualWidth / 2.0;
			double y = base.ActualHeight / 2.0;
			Pen pen2 = new Pen(new SolidColorBrush(Color.FromArgb(150, 43, 182, 200)), 1.2)
			{
				DashStyle = new DashStyle(new DoubleCollection { 8.0, 3.0, 2.0, 3.0 }, 0.0)
			};
			pen2.Freeze();
			if (ShowVerticalCenterGuide)
			{
				dc.DrawLine(pen2, new Point(x, 0.0), new Point(x, base.ActualHeight));
			}
			if (ShowHorizontalCenterGuide)
			{
				dc.DrawLine(pen2, new Point(0.0, y), new Point(base.ActualWidth, y));
			}
			if (ShowVerticalCenterGuide && ShowHorizontalCenterGuide)
			{
				dc.DrawEllipse(Brushes.White, pen2, new Point(x, y), 3.0, 3.0);
			}
		}
		if (ShowSafeArea)
		{
			double num4 = SafeMarginMm * MmToDip;
			Pen pen3 = new Pen(new SolidColorBrush(Color.FromArgb(180, 242, 106, 33)), 1.0)
			{
				DashStyle = DashStyles.Dash
			};
			pen3.Freeze();
			dc.DrawRectangle(null, pen3, new Rect(num4, num4, Math.Max(0.0, base.ActualWidth - num4 * 2.0), Math.Max(0.0, base.ActualHeight - num4 * 2.0)));
		}
		if (ShowPrintMargin && PrintMarginMm > 0.0)
		{
			double num5 = PrintMarginMm * MmToDip;
			Pen pen4 = new Pen(new SolidColorBrush(Color.FromArgb(155, 112, 83, 186)), 0.9)
			{
				DashStyle = new DashStyle(new DoubleCollection { 2.0, 3.0 }, 0.0)
			};
			pen4.Freeze();
			dc.DrawRectangle(null, pen4, new Rect(num5, num5, Math.Max(0.0, base.ActualWidth - num5 * 2.0), Math.Max(0.0, base.ActualHeight - num5 * 2.0)));
		}
		if (ShowBleed)
		{
			double num6 = BleedMm * MmToDip;
			Pen pen5 = new Pen(new SolidColorBrush(Color.FromArgb(175, 43, 182, 200)), 1.0)
			{
				DashStyle = DashStyles.Dot
			};
			pen5.Freeze();
			dc.DrawRectangle(null, pen5, new Rect(0.0 - num6, 0.0 - num6, base.ActualWidth + num6 * 2.0, base.ActualHeight + num6 * 2.0));
		}
	}

	public void RefreshGuides()
	{
		InvalidateVisual();
	}
}
