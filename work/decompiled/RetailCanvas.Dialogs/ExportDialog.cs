using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ExportDialog : Window
{
	private readonly ComboBox _format = new ComboBox();

	private readonly ComboBox _dpi = new ComboBox();

	private readonly CheckBox _allPages = new CheckBox
	{
		Content = "全ページを書き出す",
		IsChecked = true
	};

	private readonly CheckBox _transparent = new CheckBox
	{
		Content = "背景を透過（PNGのみ）"
	};

	private readonly Slider _quality = new Slider
	{
		Minimum = 40.0,
		Maximum = 100.0,
		Value = 92.0,
		TickFrequency = 5.0,
		IsSnapToTickEnabled = true
	};

	private readonly TextBlock _qualityText = new TextBlock();

	public ExportDialogResult? Result { get; private set; }

	public ExportDialog(ExportSettings settings)
	{
		base.Title = "書き出し設定";
		base.Width = 430.0;
		base.Height = 470.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 430.0, 470.0, 340.0, 280.0);
		_format.ItemsSource = new string[3] { "PDF", "PNG", "JPEG" };
		ComboBox format = _format;
		string format2 = settings.Format;
		format.SelectedItem = ((format2 == "PNG" || format2 == "JPEG") ? settings.Format : "PDF");
		_dpi.ItemsSource = new string[5] { "96 dpi（画面用）", "150 dpi（確認用）", "200 dpi（通常印刷）", "300 dpi（高品質印刷）", "600 dpi（高精細）" };
		ComboBox dpi = _dpi;
		int dpi2 = settings.Dpi;
		dpi.SelectedIndex = ((dpi2 > 200) ? ((dpi2 < 600) ? 3 : 4) : ((dpi2 > 96) ? ((dpi2 <= 150) ? 1 : 2) : 0));
		_allPages.IsChecked = settings.ExportAllPages;
		_transparent.IsChecked = settings.TransparentBackground;
		_quality.Value = settings.JpegQuality;
		_qualityText.Text = $"JPEG画質: {_quality.Value:0}%";
		_quality.ValueChanged += delegate(object _, RoutedPropertyChangedEventArgs<double> e)
		{
			_qualityText.Text = $"JPEG画質: {e.NewValue:0}%";
		};
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
			Content = "書き出しへ",
			MinWidth = 110.0,
			Style = (FindResource("PrimaryButton") as Style)
		};
		button2.Click += delegate
		{
			Result = new ExportDialogResult(_format.SelectedItem?.ToString() ?? "PDF", (new int[5] { 96, 150, 200, 300, 600 })[_dpi.SelectedIndex], _allPages.IsChecked == true, _transparent.IsChecked == true, (int)_quality.Value);
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel();
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "書き出し設定",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush),
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		});
		Add(stackPanel2, "形式", _format);
		Add(stackPanel2, "解像度", _dpi);
		_allPages.Margin = new Thickness(0.0, 4.0, 0.0, 10.0);
		stackPanel2.Children.Add(_allPages);
		_transparent.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
		stackPanel2.Children.Add(_transparent);
		stackPanel2.Children.Add(_qualityText);
		_quality.Margin = new Thickness(0.0, 4.0, 0.0, 16.0);
		stackPanel2.Children.Add(_quality);
		stackPanel2.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(244, 246, 249)),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(10.0),
			Child = new TextBlock
			{
				Text = "印刷用途は300dpiを推奨します。PDFは用紙寸法を保持して出力します。",
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

	private static void Add(Panel panel, string label, Control control)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 13.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		stackPanel.Children.Add(control);
		panel.Children.Add(stackPanel);
	}
}
