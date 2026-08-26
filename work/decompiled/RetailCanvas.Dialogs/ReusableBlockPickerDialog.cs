using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ReusableBlockPickerDialog : Window
{
	private readonly ReusableBlockService _service;

	private readonly ListBox _list = new ListBox();

	private readonly TextBlock _empty = new TextBlock();

	public ReusableBlockModel? SelectedBlock { get; private set; }

	public ReusableBlockPickerDialog(ReusableBlockService service)
	{
		_service = service;
		base.Title = "再利用ブロック";
		base.Width = 620.0;
		base.Height = 460.0;
		base.MinWidth = 440.0;
		base.MinHeight = 320.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 620.0, 460.0, 440.0, 320.0);
		Build();
		Reload();
	}

	private void Build()
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(18.0)
		};
		TextBlock element = new TextBlock
		{
			Text = "保存済みの部品をまとめて挿入",
			FontSize = 20.0,
			FontWeight = FontWeights.SemiBold,
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		TextBlock element2 = new TextBlock
		{
			Text = "文字・図形・画像・QRの組み合わせを、位置関係を保ったまま再利用できます。",
			Foreground = Brushes.DimGray,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		DockPanel.SetDock(element2, Dock.Top);
		dockPanel.Children.Add(element2);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = ButtonOf("保存先を開く", 105.0);
		Button button2 = ButtonOf("削除", 80.0);
		Button button3 = ButtonOf("キャンセル", 90.0);
		Button button4 = ButtonOf("挿入", 90.0);
		button4.IsDefault = true;
		button3.IsCancel = true;
		button.Click += delegate
		{
			Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.Blocks)
			{
				UseShellExecute = true
			});
		};
		button2.Click += Delete_Click;
		button3.Click += delegate
		{
			base.DialogResult = false;
		};
		button4.Click += delegate
		{
			Accept();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		stackPanel.Children.Add(button4);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		Grid grid = new Grid();
		_list.SelectionMode = SelectionMode.Single;
		_list.MouseDoubleClick += delegate
		{
			Accept();
		};
		_list.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				Accept();
				e.Handled = true;
			}
		};
		grid.Children.Add(_list);
		_empty.Text = "保存済みのブロックはありません。\nオブジェクトを選択し、右クリックから保存できます。";
		_empty.TextAlignment = TextAlignment.Center;
		_empty.HorizontalAlignment = HorizontalAlignment.Center;
		_empty.VerticalAlignment = VerticalAlignment.Center;
		_empty.Foreground = Brushes.Gray;
		grid.Children.Add(_empty);
		dockPanel.Children.Add(grid);
		base.Content = dockPanel;
	}

	private static Button ButtonOf(string text, double width)
	{
		return new Button
		{
			Content = text,
			MinWidth = width,
			Margin = new Thickness(4.0, 0.0, 0.0, 0.0)
		};
	}

	private void Reload()
	{
		IReadOnlyList<ReusableBlockInfo> readOnlyList = _service.List();
		_list.ItemsSource = readOnlyList;
		_empty.Visibility = ((readOnlyList.Count != 0) ? Visibility.Collapsed : Visibility.Visible);
		if (readOnlyList.Count > 0)
		{
			_list.SelectedIndex = 0;
		}
	}

	private void Delete_Click(object? sender, RoutedEventArgs e)
	{
		if (_list.SelectedItem is ReusableBlockInfo reusableBlockInfo && MessageBox.Show(this, "「" + reusableBlockInfo.Name + "」を削除しますか？", "再利用ブロック", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			_service.Delete(reusableBlockInfo.FilePath);
			Reload();
		}
	}

	private void Accept()
	{
		if (!(_list.SelectedItem is ReusableBlockInfo reusableBlockInfo))
		{
			return;
		}
		try
		{
			SelectedBlock = _service.Load(reusableBlockInfo.FilePath);
			base.DialogResult = true;
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, "ブロックを読み込めませんでした。\n\n" + ex.Message, "再利用ブロック", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}
}
