using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class NewProjectDialog : Window
{
	private readonly TextBox _name = new TextBox
	{
		Text = "無題の販促物"
	};

	private readonly ComboBox _purpose = new ComboBox();

	private readonly ComboBox _paper = new ComboBox();

	private readonly ComboBox _orientation = new ComboBox();

	private readonly ComboBox _pages = new ComboBox();

	private readonly TextBox _brand = new TextBox();

	private readonly TextBox _store = new TextBox();

	private readonly TextBox _author = new TextBox();

	private readonly ComboBox _printMode = new ComboBox();

	private readonly TextBox _background = new TextBox
	{
		Text = "#FFFFFFFF"
	};

	private readonly TextBox _customWidth = new TextBox
	{
		Text = "210"
	};

	private readonly TextBox _customHeight = new TextBox
	{
		Text = "297"
	};

	private readonly StackPanel _customPanel = new StackPanel();

	public NewProjectOptions? Result { get; private set; }

	public NewProjectDialog()
	{
		base.Title = "新規プロジェクト";
		base.Width = 520.0;
		base.Height = 690.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 520.0, 690.0, 360.0, 340.0);
		_purpose.ItemsSource = new string[12]
		{
			"製品単品POP", "新製品POP", "比較POP", "キャンペーンPOP", "ブランド訴求POP", "機能訴求POP", "プライスカード", "棚帯", "販売員向けガイド", "名刺サイズ資料",
			"折りたたみ資料", "自由制作"
		};
		_purpose.SelectedIndex = 0;
		_paper.ItemsSource = PaperCatalog.All.Select((PaperSizeDefinition x) => x.Name).ToList();
		_paper.SelectedItem = "A4";
		_paper.SelectionChanged += delegate
		{
			_customPanel.Visibility = ((!(_paper.SelectedItem?.ToString() == "自由サイズ")) ? Visibility.Collapsed : Visibility.Visible);
		};
		_orientation.ItemsSource = new string[2] { "縦", "横" };
		_orientation.SelectedIndex = 0;
		_pages.ItemsSource = Enumerable.Range(1, 20).ToList();
		_pages.SelectedIndex = 0;
		_printMode.ItemsSource = new string[5] { "家庭用プリンタ", "コンビニ印刷", "業務用プリンタ", "印刷会社入稿", "PDF閲覧用" };
		_printMode.SelectedIndex = 0;
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
			Content = "作成",
			MinWidth = 100.0,
			Style = (FindResource("PrimaryButton") as Style)
		};
		button2.Click += Create_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		ScrollViewer scrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "新しい販促物",
			FontSize = 24.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush),
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		});
		AddField(stackPanel2, "プロジェクト名", _name);
		AddField(stackPanel2, "用途", _purpose);
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(12.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "用紙サイズ"
		});
		stackPanel3.Children.Add(_paper);
		grid.Children.Add(stackPanel3);
		StackPanel stackPanel4 = new StackPanel();
		stackPanel4.Children.Add(new TextBlock
		{
			Text = "向き"
		});
		stackPanel4.Children.Add(_orientation);
		Grid.SetColumn(stackPanel4, 2);
		grid.Children.Add(stackPanel4);
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel2.Children.Add(grid);
		_customPanel.Orientation = Orientation.Horizontal;
		_customPanel.Visibility = Visibility.Collapsed;
		_customPanel.Children.Add(new TextBlock
		{
			Text = "幅(mm)",
			VerticalAlignment = VerticalAlignment.Center
		});
		_customWidth.Width = 80.0;
		_customWidth.Margin = new Thickness(8.0, 0.0, 18.0, 0.0);
		_customPanel.Children.Add(_customWidth);
		_customPanel.Children.Add(new TextBlock
		{
			Text = "高さ(mm)",
			VerticalAlignment = VerticalAlignment.Center
		});
		_customHeight.Width = 80.0;
		_customHeight.Margin = new Thickness(8.0, 0.0, 0.0, 0.0);
		_customPanel.Children.Add(_customHeight);
		_customPanel.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel2.Children.Add(_customPanel);
		AddField(stackPanel2, "ページ数", _pages);
		AddField(stackPanel2, "ブランド", _brand);
		AddField(stackPanel2, "店舗名", _store);
		AddField(stackPanel2, "作成者", _author);
		AddField(stackPanel2, "印刷モード", _printMode);
		AddColorField(stackPanel2, "背景色", _background);
		scrollViewer.Content = stackPanel2;
		dockPanel.Children.Add(scrollViewer);
		base.Content = dockPanel;
	}

	private static void AddField(Panel panel, string label, Control control)
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
		panel.Children.Add(stackPanel);
	}

	private void AddColorField(Panel panel, string label, TextBox box)
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
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(6.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(64.0)
		});
		grid.Children.Add(box);
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
		grid.Children.Add(picker);
		stackPanel.Children.Add(grid);
		panel.Children.Add(stackPanel);
	}

	private void Create_Click(object sender, RoutedEventArgs e)
	{
		if (string.IsNullOrWhiteSpace(_name.Text))
		{
			MessageBox.Show("プロジェクト名を入力してください。", "入力確認");
			return;
		}
		double? customWidthMm = null;
		double? customHeightMm = null;
		if (_paper.SelectedItem?.ToString() == "自由サイズ")
		{
			if (!double.TryParse(_customWidth.Text, out var result) || !double.TryParse(_customHeight.Text, out var result2) || result <= 0.0 || result2 <= 0.0)
			{
				MessageBox.Show("自由サイズの幅・高さを正しく入力してください。", "入力確認");
				return;
			}
			customWidthMm = result;
			customHeightMm = result2;
		}
		Result = new NewProjectOptions
		{
			ProjectName = _name.Text.Trim(),
			Purpose = (_purpose.SelectedItem?.ToString() ?? "自由制作"),
			PaperName = (_paper.SelectedItem?.ToString() ?? "A4"),
			Landscape = (_orientation.SelectedIndex == 1),
			PageCount = ((!(_pages.SelectedItem is int num)) ? 1 : num),
			Brand = _brand.Text.Trim(),
			Store = _store.Text.Trim(),
			Author = _author.Text.Trim(),
			PrintMode = (_printMode.SelectedItem?.ToString() ?? "家庭用プリンタ"),
			Background = (string.IsNullOrWhiteSpace(_background.Text) ? "#FFFFFFFF" : _background.Text.Trim()),
			CustomWidthMm = customWidthMm,
			CustomHeightMm = customHeightMm
		};
		base.DialogResult = true;
	}
}
