using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class CsvExportFormatDialog : Window
{
	private readonly RadioButton _official = new RadioButton
	{
		Content = "正式19項目（推奨）",
		IsChecked = true,
		Margin = new Thickness(0.0, 5.0, 0.0, 2.0)
	};

	private readonly RadioButton _extended = new RadioButton
	{
		Content = "拡張25項目（発売日・注意事項・訴求・素材役割を含む）",
		Margin = new Thickness(0.0, 5.0, 0.0, 2.0)
	};

	private readonly RadioButton _legacy = new RadioButton
	{
		Content = "従来13項目（旧バージョンとの受け渡し用）",
		Margin = new Thickness(0.0, 5.0, 0.0, 2.0)
	};

	public ProductCsvExportFormat Format
	{
		get
		{
			if (_extended.IsChecked != true)
			{
				if (_legacy.IsChecked != true)
				{
					return ProductCsvExportFormat.Official19;
				}
				return ProductCsvExportFormat.Legacy13;
			}
			return ProductCsvExportFormat.Extended25;
		}
	}

	public CsvExportFormatDialog()
	{
		base.Title = "CSV出力形式 – MISE";
		base.Width = 560.0;
		base.Height = 330.0;
		base.MinWidth = 470.0;
		base.MinHeight = 290.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 560.0, 330.0, 470.0, 290.0);
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(20.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "出力する商品CSVを選択",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "通常は正式19項目をお使いください。どの形式もMISEへ再インポートできます。",
			TextWrapping = TextWrapping.Wrap,
			Foreground = Brushes.SlateGray,
			Margin = new Thickness(0.0, 4.0, 0.0, 12.0)
		});
		stackPanel.Children.Add(_official);
		stackPanel.Children.Add(_extended);
		stackPanel.Children.Add(_legacy);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
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
			Content = "保存先を選ぶ",
			MinWidth = 120.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsDefault = true
		};
		button2.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel2.Children.Add(button);
		stackPanel2.Children.Add(button2);
		stackPanel.Children.Add(stackPanel2);
		base.Content = stackPanel;
	}
}
