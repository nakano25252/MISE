using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class AssetTrashDialog : Window
{
	private readonly AssetTrashService _service = new AssetTrashService();

	private readonly ListBox _list = new ListBox
	{
		SelectionMode = SelectionMode.Extended,
		DisplayMemberPath = "Label"
	};

	public AssetTrashDialog()
	{
		base.Title = "素材のごみ箱 － MISE";
		base.Width = 720.0;
		base.Height = 500.0;
		base.MinWidth = 520.0;
		base.MinHeight = 340.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 500.0, 520.0, 340.0);
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
			Content = "復元"
		};
		button.Click += delegate
		{
			_service.Restore(from AssetTrashEntry x in _list.SelectedItems
				select x.Id);
			Refresh();
		};
		Button button2 = new Button
		{
			Content = "完全削除",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button2.Click += delegate
		{
			List<AssetTrashEntry> list = _list.SelectedItems.Cast<AssetTrashEntry>().ToList();
			if (list.Count > 0 && MessageBox.Show($"{list.Count}件を完全削除します。元に戻せません。", "素材の完全削除", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.Yes)
			{
				_service.Purge(list.Select((AssetTrashEntry x) => x.Id));
				Refresh();
			}
		};
		Button button3 = new Button
		{
			Content = "閉じる",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button3.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		TextBlock element = new TextBlock
		{
			Text = "素材のごみ箱",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		dockPanel.Children.Add(_list);
		base.Content = dockPanel;
		Refresh();
	}

	private void Refresh()
	{
		_list.ItemsSource = _service.Load();
	}
}
