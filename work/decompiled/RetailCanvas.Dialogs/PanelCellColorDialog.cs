using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class PanelCellColorDialog : Window
{
	private readonly List<string> _colors;

	private readonly List<string> _roles;

	private readonly UniformGrid _cells = new UniformGrid();

	private readonly int _count;

	public IReadOnlyList<string> Result => _colors;

	public IReadOnlyList<string> Roles => _roles;

	public PanelCellColorDialog(int rows, int columns, IEnumerable<string> colors, IEnumerable<string> roles, string defaultColor)
	{
		rows = Math.Clamp(rows, 1, 12);
		columns = Math.Clamp(columns, 1, 12);
		_count = rows * columns;
		_colors = colors.Take(_count).ToList();
		while (_colors.Count < _count)
		{
			_colors.Add(defaultColor);
		}
		_roles = roles.Take(_count).ToList();
		while (_roles.Count < _count)
		{
			_roles.Add("未指定");
		}
		base.Title = "パネル区画の配色";
		base.Width = 620.0;
		base.Height = 500.0;
		base.MinWidth = 420.0;
		base.MinHeight = 340.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 620.0, 500.0, 420.0, 340.0);
		_cells.Rows = rows;
		_cells.Columns = columns;
		Build();
	}

	private void Build()
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition());
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.Children.Add(new TextBlock
		{
			Text = "区画ごとの色",
			FontSize = 21.0,
			FontWeight = FontWeights.Bold,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
		Border element = new Border
		{
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0),
			Background = Brushes.White,
			Padding = new Thickness(8.0),
			Child = _cells
		};
		Grid.SetRow(element, 1);
		grid.Children.Add(element);
		for (int i = 0; i < _count; i++)
		{
			int index = i;
			Grid grid2 = new Grid
			{
				Margin = new Thickness(2.0)
			};
			grid2.RowDefinitions.Add(new RowDefinition());
			grid2.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			Button button = new Button
			{
				Tag = index,
				Margin = new Thickness(0.0, 0.0, 0.0, 3.0),
				Content = $"区画 {index + 1}\n{_colors[index]}",
				Background = ParseBrush(_colors[index]),
				Foreground = ContrastBrush(_colors[index]),
				FontWeight = FontWeights.SemiBold
			};
			button.Click += delegate
			{
				Pick(index, button);
			};
			grid2.Children.Add(button);
			ComboBox role = new ComboBox
			{
				MinWidth = 82.0,
				ItemsSource = new string[7] { "未指定", "見出し", "本文", "画像", "価格", "QR", "注釈" },
				SelectedItem = _roles[index]
			};
			role.SelectionChanged += delegate
			{
				_roles[index] = role.SelectedItem?.ToString() ?? "未指定";
			};
			Grid.SetRow(role, 1);
			grid2.Children.Add(role);
			_cells.Children.Add(grid2);
		}
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		dockPanel.Children.Add(new TextBlock
		{
			Text = "区画をクリックして色を選択し、下段で見出し・本文・画像などの役割を指定できます。",
			Foreground = Brushes.SlateGray,
			VerticalAlignment = VerticalAlignment.Center
		});
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button button2 = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button3 = new Button
		{
			Content = "適用",
			MinWidth = 100.0
		};
		button3.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Right);
		dockPanel.Children.Add(stackPanel);
		Grid.SetRow(dockPanel, 2);
		grid.Children.Add(dockPanel);
		base.Content = grid;
	}

	private void Pick(int index, Button button)
	{
		string text = ColorPickerDialog.Show(this, _colors[index]);
		if (text != null)
		{
			_colors[index] = text;
			button.Content = $"区画 {index + 1}\n{text}";
			button.Background = ParseBrush(text);
			button.Foreground = ContrastBrush(text);
		}
	}

	private static Brush ParseBrush(string color)
	{
		try
		{
			return (Brush)new BrushConverter().ConvertFromString(color);
		}
		catch
		{
			return Brushes.White;
		}
	}

	private static Brush ContrastBrush(string color)
	{
		try
		{
			Color color2 = (Color)ColorConverter.ConvertFromString(color);
			return ((double)(int)color2.R * 0.299 + (double)(int)color2.G * 0.587 + (double)(int)color2.B * 0.114 < 145.0) ? Brushes.White : Brushes.Black;
		}
		catch
		{
			return Brushes.Black;
		}
	}
}
