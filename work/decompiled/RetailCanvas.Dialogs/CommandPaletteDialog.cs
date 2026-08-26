using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class CommandPaletteDialog : Window
{
	private readonly IReadOnlyList<CommandPaletteItem> _all;

	private readonly TextBox _search = new TextBox();

	private readonly ListBox _list = new ListBox();

	public CommandPaletteItem? SelectedCommand { get; private set; }

	public CommandPaletteDialog(IReadOnlyList<CommandPaletteItem> commands)
	{
		_all = commands;
		base.Title = "操作を検索";
		base.Width = 650.0;
		base.Height = 480.0;
		base.MinWidth = 440.0;
		base.MinHeight = 300.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.WindowStyle = WindowStyle.ToolWindow;
		WindowSizing.Attach(this, 650.0, 480.0, 440.0, 300.0);
		Build();
		Filter();
		base.Loaded += delegate
		{
			_search.Focus();
			_search.SelectAll();
		};
	}

	private void Build()
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(16.0)
		};
		TextBlock element = new TextBlock
		{
			Text = "操作名を入力してください。↑↓で選択、Enterで実行、Escで閉じます。",
			Foreground = Brushes.DimGray,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		_search.FontSize = 18.0;
		_search.Padding = new Thickness(10.0, 7.0, 10.0, 7.0);
		_search.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		_search.TextChanged += delegate
		{
			Filter();
		};
		_search.PreviewKeyDown += Search_KeyDown;
		DockPanel.SetDock(_search, Dock.Top);
		dockPanel.Children.Add(_search);
		_list.MouseDoubleClick += delegate
		{
			Accept();
		};
		_list.PreviewKeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				Accept();
				e.Handled = true;
			}
		};
		dockPanel.Children.Add(_list);
		base.Content = dockPanel;
	}

	private void Filter()
	{
		string[] terms = _search.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		List<CommandPaletteItem> list = _all.Where(delegate(CommandPaletteItem command)
		{
			string haystack = $"{command.Category} {command.Name} {command.Shortcut} {command.Keywords}";
			return terms.All((string term) => haystack.Contains(term, StringComparison.CurrentCultureIgnoreCase));
		}).ToList();
		_list.ItemsSource = list;
		if (list.Count > 0)
		{
			_list.SelectedIndex = 0;
		}
	}

	private void Search_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Down)
		{
			_list.Focus();
			if (_list.SelectedIndex < 0 && _list.Items.Count > 0)
			{
				_list.SelectedIndex = 0;
			}
			e.Handled = true;
		}
		else if (e.Key == Key.Return)
		{
			Accept();
			e.Handled = true;
		}
	}

	private void Accept()
	{
		if (_list.SelectedItem is CommandPaletteItem selectedCommand)
		{
			SelectedCommand = selectedCommand;
			base.DialogResult = true;
		}
	}
}
