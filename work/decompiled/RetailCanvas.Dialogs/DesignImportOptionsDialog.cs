using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RetailCanvas.Dialogs;

public sealed record DesignImportOptions(
	bool MakeTextEditable,
	bool MakeImagesEditable,
	bool PreserveMissingFontText,
	int BackgroundLongSide,
	string ModeLabel);

public sealed class DesignImportOptionsDialog : Window
{
	private readonly CheckBox _editableText = new CheckBox
	{
		Content = "文字を編集可能なオブジェクトにする",
		IsChecked = false,
		Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
	};

	private readonly CheckBox _editableImages = new CheckBox
	{
		Content = "画像を編集可能なオブジェクトにする",
		IsChecked = true,
		Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
	};

	private readonly CheckBox _preserveMissingFonts = new CheckBox
	{
		Content = "PCにないフォントの文字は元の見た目で残す",
		IsChecked = true,
		Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
	};

	private readonly ComboBox _quality = new ComboBox
	{
		MinWidth = 260.0
	};

	public DesignImportOptions? Options { get; private set; }

	public DesignImportOptionsDialog(string path)
	{
		base.Title = "PDF／Illustrator AIの読み込み";
		base.Width = 590.0;
		base.Height = 690.0;
		base.MinWidth = 500.0;
		base.MinHeight = 560.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		_quality.Items.Add("標準（長辺2600px・軽量）");
		_quality.Items.Add("高精細（長辺5200px・推奨）");
		_quality.SelectedIndex = 1;
		Build(path);
	}

	private void Build(string path)
	{
		DockPanel root = new DockPanel
		{
			Margin = new Thickness(22.0)
		};
		Button cancel = new Button
		{
			Content = "キャンセル",
			MinWidth = 100.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		cancel.Click += delegate
		{
			base.DialogResult = false;
		};
		DockPanel.SetDock(cancel, Dock.Bottom);
		root.Children.Add(cancel);

		StackPanel content = new StackPanel();
		content.Children.Add(new TextBlock
		{
			Text = "読み込み方法を選択",
			FontSize = 23.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush),
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		content.Children.Add(new TextBlock
		{
			Text = Path.GetFileName(path),
			Foreground = Brushes.SlateGray,
			TextTrimming = TextTrimming.CharacterEllipsis,
			Margin = new Thickness(0.0, 0.0, 0.0, 18.0)
		});

		content.Children.Add(ModeCard(
			"編集優先",
			"文字と画像を個別オブジェクトへ変換します。未導入フォントは近い標準フォントへ置換します。",
			"編集優先で読み込む",
			delegate
			{
				Complete(new DesignImportOptions(true, true, false, 2600, "編集優先"));
			}));
		content.Children.Add(ModeCard(
			"見た目優先",
			"ページ全体を高精細画像として読み込み、文字・画像・効果の外観を維持します。",
			"見た目優先で読み込む",
			delegate
			{
				Complete(new DesignImportOptions(false, false, true, 5200, "見た目優先"));
			}));

		Expander details = new Expander
		{
			Header = "詳細設定",
			IsExpanded = true,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			FontWeight = FontWeights.SemiBold
		};
		StackPanel detailPanel = new StackPanel
		{
			Margin = new Thickness(14.0, 12.0, 4.0, 4.0)
		};
		detailPanel.Children.Add(new TextBlock
		{
			Text = "チェックを外した要素は、元デザインの固定背景としてそのまま残ります。",
			TextWrapping = TextWrapping.Wrap,
			Foreground = Brushes.SlateGray,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		detailPanel.Children.Add(_editableText);
		detailPanel.Children.Add(_editableImages);
		detailPanel.Children.Add(_preserveMissingFonts);
		detailPanel.Children.Add(new TextBlock
		{
			Text = "固定背景の品質",
			Margin = new Thickness(0.0, 4.0, 0.0, 5.0)
		});
		detailPanel.Children.Add(_quality);
		Button custom = new Button
		{
			Content = "この詳細設定で読み込む",
			MinWidth = 210.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 15.0, 0.0, 0.0),
			Style = (FindResource("PrimaryButton") as Style)
		};
		custom.Click += delegate
		{
			bool text = _editableText.IsChecked == true;
			bool images = _editableImages.IsChecked == true;
			int quality = (_quality.SelectedIndex == 0) ? 2600 : 5200;
			Complete(new DesignImportOptions(text, images, _preserveMissingFonts.IsChecked == true, quality, "詳細設定"));
		};
		detailPanel.Children.Add(custom);
		details.Content = detailPanel;
		content.Children.Add(details);

		root.Children.Add(new ScrollViewer
		{
			Content = content,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		base.Content = root;
	}

	private Border ModeCard(string title, string description, string buttonText, RoutedEventHandler click)
	{
		StackPanel panel = new StackPanel();
		panel.Children.Add(new TextBlock
		{
			Text = title,
			FontSize = 17.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush)
		});
		panel.Children.Add(new TextBlock
		{
			Text = description,
			TextWrapping = TextWrapping.Wrap,
			Foreground = new SolidColorBrush(Color.FromRgb(78, 89, 106)),
			Margin = new Thickness(0.0, 5.0, 0.0, 9.0)
		});
		Button button = new Button
		{
			Content = buttonText,
			MinWidth = 170.0,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		button.Click += click;
		panel.Children.Add(button);
		return new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(246, 248, 251)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(219, 225, 233)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(7.0),
			Padding = new Thickness(14.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 11.0),
			Child = panel
		};
	}

	private void Complete(DesignImportOptions options)
	{
		Options = options;
		base.DialogResult = true;
	}
}
