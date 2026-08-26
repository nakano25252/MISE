using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ValidationDialog : Window
{
	private readonly ListBox _list = new ListBox();

	public Guid? SelectedElementId { get; private set; }

	public ValidationDialog(IReadOnlyList<ValidationIssue> issues)
	{
		base.Title = "レイアウトチェック";
		base.Width = 760.0;
		base.Height = 560.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 760.0, 560.0, 420.0, 300.0);
		Build(issues);
	}

	private void Build(IReadOnlyList<ValidationIssue> issues)
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		Button button = new Button
		{
			Content = "閉じる",
			MinWidth = 90.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		DockPanel.SetDock(button, Dock.Bottom);
		dockPanel.Children.Add(button);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "レイアウトチェック",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush)
		});
		int value = issues.Count((ValidationIssue x) => x.Severity == IssueSeverity.Error);
		int value2 = issues.Count((ValidationIssue x) => x.Severity == IssueSeverity.Warning);
		stackPanel.Children.Add(new TextBlock
		{
			Text = $"赤 {value}件 / 黄 {value2}件 / 改善提案 {issues.Count((ValidationIssue x) => x.Severity == IssueSeverity.Suggestion)}件",
			Foreground = new SolidColorBrush(Color.FromRgb(100, 111, 130)),
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		});
		DockPanel.SetDock(stackPanel, Dock.Top);
		dockPanel.Children.Add(stackPanel);
		_list.ItemsSource = issues;
		_list.MouseDoubleClick += Locate;
		_list.ItemTemplate = BuildTemplate();
		dockPanel.Children.Add(_list);
		base.Content = dockPanel;
	}

	private static DataTemplate BuildTemplate()
	{
		DataTemplate dataTemplate = new DataTemplate(typeof(ValidationIssue));
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Grid));
		frameworkElementFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4.0, 7.0, 4.0, 7.0));
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory2.SetValue(FrameworkElement.WidthProperty, 10.0);
		frameworkElementFactory2.SetValue(FrameworkElement.HeightProperty, 10.0);
		frameworkElementFactory2.SetValue(Border.CornerRadiusProperty, new CornerRadius(5.0));
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 5.0, 10.0, 0.0));
		frameworkElementFactory2.SetBinding(Border.BackgroundProperty, new Binding("Severity")
		{
			Converter = new SeverityBrushConverter()
		});
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(StackPanel));
		frameworkElementFactory3.SetValue(FrameworkElement.MarginProperty, new Thickness(22.0, 0.0, 0.0, 0.0));
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory4.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
		frameworkElementFactory4.SetBinding(TextBlock.TextProperty, new Binding("Title"));
		frameworkElementFactory3.AppendChild(frameworkElementFactory4);
		FrameworkElementFactory frameworkElementFactory5 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory5.SetValue(TextBlock.FontSizeProperty, 11.0);
		frameworkElementFactory5.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(86, 98, 116)));
		frameworkElementFactory5.SetBinding(TextBlock.TextProperty, new Binding("ElementName")
		{
			StringFormat = "対象: {0}"
		});
		frameworkElementFactory3.AppendChild(frameworkElementFactory5);
		FrameworkElementFactory frameworkElementFactory6 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory6.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
		frameworkElementFactory6.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(106, 116, 132)));
		frameworkElementFactory6.SetBinding(TextBlock.TextProperty, new Binding("Detail"));
		frameworkElementFactory3.AppendChild(frameworkElementFactory6);
		frameworkElementFactory.AppendChild(frameworkElementFactory3);
		dataTemplate.VisualTree = frameworkElementFactory;
		return dataTemplate;
	}

	private void Locate(object sender, MouseButtonEventArgs e)
	{
		if (_list.SelectedItem is ValidationIssue { ElementId: not null } validationIssue)
		{
			SelectedElementId = validationIssue.ElementId;
			base.DialogResult = true;
		}
	}
}
