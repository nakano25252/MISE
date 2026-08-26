using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ProductDatabaseDialog : Window
{
	private readonly DatabaseService _database;

	private readonly TextBox _search = new TextBox();

	private readonly ComboBox _categoryFilter = new ComboBox();

	private readonly DataGrid _grid = new DataGrid
	{
		AutoGenerateColumns = false,
		IsReadOnly = false,
		SelectionMode = DataGridSelectionMode.Extended,
		SelectionUnit = DataGridSelectionUnit.FullRow
	};

	private readonly Dictionary<string, TextBox> _fields = new Dictionary<string, TextBox>();

	private readonly ComboBox _category = new ComboBox();

	private ProductModel _editing = new ProductModel();

	private bool _refreshingList;

	private readonly TextBlock _selectionCount = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		Foreground = Brushes.SlateGray
	};

	private List<long> _lastTrashed = new List<long>();

	private readonly HashSet<long> _checkedProductIds = new HashSet<long>();

	private static readonly string[] Categories = new string[4] { "TWS", "ヘッドホン", "スピーカー", "サウンドバー" };

	public ProductModel? SelectedProduct { get; private set; }

	public ProductDatabaseDialog(DatabaseService database)
	{
		_database = database;
		base.Title = "商品データベース － MISE";
		base.Width = 1180.0;
		base.Height = 760.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 1180.0, 760.0, 640.0, 360.0);
		_categoryFilter.ItemsSource = new string[1] { "すべて" }.Concat(Categories).ToList();
		_categoryFilter.SelectedIndex = 0;
		_category.ItemsSource = Categories;
		_category.SelectedItem = "TWS";
		Build();
		RefreshProducts();
	}

	private void Build()
	{
		Grid grid = new Grid
		{
			Margin = new Thickness(14.0)
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
		Grid grid2 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(230.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(140.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition());
		_search.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		_search.ToolTip = "製品名・型番・JAN・タグで検索";
		_search.TextChanged += delegate
		{
			RefreshProducts();
		};
		grid2.Children.Add(_search);
		_categoryFilter.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		_categoryFilter.SelectionChanged += delegate
		{
			RefreshProducts();
		};
		Grid.SetColumn(_categoryFilter, 1);
		grid2.Children.Add(_categoryFilter);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button button = new Button
		{
			Content = "CSV読込"
		};
		button.Click += Import_Click;
		Button button2 = new Button
		{
			Content = "CSV出力"
		};
		button2.Click += Export_Click;
		Button button3 = new Button
		{
			Content = "新規商品"
		};
		button3.Click += New_Click;
		Button button4 = new Button
		{
			Content = "検索結果を全選択"
		};
		button4.Click += SelectAll_Click;
		Button button5 = new Button
		{
			Content = "選択解除"
		};
		button5.Click += ClearSelection_Click;
		Button button6 = new Button
		{
			Content = "ごみ箱"
		};
		button6.Click += Trash_Click;
		Button button7 = new Button
		{
			Content = "素材を一括紐づけ"
		};
		button7.Click += BatchLinkFolders_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button7);
		stackPanel.Children.Add(button4);
		stackPanel.Children.Add(button5);
		stackPanel.Children.Add(button6);
		stackPanel.Children.Add(button3);
		stackPanel.Children.Add(_selectionCount);
		Grid.SetColumn(stackPanel, 2);
		grid2.Children.Add(stackPanel);
		grid.Children.Add(grid2);
		Grid grid3 = new Grid();
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(3.0, GridUnitType.Star),
			MinWidth = 280.0
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(6.0)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(2.0, GridUnitType.Star),
			MinWidth = 260.0
		});
		Grid.SetRow(grid3, 1);
		grid.Children.Add(grid3);
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(CheckBox));
		frameworkElementFactory.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsSelectedForBatch")
		{
			Mode = BindingMode.TwoWay,
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		frameworkElementFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		_grid.Columns.Add(new DataGridTemplateColumn
		{
			Header = "選択",
			CellTemplate = new DataTemplate
			{
				VisualTree = frameworkElementFactory
			},
			Width = 52.0,
			IsReadOnly = false
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "ブランド",
			Binding = new Binding("BrandName"),
			Width = 100.0,
			IsReadOnly = true
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "カテゴリ",
			Binding = new Binding("Category"),
			Width = 90.0,
			IsReadOnly = true
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "製品名",
			Binding = new Binding("ProductName"),
			Width = new DataGridLength(1.0, DataGridLengthUnitType.Star),
			IsReadOnly = true
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "型番",
			Binding = new Binding("ModelNumber"),
			Width = 120.0,
			IsReadOnly = true
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "価格",
			Binding = new Binding("Price")
			{
				StringFormat = "{0:N0}"
			},
			Width = 90.0,
			IsReadOnly = true
		});
		_grid.Columns.Add(new DataGridTextColumn
		{
			Header = "更新",
			Binding = new Binding("UpdatedAt")
			{
				StringFormat = "{0:yyyy/MM/dd}"
			},
			Width = 95.0,
			IsReadOnly = true
		});
		_grid.SelectionChanged += Grid_SelectionChanged;
		_grid.CurrentCellChanged += delegate
		{
			UpdateSelectionCount();
		};
		_grid.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)delegate
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				CaptureCheckedIds();
				UpdateSelectionCount();
			});
		});
		_grid.ContextMenu = new ContextMenu();
		MenuItem menuItem = new MenuItem
		{
			Header = "選択状態へ追加"
		};
		menuItem.Click += delegate
		{
			foreach (ProductModel selectedItem in _grid.SelectedItems)
			{
				selectedItem.IsSelectedForBatch = true;
				if (selectedItem.ProductId != 0L)
				{
					_checkedProductIds.Add(selectedItem.ProductId);
				}
			}
			_grid.Items.Refresh();
			UpdateSelectionCount();
		};
		MenuItem menuItem2 = new MenuItem
		{
			Header = "選択した商品をごみ箱へ"
		};
		menuItem2.Click += Delete_Click;
		_grid.ContextMenu.Items.Add(menuItem);
		_grid.ContextMenu.Items.Add(menuItem2);
		_grid.MouseDoubleClick += delegate
		{
			Place_Click(this, new RoutedEventArgs());
		};
		grid3.Children.Add(_grid);
		GridSplitter element = new GridSplitter
		{
			Width = 6.0,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = new SolidColorBrush(Color.FromRgb(224, 228, 235)),
			ResizeDirection = GridResizeDirection.Columns
		};
		Grid.SetColumn(element, 1);
		grid3.Children.Add(element);
		Border border = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(221, 226, 234)),
			BorderThickness = new Thickness(1.0),
			Padding = new Thickness(12.0)
		};
		Grid.SetColumn(border, 2);
		grid3.Children.Add(border);
		UIElement uIElement = (border.Child = new DockPanel());
		DockPanel obj = (DockPanel)uIElement;
		WrapPanel wrapPanel = new WrapPanel
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		Button button8 = new Button
		{
			Content = "画像参照"
		};
		button8.Click += BrowseImage_Click;
		Button button9 = new Button
		{
			Content = "訴求カード"
		};
		button9.Click += SalesPoints_Click;
		Button button10 = new Button
		{
			Content = "素材フォルダ紐づけ"
		};
		button10.Click += LinkFolder_Click;
		Button button11 = new Button
		{
			Content = "素材画像の役割"
		};
		button11.Click += AssetRoles_Click;
		Button button12 = new Button
		{
			Content = "登録・更新",
			Style = (FindResource("PrimaryButton") as Style)
		};
		button12.Click += Save_Click;
		Button button13 = new Button
		{
			Content = "削除"
		};
		button13.Click += Delete_Click;
		wrapPanel.Children.Add(button9);
		wrapPanel.Children.Add(button10);
		wrapPanel.Children.Add(button11);
		wrapPanel.Children.Add(button8);
		wrapPanel.Children.Add(button12);
		wrapPanel.Children.Add(button13);
		DockPanel.SetDock(wrapPanel, Dock.Bottom);
		obj.Children.Add(wrapPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Children = { (UIElement)new TextBlock
			{
				Text = "商品情報",
				FontSize = 18.0,
				FontWeight = FontWeights.SemiBold,
				Foreground = (FindResource("NavyBrush") as Brush),
				Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
			} }
		};
		AddField(stackPanel2, "メーカー", "Manufacturer");
		AddField(stackPanel2, "ブランド", "BrandName");
		StackPanel stackPanel3 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "カテゴリ"
		});
		stackPanel3.Children.Add(_category);
		stackPanel2.Children.Add(stackPanel3);
		AddField(stackPanel2, "製品名 *", "ProductName");
		AddField(stackPanel2, "型番", "ModelNumber");
		AddField(stackPanel2, "JANコード", "JanCode");
		AddField(stackPanel2, "価格", "Price");
		AddField(stackPanel2, "発売日 (yyyy-MM-dd)", "ReleaseDate");
		AddField(stackPanel2, "カラーバリエーション", "Colors");
		AddField(stackPanel2, "キャッチコピー", "CatchCopy", multiline: true);
		AddField(stackPanel2, "製品特徴", "Features", multiline: true);
		AddField(stackPanel2, "主な仕様", "Specifications", multiline: true);
		AddField(stackPanel2, "コーデック／音声形式", "Codec");
		AddField(stackPanel2, "防水・防塵", "Waterproof");
		AddField(stackPanel2, "バッテリー", "Battery");
		AddField(stackPanel2, "重量", "Weight");
		AddField(stackPanel2, "画像パス", "ImagePath");
		AddField(stackPanel2, "素材フォルダ", "AssetFolderPath");
		AddField(stackPanel2, "URL", "Url");
		AddField(stackPanel2, "タグ", "Tags");
		AddField(stackPanel2, "販売トーク", "SalesTalk", multiline: true);
		AddField(stackPanel2, "注意事項", "Notes", multiline: true);
		AddField(stackPanel2, "情報元／更新状況", "SourceStatus", multiline: true);
		obj.Children.Add(new ScrollViewer
		{
			Content = stackPanel2,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Button button14 = new Button
		{
			Content = "閉じる",
			MinWidth = 90.0
		};
		button14.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button15 = new Button
		{
			Content = "選択商品を配置",
			MinWidth = 130.0,
			Style = (FindResource("PrimaryButton") as Style)
		};
		button15.Click += Place_Click;
		stackPanel4.Children.Add(button14);
		stackPanel4.Children.Add(button15);
		Grid.SetRow(stackPanel4, 2);
		grid.Children.Add(stackPanel4);
		base.Content = grid;
	}

	private void AddField(Panel panel, string label, string key, bool multiline = false)
	{
		TextBox textBox = new TextBox
		{
			TextWrapping = TextWrapping.Wrap,
			AcceptsReturn = multiline,
			MinHeight = (multiline ? 55 : 28),
			VerticalScrollBarVisibility = (multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden)
		};
		_fields[key] = textBox;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = label
		});
		stackPanel.Children.Add(textBox);
		panel.Children.Add(stackPanel);
	}

	private void RefreshProducts()
	{
		if (_refreshingList)
		{
			return;
		}
		_refreshingList = true;
		try
		{
			CaptureCheckedIds();
			string text = _categoryFilter.SelectedItem?.ToString();
			string text2 = _category.SelectedItem?.ToString();
			List<ProductModel> source = _database.Search();
			List<string> list = (from value in Categories.Concat(source.Select((ProductModel product) => product.Category))
				where !string.IsNullOrWhiteSpace(value)
				select value).Distinct().ToList();
			_categoryFilter.ItemsSource = new string[1] { "すべて" }.Concat(list).ToList();
			_categoryFilter.SelectedItem = ((text != null && _categoryFilter.Items.Contains(text)) ? text : "すべて");
			_category.ItemsSource = list;
			_category.SelectedItem = ((text2 != null && _category.Items.Contains(text2)) ? text2 : (list.FirstOrDefault() ?? "TWS"));
			List<ProductModel> list2 = _database.Search(_search.Text, _categoryFilter.SelectedItem?.ToString());
			foreach (ProductModel item in list2)
			{
				item.IsSelectedForBatch = item.ProductId != 0L && _checkedProductIds.Contains(item.ProductId);
			}
			_grid.ItemsSource = list2;
			UpdateSelectionCount();
		}
		catch
		{
		}
		finally
		{
			_refreshingList = false;
		}
	}

	private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_grid.SelectedItem is ProductModel productModel)
		{
			_editing = productModel;
			LoadEditor(productModel);
		}
	}

	private void LoadEditor(ProductModel p)
	{
		_fields["Manufacturer"].Text = p.Manufacturer;
		_fields["BrandName"].Text = p.BrandName;
		_category.SelectedItem = p.Category;
		_fields["ProductName"].Text = p.ProductName;
		_fields["ModelNumber"].Text = p.ModelNumber;
		_fields["JanCode"].Text = p.JanCode;
		_fields["Price"].Text = p.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
		_fields["ReleaseDate"].Text = p.ReleaseDate?.ToString("yyyy-MM-dd") ?? string.Empty;
		_fields["Colors"].Text = p.Colors;
		_fields["CatchCopy"].Text = p.CatchCopy;
		_fields["Features"].Text = p.Features;
		_fields["Specifications"].Text = p.Specifications;
		_fields["Codec"].Text = p.Codec;
		_fields["Waterproof"].Text = p.Waterproof;
		_fields["Battery"].Text = p.Battery;
		_fields["Weight"].Text = p.Weight;
		_fields["ImagePath"].Text = p.ImagePath;
		_fields["AssetFolderPath"].Text = p.AssetFolderPath;
		_fields["Url"].Text = p.Url;
		_fields["Tags"].Text = p.Tags;
		_fields["SalesTalk"].Text = p.SalesTalk;
		_fields["Notes"].Text = p.Notes;
		_fields["SourceStatus"].Text = p.SourceStatus;
	}

	private ProductModel ReadEditor()
	{
		_editing.Manufacturer = _fields["Manufacturer"].Text.Trim();
		_editing.BrandName = _fields["BrandName"].Text.Trim();
		_editing.Category = _category.SelectedItem?.ToString() ?? "TWS";
		_editing.ProductName = _fields["ProductName"].Text.Trim();
		_editing.ModelNumber = _fields["ModelNumber"].Text.Trim();
		_editing.JanCode = _fields["JanCode"].Text.Trim();
		_editing.Price = (decimal.TryParse(_fields["Price"].Text, out var result) ? new decimal?(result) : ((decimal?)null));
		_editing.ReleaseDate = (DateTime.TryParse(_fields["ReleaseDate"].Text, out var result2) ? new DateTime?(result2) : ((DateTime?)null));
		_editing.Colors = _fields["Colors"].Text.Trim();
		_editing.CatchCopy = _fields["CatchCopy"].Text.Trim();
		_editing.Features = _fields["Features"].Text.Trim();
		_editing.Specifications = _fields["Specifications"].Text.Trim();
		_editing.Codec = _fields["Codec"].Text.Trim();
		_editing.Waterproof = _fields["Waterproof"].Text.Trim();
		_editing.Battery = _fields["Battery"].Text.Trim();
		_editing.Weight = _fields["Weight"].Text.Trim();
		_editing.ImagePath = _fields["ImagePath"].Text.Trim();
		_editing.AssetFolderPath = _fields["AssetFolderPath"].Text.Trim();
		_editing.Url = _fields["Url"].Text.Trim();
		_editing.Tags = _fields["Tags"].Text.Trim();
		_editing.SalesTalk = _fields["SalesTalk"].Text.Trim();
		_editing.Notes = _fields["Notes"].Text.Trim();
		_editing.SourceStatus = _fields["SourceStatus"].Text.Trim();
		return _editing;
	}

	private void New_Click(object sender, RoutedEventArgs e)
	{
		_editing = new ProductModel();
		LoadEditor(_editing);
		_grid.SelectedItem = null;
		_fields["ProductName"].Focus();
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		ProductModel product = ReadEditor();
		if (string.IsNullOrWhiteSpace(product.ProductName))
		{
			MessageBox.Show("製品名を入力してください。", "入力確認");
			return;
		}
		try
		{
			product.ProductId = _database.Save(product);
			RefreshProducts();
			_grid.SelectedItem = (_grid.ItemsSource as IEnumerable<ProductModel>)?.FirstOrDefault((ProductModel x) => x.ProductId == product.ProductId);
			MessageBox.Show("商品情報を保存しました。", "商品データベース");
		}
		catch (Exception ex)
		{
			MessageBox.Show("商品情報を保存できませんでした。入力内容とデータベースの状態を確認してください。\n\n" + ex.Message, "保存エラー", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		List<ProductModel> list = BatchSelectedProducts();
		if (list.Count == 0 && _editing.ProductId != 0L)
		{
			list.Add(_editing);
		}
		string value = string.Join("\n", from product in list.Take(20)
			select "・" + product.ProductName);
		if (list.Count <= 0 || MessageBox.Show($"次の{list.Count}件をごみ箱へ移動します。\n元画像・素材フォルダは削除しません。\n\n{value}", "商品をごみ箱へ", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			return;
		}
		_lastTrashed = list.Select((ProductModel product) => product.ProductId).ToList();
		_database.MoveToTrash(_lastTrashed);
		foreach (long item in _lastTrashed)
		{
			_checkedProductIds.Remove(item);
		}
		New_Click(sender, e);
		RefreshProducts();
		if (MessageBox.Show("ごみ箱へ移動しました。今すぐ元に戻しますか？", "商品削除", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes)
		{
			_database.RestoreFromTrash(_lastTrashed);
			_lastTrashed.Clear();
			RefreshProducts();
		}
	}

	private void BrowseImage_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "画像|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|すべて|*.*"
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			_fields["ImagePath"].Text = openFileDialog.FileName;
		}
	}

	private void SalesPoints_Click(object sender, RoutedEventArgs e)
	{
		SalesPointSelectorDialog salesPointSelectorDialog = new SalesPointSelectorDialog(_category.SelectedItem?.ToString() ?? "TWS", _editing.SalesPointData)
		{
			Owner = this
		};
		if (salesPointSelectorDialog.ShowDialog() == true)
		{
			_editing.SalesPointData = salesPointSelectorDialog.ResultJson;
			if (!string.IsNullOrWhiteSpace(salesPointSelectorDialog.FeatureText))
			{
				_fields["Features"].Text = salesPointSelectorDialog.FeatureText;
			}
			if (!string.IsNullOrWhiteSpace(salesPointSelectorDialog.DetailText))
			{
				_fields["Specifications"].Text = salesPointSelectorDialog.DetailText;
			}
		}
	}

	private void LinkFolder_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "候補を検索する親フォルダを選択"
		};
		if (openFolderDialog.ShowDialog(this) == true)
		{
			FolderLinkSuggestionDialog folderLinkSuggestionDialog = new FolderLinkSuggestionDialog(_fields["ProductName"].Text, _fields["ModelNumber"].Text, openFolderDialog.FolderName)
			{
				Owner = this
			};
			if (folderLinkSuggestionDialog.ShowDialog() == true && folderLinkSuggestionDialog.SelectedFolder != null)
			{
				_fields["AssetFolderPath"].Text = folderLinkSuggestionDialog.SelectedFolder;
			}
		}
	}

	private void AssetRoles_Click(object sender, RoutedEventArgs e)
	{
		ReadEditor();
		if (string.IsNullOrWhiteSpace(_editing.AssetFolderPath) || !Directory.Exists(_editing.AssetFolderPath))
		{
			MessageBox.Show("先に素材フォルダを紐づけてください。", "素材画像");
			return;
		}
		ProductAssetRoleDialog productAssetRoleDialog = new ProductAssetRoleDialog(_editing)
		{
			Owner = this
		};
		if (productAssetRoleDialog.ShowDialog() == true)
		{
			_editing.AssetRoleData = productAssetRoleDialog.ResultJson;
			if (!string.IsNullOrWhiteSpace(productAssetRoleDialog.MainImagePath))
			{
				_editing.ImagePath = productAssetRoleDialog.MainImagePath;
				_fields["ImagePath"].Text = productAssetRoleDialog.MainImagePath;
			}
		}
	}

	private List<ProductModel> BatchSelectedProducts()
	{
		List<ProductModel> source = (_grid.ItemsSource as IEnumerable<ProductModel>)?.ToList() ?? new List<ProductModel>();
		IEnumerable<ProductModel> first = from product in _database.Search()
			where _checkedProductIds.Contains(product.ProductId)
			select product;
		IEnumerable<ProductModel> second = source.Where((ProductModel product) => product.IsSelectedForBatch);
		return first.Concat(second).Concat(_grid.SelectedItems.Cast<ProductModel>()).DistinctBy((ProductModel product) => product.ProductId)
			.ToList();
	}

	private void CaptureCheckedIds()
	{
		if (!(_grid.ItemsSource is IEnumerable<ProductModel> enumerable))
		{
			return;
		}
		foreach (ProductModel item in enumerable)
		{
			if (item.ProductId != 0L)
			{
				if (item.IsSelectedForBatch)
				{
					_checkedProductIds.Add(item.ProductId);
				}
				else
				{
					_checkedProductIds.Remove(item.ProductId);
				}
			}
		}
	}

	private void UpdateSelectionCount()
	{
		int count = BatchSelectedProducts().Count;
		_selectionCount.Text = $"選択 {count}件";
	}

	private void SelectAll_Click(object sender, RoutedEventArgs e)
	{
		if (_grid.ItemsSource is IEnumerable<ProductModel> enumerable)
		{
			foreach (ProductModel item in enumerable)
			{
				item.IsSelectedForBatch = true;
				if (item.ProductId != 0L)
				{
					_checkedProductIds.Add(item.ProductId);
				}
			}
		}
		_grid.Items.Refresh();
		UpdateSelectionCount();
	}

	private void ClearSelection_Click(object sender, RoutedEventArgs e)
	{
		_checkedProductIds.Clear();
		if (_grid.ItemsSource is IEnumerable<ProductModel> enumerable)
		{
			foreach (ProductModel item in enumerable)
			{
				item.IsSelectedForBatch = false;
				if (item.ProductId != 0L)
				{
					_checkedProductIds.Remove(item.ProductId);
				}
			}
		}
		_grid.SelectedItems.Clear();
		_grid.Items.Refresh();
		UpdateSelectionCount();
	}

	private void Trash_Click(object sender, RoutedEventArgs e)
	{
		ProductTrashDialog productTrashDialog = new ProductTrashDialog(_database);
		productTrashDialog.Owner = this;
		productTrashDialog.ShowDialog();
		RefreshProducts();
	}

	private void BatchLinkFolders_Click(object sender, RoutedEventArgs e)
	{
		List<ProductModel> list = BatchSelectedProducts();
		if (list.Count == 0)
		{
			list = (_grid.ItemsSource as IEnumerable<ProductModel>)?.ToList() ?? new List<ProductModel>();
		}
		if (list.Count == 0)
		{
			MessageBox.Show("紐づける商品がありません。", "素材フォルダ");
			return;
		}
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "製品フォルダを含む親フォルダを選択（複数可）",
			Multiselect = true
		};
		if (openFolderDialog.ShowDialog(this) != true)
		{
			return;
		}
		BatchFolderLinkDialog batchFolderLinkDialog = new BatchFolderLinkDialog(list, openFolderDialog.FolderNames)
		{
			Owner = this
		};
		if (batchFolderLinkDialog.ShowDialog() != true)
		{
			return;
		}
		int num = 0;
		foreach (BatchFolderLinkDialog.Match item in batchFolderLinkDialog.Matches.Where((BatchFolderLinkDialog.Match match) => match.Apply && Directory.Exists(match.FolderPath)))
		{
			item.Product.AssetFolderPath = item.FolderPath;
			if (string.IsNullOrWhiteSpace(item.Product.ImagePath))
			{
				item.Product.ImagePath = Directory.EnumerateFiles(item.FolderPath).FirstOrDefault((string path) => new string[6] { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".webp" }.Contains<string>(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) ?? string.Empty;
			}
			_database.Save(item.Product);
			num++;
		}
		RefreshProducts();
		MessageBox.Show($"{num}件の素材フォルダを紐づけました。", "素材フォルダ");
	}

	private void Import_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "CSV (*.csv)|*.csv"
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			ProductCsvPreview preview = _database.PreviewImportCsv(openFileDialog.FileName);
			CsvImportPreviewDialog csvImportPreviewDialog = new CsvImportPreviewDialog(preview)
			{
				Owner = this
			};
			if (csvImportPreviewDialog.ShowDialog() == true)
			{
				ProductCsvImportResult productCsvImportResult = _database.ApplyImport(preview, csvImportPreviewDialog.Options);
				RefreshProducts();
				string value = ((productCsvImportResult.Warnings.Count == 0) ? "" : ("\n\n警告（先頭20件）\n" + string.Join("\n", productCsvImportResult.Warnings.Take(20))));
				MessageBox.Show($"CSVインポートが完了しました。\n\n新規登録：{productCsvImportResult.Added}件\n更新：{productCsvImportResult.Updated}件\nスキップ：{productCsvImportResult.Skipped}件\n警告：{productCsvImportResult.Warnings.Count}件{value}", "CSV読込");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("CSVを読み込めませんでした。\n" + ex.Message);
		}
	}

	private void Export_Click(object sender, RoutedEventArgs e)
	{
		CsvExportFormatDialog csvExportFormatDialog = new CsvExportFormatDialog
		{
			Owner = this
		};
		if (csvExportFormatDialog.ShowDialog() != true)
		{
			return;
		}
		ProductCsvExportFormat format = csvExportFormatDialog.Format;
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "CSV (*.csv)|*.csv";
		saveFileDialog.DefaultExt = ".csv";
		SaveFileDialog saveFileDialog2 = saveFileDialog;
		saveFileDialog2.FileName = format switch
		{
			ProductCsvExportFormat.Extended25 => "MISE_Products_25fields.csv", 
			ProductCsvExportFormat.Legacy13 => "MISE_Products_13fields.csv", 
			_ => "MISE_Products_19fields.csv", 
		};
		SaveFileDialog saveFileDialog3 = saveFileDialog;
		if (saveFileDialog3.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			_database.ExportCsv(saveFileDialog3.FileName, format);
			MessageBox.Show("CSVを書き出しました。", "CSV出力");
		}
		catch (Exception ex)
		{
			MessageBox.Show("CSVを書き出せませんでした。\n" + ex.Message);
		}
	}

	private void Place_Click(object sender, RoutedEventArgs e)
	{
		ProductModel productModel = (_grid.SelectedItem as ProductModel) ?? ((_editing.ProductId != 0L) ? _editing : null);
		if (productModel == null)
		{
			MessageBox.Show("配置する商品を選択してください。", "商品データベース");
			return;
		}
		SelectedProduct = productModel;
		base.DialogResult = true;
	}
}
