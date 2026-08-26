using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ImpositionDialog : Window
{
	private readonly ComboBox _paper = new ComboBox();

	private readonly ComboBox _orientation = new ComboBox();

	private readonly TextBox _margin = new TextBox
	{
		Text = "5"
	};

	private readonly TextBox _gap = new TextBox
	{
		Text = "3"
	};

	private readonly TextBox _copies = new TextBox
	{
		Text = "0"
	};

	private readonly CheckBox _crop = new CheckBox
	{
		Content = "裁断線（トンボ）を付ける",
		IsChecked = true
	};

	public ImpositionDialogResult? Result { get; private set; }

	public ImpositionDialog(double itemWidth, double itemHeight)
	{
		base.Title = "自動面付け";
		base.Width = 430.0;
		base.Height = 500.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 430.0, 500.0, 340.0, 300.0);
		_paper.ItemsSource = new string[2] { "A4", "A3" };
		_paper.SelectedIndex = 0;
		_orientation.ItemsSource = new string[2] { "縦", "横" };
		_orientation.SelectedIndex = 0;
		Build(itemWidth, itemHeight);
	}

	private void Build(double itemWidth, double itemHeight)
	{
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 16.0, 0.0, 0.0)
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
			Content = "面付けして出力",
			MinWidth = 130.0,
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
			Text = "自動面付け",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush)
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = $"原稿サイズ: {itemWidth:0.#} × {itemHeight:0.#}mm",
			Foreground = new SolidColorBrush(Color.FromRgb(105, 116, 134)),
			Margin = new Thickness(0.0, 3.0, 0.0, 18.0)
		});
		Add(stackPanel2, "出力用紙", _paper);
		Add(stackPanel2, "用紙の向き", _orientation);
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
			Text = "外側余白 (mm)"
		});
		stackPanel3.Children.Add(_margin);
		grid.Children.Add(stackPanel3);
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Children.Add(new TextBlock
		{
			Text = "面の間隔 (mm)"
		});
		stackPanel4.Children.Add(_gap);
		Grid.SetColumn(stackPanel4, 2);
		grid.Children.Add(stackPanel4);
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel2.Children.Add(grid);
		Add(stackPanel2, "コピー数（0＝用紙に入る最大数）", _copies);
		_crop.Margin = new Thickness(0.0, 4.0, 0.0, 15.0);
		stackPanel2.Children.Add(_crop);
		stackPanel2.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(244, 246, 249)),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(10.0),
			Child = new TextBlock
			{
				Text = "原稿は実寸を優先して配置します。用紙より大きい場合のみ、用紙内に収まるよう縮小します。",
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

	private static void Add(Panel p, string label, Control c)
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
		stackPanel.Children.Add(c);
		p.Children.Add(stackPanel);
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(_margin.Text, out var result) || !double.TryParse(_gap.Text, out var result2) || !int.TryParse(_copies.Text, out var result3) || result < 0.0 || result2 < 0.0 || result3 < 0)
		{
			MessageBox.Show("余白・間隔・コピー数を正しく入力してください。", "入力確認");
			return;
		}
		Result = new ImpositionDialogResult(_paper.SelectedItem?.ToString() ?? "A4", _orientation.SelectedIndex == 1, result, result2, result3, _crop.IsChecked == true);
		base.DialogResult = true;
	}
}
