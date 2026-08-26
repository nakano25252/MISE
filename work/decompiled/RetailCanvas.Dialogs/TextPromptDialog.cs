using System.Windows;
using System.Windows.Controls;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public static class TextPromptDialog
{
	public static string? Show(Window owner, string title, string label, string initial = "")
	{
		Window window = new Window
		{
			Owner = owner,
			Title = title,
			Width = 410.0,
			Height = 200.0,
			ResizeMode = ResizeMode.NoResize,
			WindowStartupLocation = WindowStartupLocation.CenterOwner
		};
		WindowSizing.Attach(window, 410.0, 200.0, 320.0, 180.0);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 85.0
		};
		Button button2 = new Button
		{
			Content = "OK",
			MinWidth = 85.0,
			IsDefault = true
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(new TextBlock
		{
			Text = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
		});
		TextBox box = new TextBox
		{
			Text = initial
		};
		stackPanel2.Children.Add(box);
		dockPanel.Children.Add(stackPanel2);
		window.Content = dockPanel;
		button.Click += delegate
		{
			window.DialogResult = false;
		};
		button2.Click += delegate
		{
			window.DialogResult = true;
		};
		window.Loaded += delegate
		{
			box.Focus();
			box.SelectAll();
		};
		if (window.ShowDialog() != true)
		{
			return null;
		}
		return box.Text.Trim();
	}
}
