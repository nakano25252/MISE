using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class CsvImportPreviewDialog : Window
{
	private readonly ComboBox _duplicates = new ComboBox
	{
		MinWidth = 210.0
	};

	private readonly CheckBox _clearBlanks = new CheckBox
	{
		Content = "CSVの空欄で既存データを削除する（通常はオフ）"
	};

	public ProductCsvImportOptions Options { get; } = new ProductCsvImportOptions();

	public CsvImportPreviewDialog(ProductCsvPreview preview)
	{
		base.Title = "CSVインポート前の確認 － MISE";
		base.Width = 720.0;
		base.Height = 620.0;
		base.MinWidth = 560.0;
		base.MinHeight = 420.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 620.0, 560.0, 420.0);
		_duplicates.ItemsSource = new string[3] { "既存商品を更新", "新規商品として追加", "重複行をスキップ" };
		_duplicates.SelectedIndex = 0;
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
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = "読み込み内容を確認してください",
					FontSize = 20.0,
					FontWeight = FontWeights.SemiBold
				},
				(UIElement)new TextBlock
				{
					Text = $"文字コード：{preview.EncodingName}\n読込対象：{preview.ImportableCount}件\u3000新規：{preview.NewCount}件\u3000更新候補：{preview.UpdateCount}件\u3000スキップ：{preview.SkipCount}件\u3000警告：{preview.WarningCount}件",
					Margin = new Thickness(0.0, 8.0, 0.0, 12.0)
				}
			}
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "重複時：",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		});
		stackPanel2.Children.Add(_duplicates);
		stackPanel.Children.Add(stackPanel2);
		_clearBlanks.Margin = new Thickness(0.0, 8.0, 0.0, 12.0);
		stackPanel.Children.Add(_clearBlanks);
		Grid.SetRow(stackPanel, 0);
		grid.Children.Add(stackPanel);
		string text = string.Join("\n", preview.Rows.SelectMany((ProductCsvRow row) => row.Warnings.Select((string warning) => $"{row.RowNumber}行目：{warning}")));
		TextBox element = new TextBox
		{
			IsReadOnly = true,
			TextWrapping = TextWrapping.Wrap,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Text = "認識済み列\n" + string.Join(" / ", preview.RecognizedHeaders) + "\n\n未認識列\n" + ((preview.UnknownHeaders.Count == 0) ? "なし" : string.Join(" / ", preview.UnknownHeaders)) + "\n\n警告\n" + (string.IsNullOrWhiteSpace(text) ? "なし" : text)
		};
		Grid.SetRow(element, 1);
		grid.Children.Add(element);
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 100.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "インポート実行",
			MinWidth = 130.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button2.Click += delegate
		{
			ProductCsvImportOptions options = Options;
			options.DuplicateMode = _duplicates.SelectedIndex switch
			{
				1 => ProductCsvDuplicateMode.Add, 
				2 => ProductCsvDuplicateMode.Skip, 
				_ => ProductCsvDuplicateMode.Update, 
			};
			Options.ClearExistingOnBlank = _clearBlanks.IsChecked == true;
			base.DialogResult = true;
		};
		stackPanel3.Children.Add(button);
		stackPanel3.Children.Add(button2);
		Grid.SetRow(stackPanel3, 2);
		grid.Children.Add(stackPanel3);
		base.Content = grid;
	}
}
