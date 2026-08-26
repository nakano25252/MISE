using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class QrDialog : Window
{
	private readonly TextBox _content = new TextBox
	{
		Text = "https://",
		AcceptsReturn = true,
		Height = 82.0,
		TextWrapping = TextWrapping.Wrap
	};

	private readonly ComboBox _type = new ComboBox();

	private readonly ComboBox _level = new ComboBox();

	private readonly TextBox _foreground = new TextBox
	{
		Text = "#FF000000"
	};

	private readonly TextBox _background = new TextBox
	{
		Text = "#FFFFFFFF"
	};

	private readonly TextBox _label = new TextBox();

	public QrDialogResult? Result { get; private set; }

	public QrDialog()
	{
		base.Title = "QRコードを生成";
		base.Width = 460.0;
		base.Height = 560.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 460.0, 560.0, 340.0, 300.0);
		_type.ItemsSource = new string[5] { "URL", "テキスト", "電話番号", "メールアドレス", "Wi-Fi情報" };
		_type.SelectedIndex = 0;
		_level.ItemsSource = new string[4] { "L（小容量）", "M（標準）", "Q（高）", "H（最高）" };
		_level.SelectedIndex = 1;
		Build();
	}

	private void Build()
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
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
			Content = "生成",
			MinWidth = 100.0,
			Style = (FindResource("PrimaryButton") as Style)
		};
		button2.Click += Ok_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "QRコード",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush),
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
		});
		Add(stackPanel2, "種類", _type);
		Add(stackPanel2, "内容", _content);
		Add(stackPanel2, "誤り訂正レベル", _level);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(10.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "前景色"
		});
		stackPanel3.Children.Add(CreateColorEditor(_foreground));
		grid.Children.Add(stackPanel3);
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Children.Add(new TextBlock
		{
			Text = "背景色"
		});
		stackPanel4.Children.Add(CreateColorEditor(_background));
		Grid.SetColumn(stackPanel4, 2);
		grid.Children.Add(stackPanel4);
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel2.Children.Add(grid);
		Add(stackPanel2, "ラベル（任意）", _label);
		stackPanel2.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(244, 246, 249)),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(10.0),
			Child = new TextBlock
			{
				Text = "読み取り安定性のため18mm角以上、濃い前景色と明るい背景色を推奨します。",
				TextWrapping = TextWrapping.Wrap,
				Foreground = new SolidColorBrush(Color.FromRgb(88, 99, 116))
			}
		});
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = stackPanel2,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		base.Content = dockPanel;
	}

	private static void Add(Panel form, string label, Control control)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		stackPanel.Children.Add(control);
		form.Children.Add(stackPanel);
	}

	private Grid CreateColorEditor(TextBox box)
	{
		Grid obj = new Grid
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
			ColumnDefinitions = 
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = new GridLength(5.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(54.0)
				}
			},
			Children = { (UIElement)box }
		};
		Button picker = new Button
		{
			Content = "色…",
			ToolTip = "カラーパレットを開く",
			Background = (Brush)new BrushConverter().ConvertFromString(box.Text)
		};
		picker.Click += delegate
		{
			string text = ColorPickerDialog.Show(this, box.Text);
			if (text != null)
			{
				box.Text = text;
				picker.Background = (Brush)new BrushConverter().ConvertFromString(text);
			}
		};
		Grid.SetColumn(picker, 2);
		obj.Children.Add(picker);
		return obj;
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		string text = _content.Text.Trim();
		Uri result;
		if (string.IsNullOrWhiteSpace(text))
		{
			MessageBox.Show("QRコードの内容を入力してください。", "入力確認");
		}
		else if (_type.SelectedIndex != 0 || Uri.TryCreate(text, UriKind.Absolute, out result) || MessageBox.Show("URL形式を確認できませんでした。この内容で続行しますか？", "URL確認", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
		{
			Result = new QrDialogResult(text, (new string[4] { "L", "M", "Q", "H" })[_level.SelectedIndex], _foreground.Text.Trim(), _background.Text.Trim(), _label.Text.Trim());
			base.DialogResult = true;
		}
	}
}
