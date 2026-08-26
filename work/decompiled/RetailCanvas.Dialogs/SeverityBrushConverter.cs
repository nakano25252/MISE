using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Dialogs;

public sealed class SeverityBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is IssueSeverity)
		{
			switch ((IssueSeverity)value)
			{
			case IssueSeverity.Error:
				return Brushes.Firebrick;
			case IssueSeverity.Warning:
				return Brushes.DarkOrange;
			case IssueSeverity.Suggestion:
				return Brushes.DodgerBlue;
			}
		}
		return Brushes.ForestGreen;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotSupportedException();
	}
}
