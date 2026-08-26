using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class PdfPageSelectionDialog : Window
{
	private readonly ListBox _pages = new ListBox
	{
		SelectionMode = SelectionMode.Extended
	};

	public IReadOnlyList<int> SelectedPages { get; private set; } = new List<int>();

	public PdfPageSelectionDialog(int pageCount)
	{
		base.Title = "配置するPDFページを選択 － MISE";
		base.Width = 430.0;
		base.Height = 560.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 430.0, 560.0, 340.0, 360.0);
		_pages.ItemsSource = Enumerable.Range(1, pageCount).ToList();
		if (pageCount > 0)
		{
			_pages.SelectedIndex = 0;
		}
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(16.0)
		};
		TextBlock element = new TextBlock
		{
			Text = $"{pageCount}ページ中、配置するページを選択してください。\nCtrl／Shiftで複数選択できます。",
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "全ページ選択"
		};
		button.Click += delegate
		{
			_pages.SelectAll();
		};
		Button button2 = new Button
		{
			Content = "キャンセル",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button3 = new Button
		{
			Content = "配置",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button3.Click += delegate
		{
			SelectedPages = (from int page in _pages.SelectedItems
				select page - 1 into page
				orderby page
				select page).ToList();
			if (SelectedPages.Count == 0)
			{
				MessageBox.Show("ページを選択してください。", "PDF");
			}
			else
			{
				base.DialogResult = true;
			}
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		dockPanel.Children.Add(_pages);
		base.Content = dockPanel;
	}
}
