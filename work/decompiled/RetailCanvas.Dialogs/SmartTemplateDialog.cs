using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class SmartTemplateDialog : Window
{
	private readonly DatabaseService _database;

	private readonly TextBox _search = new TextBox
	{
		MinHeight = 30.0,
		ToolTip = "製品名・型番・JAN・タグで検索"
	};

	private readonly ComboBox _category = new ComboBox
	{
		MinHeight = 30.0
	};

	private readonly ListBox _products = new ListBox
	{
		DisplayMemberPath = "ProductName"
	};

	private readonly ComboBox _templates = new ComboBox
	{
		MinHeight = 32.0
	};

	private readonly TextBlock _productTitle = new TextBlock
	{
		FontSize = 19.0,
		FontWeight = FontWeights.SemiBold,
		TextWrapping = TextWrapping.Wrap
	};

	private readonly TextBlock _productMeta = new TextBlock
	{
		Foreground = Brushes.SlateGray,
		Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
		TextWrapping = TextWrapping.Wrap
	};

	private readonly TextBlock _mappedFields = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		LineHeight = 21.0,
		Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
	};

	public ProductModel? SelectedProduct { get; private set; }

	public string TemplateName => (_templates.SelectedItem as string) ?? "製品単品訴求";

	public SmartTemplateDialog(DatabaseService database, TemplateService templateService)
	{
		_database = database;
		base.Title = "商品データからPOPを作成 － MISE";
		base.Width = 900.0;
		base.Height = 650.0;
		base.MinWidth = 640.0;
		base.MinHeight = 440.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 900.0, 650.0, 640.0, 440.0);
		_templates.ItemsSource = templateService.BuiltInNames.Concat(templateService.UserTemplates()).ToList();
		_templates.SelectedItem = "製品単品訴求";
		List<string> second = (from product in _database.Search()
			select product.Category into value
			where !string.IsNullOrWhiteSpace(value)
			select value).Distinct().Order().ToList();
		_category.ItemsSource = new string[1] { "すべて" }.Concat(second).ToList();
		_category.SelectedIndex = 0;
		base.Content = Build();
		_search.TextChanged += delegate
		{
			RefreshProducts();
		};
		_category.SelectionChanged += delegate
		{
			RefreshProducts();
		};
		_products.SelectionChanged += delegate
		{
			UpdatePreview();
		};
		_products.MouseDoubleClick += delegate
		{
			Accept();
		};
		RefreshProducts();
	}

	private UIElement Build()
	{
		Grid obj = new Grid
		{
			Background = new SolidColorBrush(Color.FromRgb(246, 248, 251)),
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition(),
				new RowDefinition
				{
					Height = GridLength.Auto
				}
			}
		};
		Border border = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(18, 27, 45)),
			Padding = new Thickness(20.0, 15.0, 20.0, 13.0)
		};
		border.Child = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = "商品データ連動テンプレート",
					FontSize = 21.0,
					FontWeight = FontWeights.SemiBold,
					Foreground = Brushes.White
				},
				(UIElement)new TextBlock
				{
					Text = "商品を選ぶだけで、登録情報をテンプレートへ自動配置します。",
					Foreground = new SolidColorBrush(Color.FromRgb(174, 220, 226)),
					Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
				}
			}
		};
		obj.Children.Add(border);
		Grid grid = new Grid
		{
			Margin = new Thickness(16.0)
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(0.46, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(14.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(0.54, GridUnitType.Star)
		});
		Grid grid2 = new Grid
		{
			RowDefinitions = 
			{
				new RowDefinition
				{
					Height = GridLength.Auto
				},
				new RowDefinition()
			}
		};
		Grid grid3 = new Grid
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		grid3.ColumnDefinitions.Add(new ColumnDefinition());
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(135.0)
		});
		_search.Margin = new Thickness(0.0, 0.0, 7.0, 0.0);
		grid3.Children.Add(_search);
		Grid.SetColumn(_category, 1);
		grid3.Children.Add(_category);
		grid2.Children.Add(grid3);
		_products.BorderBrush = new SolidColorBrush(Color.FromRgb(214, 220, 229));
		_products.Padding = new Thickness(4.0);
		Grid.SetRow(_products, 1);
		grid2.Children.Add(_products);
		grid.Children.Add(grid2);
		Border border2 = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(218, 224, 232)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(7.0),
			Padding = new Thickness(18.0)
		};
		border2.Child = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = "選択した商品",
					FontSize = 11.0,
					Foreground = new SolidColorBrush(Color.FromRgb(43, 182, 200)),
					FontWeight = FontWeights.Bold
				},
				(UIElement)_productTitle,
				(UIElement)_productMeta,
				(UIElement)new TextBlock
				{
					Text = "使用するテンプレート",
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 18.0, 0.0, 5.0)
				},
				(UIElement)_templates,
				(UIElement)_mappedFields
			}
		};
		Grid.SetColumn(border2, 2);
		grid.Children.Add(border2);
		Grid.SetRow(grid, 1);
		obj.Children.Add(grid);
		Border border3 = new Border
		{
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(224, 228, 235)),
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(16.0, 10.0, 16.0, 10.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 96.0,
			MinHeight = 34.0,
			Margin = new Thickness(4.0)
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "この商品で作成",
			MinWidth = 145.0,
			MinHeight = 34.0,
			Margin = new Thickness(4.0),
			Background = new SolidColorBrush(Color.FromRgb(242, 106, 33)),
			Foreground = Brushes.White
		};
		button2.Click += delegate
		{
			Accept();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		border3.Child = stackPanel;
		Grid.SetRow(border3, 2);
		obj.Children.Add(border3);
		return obj;
	}

	private void RefreshProducts()
	{
		List<ProductModel> list = _database.Search(_search.Text, _category.SelectedItem?.ToString());
		_products.ItemsSource = list;
		if (list.Count > 0)
		{
			_products.SelectedIndex = 0;
		}
		else
		{
			UpdatePreview();
		}
	}

	private void UpdatePreview()
	{
		SelectedProduct = _products.SelectedItem as ProductModel;
		if (SelectedProduct == null)
		{
			_productTitle.Text = "商品が見つかりません";
			_productMeta.Text = "検索条件を変更するか、商品データベースへ登録してください。";
			_mappedFields.Text = string.Empty;
			return;
		}
		_productTitle.Text = SelectedProduct.ProductName;
		_productMeta.Text = string.Join("  /  ", new string[3] { SelectedProduct.BrandName, SelectedProduct.ModelNumber, SelectedProduct.Category }.Where((string value) => !string.IsNullOrWhiteSpace(value)));
		(string, bool)[] source = new(string, bool)[6]
		{
			("画像", !string.IsNullOrWhiteSpace(SelectedProduct.ImagePath) || !string.IsNullOrWhiteSpace(SelectedProduct.AssetFolderPath)),
			("価格", SelectedProduct.Price.HasValue),
			("キャッチコピー", !string.IsNullOrWhiteSpace(SelectedProduct.CatchCopy)),
			("特徴", !string.IsNullOrWhiteSpace(SelectedProduct.Features)),
			("仕様", !string.IsNullOrWhiteSpace(SelectedProduct.Specifications)),
			("URL / QR", !string.IsNullOrWhiteSpace(SelectedProduct.Url))
		};
		_mappedFields.Text = "自動反映される情報\n" + string.Join("\n", source.Select(((string, bool) item) => (item.Item2 ? "✓" : "－") + " " + item.Item1));
	}

	private void Accept()
	{
		SelectedProduct = _products.SelectedItem as ProductModel;
		if (SelectedProduct == null)
		{
			MessageBox.Show(this, "商品を選択してください。", "商品データ連動テンプレート", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		else
		{
			base.DialogResult = true;
		}
	}
}
