using System;

namespace RetailCanvas.Controls;

internal readonly record struct ResizeBounds(double X, double Y, double Width, double Height)
{
	public double Right => X + Width;

	public double Bottom => Y + Height;
}

internal static class ResizeMath
{
	public static ResizeBounds Calculate(ResizeBounds start, string direction, double dx, double dy, bool preserveAspect, double minimumWidth, double minimumHeight)
	{
		double x = start.X;
		double y = start.Y;
		double width = start.Width;
		double height = start.Height;
		if (direction.Contains('W'))
		{
			x = start.X + dx;
			width = start.Width - dx;
		}
		if (direction.Contains('E'))
		{
			width = start.Width + dx;
		}
		if (direction.Contains('N'))
		{
			y = start.Y + dy;
			height = start.Height - dy;
		}
		if (direction.Contains('S'))
		{
			height = start.Height + dy;
		}
		if (preserveAspect && start.Width > 0.0 && start.Height > 0.0)
		{
			bool horizontal = direction.Contains('E') || direction.Contains('W');
			bool vertical = direction.Contains('N') || direction.Contains('S');
			double scaleX = width / start.Width;
			double scaleY = height / start.Height;
			double scale;
			if (horizontal && vertical)
			{
				scale = (Math.Abs(dx) / start.Width >= Math.Abs(dy) / start.Height) ? scaleX : scaleY;
			}
			else if (horizontal)
			{
				scale = scaleX;
			}
			else if (vertical)
			{
				scale = scaleY;
			}
			else
			{
				scale = 1.0;
			}
			double minimumScale = Math.Max(minimumWidth / start.Width, minimumHeight / start.Height);
			scale = Math.Max(minimumScale, scale);
			width = start.Width * scale;
			height = start.Height * scale;
			if (horizontal && !vertical)
			{
				y = start.Y + (start.Height - height) / 2.0;
			}
			else if (vertical && !horizontal)
			{
				x = start.X + (start.Width - width) / 2.0;
			}
		}
		else
		{
			width = Math.Max(minimumWidth, width);
			height = Math.Max(minimumHeight, height);
		}
		if (direction.Contains('W'))
		{
			x = start.Right - width;
		}
		if (direction.Contains('N'))
		{
			y = start.Bottom - height;
		}
		return new ResizeBounds(x, y, width, height);
	}

	public static ResizeBounds ApplyUniformScale(ResizeBounds start, string direction, double scale)
	{
		scale = Math.Max(0.0001, double.IsFinite(scale) ? scale : 1.0);
		double width = start.Width * scale;
		double height = start.Height * scale;
		bool horizontal = direction.Contains('E') || direction.Contains('W');
		bool vertical = direction.Contains('N') || direction.Contains('S');
		double x = direction.Contains('W') ? start.Right - width : start.X;
		double y = direction.Contains('N') ? start.Bottom - height : start.Y;
		if (horizontal && !vertical)
		{
			y = start.Y + (start.Height - height) / 2.0;
		}
		else if (vertical && !horizontal)
		{
			x = start.X + (start.Width - width) / 2.0;
		}
		return new ResizeBounds(x, y, width, height);
	}
}

internal enum DimensionAxis
{
	Width,
	Height
}

internal readonly record struct DimensionResult(double Width, double Height, double UniformScale);

internal static class DimensionMath
{
	public const double GeneralMinimumMm = 1.0;

	public const double QrMinimumMm = 5.0;

	public static DimensionResult Apply(double currentWidth, double currentHeight, double requestedValue, DimensionAxis axis, bool preserveAspect, double minimumWidth, double minimumHeight)
	{
		minimumWidth = Math.Max(0.0001, FiniteOr(minimumWidth, 0.0001));
		minimumHeight = Math.Max(0.0001, FiniteOr(minimumHeight, 0.0001));
		currentWidth = Math.Max(minimumWidth, FiniteOr(currentWidth, minimumWidth));
		currentHeight = Math.Max(minimumHeight, FiniteOr(currentHeight, minimumHeight));
		requestedValue = FiniteOr(requestedValue, axis == DimensionAxis.Width ? currentWidth : currentHeight);

		if (!preserveAspect)
		{
			double width = axis == DimensionAxis.Width ? Math.Max(minimumWidth, requestedValue) : currentWidth;
			double height = axis == DimensionAxis.Height ? Math.Max(minimumHeight, requestedValue) : currentHeight;
			double scale = axis == DimensionAxis.Width ? width / currentWidth : height / currentHeight;
			return new DimensionResult(width, height, scale);
		}

		double aspect = currentWidth / currentHeight;
		double linkedWidth;
		double linkedHeight;
		if (axis == DimensionAxis.Width)
		{
			linkedWidth = Math.Max(minimumWidth, requestedValue);
			linkedHeight = linkedWidth / aspect;
			if (linkedHeight < minimumHeight)
			{
				linkedHeight = minimumHeight;
				linkedWidth = linkedHeight * aspect;
			}
		}
		else
		{
			linkedHeight = Math.Max(minimumHeight, requestedValue);
			linkedWidth = linkedHeight * aspect;
			if (linkedWidth < minimumWidth)
			{
				linkedWidth = minimumWidth;
				linkedHeight = linkedWidth / aspect;
			}
		}
		return new DimensionResult(linkedWidth, linkedHeight, linkedWidth / currentWidth);
	}

	public static double ClampTextScale(double currentFontSizePt, double requestedScale, double minimumFontSizePt = 3.0, double maximumFontSizePt = 300.0)
	{
		currentFontSizePt = Math.Max(0.0001, FiniteOr(currentFontSizePt, minimumFontSizePt));
		requestedScale = Math.Max(0.0001, FiniteOr(requestedScale, 1.0));
		return Math.Clamp(currentFontSizePt * requestedScale, minimumFontSizePt, maximumFontSizePt) / currentFontSizePt;
	}

	private static double FiniteOr(double value, double fallback)
	{
		return double.IsFinite(value) ? value : fallback;
	}
}

internal readonly record struct HandleMetrics(double ResizeSize, double ResizeOffset, double ResizeBorder, double RotationSize, double RotationOffset, double RotationBorder, double SelectionBorder);

internal static class ZoomHandleMath
{
	public static HandleMetrics Calculate(double zoom)
	{
		double inverseZoom = 1.0 / Math.Clamp(zoom, 0.25, 4.0);
		return new HandleMetrics(
			9.0 * inverseZoom,
			-4.5 * inverseZoom,
			1.2 * inverseZoom,
			12.0 * inverseZoom,
			-28.0 * inverseZoom,
			1.0 * inverseZoom,
			1.5 * inverseZoom);
	}
}
