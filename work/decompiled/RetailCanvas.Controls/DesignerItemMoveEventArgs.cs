using System;

namespace RetailCanvas.Controls;

public sealed class DesignerItemMoveEventArgs(double deltaX, double deltaY) : EventArgs()
{
	public double DeltaX { get; } = deltaX;

	public double DeltaY { get; } = deltaY;
}
