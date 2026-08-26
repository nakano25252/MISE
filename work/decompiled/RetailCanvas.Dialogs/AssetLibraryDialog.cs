using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class AssetLibraryDialog : Window
{
	private sealed class AssetEntry
	{
		public string Path { get; }

		public string Name { get; }

		public string Extension { get; }

		public long Bytes { get; }

		public bool OnlineOnly { get; }

		public BitmapSource? Thumbnail { get; }

		public DateTime LastWriteTime { get; }

		public int PixelWidth => Thumbnail?.PixelWidth ?? 0;

		public int PixelHeight => Thumbnail?.PixelHeight ?? 0;

		public long PixelArea => (long)PixelWidth * (long)PixelHeight;

		public bool IsChecked { get; set; }

		public string Label => $"{(IsLarge ? "⚠ " : string.Empty)}{(OnlineOnly ? "☁ " : string.Empty)}{Name}   [{Extension.TrimStart('.').ToUpperInvariant()} / {FormatSize(Bytes)}]";

		public bool IsLarge => Bytes >= 31457280;

		public AssetEntry(string path, string name, string extension, long bytes, bool onlineOnly, BitmapSource? thumbnail, DateTime lastWriteTime)
		{
			Path = path;
			Name = name;
			Extension = extension;
			Bytes = bytes;
			OnlineOnly = onlineOnly;
			Thumbnail = thumbnail;
			LastWriteTime = lastWriteTime;
		}
	}

	private sealed record ScanSummary(int CategoryFolders, int ProductFolders, int ImageFiles);

	private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp" };

	private readonly TreeView _folders = new TreeView();

	private readonly ListBox _files = new ListBox();

	private readonly Image _preview = new Image
	{
		Stretch = Stretch.Uniform
	};

	private readonly TextBlock _details = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap
	};

	private readonly TextBlock _warning = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Foreground = new SolidColorBrush(Color.FromRgb(190, 72, 38))
	};

	private readonly TextBlock _scanStatus = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Foreground = Brushes.SlateGray,
		Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly TextBlock _selectionCount = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		Foreground = Brushes.SlateGray,
		Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
	};

	private readonly ComboBox _sort = new ComboBox
	{
		Width = 150.0,
		ItemsSource = new string[9] { "名前（昇順）", "名前（降順）", "容量（小さい順）", "容量（大きい順）", "拡張子順", "更新日（新しい順）", "更新日（古い順）", "画像寸法（小さい順）", "画像寸法（大きい順）" },
		SelectedIndex = 0,
		ToolTip = "表示中の素材を並び替えます"
	};

	private readonly List<string> _roots;

	private int _previewRequest;

	public string? SelectedFile { get; private set; }

	public IReadOnlyList<string> SelectedFiles { get; private set; } = Array.Empty<string>();

	public string? SelectedFolder { get; private set; }

	public IReadOnlyList<string> FolderRoots => _roots;

	public AssetLibraryDialog(IEnumerable<string> initialFolders)
	{
		base.Title = "素材ライブラリ － MISE";
		base.Width = 1120.0;
		base.Height = 710.0;
		base.MinWidth = 760.0;
		base.MinHeight = 480.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 1120.0, 710.0, 760.0, 480.0);
		_roots = initialFolders.Where(Directory.Exists).Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList();
		if (_roots.Count == 0)
		{
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			if (Directory.Exists(folderPath))
			{
				_roots.Add(folderPath);
			}
		}
		base.Content = Build();
		_folders.SelectedItemChanged += FolderSelected;
		_files.SelectionChanged += FileSelected;
		_files.MouseDoubleClick += delegate
		{
			AddSelectedImage();
		};
		_sort.SelectionChanged += delegate
		{
			ReapplySort();
		};
		RefreshRoots();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(16.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "閉じる",
			MinWidth = 90.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "製品フォルダを登録",
			MinWidth = 140.0,
			ToolTip = "選択フォルダを製品単位の素材セットとして登録します"
		};
		button2.Click += delegate
		{
			UseSelectedFolder();
		};
		Button button3 = new Button
		{
			Content = "選択画像を配置",
			MinWidth = 130.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button3.Click += delegate
		{
			AddSelectedImage();
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		stackPanel.Children.Add(button3);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "素材ライブラリ",
			FontSize = 22.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "親フォルダを一度登録すると、カテゴリ → 製品名フォルダ → 画像の階層で利用できます。クラウド素材は選択したプレビュー／配置画像だけをオンデマンド取得します。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap
		});
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 7.0, 0.0, 0.0)
		};
		Button button4 = new Button
		{
			Content = "＋ 親フォルダをスキャン登録",
			ToolTip = "TWS等のカテゴリフォルダを含む上位フォルダを選択します"
		};
		button4.Click += ScanRoot;
		Button button5 = new Button
		{
			Content = "－ 選択ルートを解除"
		};
		button5.Click += RemoveRoot;
		Button button6 = new Button
		{
			Content = "表示中を全選択"
		};
		button6.Click += delegate
		{
			if (_files.ItemsSource is IEnumerable<AssetEntry> enumerable)
			{
				foreach (AssetEntry item in enumerable)
				{
					item.IsChecked = true;
				}
			}
			_files.Items.Refresh();
			UpdateSelectionCount();
		};
		Button button7 = new Button
		{
			Content = "選択解除"
		};
		button7.Click += delegate
		{
			if (_files.ItemsSource is IEnumerable<AssetEntry> enumerable)
			{
				foreach (AssetEntry item2 in enumerable)
				{
					item2.IsChecked = false;
				}
			}
			_files.SelectedItems.Clear();
			_files.Items.Refresh();
			UpdateSelectionCount();
		};
		Button button8 = new Button
		{
			Content = "ごみ箱へ"
		};
		button8.Click += MoveSelectedToTrash;
		Button button9 = new Button
		{
			Content = "素材ごみ箱"
		};
		button9.Click += delegate
		{
			AssetTrashDialog assetTrashDialog = new AssetTrashDialog();
			assetTrashDialog.Owner = this;
			assetTrashDialog.ShowDialog();
		};
		stackPanel3.Children.Add(button4);
		stackPanel3.Children.Add(button5);
		stackPanel3.Children.Add(button6);
		stackPanel3.Children.Add(button7);
		stackPanel3.Children.Add(button8);
		stackPanel3.Children.Add(button9);
		stackPanel3.Children.Add(new TextBlock
		{
			Text = "並び順",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(10.0, 0.0, 4.0, 0.0)
		});
		stackPanel3.Children.Add(_sort);
		stackPanel3.Children.Add(_selectionCount);
		stackPanel3.Children.Add(_scanStatus);
		stackPanel2.Children.Add(stackPanel3);
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition
				{
					Width = new GridLength(290.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(6.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(345.0)
				},
				new ColumnDefinition
				{
					Width = new GridLength(6.0)
				},
				new ColumnDefinition()
			}
		};
		_folders.BorderBrush = Brushes.LightGray;
		grid.Children.Add(_folders);
		GridSplitter element = new GridSplitter
		{
			Width = 6.0,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = new SolidColorBrush(Color.FromRgb(232, 235, 239))
		};
		Grid.SetColumn(element, 1);
		grid.Children.Add(element);
		_files.SelectionMode = SelectionMode.Extended;
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(StackPanel));
		frameworkElementFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(CheckBox));
		frameworkElementFactory2.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked")
		{
			Mode = BindingMode.TwoWay,
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
		});
		frameworkElementFactory2.SetValue(FrameworkElement.MarginProperty, new Thickness(2.0, 0.0, 7.0, 0.0));
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(Image));
		frameworkElementFactory3.SetBinding(Image.SourceProperty, new Binding("Thumbnail"));
		frameworkElementFactory3.SetValue(FrameworkElement.WidthProperty, 54.0);
		frameworkElementFactory3.SetValue(FrameworkElement.HeightProperty, 42.0);
		frameworkElementFactory3.SetValue(Image.StretchProperty, Stretch.Uniform);
		frameworkElementFactory3.SetValue(FrameworkElement.MarginProperty, new Thickness(0.0, 2.0, 8.0, 2.0));
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(TextBlock));
		frameworkElementFactory4.SetBinding(TextBlock.TextProperty, new Binding("Label"));
		Style style = new Style(typeof(TextBlock));
		DataTrigger dataTrigger = new DataTrigger
		{
			Binding = new Binding("IsLarge"),
			Value = true
		};
		dataTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Firebrick));
		dataTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
		style.Triggers.Add(dataTrigger);
		frameworkElementFactory4.SetValue(FrameworkElement.StyleProperty, style);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		frameworkElementFactory.AppendChild(frameworkElementFactory3);
		frameworkElementFactory.AppendChild(frameworkElementFactory4);
		_files.ItemTemplate = new DataTemplate
		{
			VisualTree = frameworkElementFactory
		};
		_files.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)delegate
		{
			UpdateSelectionCount();
		});
		Grid.SetColumn(_files, 2);
		grid.Children.Add(_files);
		GridSplitter element2 = new GridSplitter
		{
			Width = 6.0,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			Background = new SolidColorBrush(Color.FromRgb(232, 235, 239))
		};
		Grid.SetColumn(element2, 3);
		grid.Children.Add(element2);
		StackPanel stackPanel4 = new StackPanel
		{
			Margin = new Thickness(10.0, 0.0, 0.0, 0.0)
		};
		stackPanel4.Children.Add(new Border
		{
			Height = 370.0,
			Background = new SolidColorBrush(Color.FromRgb(235, 238, 242)),
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(1.0),
			Child = _preview
		});
		_details.Margin = new Thickness(0.0, 10.0, 0.0, 5.0);
		stackPanel4.Children.Add(_details);
		stackPanel4.Children.Add(_warning);
		Grid.SetColumn(stackPanel4, 4);
		grid.Children.Add(stackPanel4);
		obj.Children.Add(grid);
		return obj;
	}

	private void RefreshRoots()
	{
		_folders.Items.Clear();
		foreach (string root in _roots)
		{
			_folders.Items.Add(CreateFolder(root, isRoot: true, 0));
		}
	}

	private TreeViewItem CreateFolder(string path, bool isRoot, int depth)
	{
		int num = CountImages(path, recursive: false);
		string text = (isRoot ? path : Path.GetFileName(path));
		string text2 = ((num > 0) ? $"  [製品素材 {num}点]" : ((depth == 1) ? "  [カテゴリ]" : string.Empty));
		TreeViewItem item = new TreeViewItem
		{
			Header = text + text2,
			Tag = path,
			ToolTip = path
		};
		if (HasSubfolder(path))
		{
			item.Items.Add(new TreeViewItem
			{
				Header = "読込中…",
				Tag = null
			});
		}
		item.Expanded += delegate
		{
			if (item.Items.Count != 1 || !(item.Items[0] is TreeViewItem { Tag: null }))
			{
				return;
			}
			item.Items.Clear();
			try
			{
				foreach (string item2 in Directory.EnumerateDirectories(path).OrderBy(Path.GetFileName))
				{
					item.Items.Add(CreateFolder(item2, isRoot: false, depth + 1));
				}
			}
			catch (Exception ex)
			{
				item.Items.Add(new TreeViewItem
				{
					Header = "読込できません: " + ex.Message,
					IsEnabled = false
				});
			}
		};
		return item;
	}

	private static bool HasSubfolder(string path)
	{
		try
		{
			return Directory.EnumerateDirectories(path).Any();
		}
		catch
		{
			return false;
		}
	}

	private void FolderSelected(object? sender, RoutedPropertyChangedEventArgs<object> e)
	{
		if (_folders.SelectedItem is TreeViewItem { Tag: string tag })
		{
			SelectedFolder = tag;
			try
			{
				List<AssetEntry> entries = Directory.EnumerateFiles(tag, "*", SearchOption.TopDirectoryOnly).Where(IsAssetFile).Select(CreateEntry)
					.ToList();
				_files.ItemsSource = SortEntries(entries);
				_scanStatus.Text = $"選択: {Path.GetFileName(tag)}（直下 {CountImages(tag, recursive: false)}点／配下 {CountImages(tag, recursive: true)}点）";
			}
			catch (Exception ex)
			{
				_files.ItemsSource = null;
				_scanStatus.Text = "フォルダを参照できません: " + ex.Message;
			}
			_preview.Source = null;
			_details.Text = "";
			_warning.Text = "";
		}
	}

	private async void FileSelected(object? sender, SelectionChangedEventArgs e)
	{
		UpdateSelectionCount();
		object selectedItem = _files.SelectedItem;
		AssetEntry entry = selectedItem as AssetEntry;
		if (entry == null)
		{
			return;
		}
		int request = ++_previewRequest;
		_preview.Source = null;
		_details.Text = $"{entry.Name}\n形式: {entry.Extension.TrimStart('.').ToUpperInvariant()}\n容量: {(double)entry.Bytes / 1024.0 / 1024.0:0.00} MB";
		if (entry.IsLarge && MessageBox.Show($"この素材は{(double)entry.Bytes / 1024.0 / 1024.0:0.0}MBあります。\nプレビューのためにダウンロード／読み込みしますか？", "大容量素材", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
		{
			_warning.Text = "30MB以上のため、プレビューを保留しました。";
			return;
		}
		_warning.Text = (entry.OnlineOnly ? "クラウド上のオンライン専用素材です。プレビューに必要なこの画像だけを一時取得しています…" : "プレビューを読み込んでいます…");
		try
		{
			byte[] data = await Task.Run(() => File.ReadAllBytes(entry.Path));
			if (request == _previewRequest)
			{
				BitmapSource bitmapSource = Decode(data);
				_preview.Source = bitmapSource;
				double value = ((bitmapSource.DpiX > 0.0) ? bitmapSource.DpiX : 96.0);
				double value2 = ((bitmapSource.DpiY > 0.0) ? bitmapSource.DpiY : 96.0);
				_details.Text = $"{entry.Name}\n形式: {entry.Extension.TrimStart('.').ToUpperInvariant()}\n容量: {(double)entry.Bytes / 1024.0 / 1024.0:0.00} MB\n画像: {bitmapSource.PixelWidth} × {bitmapSource.PixelHeight} px\nDPI: {value:0.#} × {value2:0.#}";
				if (entry.Extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) || entry.Extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
				{
					_warning.Text = (entry.IsLarge ? "注意: 30MB以上の大容量TIFFです。配置時は軽量プレビューを使い、出力時に元画像を参照します。" : "TIFFを読み込みました。PNG/JPEGよりプロジェクト容量が大きくなりやすい形式です。");
				}
				else if (entry.IsLarge)
				{
					_warning.Text = "注意: 30MB以上の画像です。配置後は軽量プレビューを使います。";
				}
				else
				{
					_warning.Text = (entry.OnlineOnly ? "クラウドからオンデマンドで一時取得しました。フォルダ全体の事前ダウンロードは不要です。" : string.Empty);
				}
			}
		}
		catch (Exception ex)
		{
			if (request == _previewRequest)
			{
				_preview.Source = null;
				_warning.Text = "プレビューできません。クラウドへ接続できるか、ファイルのアクセス権を確認してください。\n" + ex.Message;
			}
		}
	}

	private void AddSelectedImage()
	{
		List<AssetEntry> list = SelectedEntries();
		if (list.Count == 0)
		{
			MessageBox.Show("配置する画像を選択してください。", "素材ライブラリ");
			return;
		}
		long num = list.Count((AssetEntry entry) => entry.IsLarge);
		if (num <= 0 || MessageBox.Show($"選択中に30MB以上の素材が{num}点あります。\n配置に必要なデータを読み込みますか？", "大容量素材", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
		{
			SelectedFiles = list.Select((AssetEntry entry) => entry.Path).ToList();
			SelectedFile = SelectedFiles.FirstOrDefault();
			base.DialogResult = true;
		}
	}

	private List<AssetEntry> SelectedEntries()
	{
		return ((_files.ItemsSource as IEnumerable<AssetEntry>)?.Where((AssetEntry entry) => entry.IsChecked) ?? Enumerable.Empty<AssetEntry>()).Concat(_files.SelectedItems.Cast<AssetEntry>()).DistinctBy<AssetEntry, string>((AssetEntry entry) => entry.Path, StringComparer.OrdinalIgnoreCase).ToList();
	}

	private void UpdateSelectionCount()
	{
		_selectionCount.Text = $"選択 {SelectedEntries().Count}点";
	}

	private void MoveSelectedToTrash(object? sender, RoutedEventArgs e)
	{
		List<AssetEntry> list = SelectedEntries();
		if (list.Count == 0 || (list.Any((AssetEntry entry) => entry.OnlineOnly) && MessageBox.Show("オンライン専用素材を含みます。ごみ箱への移動にはダウンロードが必要な場合があります。続けますか？", "素材削除", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes) || MessageBox.Show($"選択した{list.Count}点をMISEの素材ごみ箱へ移動しますか？", "素材削除", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			new AssetTrashService().MoveToTrash(list.Select((AssetEntry entry) => entry.Path));
			ReloadSelectedFolder();
		}
		catch (Exception ex)
		{
			MessageBox.Show("素材をごみ箱へ移動できませんでした。\n" + ex.Message, "素材削除");
		}
	}

	private void ReloadSelectedFolder()
	{
		if (!string.IsNullOrWhiteSpace(SelectedFolder) && Directory.Exists(SelectedFolder))
		{
			_files.ItemsSource = SortEntries(Directory.EnumerateFiles(SelectedFolder, "*", SearchOption.TopDirectoryOnly).Where(IsAssetFile).Select(CreateEntry));
			_preview.Source = null;
			UpdateSelectionCount();
		}
	}

	private void ReapplySort()
	{
		if (!(_files.ItemsSource is IEnumerable<AssetEntry> entries))
		{
			return;
		}
		string selectedPath = (_files.SelectedItem as AssetEntry)?.Path;
		List<AssetEntry> list = SortEntries(entries).ToList();
		_files.ItemsSource = list;
		if (selectedPath != null)
		{
			_files.SelectedItem = list.FirstOrDefault((AssetEntry entry) => entry.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
		}
		UpdateSelectionCount();
	}

	private IEnumerable<AssetEntry> SortEntries(IEnumerable<AssetEntry> entries)
	{
		return (_sort.SelectedItem?.ToString() ?? "名前（昇順）") switch
		{
			"名前（降順）" => entries.OrderByDescending<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"容量（小さい順）" => entries.OrderBy((AssetEntry entry) => entry.Bytes).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"容量（大きい順）" => entries.OrderByDescending((AssetEntry entry) => entry.Bytes).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"拡張子順" => entries.OrderBy<AssetEntry, string>((AssetEntry entry) => entry.Extension, StringComparer.OrdinalIgnoreCase).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"更新日（新しい順）" => entries.OrderByDescending((AssetEntry entry) => entry.LastWriteTime).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"更新日（古い順）" => entries.OrderBy((AssetEntry entry) => entry.LastWriteTime).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"画像寸法（小さい順）" => entries.OrderBy((AssetEntry entry) => entry.PixelArea).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			"画像寸法（大きい順）" => entries.OrderByDescending((AssetEntry entry) => entry.PixelArea).ThenBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
			_ => entries.OrderBy<AssetEntry, string>((AssetEntry entry) => entry.Name, StringComparer.CurrentCultureIgnoreCase), 
		};
	}

	private void UseSelectedFolder()
	{
		if (string.IsNullOrWhiteSpace(SelectedFolder))
		{
			MessageBox.Show("製品名のフォルダを選択してください。", "素材ライブラリ");
			return;
		}
		int num = CountImages(SelectedFolder, recursive: true);
		if (num == 0)
		{
			MessageBox.Show("選択フォルダ内に対応画像がありません。", "素材ライブラリ");
			return;
		}
		SelectedFile = null;
		_scanStatus.Text = $"製品素材「{Path.GetFileName(SelectedFolder)}」を登録しました（{num}点）";
		base.DialogResult = true;
	}

	private async void ScanRoot(object? sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "カテゴリフォルダを含む親フォルダを選択（複数選択可）",
			Multiselect = true
		};
		if (openFolderDialog.ShowDialog(this) != true)
		{
			return;
		}
		_scanStatus.Text = "フォルダ階層をスキャン中…（画像本体はダウンロードしません）";
		try
		{
			string[] paths = openFolderDialog.FolderNames;
			ScanSummary[] source = await Task.Run(() => paths.Select(Scan).ToArray());
			string[] array = paths;
			foreach (string text in array)
			{
				if (!_roots.Contains<string>(text, StringComparer.OrdinalIgnoreCase))
				{
					_roots.Add(text);
				}
			}
			RefreshRoots();
			_scanStatus.Text = $"{paths.Length}ルート登録: カテゴリ {source.Sum((ScanSummary summary) => summary.CategoryFolders)}／製品 {source.Sum((ScanSummary summary) => summary.ProductFolders)}／画像 {source.Sum((ScanSummary summary) => summary.ImageFiles)}点";
		}
		catch (Exception ex)
		{
			_scanStatus.Text = "スキャンできません: " + ex.Message;
		}
	}

	private void RemoveRoot(object? sender, RoutedEventArgs e)
	{
		if (!(_folders.SelectedItem is TreeViewItem { Tag: var tag }))
		{
			return;
		}
		string path = tag as string;
		if (path != null)
		{
			string text = _roots.FirstOrDefault((string x) => path.Equals(x, StringComparison.OrdinalIgnoreCase));
			if (text == null)
			{
				MessageBox.Show("解除するには一番上のルートフォルダを選択してください。", "素材ライブラリ");
				return;
			}
			_roots.Remove(text);
			RefreshRoots();
		}
	}

	private static ScanSummary Scan(string root)
	{
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Stack<(string, int)> stack = new Stack<(string, int)>();
		stack.Push((root, 0));
		while (stack.Count > 0)
		{
			var (path, num4) = stack.Pop();
			if (num4 > 12 || !hashSet.Add(Path.GetFullPath(path)))
			{
				continue;
			}
			int num5 = 0;
			try
			{
				num5 = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Count(IsAssetFile);
			}
			catch
			{
			}
			num3 += num5;
			if (num5 > 0)
			{
				num2++;
			}
			if (num4 == 1)
			{
				num++;
			}
			try
			{
				foreach (string item in Directory.EnumerateDirectories(path))
				{
					stack.Push((item, num4 + 1));
				}
			}
			catch
			{
			}
		}
		return new ScanSummary(num, num2, num3);
	}

	private static int CountImages(string path, bool recursive)
	{
		try
		{
			return Directory.EnumerateFiles(path, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Count(IsAssetFile);
		}
		catch
		{
			return 0;
		}
	}

	private static bool IsAssetFile(string path)
	{
		return Extensions.Contains(Path.GetExtension(path));
	}

	private static AssetEntry CreateEntry(string path)
	{
		FileInfo fileInfo = new FileInfo(path);
		long num;
		try
		{
			num = fileInfo.Length;
		}
		catch
		{
			num = 0L;
		}
		DateTime lastWriteTime;
		try
		{
			lastWriteTime = fileInfo.LastWriteTime;
		}
		catch
		{
			lastWriteTime = DateTime.MinValue;
		}
		bool onlineOnly = IsOnlineOnly(path);
		return new AssetEntry(path, fileInfo.Name, fileInfo.Extension, num, onlineOnly, CreateThumbnail(path, num, onlineOnly), lastWriteTime);
	}

	private static BitmapSource? CreateThumbnail(string path, long length, bool onlineOnly)
	{
		if (onlineOnly || length >= 31457280)
		{
			return null;
		}
		try
		{
			using FileStream streamSource = File.OpenRead(path);
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.DecodePixelWidth = 96;
			bitmapImage.StreamSource = streamSource;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private static bool IsOnlineOnly(string path)
	{
		try
		{
			return (File.GetAttributes(path) & (FileAttributes)4460544) != 0;
		}
		catch
		{
			return false;
		}
	}

	private static BitmapSource Decode(byte[] data)
	{
		using MemoryStream streamSource = new MemoryStream(data, writable: false);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static string FormatSize(long bytes)
	{
		if (bytes >= 1048576)
		{
			return $"{(double)bytes / 1024.0 / 1024.0:0.0}MB";
		}
		return $"{Math.Max(1.0, (double)bytes / 1024.0):0}KB";
	}
}
