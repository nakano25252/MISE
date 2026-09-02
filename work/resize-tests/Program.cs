using RetailCanvas.Controls;

const double tolerance = 0.000001;

static void Equal(double expected, double actual, string label)
{
	if (Math.Abs(expected - actual) > tolerance)
	{
		throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
	}
}

ResizeBounds start = new ResizeBounds(10.0, 20.0, 200.0, 50.0);
double minimumScale = 3.0 / 34.0;
double minimumWidth = start.Width * minimumScale;
double minimumHeight = start.Height * minimumScale;

ResizeBounds southEast = ResizeMath.Calculate(start, "SE", -190.0, -45.0, true, minimumWidth, minimumHeight);
Equal(4.0, southEast.Width / southEast.Height, "SE aspect ratio");
Equal(3.0, 34.0 * southEast.Width / start.Width, "SE font lower bound");
Equal(start.X, southEast.X, "SE left anchor");
Equal(start.Y, southEast.Y, "SE top anchor");

ResizeBounds northWest = ResizeMath.Calculate(start, "NW", 190.0, 45.0, true, minimumWidth, minimumHeight);
Equal(4.0, northWest.Width / northWest.Height, "NW aspect ratio");
Equal(start.Right, northWest.Right, "NW right anchor");
Equal(start.Bottom, northWest.Bottom, "NW bottom anchor");

ResizeBounds east = ResizeMath.Calculate(start, "E", -190.0, 0.0, true, minimumWidth, minimumHeight);
Equal(4.0, east.Width / east.Height, "E aspect ratio");
Equal(start.Y + (start.Height - east.Height) / 2.0, east.Y, "E vertical center");

ResizeBounds free = ResizeMath.Calculate(start, "SE", -199.0, -49.0, false, 12.0, 12.0);
Equal(12.0, free.Width, "free minimum width");
Equal(12.0, free.Height, "free minimum height");

DimensionResult linkedWidth = DimensionMath.Apply(80.0, 20.0, 40.0, DimensionAxis.Width, true, 0.1, 0.1);
Equal(40.0, linkedWidth.Width, "linked width input");
Equal(10.0, linkedWidth.Height, "height follows linked width");
Equal(0.5, linkedWidth.UniformScale, "linked width scale");

DimensionResult linkedHeight = DimensionMath.Apply(80.0, 20.0, 30.0, DimensionAxis.Height, true, 0.1, 0.1);
Equal(120.0, linkedHeight.Width, "width follows linked height");
Equal(30.0, linkedHeight.Height, "linked height input");
Equal(1.5, linkedHeight.UniformScale, "linked height scale");

DimensionResult unlockedWidth = DimensionMath.Apply(80.0, 20.0, 40.0, DimensionAxis.Width, false, 0.1, 0.1);
Equal(40.0, unlockedWidth.Width, "unlocked width input");
Equal(20.0, unlockedWidth.Height, "unlocked height remains unchanged");

DimensionResult linkedMinimum = DimensionMath.Apply(4.0, 1.0, 0.01, DimensionAxis.Width, true, 0.1, 0.1);
Equal(0.4, linkedMinimum.Width, "linked minimum width follows ratio");
Equal(0.1, linkedMinimum.Height, "linked minimum height");

Equal(3.0 / 34.0, DimensionMath.ClampTextScale(34.0, 0.01), "text scale minimum");
Equal(300.0 / 34.0, DimensionMath.ClampTextScale(34.0, 20.0), "text scale maximum");
Equal(1.0, DimensionMath.GeneralMinimumMm, "general minimum size policy");
Equal(5.0, DimensionMath.QrMinimumMm, "QR minimum size policy");

ResizeBounds cappedText = ResizeMath.ApplyUniformScale(start, "NW", DimensionMath.ClampTextScale(34.0, 20.0));
Equal(start.Right, cappedText.Right, "capped text keeps right anchor");
Equal(start.Bottom, cappedText.Bottom, "capped text keeps bottom anchor");
Equal(4.0, cappedText.Width / cappedText.Height, "capped text aspect ratio");

foreach (double zoom in new[] { 0.25, 0.5, 1.0, 2.0, 4.0 })
{
	HandleMetrics metrics = ZoomHandleMath.Calculate(zoom);
	Equal(9.0, metrics.ResizeSize * zoom, $"resize handle screen size at {zoom:0.##}x");
	Equal(12.0, metrics.RotationSize * zoom, $"rotation handle screen size at {zoom:0.##}x");
	Equal(1.5, metrics.SelectionBorder * zoom, $"selection border screen width at {zoom:0.##}x");
}

Console.WriteLine($"PASS minFont={34.0 * southEast.Width / start.Width:0.###}pt aspect={southEast.Width / southEast.Height:0.###} dimensions=OK anchors=OK zoomHandles=OK");
