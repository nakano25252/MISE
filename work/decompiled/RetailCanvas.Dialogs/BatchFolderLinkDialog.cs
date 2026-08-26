using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class BatchFolderLinkDialog : Window
{
	public sealed class Match
	{
		public bool Apply { get; set; } = true;

		public ProductModel Product { get; init; } = new ProductModel();

		public string ProductLabel => Product.ProductName + "  " + Product.ModelNumber;

		public string FolderPath { get; set; } = string.Empty;

		public double Score { get; set; }
	}

	public IReadOnlyList<Match> Matches { get; }

	public BatchFolderLinkDialog(IEnumerable<ProductModel> products, IEnumerable<string> roots)
	{
		base.Title = "素材フォルダの一括紐づけ － MISE";
		base.Width = 980.0;
		base.Height = 620.0;
		base.MinWidth = 720.0;
		base.MinHeight = 440.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 980.0, 620.0, 720.0, 440.0);
		List<string> folders = roots.Where(Directory.Exists).SelectMany(FolderLinkSuggestionDialog.ScanFolders).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToList();
		Matches = products.Select(delegate(ProductModel product)
		{
			var anon = (from folder in folders
				select new
				{
					folder = folder,
					score = FolderLinkSuggestionDialog.Similarity(product.ProductName + product.ModelNumber, Path.GetFileName(folder))
				} into x
				orderby x.score descending
				select x).FirstOrDefault();
			return new Match
			{
				Product = product,
				FolderPath = (anon?.folder ?? string.Empty),
				Score = (anon?.score ?? 0.0),
				Apply = (anon != null)
			};
		}).ToList();
		DockPanel dockPanel = new DockPanel
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
			Content = "選択した候補を紐づけ",
			MinWidth = 150.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsDefault = true
		};
		button2.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "製品名とフォルダ名から候補を作成",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "候補はチェックで除外でき、フォルダ欄は手動修正できます。",
			Foreground = Brushes.SlateGray
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		dockPanel.Children.Add(stackPanel2);
		DataGrid dataGrid = new DataGrid
		{
			ItemsSource = Matches,
			AutoGenerateColumns = false,
			CanUserAddRows = false
		};
		dataGrid.Columns.Add(new DataGridCheckBoxColumn
		{
			Header = "適用",
			Binding = new Binding("Apply")
			{
				Mode = BindingMode.TwoWay
			},
			Width = 55.0
		});
		dataGrid.Columns.Add(new DataGridTextColumn
		{
			Header = "製品",
			Binding = new Binding("ProductLabel"),
			IsReadOnly = true,
			Width = 230.0
		});
		dataGrid.Columns.Add(new DataGridTextColumn
		{
			Header = "一致度",
			Binding = new Binding("Score")
			{
				StringFormat = "{0:P0}"
			},
			IsReadOnly = true,
			Width = 75.0
		});
		dataGrid.Columns.Add(new DataGridTextColumn
		{
			Header = "素材フォルダ候補（編集可）",
			Binding = new Binding("FolderPath")
			{
				Mode = BindingMode.TwoWay
			},
			Width = new DataGridLength(1.0, DataGridLengthUnitType.Star)
		});
		dockPanel.Children.Add(dataGrid);
		base.Content = dockPanel;
	}
}
