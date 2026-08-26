using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ProductTrashDialog : Window
{
	private readonly DatabaseService _database;

	private readonly ListBox _list = new ListBox
	{
		SelectionMode = SelectionMode.Extended,
		DisplayMemberPath = "ProductName"
	};

	private readonly CheckBox _deleteAssets = new CheckBox
	{
		Content = "紐づく元画像・製品素材フォルダも削除する（通常はオフ）"
	};

	public ProductTrashDialog(DatabaseService database)
	{
		_database = database;
		base.Title = "商品のごみ箱 － MISE";
		base.Width = 680.0;
		base.Height = 520.0;
		base.MinWidth = 500.0;
		base.MinHeight = 360.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 680.0, 520.0, 500.0, 360.0);
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
			Content = "選択項目を復元"
		};
		button.Click += Restore;
		Button button2 = new Button
		{
			Content = "完全削除",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button2.Click += Purge;
		Button button3 = new Button
		{
			Content = "閉じる",
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button3.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "商品のごみ箱",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "Ctrl／Shiftで複数選択できます。素材データは明示的に指定しない限り削除しません。",
			Foreground = Brushes.SlateGray
		});
		stackPanel2.Children.Add(_deleteAssets);
		DockPanel.SetDock(stackPanel2, Dock.Top);
		dockPanel.Children.Add(stackPanel2);
		dockPanel.Children.Add(_list);
		base.Content = dockPanel;
		Refresh();
	}

	private List<ProductModel> Selected()
	{
		return _list.SelectedItems.Cast<ProductModel>().ToList();
	}

	private void Refresh()
	{
		_list.ItemsSource = _database.SearchDeleted();
	}

	private void Restore(object? sender, RoutedEventArgs e)
	{
		List<ProductModel> list = Selected();
		if (list.Count != 0)
		{
			_database.RestoreFromTrash(list.Select((ProductModel x) => x.ProductId));
			Refresh();
		}
	}

	private void Purge(object? sender, RoutedEventArgs e)
	{
		List<ProductModel> list = Selected();
		if (list.Count == 0)
		{
			return;
		}
		string value = string.Join("\n", from x in list.Take(20)
			select "・" + x.ProductName);
		if (MessageBox.Show($"{list.Count}件を完全削除します。元に戻せません。\n\n{value}", "完全削除", MessageBoxButton.YesNo, MessageBoxImage.Hand) != MessageBoxResult.Yes)
		{
			return;
		}
		if (_deleteAssets.IsChecked == true)
		{
			foreach (ProductModel item in list)
			{
				DeleteAssetsSafely(item);
			}
		}
		_database.PermanentlyDelete(list.Select((ProductModel x) => x.ProductId));
		Refresh();
	}

	private static void DeleteAssetsSafely(ProductModel product)
	{
		try
		{
			if (File.Exists(product.ImagePath))
			{
				File.Delete(product.ImagePath);
			}
		}
		catch
		{
		}
		try
		{
			if (Directory.Exists(product.AssetFolderPath))
			{
				string text = Path.GetFileName(product.AssetFolderPath).ToUpperInvariant();
				if (((!string.IsNullOrWhiteSpace(product.ModelNumber) && text.Contains(product.ModelNumber.ToUpperInvariant())) || (!string.IsNullOrWhiteSpace(product.ProductName) && text.Contains(product.ProductName.ToUpperInvariant()))) && product.AssetFolderPath.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Length >= 3)
				{
					Directory.Delete(product.AssetFolderPath, recursive: true);
				}
			}
		}
		catch
		{
		}
	}
}
