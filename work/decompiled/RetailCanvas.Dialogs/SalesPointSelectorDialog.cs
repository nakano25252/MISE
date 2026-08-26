using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class SalesPointSelectorDialog : Window
{
	private readonly string _category;

	private readonly ComboBox _level = new ComboBox
	{
		ItemsSource = new string[3] { "ライト", "標準", "詳しい" },
		SelectedIndex = 1,
		Width = 130.0
	};

	private readonly StackPanel _cards = new StackPanel();

	private readonly HashSet<string> _selected;

	public string ResultJson { get; private set; } = string.Empty;

	public string FeatureText { get; private set; } = string.Empty;

	public string DetailText { get; private set; } = string.Empty;

	public SalesPointSelectorDialog(string category, string currentJson)
	{
		_category = category;
		_selected = ReadSelected(currentJson);
		base.Title = "セールスポイントカード － MISE";
		base.Width = 760.0;
		base.Height = 680.0;
		base.MinWidth = 580.0;
		base.MinHeight = 450.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 760.0, 680.0, 580.0, 450.0);
		base.Content = Build();
		_level.SelectionChanged += delegate
		{
			RefreshCards();
		};
		RefreshCards();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
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
			MinWidth = 90.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "選択を反映",
			MinWidth = 115.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button2.Click += delegate
		{
			Accept();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = _category + " のセールスポイント",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "お客様の詳しさに合わせて表示範囲を選び、使う訴求だけをチェックします。",
			Foreground = Brushes.SlateGray
		});
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "情報レベル",
			Width = 100.0,
			VerticalAlignment = VerticalAlignment.Center
		});
		stackPanel3.Children.Add(_level);
		stackPanel2.Children.Add(stackPanel3);
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		obj.Children.Add(new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Content = _cards
		});
		return obj;
	}

	private void RefreshCards()
	{
		_cards.Children.Clear();
		foreach (SalesPointCard card in SalesPointCatalog.For(_category, _level.SelectedItem?.ToString() ?? "標準"))
		{
			CheckBox checkBox = new CheckBox
			{
				IsChecked = _selected.Contains(card.Title),
				Tag = card,
				Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(new TextBlock
			{
				Text = "[" + card.Level + "] " + card.Title,
				FontWeight = FontWeights.SemiBold
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = card.CustomerCopy,
				Foreground = new SolidColorBrush(Color.FromRgb(40, 90, 95))
			});
			stackPanel.Children.Add(new TextBlock
			{
				Text = card.Detail,
				Foreground = Brushes.SlateGray,
				TextWrapping = TextWrapping.Wrap
			});
			checkBox.Content = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(247, 248, 249)),
				CornerRadius = new CornerRadius(7.0),
				Padding = new Thickness(9.0),
				Child = stackPanel
			};
			checkBox.Checked += delegate
			{
				_selected.Add(card.Title);
			};
			checkBox.Unchecked += delegate
			{
				_selected.Remove(card.Title);
			};
			_cards.Children.Add(checkBox);
		}
	}

	private void Accept()
	{
		List<SalesPointCard> list = (from x in SalesPointCatalog.For(_category, "詳しい")
			where _selected.Contains(x.Title)
			select x).ToList();
		ResultJson = JsonSerializer.Serialize(list);
		FeatureText = string.Join("\n", list.Select((SalesPointCard x) => "● " + x.Title + "：" + x.CustomerCopy));
		DetailText = string.Join("\n", list.Select((SalesPointCard x) => "・" + x.Title + "：" + x.Detail));
		base.DialogResult = true;
	}

	private static HashSet<string> ReadSelected(string json)
	{
		try
		{
			return JsonSerializer.Deserialize<List<SalesPointCard>>(json)?.Select((SalesPointCard x) => x.Title).ToHashSet() ?? new HashSet<string>();
		}
		catch
		{
			return new HashSet<string>();
		}
	}
}
