using System;

namespace RetailCanvas.Controls;

public sealed class DesignerItemSelectionEventArgs(bool additive) : EventArgs()
{
	public bool Additive { get; } = additive;
}
