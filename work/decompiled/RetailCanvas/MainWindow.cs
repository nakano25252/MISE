using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Printing;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using RetailCanvas.Controls;
using RetailCanvas.Dialogs;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas;

public class MainWindow : Window, IComponentConnector
{
	private sealed record ImpositionOutput(RenderTargetBitmap Bitmap, double WidthMm, double HeightMm, int Copies);

	private sealed record ExportRunResult(bool Saved, IReadOnlyList<QrOutputCheckResult> QrChecks);

	private sealed record ImageSnapshot(string? DataBase64, string? OriginalDataBase64, string? PreTrimDataBase64);

	private sealed record ProjectSnapshot(string Json, Dictionary<Guid, ImageSnapshot> Images);

	private sealed record SnapCandidate(double Offset, double Guide, string Label);

	private const double DipPerMm = 3.7795275590551185;

	private const long LargeImageThresholdBytes = 31457280L;

	private const int LargeImagePreviewMaxPixels = 2048;

	private readonly ProjectService _projectService = new ProjectService();

	private readonly SettingsService _settings = new SettingsService();

	private readonly TemplateService _templates = new TemplateService();

	private readonly ReusableBlockService _reusableBlocks = new ReusableBlockService();

	private readonly ValidationService _validator = new ValidationService();

	private readonly DispatcherTimer _autoSaveTimer = new DispatcherTimer();

	private readonly Stack<ProjectSnapshot> _undo = new Stack<ProjectSnapshot>();

	private readonly Stack<ProjectSnapshot> _redo = new Stack<ProjectSnapshot>();

	private readonly Dictionary<Guid, DesignerItem> _visuals = new Dictionary<Guid, DesignerItem>();

	private readonly Dictionary<Guid, FrameworkElement> _overflowVisuals = new Dictionary<Guid, FrameworkElement>();

	private readonly HashSet<Guid> _selectedIds = new HashSet<Guid>();

	private readonly Dictionary<Guid, Point> _groupMoveOrigins = new Dictionary<Guid, Point>();

	private readonly Dictionary<string, FontFamily> _embeddedFontFamilies = new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

	private ProjectModel _project = new ProjectModel();

	private int _pageIndex;

	private string? _filePath;

	private bool _dirty;

	private bool _outputInProgress;

	private bool _refreshing;

	private bool _updatingProperties;

	private bool _leftManuallyHidden;

	private bool _rightManuallyHidden;

	private double _zoom = 0.75;

	private bool _panning;

	private bool _marqueeSelecting;

	private Point _marqueeStart;

	private Rectangle? _marqueeRectangle;

	private HashSet<Guid> _marqueeBaseSelection = new HashSet<Guid>();

	private bool _overlapDragArmed;

	private bool _overlapDragging;

	private Point _overlapDragStart;

	private Guid? _overlapDragLeaderId;

	private readonly Dictionary<Guid, Point> _overlapDragOrigins = new Dictionary<Guid, Point>();

	private Point _panStart;

	private double _panHorizontal;

	private double _panVertical;

	private bool _freehandMode;

	private bool _freehandDrawing;

	private readonly List<(Expander Folder, ElementKind? Kind)> _propertyFolders = new List<(Expander, ElementKind?)>();

	private ContentControl? _leftContentHost;

	private StackPanel? _insertPaletteHost;

	private ScrollViewer? _creationScroll;

	private UIElement? _pageManagementPanel;

	private UIElement? _layerManagementPanel;

	private TextBlock? _leftSectionTitle;

	private TextBlock? _leftSectionDescription;

	private string _activeInsertTool = "テンプレート";

	private readonly Dictionary<Button, (string Icon, string Label, string Role)> _topCommandButtons = new Dictionary<Button, (string, string, string)>();

	private readonly Dictionary<Button, (string Icon, string Label, string Role)> _leftToolButtons = new Dictionary<Button, (string, string, string)>();

	private ContextMenu? _guideMenu;

	private bool _eyedropperMode;

	private Point _lastCanvasContextPosition;

	private CanvasElementModel? _copiedStyle;

	private Line? _snapGuideVertical;

	private Line? _snapGuideHorizontal;

	private TextBlock? _snapGuideLabel;

	private Guid? _snapMovingId;

	private double? _snapLatchX;

	private double? _snapLatchY;

	private double _snapLatchXGuide;

	private double _snapLatchYGuide;

	private string _snapLatchXLabel = string.Empty;

	private string _snapLatchYLabel = string.Empty;

	private HashSet<Guid>? _isolatedIds;

	private readonly List<Point> _freehandPoints = new List<Point>();

	private Polyline? _freehandPreview;

	internal Grid RootGrid;

	internal RowDefinition ToolbarRow;

	internal RowDefinition StatusRow;

	internal Button SelectToolButton;

	internal Button FreehandButton;

	internal Button EyedropperButton;

	internal TextBlock ProjectTitleText;

	internal TextBox ProjectTitleEditor;

	internal Button LeftPanelToggleButton;

	internal Button RightPanelToggleButton;

	internal Grid EditorGrid;

	internal ColumnDefinition LeftColumn;

	internal ColumnDefinition LeftSplitterColumn;

	internal ColumnDefinition RightSplitterColumn;

	internal ColumnDefinition RightColumn;

	internal Border LeftPanel;

	internal ComboBox TemplateCombo;

	internal ListBox PageList;

	internal ListBox LayerList;

	internal GridSplitter LeftSplitter;

	internal Grid CanvasWorkspace;

	internal ScrollViewer CanvasScroll;

	internal Grid CanvasOuter;

	internal Border PaperBorder;

	internal DesignCanvas PageCanvas;

	internal Canvas OverflowCanvas;

	internal DesignCanvas GuideOverlayCanvas;

	internal TextBlock PageInfoOverlay;

	internal Border SelectionMiniToolbar;

	internal GridSplitter RightSplitter;

	internal Border RightPanel;

	internal StackPanel PropertyPanel;

	internal TextBlock NoSelectionText;

	internal StackPanel PropertyFields;

	internal TextBox NameBox;

	internal TextBox XBox;

	internal TextBox YBox;

	internal TextBox SkewXBox;

	internal TextBox SkewYBox;

	internal TextBox WidthBox;

	internal TextBox HeightBox;

	internal TextBox RotationBox;

	internal TextBox OpacityBox;

	internal CheckBox AspectCheck;

	internal CheckBox LockCheck;

	internal CheckBox VisibleCheck;

	internal StackPanel TextProperties;

	internal TextBox TextContentBox;

	internal ComboBox FontCombo;

	internal Button FavoriteFontButton;

	internal TextBox FontSizeBox;

	internal TextBox TextColorBox;

	internal Button TextColorButton;

	internal TextBox TextBackgroundBox;

	internal Button TextBackgroundButton;

	internal ToggleButton BoldToggle;

	internal ToggleButton ItalicToggle;

	internal ToggleButton UnderlineToggle;

	private ComboBox? _fontWeightCombo;

	private Slider? _fontWeightSlider;

	private TextBlock? _fontWeightValueText;

	private bool _fontWeightUndoCaptured;
	private bool _textContentUndoCaptured;
	private bool _generalPropertyUndoCaptured;

	private TextBox? _characterSpacingBox;

	private TextBox? _lineSpacingBox;

	internal TextBox TextOutlineColorBox;

	internal Button TextOutlineColorButton;

	internal TextBox TextOutlineThicknessBox;

	internal TextBox TextExtrusionColorBox;

	internal Button TextExtrusionColorButton;

	internal TextBox TextExtrusionDepthBox;

	internal TextBox TextExtrusionAngleBox;

	internal StackPanel ShapeProperties;

	internal TextBox FillColorBox;

	internal Button FillColorButton;

	internal TextBox StrokeColorBox;

	internal Button StrokeColorButton;

	internal TextBox StrokeThicknessBox;

	internal TextBox CornerRadiusBox;

	internal TextBox CornerLeftBox;

	internal TextBox CornerRightBox;

	internal TextBox PanelRowsBox;

	internal TextBox PanelColumnsBox;

	internal TextBox PanelRowSplitsBox;

	internal TextBox PanelColumnSplitsBox;

	internal TextBox ShapeExtrusionColorBox;

	internal Button ShapeExtrusionColorButton;

	internal TextBox ShapeExtrusionDepthBox;

	internal TextBox ShapeExtrusionAngleBox;

	internal StackPanel ImageProperties;

	internal TextBlock ImageDpiText;

	internal TextBlock ImageSizeText;

	internal StackPanel QrProperties;

	internal TextBox QrContentBox;

	internal TextBox QrForegroundBox;

	internal Button QrForegroundButton;

	internal TextBox QrBackgroundBox;

	internal Button QrBackgroundButton;

	internal ComboBox QrLevelCombo;

	internal TextBlock StatusText;

	internal TextBlock ErrorCountText;

	internal TextBlock AutoSaveText;

	internal TextBlock PageStatusText;

	internal Slider ZoomSlider;

	internal TextBlock ZoomText;

	internal Grid HomeOverlay;

	internal ListBox RecentList;

	internal TextBlock VersionText;

	private bool _contentLoaded;

	private PageModel CurrentPage => _project.Pages[Math.Clamp(_pageIndex, 0, _project.Pages.Count - 1)];

	private CanvasElementModel? ActiveElement
	{
		get
		{
			if (_selectedIds.Count != 0)
			{
				return CurrentPage.Elements.FirstOrDefault((CanvasElementModel x) => x.Id == _selectedIds.Last());
			}
			return null;
		}
	}

	public MainWindow()
	{
		InitializeComponent();
		CanvasScroll.RequestBringIntoView += delegate(object _, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		};
		HomeOverlay.IsVisibleChanged += delegate
		{
			base.Dispatcher.BeginInvoke(new Action(RefreshMiseVisibleLabels), DispatcherPriority.Loaded);
		};
		ApplyMiseBranding();
		PageCanvas.ContextMenu = BuildCanvasContextMenu();
		WindowSizing.AttachMainWindow(this, _settings.Current);
		VersionText.Text = "MISE 1.1.12";
		RefreshFontList();
		TemplateCombo.ItemsSource = _templates.BuiltInNames.Concat(_templates.UserTemplates()).ToList();
		TemplateCombo.SelectedIndex = 0;
		RecentList.ItemsSource = _settings.Current.RecentProjects;
		QrLevelCombo.SelectedIndex = 1;
		_autoSaveTimer.Tick += delegate
		{
			AutoSave();
		};
		ConfigureAutoSave();
		CreateBlankProject("A4", landscape: false, hideHome: false);
		base.Loaded += MainWindow_Loaded;
	}

	private void ApplyMiseBranding()
	{
		RefreshMiseVisibleLabels();
		if (RootGrid.Children.OfType<Border>().FirstOrDefault((Border x) => Grid.GetRow(x) == 1)?.Child is Grid grid)
		{
			StackPanel stackPanel = grid.Children.OfType<StackPanel>().FirstOrDefault((StackPanel x) => Grid.GetColumn(x) == 0);
			if (stackPanel != null)
			{
				Border border = stackPanel.Children.OfType<Border>().FirstOrDefault();
				if (border?.Child is TextBlock textBlock)
				{
					BitmapSource bitmapSource = LoadMiseIcon();
					if (bitmapSource != null)
					{
						border.Child = new Image
						{
							Source = bitmapSource,
							Stretch = Stretch.Uniform
						};
					}
					else
					{
						textBlock.Text = "M";
					}
					border.ToolTip = "MISE（マイズ） 1.1.12";
					border.Margin = new Thickness(3.0, 0.0, 8.0, 0.0);
				}
				foreach (TextBlock item in stackPanel.Children.OfType<TextBlock>())
				{
					item.Visibility = Visibility.Collapsed;
				}
			}
			StackPanel stackPanel2 = grid.Children.OfType<StackPanel>().FirstOrDefault((StackPanel x) => Grid.GetColumn(x) == 1);
			if (stackPanel2 != null)
			{
				RebuildTopCommandBar(grid, stackPanel2);
			}
		}
		RebuildLeftCommandPanel();
		Menu menu = RootGrid.Children.OfType<Menu>().FirstOrDefault((Menu x) => Grid.GetRow(x) == 0);
		if (menu != null)
		{
			MenuItem menuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(delegate(MenuItem x)
			{
				object header = x.Header;
				return header != null && header.ToString()?.StartsWith("ツール") == true;
			});
			if (menuItem != null)
			{
				MenuItem insertItem = MenuItemOf("操作を検索…", delegate
				{
					ShowCommandPalette();
				}, "Ctrl+K");
				menuItem.Items.Insert(0, new Separator());
				menuItem.Items.Insert(0, insertItem);
			}
			MenuItem menuItem2 = menu.Items.OfType<MenuItem>().FirstOrDefault(delegate(MenuItem x)
			{
				object header = x.Header;
				return header != null && header.ToString()?.StartsWith("挿入") == true;
			});
			if (menuItem2 != null)
			{
				menuItem2.Items.Add(new Separator());
				menuItem2.Items.Add(MenuItemOf("再利用ブロック…", OpenReusableBlocks_Click));
			}
		}
		SimplifyLegacyPropertyPanel();
		EnhanceTextWeightControls();
		EnhanceTextSpacingControls();
		EnhanceTextureControls();
		OrganizePropertyPanel();
		AddNumericSpinners();
		EnhanceImagePropertyPanel();
		ShowInsertPalette("テンプレート");
	}

	private void RebuildTopCommandBar(Grid toolbar, StackPanel tools)
	{
		_topCommandButtons.Clear();
		tools.Children.Clear();
		tools.Margin = new Thickness(4.0, 0.0, 4.0, 0.0);
		Add("▣", "保存", "Save", SaveProject_Click, "保存（Ctrl+S）");
		Add("↶", "戻す", "Undo", Undo_Click, "元に戻す（Ctrl+Z）");
		Add("↷", "進む", "Redo", Redo_Click, "やり直す（Ctrl+Y）");
		StackPanel stackPanel = toolbar.Children.OfType<StackPanel>().FirstOrDefault((StackPanel x) => Grid.GetColumn(x) == 3);
		if (stackPanel != null)
		{
			stackPanel.Children.Clear();
			LeftPanelToggleButton = CreateTopCommandButton("☰", "作成", "LeftPanel", ToggleLeftPanel_Click, "左パネル");
			LeftPanelToggleButton.Visibility = Visibility.Collapsed;
			stackPanel.Children.Add(LeftPanelToggleButton);
			RightPanelToggleButton = CreateTopCommandButton("⚙", "設定", "Properties", ToggleRightPanel_Click, "右プロパティ");
			stackPanel.Children.Add(RightPanelToggleButton);
			stackPanel.Children.Add(CreateTopCommandButton("▱", "台紙", "Paper", EditPageSettings_Click, "台紙サイズ・色・回転"));
			Button guides = null;
			guides = CreateTopCommandButton("╋", "ガイド", "Guides", delegate
			{
				OpenButtonMenu(guides);
			}, "グリッド・正中線・安全領域");
			_guideMenu = BuildGuideMenu();
			guides.ContextMenu = _guideMenu;
			stackPanel.Children.Add(guides);
			stackPanel.Children.Add(CreateTopCommandButton("✓", "チェック", "Check", Validate_Click, "レイアウトチェック"));
			Button button = CreateTopCommandButton("⇩", "書き出し", "Export", Export_Click, "PDF・PNG・JPEGを書き出し");
			button.Style = TryFindResource("PrimaryButton") as Style;
			button.MinWidth = 84.0;
			stackPanel.Children.Add(button);
		}
		Button Add(string icon, string label, string role, RoutedEventHandler action, string? tooltip = null)
		{
			Button button2 = CreateTopCommandButton(icon, label, role, action, tooltip);
			tools.Children.Add(button2);
			return button2;
		}
	}

	private Button CreateTopCommandButton(string icon, string label, string role, RoutedEventHandler action, string? tooltip = null)
	{
		Button button = new Button
		{
			Style = (TryFindResource("ToolbarButton") as Style),
			Content = TopButtonContent(icon, label, compact: false),
			Tag = role,
			ToolTip = (tooltip ?? label),
			MinWidth = 42.0,
			Margin = new Thickness(1.0, 0.0, 1.0, 0.0)
		};
		button.Click += action;
		_topCommandButtons[button] = (icon, label, role);
		return button;
	}

	private static UIElement TopButtonContent(string icon, string label, bool compact)
	{
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = icon,
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold,
			VerticalAlignment = VerticalAlignment.Center
		});
		if (!compact)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = label,
				Margin = new Thickness(5.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center
			});
		}
		return stackPanel;
	}

	private static void OpenButtonMenu(Button button)
	{
		if (button.ContextMenu != null)
		{
			button.ContextMenu.PlacementTarget = button;
			button.ContextMenu.Placement = PlacementMode.Bottom;
			button.ContextMenu.IsOpen = true;
		}
	}

	private ContextMenu BuildShapeQuickMenu(bool includeLines, bool linesOnly = false)
	{
		ContextMenu contextMenu = new ContextMenu();
		if (!linesOnly)
		{
			contextMenu.Items.Add(ShapeCategory("四角形", new(string, string)[3]
			{
				("長方形", "Rectangle"),
				("正方形", "Square"),
				("角丸長方形", "RoundedRectangle")
			}));
			contextMenu.Items.Add(ShapeCategory("円形", new(string, string)[4]
			{
				("円", "Circle"),
				("楕円", "Ellipse"),
				("半円", "SemiCircle"),
				("リング", "Ring")
			}));
			contextMenu.Items.Add(ShapeCategory("特殊図形", new(string, string)[8]
			{
				("三角形", "Triangle"),
				("星", "Star"),
				("ハート", "Heart"),
				("ひし形", "Diamond"),
				("バッジ", "Badge"),
				("吹き出し", "SpeechBubble"),
				("ラベル", "Label"),
				("多角形", "Polygon")
			}));
		}
		if (includeLines || linesOnly)
		{
			contextMenu.Items.Add(ShapeCategory("線・矢印", new(string, string)[7]
			{
				("実線", "Line"),
				("区切り線", "Line:Divider"),
				("破線", "Line:Dash"),
				("点線", "Line:Dot"),
				("片側矢印", "Line:Arrow"),
				("両側矢印", "Line:BothArrow"),
				("開き矢印", "Line:OpenArrow")
			}));
		}
		return contextMenu;
		MenuItem ShapeCategory(string header, (string label, string type)[] shapes)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = header
			};
			for (int i = 0; i < shapes.Length; i++)
			{
				(string label, string type) tuple = shapes[i];
				string item = tuple.label;
				string item2 = tuple.type;
				MenuItem menuItem2 = new MenuItem
				{
					Header = item,
					Tag = item2
				};
				menuItem2.Click += AddShape_Click;
				menuItem.Items.Add(menuItem2);
			}
			return menuItem;
		}
	}

	private ContextMenu BuildGuideMenu()
	{
		ContextMenu contextMenu = new ContextMenu();
		MenuItem grid = new MenuItem
		{
			Header = "グリッドを表示",
			IsCheckable = true
		};
		MenuItem vertical = new MenuItem
		{
			Header = "縦の正中線を表示",
			IsCheckable = true
		};
		MenuItem horizontal = new MenuItem
		{
			Header = "横の正中線を表示",
			IsCheckable = true
		};
		MenuItem safe = new MenuItem
		{
			Header = "安全領域を表示",
			IsCheckable = true
		};
		MenuItem bleed = new MenuItem
		{
			Header = "塗り足しを表示",
			IsCheckable = true
		};
		MenuItem snapVertical = new MenuItem
		{
			Header = "縦の正中線へ吸着",
			IsCheckable = true
		};
		MenuItem snapHorizontal = new MenuItem
		{
			Header = "横の正中線へ吸着",
			IsCheckable = true
		};
		grid.Click += delegate
		{
			CurrentPage.ShowGrid = grid.IsChecked;
			MarkDirty();
			RefreshAll();
		};
		vertical.Click += delegate
		{
			_settings.Current.ShowVerticalCenterGuide = vertical.IsChecked;
			SaveCenterGuideState();
		};
		horizontal.Click += delegate
		{
			_settings.Current.ShowHorizontalCenterGuide = horizontal.IsChecked;
			SaveCenterGuideState();
		};
		safe.Click += delegate
		{
			CurrentPage.ShowSafeArea = safe.IsChecked;
			MarkDirty();
			RefreshAll();
		};
		bleed.Click += delegate
		{
			CurrentPage.ShowBleed = bleed.IsChecked;
			MarkDirty();
			RefreshAll();
		};
		snapVertical.Click += delegate
		{
			_settings.Current.SnapToVerticalCenterGuide = snapVertical.IsChecked;
			_settings.Save();
		};
		snapHorizontal.Click += delegate
		{
			_settings.Current.SnapToHorizontalCenterGuide = snapHorizontal.IsChecked;
			_settings.Save();
		};
		contextMenu.Items.Add(grid);
		contextMenu.Items.Add(vertical);
		contextMenu.Items.Add(horizontal);
		contextMenu.Items.Add(safe);
		contextMenu.Items.Add(bleed);
		contextMenu.Items.Add(new Separator());
		contextMenu.Items.Add(snapVertical);
		contextMenu.Items.Add(snapHorizontal);
		contextMenu.Items.Add(new Separator());
		contextMenu.Items.Add(MenuItemOf("正中線を両方表示", delegate
		{
			_settings.Current.ShowVerticalCenterGuide = true;
			_settings.Current.ShowHorizontalCenterGuide = true;
			SaveCenterGuideState();
		}));
		contextMenu.Items.Add(MenuItemOf("正中線をすべて隠す", delegate
		{
			_settings.Current.ShowVerticalCenterGuide = false;
			_settings.Current.ShowHorizontalCenterGuide = false;
			SaveCenterGuideState();
		}));
		contextMenu.Opened += delegate
		{
			grid.IsChecked = CurrentPage.ShowGrid;
			vertical.IsChecked = _settings.Current.ShowCenterGuides && _settings.Current.ShowVerticalCenterGuide;
			horizontal.IsChecked = _settings.Current.ShowCenterGuides && _settings.Current.ShowHorizontalCenterGuide;
			safe.IsChecked = CurrentPage.ShowSafeArea;
			bleed.IsChecked = CurrentPage.ShowBleed;
			snapVertical.IsChecked = _settings.Current.SnapToVerticalCenterGuide;
			snapHorizontal.IsChecked = _settings.Current.SnapToHorizontalCenterGuide;
		};
		return contextMenu;
	}

	private void SaveCenterGuideState()
	{
		_settings.Current.ShowCenterGuides = _settings.Current.ShowVerticalCenterGuide || _settings.Current.ShowHorizontalCenterGuide;
		_settings.Save();
		RefreshAll();
	}

	private void RebuildLeftCommandPanel()
	{
		_leftToolButtons.Clear();
		Grid grid = new Grid
		{
			Background = Brushes.White
		};
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(76.0)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		DockPanel dockPanel = new DockPanel
		{
			Background = new SolidColorBrush(Color.FromRgb(18, 27, 45))
		};
		TextBlock element = new TextBlock
		{
			Text = "TOOLS",
			Foreground = new SolidColorBrush(Color.FromRgb(116, 205, 218)),
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			HorizontalAlignment = HorizontalAlignment.Center,
			Margin = new Thickness(0.0, 10.0, 0.0, 6.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		dockPanel.Children.Add(element);
		StackPanel nav = new StackPanel
		{
			Margin = new Thickness(4.0, 0.0, 4.0, 8.0)
		};
		ScrollViewer element2 = new ScrollViewer
		{
			Content = nav,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		dockPanel.Children.Add(element2);
		SelectToolButton = Nav("↖", "選択", "Select", delegate(object _, RoutedEventArgs e)
		{
			SelectTool_Click(this, e);
			ShowInsertPalette("選択");
		}, "選択・移動（Esc）");
		Nav("✦", "テンプレ", "Template", delegate
		{
			ShowInsertPalette("テンプレート");
		}, "商品データ連動テンプレート");
		Divider();
		Nav("T", "文字", "Text", delegate
		{
			ShowInsertPalette("文字");
		}, "文字を追加");
		Nav("▧", "画像", "Image", delegate
		{
			ShowInsertPalette("画像");
		}, "画像・PDFを追加");
		Nav("◇", "図形", "Shape", delegate
		{
			ShowInsertPalette("図形");
		}, "四角形・円形・特殊図形").ContextMenu = BuildShapeQuickMenu(includeLines: false);
		Nav("▤", "パネル", "Panel", delegate
		{
			ShowInsertPalette("パネル");
		}, "区画付きパネル");
		Nav("╱", "線", "Line", delegate
		{
			ShowInsertPalette("線・矢印");
		}, "線・矢印").ContextMenu = BuildShapeQuickMenu(includeLines: true, linesOnly: true);
		FreehandButton = Nav("✎", "手描き", "Freehand", delegate
		{
			ShowInsertPalette("手描き");
		}, "フリーハンド描画");
		EyedropperButton = Nav("⌾", "スポイト", "Eyedropper", delegate
		{
			ShowInsertPalette("スポイト");
		}, "色を取得");
		Nav("▦", "QR", "QR", delegate
		{
			ShowInsertPalette("QR");
		}, "QRコードを作成");
		Divider();
		Nav("品", "商品", "Product", delegate
		{
			ShowInsertPalette("商品");
		}, "商品データベース");
		Nav("▥", "素材", "Asset", delegate
		{
			ShowInsertPalette("素材");
		}, "素材ライブラリ");
		Divider();
		Nav("▤", "ページ", "Pages", delegate
		{
			ShowInsertPalette("ページ");
		}, "ページ管理");
		Nav("≡", "レイヤー", "Layers", delegate
		{
			ShowInsertPalette("レイヤー");
		}, "レイヤー管理");
		grid.Children.Add(dockPanel);
		Grid grid2 = new Grid
		{
			Background = new SolidColorBrush(Color.FromRgb(249, 250, 252))
		};
		grid2.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid2.RowDefinitions.Add(new RowDefinition());
		Border border = new Border
		{
			Padding = new Thickness(12.0, 11.0, 10.0, 9.0),
			Background = Brushes.White,
			BorderBrush = new SolidColorBrush(Color.FromRgb(226, 230, 237)),
			BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0)
		};
		StackPanel stackPanel = new StackPanel();
		_leftSectionTitle = new TextBlock
		{
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51))
		};
		_leftSectionDescription = new TextBlock
		{
			FontSize = 10.0,
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		stackPanel.Children.Add(_leftSectionTitle);
		stackPanel.Children.Add(_leftSectionDescription);
		border.Child = stackPanel;
		grid2.Children.Add(border);
		_leftContentHost = new ContentControl();
		Grid.SetRow(_leftContentHost, 1);
		grid2.Children.Add(_leftContentHost);
		Grid.SetColumn(grid2, 1);
		grid.Children.Add(grid2);
		_insertPaletteHost = new StackPanel
		{
			Margin = new Thickness(10.0)
		};
		_creationScroll = new ScrollViewer
		{
			Content = _insertPaletteHost,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		};
		_pageManagementPanel = BuildPageManagementPanel();
		_layerManagementPanel = BuildLayerManagementPanel();
		TemplateCombo = new ComboBox
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
		};
		LeftPanel.Child = grid;
		LeftColumn.Width = new GridLength(Math.Max(290.0, _settings.Current.LeftPanelWidth));
		void Divider()
		{
			nav.Children.Add(new Border
			{
				Height = 1.0,
				Background = new SolidColorBrush(Color.FromArgb(55, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
				Margin = new Thickness(8.0, 5.0, 8.0, 5.0)
			});
		}
		Button Nav(string icon, string label, string role, RoutedEventHandler click, string tooltip)
		{
			Button button = CreateLeftNavButton(icon, label, role, click, tooltip);
			nav.Children.Add(button);
			return button;
		}
	}

	private Button CreateLeftNavButton(string icon, string label, string role, RoutedEventHandler action, string tooltip)
	{
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = icon,
			FontSize = 17.0,
			FontWeight = FontWeights.SemiBold,
			HorizontalAlignment = HorizontalAlignment.Center
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			FontSize = 9.0,
			Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
			HorizontalAlignment = HorizontalAlignment.Center
		});
		Button button = new Button
		{
			Content = stackPanel,
			Tag = role,
			ToolTip = tooltip,
			Height = 46.0,
			Margin = new Thickness(1.0),
			Padding = new Thickness(0.0),
			Background = Brushes.Transparent,
			Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 236)),
			BorderThickness = new Thickness(0.0),
			HorizontalContentAlignment = HorizontalAlignment.Center
		};
		button.Click += action;
		_leftToolButtons[button] = (icon, label, role);
		return button;
	}

	private UIElement BuildPageManagementPanel()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(10.0)
		};
		UniformGrid buttons = new UniformGrid
		{
			Columns = 3,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		Add("追加", AddPage_Click);
		Add("複製", DuplicatePage_Click);
		Add("削除", DeletePage_Click);
		DockPanel.SetDock(buttons, Dock.Bottom);
		obj.Children.Add(buttons);
		PageList = new ListBox
		{
			DisplayMemberPath = "Name"
		};
		PageList.SelectionChanged += PageList_SelectionChanged;
		obj.Children.Add(PageList);
		return obj;
		Button Add(string text, RoutedEventHandler click)
		{
			Button button = new Button
			{
				Content = text,
				MinHeight = 31.0,
				Margin = new Thickness(2.0)
			};
			button.Click += click;
			buttons.Children.Add(button);
			return button;
		}
	}

	private UIElement BuildLayerManagementPanel()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(10.0)
		};
		UniformGrid buttons = new UniformGrid
		{
			Columns = 2,
			Margin = new Thickness(0.0, 8.0, 0.0, 0.0)
		};
		Add("前へ", "Forward", LayerOrder_Click);
		Add("後ろへ", "Backward", LayerOrder_Click);
		Add("複製", null, Duplicate_Click);
		Add("削除", null, Delete_Click);
		DockPanel.SetDock(buttons, Dock.Bottom);
		obj.Children.Add(buttons);
		LayerList = new ListBox
		{
			DisplayMemberPath = "Name"
		};
		LayerList.SelectionChanged += LayerList_SelectionChanged;
		obj.Children.Add(LayerList);
		return obj;
		Button Add(string text, string? tag, RoutedEventHandler click)
		{
			Button button = new Button
			{
				Content = text,
				Tag = tag,
				MinHeight = 31.0,
				Margin = new Thickness(2.0)
			};
			button.Click += click;
			buttons.Children.Add(button);
			return button;
		}
	}

	private void ShowInsertPalette(string mode)
	{
		_activeInsertTool = mode;
		if (_insertPaletteHost == null || _leftContentHost == null || _creationScroll == null)
		{
			return;
		}
		if (LeftColumn.Width.Value <= 0.0)
		{
			SetLeftPanelVisible(show: true);
			_leftManuallyHidden = false;
		}
		if (_leftSectionTitle != null)
		{
			_leftSectionTitle.Text = mode;
		}
		if (_leftSectionDescription != null)
		{
			TextBlock leftSectionDescription = _leftSectionDescription;
			leftSectionDescription.Text = mode switch
			{
				"選択" => "クリックで選択・ドラッグで移動。背面はAlt＋クリック。", 
				"テンプレート" => "商品を選ぶだけで登録情報をデザインへ自動反映します。", 
				"文字" => "見出し、本文、価格など用途から選びます。", 
				"画像" => "画像・PDF・登録済み商品画像を配置します。", 
				"図形" => "種類を探して中央追加、またはキャンバスへドラッグします。", 
				"パネル" => "見出しと本文に適した区画構成を選びます。", 
				"線・矢印" => "線種と矢印方向を選びます。", 
				"ページ" => "ページの追加、複製、削除を管理します。", 
				"レイヤー" => "重なり順と選択対象を管理します。", 
				_ => "目的の操作を選択してください。", 
			};
		}
		if (mode == "ページ")
		{
			_leftContentHost.Content = _pageManagementPanel;
			PageList.ItemsSource = null;
			PageList.ItemsSource = _project.Pages;
			PageList.SelectedIndex = _pageIndex;
			SetActiveTopTool(mode);
			return;
		}
		if (mode == "レイヤー")
		{
			_leftContentHost.Content = _layerManagementPanel;
			RefreshLayers();
			SetActiveTopTool(mode);
			return;
		}
		_leftContentHost.Content = _creationScroll;
		_insertPaletteHost.Children.Clear();
		switch (mode)
		{
		case "選択":
			AddPaletteSection("選択の使い方", new TextBlock
			{
				Text = "通常クリック：最前面を選択\nそのままドラッグ：すぐ移動\nAlt＋クリック：重なった背面へ切替\nAltのままドラッグ：選んだ背面を移動\n右クリック：名前から対象を選択\nTab／Shift＋Tab：前後の要素へ移動",
				TextWrapping = TextWrapping.Wrap,
				LineHeight = 22.0,
				Foreground = Brushes.SlateGray
			});
			break;
		case "テンプレート":
			BuildTemplatePalette();
			break;
		case "文字":
			AddPaletteSection("文字プリセット", PaletteGrid(PaletteButton("H1", "大見出し", AddHeading_Click), PaletteButton("H2", "中見出し", AddSubheading_Click), PaletteButton("本文", "本文", AddBody_Click), PaletteButton("注", "注釈", AddNote_Click), PaletteButton("¥", "価格", AddPrice_Click), PaletteButton("品", "製品名", AddProductName_Click)));
			break;
		case "画像":
			AddPaletteSection("画像を配置", PaletteGrid(PaletteButton("＋", "画像ファイル", AddImage_Click), PaletteButton("▥", "素材ライブラリ", AssetLibrary_Click), PaletteButton("品", "商品情報から", ProductDatabase_Click), PaletteButton("PDF", "PDFページ", AddPdf_Click)));
			break;
		case "図形":
			BuildShapePalette();
			break;
		case "パネル":
			BuildPanelPalette();
			break;
		case "線・矢印":
			BuildLinePalette();
			break;
		case "手描き":
			AddPaletteSection("描画", PaletteGrid(PaletteButton("✎", _freehandMode ? "描画を終了" : "描画を開始", Freehand_Click)));
			break;
		case "スポイト":
			AddPaletteSection("色を取得", PaletteGrid(PaletteButton("⌾", "スポイトを開始", Eyedropper_Click)));
			break;
		case "QR":
			AddPaletteSection("QRコード", PaletteGrid(PaletteButton("▦", "QRコードを生成", AddQr_Click)));
			break;
		case "商品":
			AddPaletteSection("商品データ", PaletteGrid(PaletteButton("品", "商品データベース", ProductDatabase_Click), PaletteButton("✦", "商品からPOP作成", SmartTemplate_Click)));
			break;
		case "素材":
			AddPaletteSection("素材", PaletteGrid(PaletteButton("▧", "素材ライブラリ", AssetLibrary_Click), PaletteButton("＋", "画像ファイル", AddImage_Click)));
			break;
		default:
			AddPaletteSection("ライブラリ", PaletteGrid(PaletteButton("▣", "再利用ブロック", OpenReusableBlocks_Click), PaletteButton("品", "商品データベース", ProductDatabase_Click), PaletteButton("▧", "素材ライブラリ", AssetLibrary_Click), PaletteButton("型", "テンプレート", delegate
			{
				ShowInsertPalette("テンプレート");
			})));
			break;
		}
		SetActiveTopTool(mode);
	}

	private void AddPaletteSection(string title, UIElement content)
	{
		if (_insertPaletteHost != null)
		{
			_insertPaletteHost.Children.Add(new TextBlock
			{
				Text = title,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 8.0, 0.0, 5.0)
			});
			_insertPaletteHost.Children.Add(content);
		}
	}

	private void BuildTemplatePalette()
	{
		if (_insertPaletteHost != null)
		{
			List<string> list = _templates.BuiltInNames.Concat(_templates.UserTemplates()).ToList();
			string text = TemplateCombo.SelectedItem as string;
			TemplateCombo.ItemsSource = list;
			TemplateCombo.SelectedItem = ((text != null && list.Contains(text)) ? text : (list.FirstOrDefault((string name) => name != "白紙") ?? list.FirstOrDefault()));
			AddPaletteSection("テンプレートを選択", TemplateCombo);
			Button button = PaletteButton("✦", "商品データから自動作成", SmartTemplate_Click);
			button.Background = new SolidColorBrush(Color.FromRgb(242, 106, 33));
			button.Foreground = Brushes.White;
			AddPaletteSection("すぐ作る", PaletteGrid(button, PaletteButton("✓", "選択中を適用", ApplyTemplate_Click)));
			AddPaletteSection("再利用", PaletteGrid(PaletteButton("＋", "現在のデザインを保存", SaveTemplate_Click), PaletteButton("▣", "再利用ブロック", OpenReusableBlocks_Click)));
			Border element = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(234, 247, 249)),
				CornerRadius = new CornerRadius(6.0),
				Padding = new Thickness(9.0),
				Margin = new Thickness(3.0, 8.0, 3.0, 0.0),
				Child = new TextBlock
				{
					Text = "商品名・画像・価格・特徴・仕様・QRを、登録済みの商品データからプレースホルダーへ自動反映します。",
					TextWrapping = TextWrapping.Wrap,
					FontSize = 10.0,
					Foreground = new SolidColorBrush(Color.FromRgb(43, 88, 98))
				}
			};
			_insertPaletteHost.Children.Add(element);
		}
	}

	private static UniformGrid PaletteGrid(params Button[] buttons)
	{
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = 2
		};
		foreach (Button element in buttons)
		{
			uniformGrid.Children.Add(element);
		}
		return uniformGrid;
	}

	private Button PaletteButton(string icon, string label, RoutedEventHandler action, string? shapeType = null)
	{
		StackPanel stackPanel = new StackPanel
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = icon,
			FontSize = 18.0,
			HorizontalAlignment = HorizontalAlignment.Center
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			TextWrapping = TextWrapping.Wrap,
			TextAlignment = TextAlignment.Center,
			FontSize = 11.0
		});
		Button button = new Button
		{
			Content = stackPanel,
			MinHeight = 58.0,
			Margin = new Thickness(3.0),
			Padding = new Thickness(5.0),
			Tag = shapeType
		};
		button.Click += action;
		MenuItem favorite;
		if (!string.IsNullOrWhiteSpace(shapeType))
		{
			button.ToolTip = "クリックで表示範囲中央へ追加／ドラッグで位置を指定／右クリックでお気に入り";
			Point dragStart = default(Point);
			bool dragArmed = false;
			button.PreviewMouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
			{
				dragStart = e.GetPosition(button);
				dragArmed = true;
			};
			button.PreviewMouseLeftButtonUp += delegate
			{
				dragArmed = false;
			};
			button.MouseMove += delegate(object _, MouseEventArgs e)
			{
				if (dragArmed && e.LeftButton == MouseButtonState.Pressed && !((e.GetPosition(button) - dragStart).Length < 4.0))
				{
					dragArmed = false;
					DataObject data = new DataObject("MISE.ShapeType", shapeType);
					DragDrop.DoDragDrop(button, data, DragDropEffects.Copy);
					e.Handled = true;
				}
			};
			favorite = new MenuItem();
			RefreshFavoriteLabel();
			favorite.Click += delegate
			{
				if (_settings.Current.FavoriteShapeTypes.Contains<string>(shapeType, StringComparer.OrdinalIgnoreCase))
				{
					_settings.Current.FavoriteShapeTypes.RemoveAll((string x) => string.Equals(x, shapeType, StringComparison.OrdinalIgnoreCase));
				}
				else
				{
					_settings.Current.FavoriteShapeTypes.Insert(0, shapeType);
				}
				_settings.Save();
				RefreshFavoriteLabel();
			};
			button.ContextMenu = new ContextMenu();
			button.ContextMenu.Items.Add(favorite);
		}
		return button;
		void RefreshFavoriteLabel()
		{
			favorite.Header = (_settings.Current.FavoriteShapeTypes.Contains<string>(shapeType, StringComparer.OrdinalIgnoreCase) ? "★ お気に入りから外す" : "☆ お気に入りに追加");
		}
	}

	private void BuildShapePalette()
	{
		if (_insertPaletteHost == null)
		{
			return;
		}
		(string Category, string Label, string Type, string Icon)[] choices = new(string, string, string, string)[15]
		{
			("四角形", "長方形", "Rectangle", "▭"),
			("四角形", "正方形", "Square", "□"),
			("四角形", "角丸長方形", "RoundedRectangle", "▢"),
			("円形", "円", "Circle", "○"),
			("円形", "楕円", "Ellipse", "⬭"),
			("円形", "半円", "SemiCircle", "◒"),
			("円形", "リング", "Ring", "◎"),
			("特殊図形", "三角形", "Triangle", "△"),
			("特殊図形", "星", "Star", "☆"),
			("特殊図形", "ハート", "Heart", "♡"),
			("特殊図形", "ひし形", "Diamond", "◇"),
			("特殊図形", "バッジ", "Badge", "✺"),
			("特殊図形", "吹き出し", "SpeechBubble", "▱"),
			("特殊図形", "ラベル", "Label", "▰"),
			("特殊図形", "多角形", "Polygon", "⬡")
		};
		TextBox search = new TextBox
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
			ToolTip = "図形名を検索"
		};
		search.SetCurrentValue(TextBox.TextProperty, string.Empty);
		_insertPaletteHost.Children.Add(search);
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 7.0)
		};
		_insertPaletteHost.Children.Add(wrapPanel);
		StackPanel host = new StackPanel();
		_insertPaletteHost.Children.Add(host);
		string category = "最近";
		string[] array = new string[4] { "最近", "四角形", "円形", "特殊図形" };
		foreach (string name in array)
		{
			Button button = new Button
			{
				Content = name,
				Tag = name,
				Padding = new Thickness(8.0, 3.0, 8.0, 3.0),
				MinHeight = 27.0
			};
			button.Click += delegate
			{
				category = name;
				Refresh();
			};
			wrapPanel.Children.Add(button);
		}
		search.TextChanged += delegate
		{
			Refresh();
		};
		Refresh();
		void Refresh()
		{
			host.Children.Clear();
			IEnumerable<(string, string, string, string)> source = choices;
			source = (IEnumerable<(string, string, string, string)>)((!string.IsNullOrWhiteSpace(search.Text)) ? source.Where<(string, string, string, string)>(((string Category, string Label, string Type, string Icon) x) => x.Label.Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)) : ((!(category == "最近")) ? ((IEnumerable)source.Where<(string, string, string, string)>(((string Category, string Label, string Type, string Icon) x) => x.Category == category)) : ((IEnumerable)(from type in _settings.Current.FavoriteShapeTypes.Concat(_settings.Current.RecentShapeTypes).Append(_settings.Current.LastShapeType).Distinct<string>(StringComparer.OrdinalIgnoreCase)
					.ToList()
				select choices.FirstOrDefault(((string Category, string Label, string Type, string Icon) x) => x.Type == type) into x
				where !string.IsNullOrWhiteSpace(x.Type)
				select x))));
			UniformGrid uniformGrid = new UniformGrid
			{
				Columns = 2
			};
			foreach (var item in source)
			{
				uniformGrid.Children.Add(PaletteButton(item.Item4, item.Item2, AddShape_Click, item.Item3));
			}
			if (uniformGrid.Children.Count == 0)
			{
				host.Children.Add(new TextBlock
				{
					Text = "該当する図形がありません。",
					Foreground = Brushes.SlateGray,
					Margin = new Thickness(4.0, 12.0, 4.0, 12.0)
				});
			}
			else
			{
				host.Children.Add(uniformGrid);
			}
		}
	}

	private void BuildLinePalette()
	{
		AddPaletteSection("線・矢印", PaletteGrid(PaletteButton("―", "実線", AddShape_Click, "Line"), PaletteButton("｜", "区切り線", AddShape_Click, "Line:Divider"), PaletteButton("--", "破線", AddShape_Click, "Line:Dash"), PaletteButton("··", "点線", AddShape_Click, "Line:Dot"), PaletteButton("→", "片側矢印", AddShape_Click, "Line:Arrow"), PaletteButton("↔", "両側矢印", AddShape_Click, "Line:BothArrow"), PaletteButton("＞", "開き矢印", AddShape_Click, "Line:OpenArrow")));
	}

	private void BuildPanelPalette()
	{
		AddPaletteSection("パネル構成", PaletteGrid(PaletteButton("▭", "白紙パネル", delegate
		{
			AddPanelPreset("白紙");
		}), PaletteButton("▤", "2分割", delegate
		{
			AddPanelPreset("2分割");
		}), PaletteButton("▤", "3分割", delegate
		{
			AddPanelPreset("3分割");
		}), PaletteButton("▥", "4分割", delegate
		{
			AddPanelPreset("4分割");
		}), PaletteButton("見＋本", "見出し＋本文", delegate
		{
			AddPanelPreset("見出し＋本文");
		}), PaletteButton("見＋2", "見出し＋2列", delegate
		{
			AddPanelPreset("見出し＋2列");
		})));
	}

	private void AddPanelPreset(string preset)
	{
		AddShape_Click(new Button
		{
			Tag = "Panel"
		}, new RoutedEventArgs());
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null)
		{
			activeElement.PanelRowSplits.Clear();
			activeElement.PanelColumnSplits.Clear();
			switch (preset)
			{
			case "白紙":
			{
				int panelRows = (activeElement.PanelColumns = 1);
				activeElement.PanelRows = panelRows;
				break;
			}
			case "3分割":
				activeElement.PanelRows = 3;
				activeElement.PanelColumns = 1;
				activeElement.PanelRowSplits = new List<double> { 33.33, 66.67 };
				break;
			case "4分割":
			{
				int panelRows = (activeElement.PanelColumns = 2);
				activeElement.PanelRows = panelRows;
				activeElement.PanelRowSplits = new List<double> { 50.0 };
				activeElement.PanelColumnSplits = new List<double> { 50.0 };
				break;
			}
			case "見出し＋本文":
				activeElement.PanelRows = 2;
				activeElement.PanelColumns = 1;
				activeElement.PanelRowSplits = new List<double> { 28.0 };
				break;
			case "見出し＋2列":
				activeElement.PanelRows = 2;
				activeElement.PanelColumns = 2;
				activeElement.PanelRowSplits = new List<double> { 28.0 };
				activeElement.PanelColumnSplits = new List<double> { 50.0 };
				break;
			default:
				activeElement.PanelRows = 2;
				activeElement.PanelColumns = 1;
				activeElement.PanelRowSplits = new List<double> { 50.0 };
				break;
			}
			activeElement.Name = UniqueName(preset + "パネル");
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
			UpdatePropertyPanel();
		}
	}

	private void SetActiveTopTool(string mode)
	{
		string text = mode switch
		{
			"テンプレート" => "Template", 
			"文字" => "Text", 
			"画像" => "Image", 
			"図形" => "Shape", 
			"パネル" => "Panel", 
			"線・矢印" => "Line", 
			"手描き" => "Freehand", 
			"スポイト" => "Eyedropper", 
			"QR" => "QR", 
			"商品" => "Product", 
			"素材" => "Asset", 
			"ページ" => "Pages", 
			"レイヤー" => "Layers", 
			_ => "Select", 
		};
		foreach (KeyValuePair<Button, (string, string, string)> leftToolButton in _leftToolButtons)
		{
			bool flag = leftToolButton.Value.Item3 == text;
			leftToolButton.Key.Background = (flag ? new SolidColorBrush(Color.FromRgb(242, 106, 33)) : Brushes.Transparent);
			leftToolButton.Key.Foreground = (flag ? Brushes.White : new SolidColorBrush(Color.FromRgb(220, 226, 236)));
			leftToolButton.Key.BorderThickness = (flag ? new Thickness(3.0, 0.0, 0.0, 0.0) : new Thickness(0.0));
			leftToolButton.Key.BorderBrush = (flag ? new SolidColorBrush(Color.FromRgb(112, 215, 226)) : Brushes.Transparent);
		}
	}

	private void ConfigureShapeMenus()
	{
		foreach (Button item3 in from button in VisualDescendants(this).OfType<Button>()
			where string.Equals(button.Content?.ToString(), "図形", StringComparison.Ordinal)
			select button)
		{
			item3.Tag = _settings.Current.LastShapeType;
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.Items.Add(ShapeCategory("四角形", new(string, string)[3]
			{
				("長方形", "Rectangle"),
				("正方形", "Square"),
				("角丸長方形", "RoundedRectangle")
			}));
			contextMenu.Items.Add(ShapeCategory("円形", new(string, string)[4]
			{
				("円", "Circle"),
				("楕円", "Ellipse"),
				("半円", "SemiCircle"),
				("リング", "Ring")
			}));
			contextMenu.Items.Add(ShapeCategory("線・矢印", new(string, string)[7]
			{
				("直線", "Line"),
				("区切り線", "Line:Divider"),
				("破線", "Line:Dash"),
				("点線", "Line:Dot"),
				("片側矢印", "Line:Arrow"),
				("両側矢印", "Line:BothArrow"),
				("開き矢印", "Line:OpenArrow")
			}));
			contextMenu.Items.Add(ShapeCategory("特殊図形", new(string, string)[8]
			{
				("三角形", "Triangle"),
				("星", "Star"),
				("ハート", "Heart"),
				("ひし形", "Diamond"),
				("バッジ", "Badge"),
				("吹き出し", "SpeechBubble"),
				("ラベル", "Label"),
				("多角形", "Polygon")
			}));
			item3.ContextMenu = contextMenu;
		}
		MenuItem ShapeCategory(string header, (string label, string type)[] shapes)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = header
			};
			for (int i = 0; i < shapes.Length; i++)
			{
				(string label, string type) tuple = shapes[i];
				string item = tuple.label;
				string item2 = tuple.type;
				MenuItem menuItem2 = new MenuItem
				{
					Header = item,
					Tag = item2
				};
				menuItem2.Click += AddShape_Click;
				menuItem.Items.Add(menuItem2);
			}
			return menuItem;
		}
	}

	private void EnhanceImagePropertyPanel()
	{
		if (!ImageProperties.Children.OfType<Button>().Any((Button button2) => object.Equals(button2.Tag, "ImageExtrusion")))
		{
			Button button = new Button
			{
				Content = "画像の立体効果…",
				Tag = "ImageExtrusion",
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				ToolTip = "PNGの透明輪郭に奥行き・角度・色を設定"
			};
			button.Click += EditImageExtrusion_Click;
			ImageProperties.Children.Add(button);
		}
	}

	private void EditPageSettings_Click(object sender, RoutedEventArgs e)
	{
		PageSettingsDialog pageSettingsDialog = new PageSettingsDialog(CurrentPage, _project.PaperName, _settings.Current)
		{
			Owner = this
		};
		if (pageSettingsDialog.ShowDialog() == true)
		{
			PushUndo();
			double widthMm = CurrentPage.WidthMm;
			double heightMm = CurrentPage.HeightMm;
			ResizeElementsForPageChange(widthMm, heightMm, pageSettingsDialog.WidthMm, pageSettingsDialog.HeightMm, pageSettingsDialog.ResizeMode);
			CurrentPage.WidthMm = pageSettingsDialog.WidthMm;
			CurrentPage.HeightMm = pageSettingsDialog.HeightMm;
			CurrentPage.Background = pageSettingsDialog.Background;
			CurrentPage.BackgroundTextureName = pageSettingsDialog.TextureName;
			CurrentPage.BackgroundTextureDataBase64 = pageSettingsDialog.TextureDataBase64;
			CurrentPage.BackgroundTextureOpacity = pageSettingsDialog.TextureOpacity;
			CurrentPage.BackgroundTextureScale = pageSettingsDialog.TextureScale;
			CurrentPage.SafeMarginMm = pageSettingsDialog.SafeMarginMm;
			CurrentPage.BleedMm = pageSettingsDialog.BleedMm;
			CurrentPage.PrintMarginMm = pageSettingsDialog.PrintMarginMm;
			CurrentPage.ShowSafeArea = pageSettingsDialog.ShowSafeArea;
			CurrentPage.ShowBleed = pageSettingsDialog.ShowBleed;
			CurrentPage.ShowGrid = pageSettingsDialog.ShowGrid;
			_settings.Current.GridSizeMm = pageSettingsDialog.GridSizeMm;
			_settings.Save();
			_project.PaperName = pageSettingsDialog.PaperName;
			if (pageSettingsDialog.RotationMode == "台紙だけ90°回転")
			{
				PageModel currentPage = CurrentPage;
				PageModel currentPage2 = CurrentPage;
				double heightMm2 = CurrentPage.HeightMm;
				double widthMm2 = CurrentPage.WidthMm;
				currentPage.WidthMm = heightMm2;
				currentPage2.HeightMm = widthMm2;
			}
			else if (pageSettingsDialog.RotationMode == "オブジェクトだけ90°回転")
			{
				RotateElements(CurrentPage.WidthMm, CurrentPage.HeightMm, swapPage: false);
			}
			else if (pageSettingsDialog.RotationMode == "台紙とオブジェクトを90°回転")
			{
				RotateElements(CurrentPage.WidthMm, CurrentPage.HeightMm, swapPage: true);
				PageModel currentPage = CurrentPage;
				PageModel currentPage3 = CurrentPage;
				double widthMm2 = CurrentPage.HeightMm;
				double heightMm2 = CurrentPage.WidthMm;
				currentPage.WidthMm = widthMm2;
				currentPage3.HeightMm = heightMm2;
			}
			else if (pageSettingsDialog.RotationMode == "オブジェクトだけ180°回転" || pageSettingsDialog.RotationMode == "台紙とオブジェクトを180°回転")
			{
				RotateElements180(CurrentPage.WidthMm, CurrentPage.HeightMm);
			}
			MarkDirty();
			RefreshAll();
		}
	}

	private void ResizeElementsForPageChange(double oldWidth, double oldHeight, double newWidth, double newHeight, string mode)
	{
		if (Math.Abs(oldWidth - newWidth) < 0.0001 && Math.Abs(oldHeight - newHeight) < 0.0001)
		{
			return;
		}
		switch (mode)
		{
		case "中央位置を維持":
		{
			double num4 = (newWidth - oldWidth) / 2.0;
			double num5 = (newHeight - oldHeight) / 2.0;
			{
				foreach (CanvasElementModel element in CurrentPage.Elements)
				{
					element.Xmm += num4;
					element.Ymm += num5;
				}
				break;
			}
		}
		case "比率を維持して拡大縮小":
		{
			double num = Math.Min(newWidth / Math.Max(0.01, oldWidth), newHeight / Math.Max(0.01, oldHeight));
			double num2 = (newWidth - oldWidth * num) / 2.0;
			double num3 = (newHeight - oldHeight * num) / 2.0;
			{
				foreach (CanvasElementModel element2 in CurrentPage.Elements)
				{
					element2.Xmm = num2 + element2.Xmm * num;
					element2.Ymm = num3 + element2.Ymm * num;
					element2.WidthMm *= num;
					element2.HeightMm *= num;
				}
				break;
			}
		}
		case "台紙と一緒に回転":
			if (Math.Abs(oldWidth - newHeight) < 0.01 && Math.Abs(oldHeight - newWidth) < 0.01)
			{
				RotateElements(oldWidth, oldHeight, swapPage: true);
			}
			break;
		}
	}

	private void RotateElements(double pageWidth, double pageHeight, bool swapPage)
	{
		foreach (CanvasElementModel element in CurrentPage.Elements)
		{
			double num = element.Xmm + element.WidthMm / 2.0;
			double num2 = element.Ymm + element.HeightMm / 2.0;
			double num3;
			double num4;
			if (swapPage)
			{
				num3 = pageHeight - num2;
				num4 = num;
			}
			else
			{
				num3 = pageWidth / 2.0 - (num2 - pageHeight / 2.0);
				num4 = pageHeight / 2.0 + (num - pageWidth / 2.0);
			}
			element.Xmm = num3 - element.WidthMm / 2.0;
			element.Ymm = num4 - element.HeightMm / 2.0;
			element.Rotation = (element.Rotation + 90.0) % 360.0;
		}
	}

	private void RotateElements180(double pageWidth, double pageHeight)
	{
		foreach (CanvasElementModel element in CurrentPage.Elements)
		{
			double num = element.Xmm + element.WidthMm / 2.0;
			double num2 = element.Ymm + element.HeightMm / 2.0;
			element.Xmm = pageWidth - num - element.WidthMm / 2.0;
			element.Ymm = pageHeight - num2 - element.HeightMm / 2.0;
			element.Rotation = (element.Rotation + 180.0) % 360.0;
		}
	}

	private void RefreshMiseVisibleLabels()
	{
		base.Title = "MISE";
		foreach (TextBlock item in VisualDescendants(this).OfType<TextBlock>())
		{
			string text = (item.Text ?? string.Empty).Replace(" ", string.Empty);
			if (text.Contains("RetailCanvas", StringComparison.OrdinalIgnoreCase) || text.Contains("RetailCambas", StringComparison.OrdinalIgnoreCase))
			{
				item.Text = (item.Text ?? string.Empty).Replace("Retail Canvas", "MISE", StringComparison.OrdinalIgnoreCase).Replace("Retail Cambas", "MISE", StringComparison.OrdinalIgnoreCase).Replace("RetailCanvas", "MISE", StringComparison.OrdinalIgnoreCase)
					.Replace("RetailCambas", "MISE", StringComparison.OrdinalIgnoreCase);
			}
			else if (item.Text == "店頭販促物を、正しく・速く。")
			{
				item.Visibility = Visibility.Collapsed;
			}
		}
		foreach (MenuItem item2 in VisualDescendants(this).OfType<MenuItem>())
		{
			string text2 = item2.Header?.ToString() ?? string.Empty;
			if (text2.StartsWith("Retail Canvas", StringComparison.OrdinalIgnoreCase) || text2.StartsWith("Retail Cambas", StringComparison.OrdinalIgnoreCase))
			{
				item2.Header = "MISEについて";
			}
		}
		VersionText.Text = "MISE 1.1.12";
	}

	private static BitmapSource? LoadMiseIcon()
	{
		try
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string text = executingAssembly.GetManifestResourceNames().FirstOrDefault((string x) => x.EndsWith("mise-icon.png", StringComparison.OrdinalIgnoreCase));
			if (text == null)
			{
				return null;
			}
			using Stream stream = executingAssembly.GetManifestResourceStream(text);
			if (stream == null)
			{
				return null;
			}
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = stream;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			return bitmapImage;
		}
		catch
		{
			return null;
		}
	}

	private void SimplifyLegacyPropertyPanel()
	{
		CollapseNearestGrid(PanelRowsBox);
		CollapseNearestGrid(PanelRowSplitsBox);
		CornerRadiusBox.Visibility = Visibility.Collapsed;
		CollapseNearestGrid(CornerLeftBox);
		foreach (TextBlock item in ShapeProperties.Children.OfType<TextBlock>())
		{
			bool flag;
			switch (item.Text)
			{
			case "角丸 (mm)":
			case "左右の角丸（空欄は共通値）":
			case "パネル分割":
			case "区切り位置（%・カンマ区切り）":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				item.Visibility = Visibility.Collapsed;
			}
		}
		Button button = new Button
		{
			Content = "角の形を選ぶ…",
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0),
			ToolTip = "どこを／どのように／どれくらい、の順で角を設定"
		};
		button.Click += EditCorners_Click;
		ShapeProperties.Children.Insert(Math.Min(ShapeProperties.Children.Count, 7), button);
	}

	private void EnhanceTextWeightControls()
	{
		if (_fontWeightSlider != null)
		{
			return;
		}
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 8.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "文字の太さ",
			FontWeight = FontWeights.SemiBold
		});
		_fontWeightCombo = new ComboBox
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 5.0)
		};
		(string, int)[] array = new(string, int)[6]
		{
			("Light", 300),
			("Regular", 400),
			("Medium", 500),
			("SemiBold", 600),
			("Bold", 700),
			("Black", 900)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(string, int) tuple = array[i];
			_fontWeightCombo.Items.Add(new ComboBoxItem
			{
				Content = $"{tuple.Item1}（{tuple.Item2}）",
				Tag = tuple.Item2
			});
		}
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition());
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(56.0)
		});
		_fontWeightSlider = new Slider
		{
			Minimum = 100.0,
			Maximum = 900.0,
			TickFrequency = 100.0,
			IsSnapToTickEnabled = true,
			Value = 400.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		_fontWeightValueText = new TextBlock
		{
			Text = "400",
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		Grid.SetColumn(_fontWeightValueText, 1);
		grid.Children.Add(_fontWeightSlider);
		grid.Children.Add(_fontWeightValueText);
		stackPanel.Children.Add(_fontWeightCombo);
		stackPanel.Children.Add(grid);
		_fontWeightSlider.PreviewMouseLeftButtonDown += delegate
		{
			_fontWeightUndoCaptured = false;
		};
		_fontWeightSlider.PreviewMouseLeftButtonUp += delegate
		{
			_fontWeightUndoCaptured = false;
		};
		_fontWeightSlider.ValueChanged += delegate
		{
			int value = NormalizeFontWeight((int)Math.Round(_fontWeightSlider.Value));
			if (_fontWeightValueText != null)
			{
				_fontWeightValueText.Text = value.ToString(CultureInfo.InvariantCulture);
			}
			if (!_updatingProperties)
			{
				if (!_fontWeightUndoCaptured)
				{
					PushUndo();
					_fontWeightUndoCaptured = true;
				}
				ApplyFontWeight(value);
			}
		};
		_fontWeightCombo.SelectionChanged += delegate
		{
			if (!_updatingProperties && _fontWeightCombo.SelectedItem is ComboBoxItem { Tag: var tag } && tag is int num && _fontWeightSlider != null)
			{
				_fontWeightUndoCaptured = false;
				_fontWeightSlider.Value = num;
				_fontWeightUndoCaptured = false;
			}
		};
		TextProperties.Children.Insert(Math.Min(5, TextProperties.Children.Count), stackPanel);
	}

	private static int NormalizeFontWeight(int value)
	{
		return Math.Clamp((int)Math.Round((double)value / 100.0, MidpointRounding.AwayFromZero) * 100, 100, 900);
	}

	private void EnhanceTextSpacingControls()
	{
		if (_characterSpacingBox == null && _lineSpacingBox == null)
		{
			StackPanel stackPanel = new StackPanel
			{
				Margin = new Thickness(0.0, 3.0, 0.0, 8.0)
			};
			stackPanel.Children.Add(new TextBlock
			{
				Text = "文字の間隔",
				FontWeight = FontWeights.SemiBold
			});
			Grid grid = new Grid
			{
				Margin = new Thickness(0.0, 4.0, 0.0, 2.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(8.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			StackPanel stackPanel2 = new StackPanel();
			stackPanel2.Children.Add(new TextBlock
			{
				Text = "字間・左右 (pt)",
				ToolTip = "隣り合う文字同士の左右方向の間隔"
			});
			_characterSpacingBox = new TextBox
			{
				Text = "0",
				Tag = "CharacterSpacing",
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				ToolTip = "0が標準。プラスで広げ、マイナスで詰めます"
			};
			_characterSpacingBox.LostFocus += TextProperty_LostFocus;
			stackPanel2.Children.Add(_characterSpacingBox);
			grid.Children.Add(stackPanel2);
			StackPanel stackPanel3 = new StackPanel();
			Grid.SetColumn(stackPanel3, 2);
			stackPanel3.Children.Add(new TextBlock
			{
				Text = "行間・上下 (pt)",
				ToolTip = "複数行の文字同士の上下方向の間隔"
			});
			_lineSpacingBox = new TextBox
			{
				Text = "0",
				Tag = "LineSpacing",
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0),
				ToolTip = "0が標準。プラスで広げ、マイナスで詰めます"
			};
			_lineSpacingBox.LostFocus += TextProperty_LostFocus;
			stackPanel3.Children.Add(_lineSpacingBox);
			grid.Children.Add(stackPanel3);
			stackPanel.Children.Add(grid);
			stackPanel.Children.Add(new TextBlock
			{
				Text = "0＝標準\u3000＋で広げる\u3000－で詰める",
				FontSize = 10.0,
				Foreground = Brushes.SlateGray,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			});
			TextProperties.Children.Insert(Math.Min(6, TextProperties.Children.Count), stackPanel);
		}
	}

	private void EnhanceTextureControls()
	{
		Button button = new Button
		{
			Content = "文字背景のテクスチャ…",
			ToolTip = "背景色にテクスチャを重ね、濃さと大きさを調整します",
			Margin = new Thickness(0.0, 2.0, 0.0, 8.0)
		};
		button.Click += EditElementTexture_Click;
		TextProperties.Children.Insert(Math.Min(9, TextProperties.Children.Count), button);
		Button button2 = new Button
		{
			Content = "塗り面のテクスチャ…",
			ToolTip = "図形・パネルの塗り面にテクスチャを適用します",
			Margin = new Thickness(0.0, 2.0, 0.0, 8.0)
		};
		button2.Click += EditElementTexture_Click;
		ShapeProperties.Children.Insert(Math.Min(4, ShapeProperties.Children.Count), button2);
	}

	private void ApplyFontWeight(int value)
	{
		value = NormalizeFontWeight(value);
		bool flag = false;
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel element) => element.Kind == ElementKind.Text && _selectedIds.Contains(element.Id)))
		{
			item.FontWeightValue = value;
			item.Bold = value >= 700;
			flag = true;
		}
		if (!flag)
		{
			CanvasElementModel activeElement = ActiveElement;
			if (activeElement != null && activeElement.Kind == ElementKind.Text)
			{
				activeElement.FontWeightValue = value;
				activeElement.Bold = value >= 700;
				flag = true;
			}
		}
		if (flag)
		{
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void OrganizePropertyPanel()
	{
		if (_propertyFolders.Count > 0)
		{
			return;
		}
		StackPanel stackPanel = new StackPanel();
		StackPanel stackPanel2 = new StackPanel();
		List<UIElement> list = new List<UIElement>();
		List<UIElement> list2 = new List<UIElement>();
		int num = 0;
		foreach (UIElement item in TextProperties.Children.Cast<UIElement>().ToList())
		{
			if (item is TextBlock { Text: "縁取り" })
			{
				num = 1;
			}
			if (item is TextBlock { Text: "立体効果" })
			{
				num = 2;
			}
			switch (num)
			{
			case 1:
				list.Add(item);
				break;
			case 2:
				list2.Add(item);
				break;
			}
		}
		foreach (UIElement item2 in list)
		{
			TextProperties.Children.Remove(item2);
			stackPanel.Children.Add(item2);
		}
		foreach (UIElement item3 in list2)
		{
			TextProperties.Children.Remove(item3);
			stackPanel2.Children.Add(item3);
		}
		StackPanel stackPanel3 = new StackPanel();
		StackPanel stackPanel4 = new StackPanel();
		List<UIElement> list3 = new List<UIElement>();
		List<UIElement> list4 = new List<UIElement>();
		bool flag = false;
		foreach (UIElement item4 in ShapeProperties.Children.Cast<UIElement>().ToList())
		{
			if (item4 is TextBlock { Text: "立体色" })
			{
				flag = true;
			}
			if (flag)
			{
				list4.Add(item4);
			}
			else if (item4 is Button { Content: var content })
			{
				string text = content?.ToString();
				if (text != null && (text.Contains("頂点") || text.Contains("分割線") || text.Contains("区画ごと")))
				{
					list3.Add(item4);
				}
			}
		}
		foreach (UIElement item5 in list3)
		{
			ShapeProperties.Children.Remove(item5);
			stackPanel3.Children.Add(item5);
		}
		foreach (UIElement item6 in list4)
		{
			ShapeProperties.Children.Remove(item6);
			stackPanel4.Children.Add(item6);
		}
		StackPanel stackPanel5 = new StackPanel();
		foreach (UIElement item7 in from child in PropertyFields.Children.Cast<UIElement>().ToList()
			where child != TextProperties && child != ShapeProperties && child != ImageProperties && child != QrProperties
			select child)
		{
			PropertyFields.Children.Remove(item7);
			stackPanel5.Children.Add(item7);
		}
		PropertyFields.Children.Remove(TextProperties);
		PropertyFields.Children.Remove(ShapeProperties);
		PropertyFields.Children.Remove(ImageProperties);
		PropertyFields.Children.Remove(QrProperties);
		StackPanel shell = new StackPanel
		{
			Margin = new Thickness(0.0, 2.0, 0.0, 10.0)
		};
		AddFolder("位置とサイズ", "座標・傾き・大きさ・回転", stackPanel5, null, expanded: true);
		AddFolder("文字とフォント", "文章・書体・文字色・配置", TextProperties, ElementKind.Text, expanded: true);
		AddFolder("文字の縁取り", "色・太さ", stackPanel, ElementKind.Text, expanded: false);
		AddFolder("文字の立体効果", "色・深さ・角度", stackPanel2, ElementKind.Text, expanded: false);
		AddFolder("図形の塗りと線", "塗り・線・角・分割線", ShapeProperties, ElementKind.Shape, expanded: true);
		AddFolder("図形の立体効果", "色・深さ・角度", stackPanel4, ElementKind.Shape, expanded: false);
		AddFolder("パネルと頂点編集", "区画・分割線・自由変形", stackPanel3, ElementKind.Shape, expanded: false);
		AddFolder("画像", "配置・画質・切り抜き・立体", ImageProperties, ElementKind.Image, expanded: true);
		AddFolder("QRコード", "内容・色・誤り訂正", QrProperties, ElementKind.QrCode, expanded: true);
		PropertyFields.Children.Add(shell);
		void AddFolder(string header, string description, UIElement child, ElementKind? kind, bool expanded)
		{
			StackPanel stackPanel6 = new StackPanel
			{
				Margin = new Thickness(2.0, 1.0, 0.0, 1.0)
			};
			stackPanel6.Children.Add(new TextBlock
			{
				Text = header,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(Color.FromRgb(25, 35, 54))
			});
			stackPanel6.Children.Add(new TextBlock
			{
				Text = description,
				FontSize = 9.0,
				Foreground = Brushes.SlateGray,
				Margin = new Thickness(0.0, 1.0, 0.0, 0.0)
			});
			StackPanel stackPanel7 = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			stackPanel7.Children.Add(new TextBlock
			{
				Text = "▱",
				FontSize = 17.0,
				Foreground = new SolidColorBrush(Color.FromRgb(43, 182, 200)),
				Margin = new Thickness(0.0, 2.0, 7.0, 0.0)
			});
			stackPanel7.Children.Add(stackPanel6);
			Border content2 = new Border
			{
				Child = child,
				BorderBrush = new SolidColorBrush(Color.FromRgb(75, 190, 205)),
				BorderThickness = new Thickness(2.0, 0.0, 0.0, 0.0),
				Padding = new Thickness(10.0, 5.0, 1.0, 9.0),
				Margin = new Thickness(7.0, 0.0, 0.0, 5.0)
			};
			Expander expander = new Expander
			{
				Header = stackPanel7,
				Content = content2,
				IsExpanded = expanded,
				Margin = new Thickness(0.0, 1.0, 0.0, 1.0),
				Background = Brushes.White,
				HorizontalContentAlignment = HorizontalAlignment.Stretch
			};
			shell.Children.Add(expander);
			_propertyFolders.Add((expander, kind));
		}
	}

	private void UpdatePropertyTabs(CanvasElementModel? element)
	{
		if (_propertyFolders.Count == 0)
		{
			return;
		}
		foreach (var propertyFolder in _propertyFolders)
		{
			propertyFolder.Folder.Visibility = ((propertyFolder.Kind.HasValue && (element == null || propertyFolder.Kind != element.Kind)) ? Visibility.Collapsed : Visibility.Visible);
		}
	}

	private void AddNumericSpinners()
	{
		foreach (TextBox box in new TextBox[20]
		{
			XBox, YBox, SkewXBox, SkewYBox, WidthBox, HeightBox, RotationBox, OpacityBox, FontSizeBox, TextOutlineThicknessBox,
			TextExtrusionDepthBox, TextExtrusionAngleBox, StrokeThicknessBox, CornerRadiusBox, CornerLeftBox, CornerRightBox, PanelRowsBox, PanelColumnsBox, ShapeExtrusionDepthBox, ShapeExtrusionAngleBox
		}.Concat(new TextBox[2] { _characterSpacingBox, _lineSpacingBox }).OfType<TextBox>())
		{
			if (!(LogicalTreeHelper.GetParent(box) is Panel panel))
			{
				continue;
			}
			int num = panel.Children.IndexOf(box);
			if (num >= 0)
			{
				Thickness margin = box.Margin;
				box.Margin = new Thickness(0.0);
				box.MinHeight = 28.0;
				box.VerticalContentAlignment = VerticalAlignment.Center;
				Grid grid = new Grid
				{
					Margin = margin,
					Height = 30.0
				};
				grid.ColumnDefinitions.Add(new ColumnDefinition());
				grid.ColumnDefinitions.Add(new ColumnDefinition
				{
					Width = new GridLength(20.0)
				});
				panel.Children.RemoveAt(num);
				grid.Children.Add(box);
				Grid grid2 = new Grid
				{
					Margin = new Thickness(2.0, 0.0, 0.0, 0.0)
				};
				grid2.RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(15.0)
				});
				grid2.RowDefinitions.Add(new RowDefinition
				{
					Height = new GridLength(15.0)
				});
				Button button = new Button
				{
					Content = "▴",
					Padding = new Thickness(0.0),
					Margin = new Thickness(0.0),
					FontSize = 8.0,
					MinHeight = 0.0,
					Height = 15.0,
					ToolTip = "値を増やす"
				};
				Button button2 = new Button
				{
					Content = "▾",
					Padding = new Thickness(0.0),
					Margin = new Thickness(0.0),
					FontSize = 8.0,
					MinHeight = 0.0,
					Height = 15.0,
					ToolTip = "値を減らす"
				};
				Grid.SetRow(button2, 1);
				button.Click += delegate
				{
					StepNumericBox(box, 1);
				};
				button2.Click += delegate
				{
					StepNumericBox(box, -1);
				};
				grid2.Children.Add(button);
				grid2.Children.Add(button2);
				Grid.SetColumn(grid2, 1);
				grid.Children.Add(grid2);
				panel.Children.Insert(num, grid);
			}
		}
	}

	private void StepNumericBox(TextBox box, int direction)
	{
		if (!TryNumber(box.Text, out var value))
		{
			value = 0.0;
		}
		double num;
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
		{
			num = 10.0;
		}
		else
		{
			string text = box.Tag?.ToString();
			bool flag = ((text == "PanelRows" || text == "PanelColumns") ? true : false);
			num = (flag ? 1.0 : 0.5);
		}
		double num2 = num;
		box.Text = (value + (double)direction * num2).ToString("0.##", CultureInfo.CurrentCulture);
		box.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent, box));
	}

	private static void CollapseNearestGrid(DependencyObject child)
	{
		DependencyObject dependencyObject = child;
		while (dependencyObject != null && !(dependencyObject is Grid))
		{
			dependencyObject = VisualTreeHelper.GetParent(dependencyObject) ?? LogicalTreeHelper.GetParent(dependencyObject);
		}
		if (dependencyObject is Grid grid)
		{
			grid.Visibility = Visibility.Collapsed;
		}
	}

	private void AddToolbarShortcut(Panel panel, string text, string tooltip, RoutedEventHandler click)
	{
		Button button = new Button
		{
			Content = text,
			ToolTip = tooltip,
			Style = (TryFindResource("ToolbarButton") as Style)
		};
		button.Click += click;
		panel.Children.Add(button);
	}

	private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(root, i);
			yield return child;
			foreach (DependencyObject item in VisualDescendants(child))
			{
				yield return item;
			}
		}
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		RefreshMiseVisibleLabels();
		ApplyUiPreferences();
		UpdateToolbarForWidth(base.ActualWidth);
		if (!string.IsNullOrWhiteSpace(App.StartupProjectPath))
		{
			OpenProject(App.StartupProjectPath);
		}
		if (string.IsNullOrWhiteSpace(App.StartupProjectPath) && !_settings.Current.ShowHomeOnStartup)
		{
			HomeOverlay.Visibility = Visibility.Collapsed;
		}
		RefreshRecent();
		base.Dispatcher.BeginInvoke(new Action(RefreshMiseVisibleLabels), DispatcherPriority.Loaded);
		base.Dispatcher.BeginInvoke(new Action(ApplyStartupZoom), DispatcherPriority.Loaded);
	}

	private void ConfigureAutoSave()
	{
		_autoSaveTimer.Stop();
		if (_settings.Current.AutoSaveMinutes <= 0)
		{
			AutoSaveText.Text = "自動保存: 無効";
			return;
		}
		_autoSaveTimer.Interval = TimeSpan.FromMinutes(_settings.Current.AutoSaveMinutes);
		_autoSaveTimer.Start();
		AutoSaveText.Text = $"自動保存: {_settings.Current.AutoSaveMinutes}分";
	}

	private void ApplyStartupZoom()
	{
		switch (_settings.Current.StartupZoomMode)
		{
		case "50%":
			ApplyZoom(0.5);
			break;
		case "75%":
			ApplyZoom(0.75);
			break;
		case "100%":
			ApplyZoom(1.0);
			break;
		case "カスタム":
			ApplyZoom((double)_settings.Current.DefaultZoomPercent / 100.0);
			break;
		default:
			FitPage();
			break;
		}
	}

	private void ApplyUiPreferences()
	{
		string uiDensity = _settings.Current.UiDensity;
		ToolbarRow.Height = new GridLength((uiDensity == "コンパクト") ? 42 : ((uiDensity == "ゆったり") ? 56 : 48));
		StatusRow.Height = new GridLength((uiDensity == "コンパクト") ? 28 : ((uiDensity == "ゆったり") ? 38 : 32));
		CanvasOuter.Margin = new Thickness((uiDensity == "コンパクト") ? 180 : ((uiDensity == "ゆったり") ? 360 : 280));
		_leftManuallyHidden = !_settings.Current.ShowLeftPanelOnStartup;
		_rightManuallyHidden = !_settings.Current.ShowRightPanelOnStartup;
		SetLeftPanelVisible(_settings.Current.ShowLeftPanelOnStartup);
		SetRightPanelVisible(_settings.Current.ShowRightPanelOnStartup);
		Window_SizeChanged(this, null);
	}

	private void SetLeftPanelVisible(bool show)
	{
		LeftColumn.Width = (show ? new GridLength(Math.Clamp(_settings.Current.LeftPanelWidth, 285.0, 500.0)) : new GridLength(0.0));
		LeftSplitterColumn.Width = (show ? new GridLength(5.0) : new GridLength(0.0));
		LeftPanel.Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
		LeftSplitter.Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
		LeftPanelToggleButton.Visibility = (show ? Visibility.Collapsed : Visibility.Visible);
	}

	private void SetRightPanelVisible(bool show)
	{
		RightColumn.Width = (show ? new GridLength(Math.Clamp(_settings.Current.RightPanelWidth, 200.0, 600.0)) : new GridLength(0.0));
		RightSplitterColumn.Width = (show ? new GridLength(5.0) : new GridLength(0.0));
		RightPanel.Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
		RightSplitter.Visibility = ((!show) ? Visibility.Collapsed : Visibility.Visible);
		RightPanelToggleButton.Visibility = (show ? Visibility.Collapsed : Visibility.Visible);
	}

	private void RefreshRecent()
	{
		List<RecentProjectInfo> list = new List<RecentProjectInfo>();
		foreach (string item in _projectService.FindRecoveryFiles())
		{
			try
			{
				ProjectModel projectModel = _projectService.Load(item);
				list.Add(new RecentProjectInfo
				{
					FilePath = item,
					ProjectName = projectModel.ProjectName + "（自動保存）",
					PaperName = projectModel.PaperName,
					BrandName = projectModel.BrandName,
					StoreName = projectModel.StoreName,
					LastOpenedAt = File.GetLastWriteTime(item),
					IsAutoSave = true
				});
			}
			catch (Exception ex)
			{
				LogService.Error("Recovery list load failed", ex);
			}
		}
		foreach (RecentProjectInfo recentProject in _settings.Current.RecentProjects)
		{
			string text = recentProject.ProjectName;
			if (File.Exists(recentProject.FilePath) && text.EndsWith("（リンク切れ）", StringComparison.Ordinal))
			{
				string text2 = text;
				int length = "（リンク切れ）".Length;
				text = text2.Substring(0, text2.Length - length);
			}
			else if (!File.Exists(recentProject.FilePath) && !text.EndsWith("（リンク切れ）", StringComparison.Ordinal))
			{
				text += "（リンク切れ）";
			}
			list.Add(new RecentProjectInfo
			{
				FilePath = recentProject.FilePath,
				ProjectName = text,
				PaperName = recentProject.PaperName,
				BrandName = recentProject.BrandName,
				StoreName = recentProject.StoreName,
				LastOpenedAt = recentProject.LastOpenedAt
			});
		}
		RecentList.ItemsSource = null;
		RecentList.ItemsSource = list;
	}

	private void CreateBlankProject(string paper, bool landscape, bool hideHome = true, double? customWidth = null, double? customHeight = null)
	{
		PageModel pageModel = PageModel.Create(paper, landscape);
		if (customWidth.HasValue && customHeight.HasValue)
		{
			pageModel.WidthMm = customWidth.Value;
			pageModel.HeightMm = customHeight.Value;
		}
		pageModel.SafeMarginMm = _settings.Current.DefaultSafeMarginMm;
		pageModel.ShowGrid = _settings.Current.ShowGridOnNewProjects;
		pageModel.ShowSafeArea = _settings.Current.ShowSafeAreaOnNewProjects;
		_project = new ProjectModel
		{
			PaperName = paper,
			Landscape = landscape,
			PrintMode = _settings.Current.DefaultPrintMode,
			Pages = new List<PageModel> { pageModel },
			ExportSettings = new ExportSettings
			{
				Dpi = _settings.Current.DefaultExportDpi
			}
		};
		ActivateEmbeddedFonts();
		_pageIndex = 0;
		_filePath = null;
		_dirty = false;
		_undo.Clear();
		_redo.Clear();
		_selectedIds.Clear();
		if (hideHome)
		{
			HomeOverlay.Visibility = Visibility.Collapsed;
		}
		RefreshAll();
	}

	private void RefreshAll()
	{
		if (_project.Pages.Count == 0)
		{
			_project.Pages.Add(PageModel.Create("A4", landscape: false));
		}
		_pageIndex = Math.Clamp(_pageIndex, 0, _project.Pages.Count - 1);
		_refreshing = true;
		try
		{
			PageModel currentPage = CurrentPage;
			PageCanvas.Width = currentPage.WidthMm * 3.7795275590551185;
			PageCanvas.Height = currentPage.HeightMm * 3.7795275590551185;
			OverflowCanvas.Width = PageCanvas.Width;
			OverflowCanvas.Height = PageCanvas.Height;
			GuideOverlayCanvas.Width = PageCanvas.Width;
			GuideOverlayCanvas.Height = PageCanvas.Height;
			PageCanvas.Background = TextureCatalogService.Blend(BrushFrom(currentPage.Background, Brushes.White), currentPage.BackgroundTextureDataBase64, currentPage.BackgroundTextureOpacity, currentPage.BackgroundTextureScale);
			PageCanvas.SafeMarginMm = currentPage.SafeMarginMm;
			PageCanvas.BleedMm = currentPage.BleedMm;
			PageCanvas.PrintMarginMm = currentPage.PrintMarginMm;
			PageCanvas.GridMm = _settings.Current.GridSizeMm;
			PageCanvas.ShowGrid = false;
			PageCanvas.ShowSafeArea = false;
			PageCanvas.ShowBleed = false;
			PageCanvas.ShowPrintMargin = false;
			PageCanvas.ShowCenterGuides = false;
			PageCanvas.ShowVerticalCenterGuide = false;
			PageCanvas.ShowHorizontalCenterGuide = false;
			PageCanvas.RefreshGuides();
			GuideOverlayCanvas.GridMm = _settings.Current.GridSizeMm;
			GuideOverlayCanvas.SafeMarginMm = currentPage.SafeMarginMm;
			GuideOverlayCanvas.BleedMm = currentPage.BleedMm;
			GuideOverlayCanvas.PrintMarginMm = currentPage.PrintMarginMm;
			GuideOverlayCanvas.ShowGrid = currentPage.ShowGrid;
			GuideOverlayCanvas.ShowSafeArea = currentPage.ShowSafeArea;
			GuideOverlayCanvas.ShowBleed = currentPage.ShowBleed;
			GuideOverlayCanvas.ShowPrintMargin = currentPage.PrintMarginMm > 0.0;
			GuideOverlayCanvas.ShowCenterGuides = _settings.Current.ShowCenterGuides;
			GuideOverlayCanvas.ShowVerticalCenterGuide = _settings.Current.ShowVerticalCenterGuide;
			GuideOverlayCanvas.ShowHorizontalCenterGuide = _settings.Current.ShowHorizontalCenterGuide;
			GuideOverlayCanvas.RefreshGuides();
			RebuildCanvas();
			PageList.ItemsSource = null;
			PageList.ItemsSource = _project.Pages;
			PageList.SelectedIndex = _pageIndex;
			RefreshLayers();
			UpdatePropertyPanel();
			UpdateStatus();
			UpdateValidationCount();
		}
		finally
		{
			_refreshing = false;
		}
	}

	private void RebuildCanvas()
	{
		PageCanvas.Children.Clear();
		OverflowCanvas.Children.Clear();
		_visuals.Clear();
		_overflowVisuals.Clear();
		UpdateOverflowClip();
		foreach (CanvasElementModel model in CurrentPage.Elements.OrderBy((CanvasElementModel x) => x.ZIndex))
		{
			model.WidthMm = Math.Clamp(model.WidthMm, 1.0, Math.Max(1.0, CurrentPage.WidthMm));
			model.HeightMm = Math.Clamp(model.HeightMm, 1.0, Math.Max(1.0, CurrentPage.HeightMm));
			model.Xmm = Math.Clamp(model.Xmm, 0.0, Math.Max(0.0, CurrentPage.WidthMm - model.WidthMm));
			model.Ymm = Math.Clamp(model.Ymm, 0.0, Math.Max(0.0, CurrentPage.HeightMm - model.HeightMm));
			FrameworkElement visual = BuildVisual(model, inverted: false);
			DesignerItem designerItem = new DesignerItem(model, visual)
			{
				Width = Math.Max(4.0, model.WidthMm * 3.7795275590551185),
				Height = Math.Max(4.0, model.HeightMm * 3.7795275590551185),
				Opacity = Math.Clamp(model.Opacity, 0.0, 1.0),
				Visibility = ((!model.IsVisible || (_isolatedIds != null && !_isolatedIds.Contains(model.Id))) ? Visibility.Collapsed : Visibility.Visible),
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = CreateElementTransform(model),
				SnapPosition = SnapPosition
			};
			Canvas.SetLeft(designerItem, model.Xmm * 3.7795275590551185);
			Canvas.SetTop(designerItem, model.Ymm * 3.7795275590551185);
			Panel.SetZIndex(designerItem, model.ZIndex);
			designerItem.SelectionRequested += DesignerItem_SelectionRequested;
			designerItem.ChangeStarted += delegate
			{
				PushUndo();
				BeginLightweightPreview();
			};
			designerItem.MoveStarted += DesignerItem_MoveStarted;
			designerItem.MovePreview += DesignerItem_MovePreview;
			designerItem.MoveFinished += DesignerItem_MoveFinished;
			designerItem.ResizePreview += delegate
			{
				AutoScrollDuringObjectDrag();
			};
			designerItem.InteractionCanceled += DesignerItem_InteractionCanceled;
			designerItem.VisualBoundsChanged += delegate
			{
				SyncOverflowVisual(model.Id);
			};
			designerItem.PreviewMouseRightButtonDown += delegate(object _, MouseButtonEventArgs e)
			{
				_lastCanvasContextPosition = e.GetPosition(PageCanvas);
				if (!_selectedIds.Contains(model.Id))
				{
					SelectOnly(model.Id);
				}
				UpdateSelectionVisuals();
				e.Handled = false;
			};
			designerItem.ContextMenu = BuildObjectContextMenu(model);
			designerItem.ModelChanged += delegate
			{
				// Normalize committed bounds so a resize or restored project cannot
				// leave elements at negative coordinates or beyond the page.
				model.WidthMm = Math.Clamp(model.WidthMm, 1.0, Math.Max(1.0, CurrentPage.WidthMm));
				model.HeightMm = Math.Clamp(model.HeightMm, 1.0, Math.Max(1.0, CurrentPage.HeightMm));
				model.Xmm = Math.Clamp(model.Xmm, 0.0, Math.Max(0.0, CurrentPage.WidthMm - model.WidthMm));
				model.Ymm = Math.Clamp(model.Ymm, 0.0, Math.Max(0.0, CurrentPage.HeightMm - model.HeightMm));
				MarkDirty();
				UpdatePropertyPanel();
				UpdateStatus();
			};
			designerItem.ChangeCompleted += delegate
			{
				EndLightweightPreview();
				NormalizeZ();
				RefreshLayers();
				UpdateValidationCount();
			};
			designerItem.IsSelected = _selectedIds.Contains(model.Id);
			PageCanvas.Children.Add(designerItem);
			_visuals[model.Id] = designerItem;
			ContentControl contentControl = new ContentControl
			{
				Content = BuildVisual(model, inverted: true),
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Stretch,
				IsHitTestVisible = false
			};
			OverflowCanvas.Children.Add(contentControl);
			_overflowVisuals[model.Id] = contentControl;
			SyncOverflowVisual(model.Id);
		}
	}

	private void BeginLightweightPreview()
	{
		if (!_settings.Current.UseLightweightDragPreview && _settings.Current.PerformanceMode != "軽快さ優先")
		{
			return;
		}
		foreach (KeyValuePair<Guid, DesignerItem> item in _visuals.Where<KeyValuePair<Guid, DesignerItem>>((KeyValuePair<Guid, DesignerItem> x) => _selectedIds.Contains(x.Key)))
		{
			RenderOptions.SetBitmapScalingMode(item.Value, BitmapScalingMode.LowQuality);
			item.Value.CacheMode = new BitmapCache
			{
				RenderAtScale = Math.Clamp(_zoom, 0.5, 1.0),
				EnableClearType = false
			};
		}
	}

	private void EndLightweightPreview()
	{
		foreach (KeyValuePair<Guid, DesignerItem> item in _visuals.Where<KeyValuePair<Guid, DesignerItem>>((KeyValuePair<Guid, DesignerItem> x) => _selectedIds.Contains(x.Key)))
		{
			item.Value.CacheMode = null;
			RenderOptions.SetBitmapScalingMode(item.Value, BitmapScalingMode.HighQuality);
		}
	}

	private ContextMenu BuildCanvasContextMenu()
	{
		ContextMenu contextMenu = new ContextMenu();
		contextMenu.Opened += delegate
		{
			_lastCanvasContextPosition = Mouse.GetPosition(PageCanvas);
		};
		contextMenu.Items.Add(MenuItemOf("ここに貼り付け", delegate(object _, RoutedEventArgs e)
		{
			Paste_Click(this, e);
			List<CanvasElementModel> list = CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)).ToList();
			if (list.Count != 0)
			{
				double num = list.Min((CanvasElementModel x) => x.Xmm);
				double num2 = list.Min((CanvasElementModel x) => x.Ymm);
				double num3 = _lastCanvasContextPosition.X / 3.7795275590551185;
				double num4 = _lastCanvasContextPosition.Y / 3.7795275590551185;
				foreach (CanvasElementModel item in list)
				{
					item.Xmm += num3 - num;
					item.Ymm += num4 - num2;
				}
				RebuildCanvas();
				UpdatePropertyPanel();
			}
		}, "Ctrl+V"));
		contextMenu.Items.Add(new Separator());
		contextMenu.Items.Add(MenuItemOf("文字を追加", AddHeading_Click));
		contextMenu.Items.Add(MenuItemOf("角丸図形を追加", delegate(object s, RoutedEventArgs e)
		{
			AddShape_Click(new MenuItem
			{
				Tag = "RoundedRectangle"
			}, e);
		}));
		contextMenu.Items.Add(MenuItemOf("パネルを追加", delegate(object s, RoutedEventArgs e)
		{
			AddShape_Click(new MenuItem
			{
				Tag = "Panel"
			}, e);
		}));
		contextMenu.Items.Add(MenuItemOf("再利用ブロックをここに挿入…", delegate
		{
			OpenReusableBlocksAt(new Point(_lastCanvasContextPosition.X / 3.7795275590551185, _lastCanvasContextPosition.Y / 3.7795275590551185));
		}));
		contextMenu.Items.Add(MenuItemOf("スポイト", Eyedropper_Click));
		contextMenu.Items.Add(new Separator());
		contextMenu.Items.Add(MenuItemOf("すべて選択", SelectAll_Click, "Ctrl+A"));
		return contextMenu;
	}

	private ContextMenu BuildObjectContextMenu(CanvasElementModel model)
	{
		ContextMenu contextMenu = new ContextMenu();
		MenuItem overlapMenu = new MenuItem
		{
			Header = "重なりから選択"
		};
		contextMenu.Items.Add(overlapMenu);
		contextMenu.Opened += delegate
		{
			_lastCanvasContextPosition = Mouse.GetPosition(PageCanvas);
			overlapMenu.Items.Clear();
			foreach (DesignerItem item3 in GetElementsAt(_lastCanvasContextPosition))
			{
				CanvasElementModel candidate = item3.Model;
				string text2 = candidate.Kind switch
				{
					ElementKind.Text => "文字", 
					ElementKind.Image => (candidate.PdfSourcePath == null) ? "画像" : "PDF", 
					ElementKind.Shape => "図形", 
					ElementKind.QrCode => "QR", 
					_ => candidate.Kind.ToString(), 
				};
				MenuItem menuItem8 = new MenuItem
				{
					Header = text2 + "：" + candidate.Name + (candidate.IsLocked ? "（ロック中）" : string.Empty),
					IsCheckable = true,
					IsChecked = _selectedIds.Contains(candidate.Id)
				};
				menuItem8.Click += delegate
				{
					SelectOnly(candidate.Id);
					UpdateSelectionVisuals();
				};
				overlapMenu.Items.Add(menuItem8);
			}
			overlapMenu.IsEnabled = overlapMenu.Items.Count > 0;
		};
		contextMenu.Items.Add(MenuItemOf((_selectedIds.Count > 1) ? "選択項目をコピー" : "コピー", Copy_Click, "Ctrl+C"));
		contextMenu.Items.Add(MenuItemOf("複製", Duplicate_Click, "Ctrl+D"));
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem = new MenuItem
		{
			Header = "前面・背面"
		};
		menuItem.Items.Add(LayerMenu("最前面へ", "Front"));
		menuItem.Items.Add(LayerMenu("一つ前へ", "Forward"));
		menuItem.Items.Add(LayerMenu("一つ後ろへ", "Backward"));
		menuItem.Items.Add(LayerMenu("最背面へ", "Back"));
		if (_selectedIds.Count > 1)
		{
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(LayerMenu("もう一つの選択要素より前へ", "AboveSelected"));
			menuItem.Items.Add(LayerMenu("もう一つの選択要素より後ろへ", "BelowSelected"));
		}
		contextMenu.Items.Add(menuItem);
		MenuItem menuItem2 = new MenuItem
		{
			Header = "配置・整列"
		};
		(string, string)[] array = new(string, string)[8]
		{
			("左揃え", "Left"),
			("中央揃え", "Center"),
			("右揃え", "Right"),
			("上揃え", "Top"),
			("縦中央揃え", "Middle"),
			("下揃え", "Bottom"),
			("ページ左右中央", "PageCenterX"),
			("ページ上下中央", "PageCenterY")
		};
		for (int num = 0; num < array.Length; num++)
		{
			(string, string) tuple = array[num];
			string action = tuple.Item2;
			menuItem2.Items.Add(MenuItemOf(tuple.Item1, delegate(object s, RoutedEventArgs e)
			{
				Align_Click(new MenuItem
				{
					Tag = action
				}, e);
			}));
		}
		contextMenu.Items.Add(menuItem2);
		contextMenu.Items.Add(MenuItemOf("同じ種類をすべて選択", delegate
		{
			_selectedIds.Clear();
			foreach (CanvasElementModel item4 in CurrentPage.Elements.Where((CanvasElementModel x) => x.Kind == model.Kind && x.IsVisible))
			{
				_selectedIds.Add(item4.Id);
			}
			UpdateSelectionVisuals();
		}));
		contextMenu.Items.Add(MenuItemOf("選択項目を再利用ブロックとして保存…", SaveReusableBlock_Click));
		contextMenu.Items.Add(MenuItemOf((_isolatedIds == null) ? "選択項目だけを一時表示" : "分離表示を終了", delegate
		{
			_isolatedIds = ((_isolatedIds == null) ? new HashSet<Guid>(_selectedIds) : null);
			RebuildCanvas();
			StatusText.Text = ((_isolatedIds == null) ? "分離表示を終了しました" : "選択項目だけを分離表示中");
		}));
		MenuItem menuItem3 = new MenuItem
		{
			Header = "スタイル"
		};
		menuItem3.Items.Add(MenuItemOf("スタイルをコピー", delegate
		{
			_copiedStyle = CloneElement(model);
			StatusText.Text = "スタイルをコピーしました";
		}));
		menuItem3.Items.Add(MenuItemOf("スタイルを貼り付け", delegate
		{
			PasteStyleToSelection();
		}, null, _copiedStyle != null));
		menuItem3.Items.Add(MenuItemOf("スポイトで色を取得", Eyedropper_Click));
		menuItem3.Items.Add(MenuItemOf("テクスチャ…", EditElementTexture_Click));
		contextMenu.Items.Add(menuItem3);
		if (model.Kind == ElementKind.Shape)
		{
			contextMenu.Items.Add(new Separator());
			if (model.ShapeType == "Line")
			{
				MenuItem menuItem4 = new MenuItem
				{
					Header = "線の種類・矢印"
				};
				menuItem4.Items.Add(LinePreset("通常線", "実線", "なし", "なし"));
				menuItem4.Items.Add(LinePreset("破線", "破線", "なし", "なし"));
				menuItem4.Items.Add(LinePreset("点線", "点線", "なし", "なし"));
				menuItem4.Items.Add(LinePreset("片側矢印", "実線", "なし", "三角矢印"));
				menuItem4.Items.Add(LinePreset("両側矢印", "実線", "三角矢印", "三角矢印"));
				menuItem4.Items.Add(LinePreset("開き矢印", "実線", "なし", "開き矢印"));
				menuItem4.Items.Add(LinePreset("丸端", "実線", "丸", "丸"));
				contextMenu.Items.Add(menuItem4);
				string[] obj = new string[10] { "なし", "三角矢印", "細型矢印", "幅広矢印", "中抜き矢印", "V字", "山形", "ひし形", "丸", "四角" };
				MenuItem menuItem5 = new MenuItem
				{
					Header = "始点の形"
				};
				MenuItem menuItem6 = new MenuItem
				{
					Header = "終点の形"
				};
				string[] array2 = obj;
				foreach (string text in array2)
				{
					string selectedCap = text;
					menuItem5.Items.Add(MenuItemOf(text, delegate
					{
						PushUndo();
						model.LineStartCap = selectedCap;
						MarkDirty();
						RebuildCanvas();
					}));
					menuItem6.Items.Add(MenuItemOf(text, delegate
					{
						PushUndo();
						model.LineEndCap = selectedCap;
						MarkDirty();
						RebuildCanvas();
					}));
				}
				menuItem4.Items.Add(new Separator());
				menuItem4.Items.Add(menuItem5);
				menuItem4.Items.Add(menuItem6);
				MenuItem menuItem7 = new MenuItem
				{
					Header = "矢印サイズ"
				};
				(string, double)[] array3 = new(string, double)[4]
				{
					("小", 5.0),
					("標準", 8.0),
					("大", 13.0),
					("特大", 20.0)
				};
				for (int num2 = 0; num2 < array3.Length; num2++)
				{
					(string, double) tuple2 = array3[num2];
					string item = tuple2.Item1;
					double item2 = tuple2.Item2;
					double selectedSize = item2;
					menuItem7.Items.Add(MenuItemOf(item, delegate
					{
						PushUndo();
						model.ArrowSize = selectedSize;
						MarkDirty();
						RebuildCanvas();
					}));
				}
				menuItem4.Items.Add(menuItem7);
			}
			contextMenu.Items.Add(MenuItemOf("角の形…", EditCorners_Click));
			contextMenu.Items.Add(MenuItemOf("頂点・精密編集…", EditShapePoints_Click));
			if (model.ShapeType != "Line")
			{
				contextMenu.Items.Add(MenuItemOf(IsPanelElement(model) ? "パネル分割線を移動…" : "この図形に分割線を追加…", EditPanelDividers_Click));
			}
			if (IsPanelElement(model))
			{
				contextMenu.Items.Add(MenuItemOf("区画ごとの色…", EditPanelCellColors_Click));
			}
		}
		if (model.Kind == ElementKind.Image)
		{
			contextMenu.Items.Add(MenuItemOf("画像を差し替え…", ReplaceImage_Click));
			contextMenu.Items.Add(MenuItemOf("画像の立体効果…", EditImageExtrusion_Click));
			contextMenu.Items.Add(MenuItemOf("透明余白のトリミング…", EditTransparentImageTrim_Click));
			contextMenu.Items.Add(MenuItemOf("パス抜き・背景除去（非破壊）…", RemoveImageBackground_Click));
			if (!string.IsNullOrWhiteSpace(model.ImageOriginalDataBase64))
			{
				contextMenu.Items.Add(MenuItemOf("パス抜きを解除して元画像へ戻す", ResetImageCutout_Click));
			}
		}
		contextMenu.Items.Add(new Separator());
		contextMenu.Items.Add(MenuItemOf(model.IsLocked ? "ロック解除" : "ロック", delegate
		{
			PushUndo();
			foreach (CanvasElementModel item5 in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)))
			{
				item5.IsLocked = !model.IsLocked;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}));
		contextMenu.Items.Add(MenuItemOf("削除", Delete_Click, "Delete"));
		return contextMenu;
		MenuItem LayerMenu(string header, string tag)
		{
			return MenuItemOf(header, delegate(object s, RoutedEventArgs e)
			{
				LayerOrder_Click(new MenuItem
				{
					Tag = tag
				}, e);
			});
		}
		MenuItem LinePreset(string header, string style, string start, string end)
		{
			return MenuItemOf(header, delegate
			{
				PushUndo();
				model.LineStyle = style;
				model.LineStartCap = start;
				model.LineEndCap = end;
				MarkDirty();
				RebuildCanvas();
				UpdatePropertyPanel();
			});
		}
	}

	private static MenuItem MenuItemOf(string header, RoutedEventHandler click, string? gesture = null, bool enabled = true)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Header = header;
		menuItem.InputGestureText = gesture ?? string.Empty;
		menuItem.IsEnabled = enabled;
		menuItem.Click += click;
		return menuItem;
	}

	private void PasteStyleToSelection()
	{
		if (_copiedStyle == null || _selectedIds.Count == 0)
		{
			return;
		}
		PushUndo();
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)))
		{
			item.Opacity = _copiedStyle.Opacity;
			if (item.Kind == ElementKind.Text && _copiedStyle.Kind == ElementKind.Text)
			{
				item.FontFamily = _copiedStyle.FontFamily;
				item.FontSizePt = _copiedStyle.FontSizePt;
				item.FontWeightValue = _copiedStyle.FontWeightValue;
				item.Bold = _copiedStyle.Bold;
				item.Italic = _copiedStyle.Italic;
				item.Underline = _copiedStyle.Underline;
				item.CharacterSpacing = _copiedStyle.CharacterSpacing;
				item.LineHeight = _copiedStyle.LineHeight;
				item.LineSpacingPt = _copiedStyle.LineSpacingPt;
				item.TextColor = _copiedStyle.TextColor;
				item.TextBackground = _copiedStyle.TextBackground;
				item.TextOutlineColor = _copiedStyle.TextOutlineColor;
				item.TextOutlineThicknessPt = _copiedStyle.TextOutlineThicknessPt;
				item.TextExtrusionColor = _copiedStyle.TextExtrusionColor;
				item.TextExtrusionDepthPt = _copiedStyle.TextExtrusionDepthPt;
				item.TextExtrusionAngle = _copiedStyle.TextExtrusionAngle;
			}
			else if (item.Kind == ElementKind.Shape && _copiedStyle.Kind == ElementKind.Shape)
			{
				item.FillColor = _copiedStyle.FillColor;
				item.StrokeColor = _copiedStyle.StrokeColor;
				item.StrokeThicknessPt = _copiedStyle.StrokeThicknessPt;
				item.CornerRadiusMm = _copiedStyle.CornerRadiusMm;
				item.ShapeExtrusionColor = _copiedStyle.ShapeExtrusionColor;
				item.ShapeExtrusionDepthPt = _copiedStyle.ShapeExtrusionDepthPt;
				item.ShapeExtrusionAngle = _copiedStyle.ShapeExtrusionAngle;
			}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private void UpdateOverflowClip()
	{
		OverflowCanvas.Visibility = ((!_settings.Current.InvertOutOfBoundsObjects) ? Visibility.Collapsed : Visibility.Visible);
		double num = Math.Max(1.0, PageCanvas.Width);
		double num2 = Math.Max(1.0, PageCanvas.Height);
		double num3 = Math.Max(num, num2) * 2.0;
		RectangleGeometry geometry = new RectangleGeometry(new Rect(0.0 - num3, 0.0 - num3, num + num3 * 2.0, num2 + num3 * 2.0));
		RectangleGeometry geometry2 = new RectangleGeometry(new Rect(0.0, 0.0, num, num2));
		OverflowCanvas.Clip = new CombinedGeometry(GeometryCombineMode.Exclude, geometry, geometry2);
	}

	private void SyncOverflowVisual(Guid id)
	{
		if (_visuals.TryGetValue(id, out DesignerItem value) && _overflowVisuals.TryGetValue(id, out FrameworkElement value2))
		{
			double num = Canvas.GetLeft(value);
			if (double.IsNaN(num))
			{
				num = 0.0;
			}
			double num2 = Canvas.GetTop(value);
			if (double.IsNaN(num2))
			{
				num2 = 0.0;
			}
			Canvas.SetLeft(value2, num);
			Canvas.SetTop(value2, num2);
			value2.Width = (double.IsNaN(value.Width) ? value.ActualWidth : value.Width);
			value2.Height = (double.IsNaN(value.Height) ? value.ActualHeight : value.Height);
			value2.Opacity = value.Opacity;
			bool outside = num < 0.0 || num2 < 0.0 || num + value2.Width > OverflowCanvas.Width || num2 + value2.Height > OverflowCanvas.Height;
			value2.Visibility = (value.Visibility == Visibility.Visible && outside) ? Visibility.Visible : Visibility.Collapsed;
			value2.RenderTransformOrigin = value.RenderTransformOrigin;
			value2.RenderTransform = CreateElementTransform(value.Model);
			Panel.SetZIndex(value2, Panel.GetZIndex(value));
		}
	}

	private FrameworkElement BuildVisual(CanvasElementModel model, bool inverted)
	{
		return model.Kind switch
		{
			ElementKind.Text => BuildTextVisual(model, inverted), 
			ElementKind.Image => BuildImageVisual(model, qr: false, inverted), 
			ElementKind.QrCode => BuildImageVisual(model, qr: true, inverted), 
			ElementKind.Shape => BuildShapeVisual(model, inverted), 
			_ => new Border
			{
				Background = Brushes.LightGray
			}, 
		};
	}

	private static Transform CreateElementTransform(CanvasElementModel model)
	{
		return new TransformGroup
		{
			Children = 
			{
				(Transform)new SkewTransform(Math.Clamp(model.SkewX, -80.0, 80.0), Math.Clamp(model.SkewY, -80.0, 80.0)),
				(Transform)new RotateTransform(model.Rotation)
			}
		};
	}

	private FrameworkElement BuildTextVisual(CanvasElementModel model, bool inverted)
	{
		return new Border
		{
			Background = (inverted ? DisplayBrush(model.TextBackground, Brushes.Transparent, inverted: true) : TextureCatalogService.Blend(DisplayBrush(model.TextBackground, Brushes.Transparent, inverted: false), model.TextureDataBase64, model.TextureOpacity, model.TextureScale)),
			Padding = new Thickness(2.0),
			Child = new OutlinedTextVisual(model, ResolveFontFamily(model.FontFamily), DisplayBrush(model.TextColor, Brushes.Black, inverted), DisplayBrush(model.TextOutlineColor, Brushes.White, inverted), DisplayBrush(model.TextExtrusionColor, Brushes.DimGray, inverted)),
			ClipToBounds = false
		};
	}

	private static FrameworkElement BuildImageVisual(CanvasElementModel model, bool qr, bool inverted)
	{
		try
		{
			byte[] array = null;
			if (qr)
			{
				array = QrService.CreatePng(model.QrContent, model.QrErrorCorrection, model.QrForeground, model.QrBackground);
			}
			else if (!string.IsNullOrWhiteSpace(model.ImageDataBase64))
			{
				array = Convert.FromBase64String(model.ImageDataBase64);
			}
			else if (!string.IsNullOrWhiteSpace(model.ImageSourcePath) && File.Exists(model.ImageSourcePath))
			{
				array = File.ReadAllBytes(model.ImageSourcePath);
			}
			if (array != null)
			{
				BitmapSource bitmapSource = LoadBitmap(array);
				if (inverted)
				{
					bitmapSource = InvertBitmap(bitmapSource);
				}
				Image image = new Image
				{
					Source = bitmapSource,
					Stretch = ((!model.PreserveAspectRatio) ? Stretch.Fill : Stretch.Uniform)
				};
				RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
				if (!qr && model.ImageExtrusionDepthPt > 0.0)
				{
					Grid grid = new Grid
					{
						ClipToBounds = false
					};
					double num = Math.Clamp(model.ImageExtrusionDepthPt, 0.0, 30.0) * 96.0 / 72.0;
					double num2 = model.ImageExtrusionAngle * Math.PI / 180.0;
					int num3 = Math.Max(1, (int)Math.Ceiling(num * Math.Clamp(model.ImageExtrusionSmoothness, 0.25, 4.0)));
					for (int num4 = num3; num4 >= 1; num4--)
					{
						double num5 = num * (double)num4 / (double)num3;
						Border element = new Border
						{
							Background = BrushFrom(model.ImageExtrusionColor, Brushes.Black),
							OpacityMask = new ImageBrush(bitmapSource)
							{
								Stretch = ((!model.PreserveAspectRatio) ? Stretch.Fill : Stretch.Uniform)
							},
							RenderTransform = new TranslateTransform(Math.Cos(num2) * num5, Math.Sin(num2) * num5)
						};
						grid.Children.Add(element);
					}
					grid.Children.Add(image);
					return grid;
				}
				return image;
			}
		}
		catch
		{
		}
		return new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(238, 241, 246)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(190, 198, 210)),
			BorderThickness = new Thickness(1.0),
			Child = new TextBlock
			{
				Text = (qr ? "QRコード" : "画像を再リンク"),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Foreground = new SolidColorBrush(Color.FromRgb(110, 120, 137))
			}
		};
	}

	private static bool IsPanelElement(CanvasElementModel model)
	{
		if (!model.PanelEnabled)
		{
			return model.ShapeType == "Panel";
		}
		return true;
	}

	private static FrameworkElement BuildShapeVisual(CanvasElementModel model, bool inverted)
	{
		Brush brush = DisplayBrush(model.FillColor, Brushes.Transparent, inverted);
		Brush baseFill = (inverted ? brush : TextureCatalogService.Blend(brush, model.TextureDataBase64, model.TextureOpacity, model.TextureScale));
		Brush brush2 = DisplayBrush(model.StrokeColor, Brushes.Transparent, inverted);
		double thickness = model.StrokeThicknessPt * 96.0 / 72.0;
		double num = Math.Clamp(model.ShapeExtrusionDepthPt, 0.0, 24.0) * 96.0 / 72.0;
		if (num <= 0.1)
		{
			return CreateBase(baseFill, brush2);
		}
		Grid grid = new Grid
		{
			ClipToBounds = false
		};
		double num2 = model.ShapeExtrusionAngle * Math.PI / 180.0;
		int num3 = Math.Max(1, (int)Math.Ceiling(num));
		Brush brush3 = DisplayBrush(model.ShapeExtrusionColor, Brushes.DimGray, inverted);
		for (int num4 = num3; num4 >= 1; num4--)
		{
			double num5 = num * (double)num4 / (double)num3;
			FrameworkElement frameworkElement = CreateBase(brush3, brush3);
			frameworkElement.RenderTransform = new TranslateTransform(Math.Cos(num2) * num5, Math.Sin(num2) * num5);
			grid.Children.Add(frameworkElement);
		}
		grid.Children.Add(CreateBase(baseFill, brush2));
		return grid;
		FrameworkElement CreateBase(Brush brush4, Brush brush6)
		{
			if (IsPanelElement(model))
			{
				bool flag = brush4 == baseFill;
				int num6 = Math.Clamp(model.PanelRows, 1, 12) * Math.Clamp(model.PanelColumns, 1, 12);
				List<Brush> list = new List<Brush>();
				for (int i = 0; i < num6; i++)
				{
					string value = ((flag && i < model.PanelCellColors.Count) ? model.PanelCellColors[i] : model.FillColor);
					Brush brush5 = (flag ? DisplayBrush(value, brush4, inverted) : brush4);
					list.Add((flag && !inverted) ? TextureCatalogService.Blend(brush5, model.TextureDataBase64, model.TextureOpacity, model.TextureScale) : brush5);
				}
				Brush divider = DisplayBrush(model.PanelDividerColor, brush6, inverted);
				return new PanelVisual(model, brush4, brush6, list, divider, thickness);
			}
			if (model.ShapePoints.Count >= 2)
			{
				PointCollection points = new PointCollection(model.ShapePoints.Select((ShapePointModel x) => new Point(x.X, x.Y)));
				if (model.ShapeClosed)
				{
					return ShapeViewbox(new Polygon
					{
						Points = points,
						Fill = brush4,
						Stroke = brush6,
						StrokeThickness = Math.Max(1.0, thickness)
					});
				}
				return ShapeViewbox(new Polyline
				{
					Points = points,
					Fill = Brushes.Transparent,
					Stroke = brush6,
					StrokeThickness = Math.Max(1.0, thickness),
					StrokeLineJoin = PenLineJoin.Round,
					StrokeStartLineCap = PenLineCap.Round,
					StrokeEndLineCap = PenLineCap.Round
				});
			}
			double num7 = model.CornerRadiusMm * 3.7795275590551185;
			CornerRadius cornerRadius = new CornerRadius((model.CornerRadiusTopLeftMm >= 0.0) ? (model.CornerRadiusTopLeftMm * 3.7795275590551185) : num7, (model.CornerRadiusTopRightMm >= 0.0) ? (model.CornerRadiusTopRightMm * 3.7795275590551185) : num7, (model.CornerRadiusBottomRightMm >= 0.0) ? (model.CornerRadiusBottomRightMm * 3.7795275590551185) : num7, (model.CornerRadiusBottomLeftMm >= 0.0) ? (model.CornerRadiusBottomLeftMm * 3.7795275590551185) : num7);
			if (model.ShapeType == "RoundedRectangle")
			{
				return new Border
				{
					Background = brush4,
					BorderBrush = brush6,
					BorderThickness = new Thickness(thickness),
					CornerRadius = cornerRadius
				};
			}
			string shapeType = model.ShapeType;
			if ((shapeType == "Ellipse" || shapeType == "Circle") ? true : false)
			{
				return new Ellipse
				{
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness,
					Stretch = Stretch.Fill
				};
			}
			if (model.ShapeType == "Triangle")
			{
				return ShapeViewbox(new Polygon
				{
					Points = new PointCollection
					{
						new Point(50.0, 0.0),
						new Point(100.0, 100.0),
						new Point(0.0, 100.0)
					},
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Star")
			{
				PointCollection pointCollection = new PointCollection();
				for (int num8 = 0; num8 < 10; num8++)
				{
					double num9 = -Math.PI / 2.0 + (double)num8 * Math.PI / 5.0;
					int num10 = ((num8 % 2 == 0) ? 50 : 22);
					pointCollection.Add(new Point(50.0 + Math.Cos(num9) * (double)num10, 50.0 + Math.Sin(num9) * (double)num10));
				}
				return ShapeViewbox(new Polygon
				{
					Points = pointCollection,
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "SemiCircle")
			{
				PathFigure pathFigure = new PathFigure
				{
					StartPoint = new Point(0.0, 100.0),
					IsClosed = true,
					IsFilled = true
				};
				pathFigure.Segments.Add(new ArcSegment(new Point(100.0, 100.0), new Size(50.0, 50.0), 0.0, isLargeArc: false, SweepDirection.Clockwise, isStroked: true));
				pathFigure.Segments.Add(new LineSegment(new Point(0.0, 100.0), isStroked: true));
				return ShapeViewbox(new System.Windows.Shapes.Path
				{
					Data = new PathGeometry(new PathFigure[1] { pathFigure }),
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Heart")
			{
				PathFigure pathFigure2 = new PathFigure
				{
					StartPoint = new Point(50.0, 92.0),
					IsClosed = true,
					IsFilled = true
				};
				pathFigure2.Segments.Add(new BezierSegment(new Point(42.0, 80.0), new Point(5.0, 58.0), new Point(8.0, 30.0), isStroked: true));
				pathFigure2.Segments.Add(new BezierSegment(new Point(10.0, 8.0), new Point(38.0, 3.0), new Point(50.0, 24.0), isStroked: true));
				pathFigure2.Segments.Add(new BezierSegment(new Point(62.0, 3.0), new Point(90.0, 8.0), new Point(92.0, 30.0), isStroked: true));
				pathFigure2.Segments.Add(new BezierSegment(new Point(95.0, 58.0), new Point(58.0, 80.0), new Point(50.0, 92.0), isStroked: true));
				return ShapeViewbox(new System.Windows.Shapes.Path
				{
					Data = new PathGeometry(new PathFigure[1] { pathFigure2 }),
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Ring")
			{
				GeometryGroup geometryGroup = new GeometryGroup
				{
					FillRule = FillRule.EvenOdd
				};
				geometryGroup.Children.Add(new EllipseGeometry(new Rect(0.0, 0.0, 100.0, 100.0)));
				geometryGroup.Children.Add(new EllipseGeometry(new Rect(25.0, 25.0, 50.0, 50.0)));
				return ShapeViewbox(new System.Windows.Shapes.Path
				{
					Data = geometryGroup,
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Diamond")
			{
				return ShapeViewbox(new Polygon
				{
					Points = new PointCollection
					{
						new Point(50.0, 0.0),
						new Point(100.0, 50.0),
						new Point(50.0, 100.0),
						new Point(0.0, 50.0)
					},
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Badge")
			{
				PointCollection pointCollection2 = new PointCollection();
				for (int num11 = 0; num11 < 24; num11++)
				{
					double num12 = -Math.PI / 2.0 + (double)num11 * Math.PI / 12.0;
					double num13 = ((num11 % 2 == 0) ? 49 : 41);
					pointCollection2.Add(new Point(50.0 + Math.Cos(num12) * num13, 50.0 + Math.Sin(num12) * num13));
				}
				return ShapeViewbox(new Polygon
				{
					Points = pointCollection2,
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "SpeechBubble")
			{
				return ShapeViewbox(new Polygon
				{
					Points = new PointCollection
					{
						new Point(5.0, 5.0),
						new Point(95.0, 5.0),
						new Point(95.0, 75.0),
						new Point(62.0, 75.0),
						new Point(48.0, 96.0),
						new Point(48.0, 75.0),
						new Point(5.0, 75.0)
					},
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Label")
			{
				return ShapeViewbox(new Polygon
				{
					Points = new PointCollection
					{
						new Point(0.0, 8.0),
						new Point(82.0, 8.0),
						new Point(100.0, 50.0),
						new Point(82.0, 92.0),
						new Point(0.0, 92.0),
						new Point(12.0, 50.0)
					},
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Polygon")
			{
				return ShapeViewbox(new Polygon
				{
					Points = new PointCollection
					{
						new Point(50.0, 0.0),
						new Point(97.0, 35.0),
						new Point(80.0, 95.0),
						new Point(20.0, 95.0),
						new Point(3.0, 35.0)
					},
					Fill = brush4,
					Stroke = brush6,
					StrokeThickness = thickness
				});
			}
			if (model.ShapeType == "Line")
			{
				return new LineShapeVisual(model, brush6, thickness);
			}
			return new Rectangle
			{
				Fill = brush4,
				Stroke = brush6,
				StrokeThickness = thickness,
				Stretch = Stretch.Fill
			};
		}
	}

	private static Viewbox ShapeViewbox(FrameworkElement shape)
	{
		return new Viewbox
		{
			Stretch = Stretch.Fill,
			Child = shape
		};
	}

	private static BitmapImage LoadBitmap(byte[] bytes)
	{
		using MemoryStream streamSource = new MemoryStream(bytes);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
		bitmapImage.StreamSource = streamSource;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static BitmapSource StoreImageForEditing(CanvasElementModel model, byte[] sourceBytes, BitmapSource bitmap, string sourcePath)
	{
		bool flag = sourceBytes.LongLength >= 31457280;
		BitmapSource bitmapSource = bitmap;
		byte[] inArray = sourceBytes;
		if (!flag && string.Equals(System.IO.Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase) && TryTrimTransparentEdges(bitmap, model.ImageTransparentTrimThreshold, model.ImageTransparentTrimPaddingPixels, out BitmapSource result))
		{
			model.ImagePreTrimDataBase64 = Convert.ToBase64String(sourceBytes);
			model.ImageTransparentTrimApplied = true;
			bitmapSource = result;
			inArray = EncodeBitmap(result, "PNG", 100);
		}
		else if (flag)
		{
			inArray = CreateLargeImagePreview(bitmap);
		}
		model.ImageDataBase64 = Convert.ToBase64String(inArray);
		model.ImageSourcePath = sourcePath;
		model.ImageUsesLinkedOriginal = flag;
		model.ImageSourceBytes = sourceBytes.LongLength;
		model.ImagePixelWidth = bitmapSource.PixelWidth;
		model.ImagePixelHeight = bitmapSource.PixelHeight;
		return bitmapSource;
	}

	private static bool TryTrimTransparentEdges(BitmapSource source, byte alphaThreshold, int paddingPixels, out BitmapSource result)
	{
		result = source;
		if (source.PixelWidth < 2 || source.PixelHeight < 2 || (long)source.PixelWidth * (long)source.PixelHeight > 60000000)
		{
			return false;
		}
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
		int num = formatConvertedBitmap.PixelWidth * 4;
		byte[] array = new byte[num * formatConvertedBitmap.PixelHeight];
		formatConvertedBitmap.CopyPixels(array, num, 0);
		int num2 = formatConvertedBitmap.PixelWidth;
		int num3 = formatConvertedBitmap.PixelHeight;
		int num4 = -1;
		int num5 = -1;
		for (int i = 0; i < formatConvertedBitmap.PixelHeight; i++)
		{
			for (int j = 0; j < formatConvertedBitmap.PixelWidth; j++)
			{
				if (array[i * num + j * 4 + 3] > alphaThreshold)
				{
					num2 = Math.Min(num2, j);
					num4 = Math.Max(num4, j);
					num3 = Math.Min(num3, i);
					num5 = Math.Max(num5, i);
				}
			}
		}
		if (num4 < num2 || num5 < num3 || (num2 == 0 && num3 == 0 && num4 == formatConvertedBitmap.PixelWidth - 1 && num5 == formatConvertedBitmap.PixelHeight - 1))
		{
			return false;
		}
		int num6 = Math.Clamp(paddingPixels, 0, 500);
		num2 = Math.Max(0, num2 - num6);
		num3 = Math.Max(0, num3 - num6);
		num4 = Math.Min(formatConvertedBitmap.PixelWidth - 1, num4 + num6);
		num5 = Math.Min(formatConvertedBitmap.PixelHeight - 1, num5 + num6);
		CroppedBitmap croppedBitmap = new CroppedBitmap(source, new Int32Rect(num2, num3, num4 - num2 + 1, num5 - num3 + 1));
		croppedBitmap.Freeze();
		result = croppedBitmap;
		return true;
	}

	private static byte[] CreateLargeImagePreview(BitmapSource source)
	{
		BitmapSource source2 = source;
		int num = Math.Max(source.PixelWidth, source.PixelHeight);
		if (num > 2048)
		{
			double num2 = 2048.0 / (double)num;
			TransformedBitmap transformedBitmap = new TransformedBitmap(source, new ScaleTransform(num2, num2));
			transformedBitmap.Freeze();
			source2 = transformedBitmap;
		}
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source2));
		using MemoryStream memoryStream = new MemoryStream();
		pngBitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}

	private static BitmapSource InvertBitmap(BitmapSource source)
	{
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
		int num = formatConvertedBitmap.PixelWidth * 4;
		byte[] array = new byte[num * formatConvertedBitmap.PixelHeight];
		formatConvertedBitmap.CopyPixels(array, num, 0);
		for (int i = 0; i < array.Length; i += 4)
		{
			array[i] = (byte)(255 - array[i]);
			array[i + 1] = (byte)(255 - array[i + 1]);
			array[i + 2] = (byte)(255 - array[i + 2]);
		}
		BitmapSource bitmapSource = BitmapSource.Create(formatConvertedBitmap.PixelWidth, formatConvertedBitmap.PixelHeight, formatConvertedBitmap.DpiX, formatConvertedBitmap.DpiY, PixelFormats.Bgra32, null, array, num);
		bitmapSource.Freeze();
		return bitmapSource;
	}

	private static Brush DisplayBrush(string value, Brush fallback, bool inverted)
	{
		Brush brush = BrushFrom(value, fallback);
		if (inverted && brush is SolidColorBrush { Color: var color })
		{
			SolidColorBrush solidColorBrush2 = new SolidColorBrush(Color.FromArgb(color.A, (byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B)));
			solidColorBrush2.Freeze();
			return solidColorBrush2;
		}
		return brush;
	}

	private Point SnapPosition(DesignerItem moving, Point position)
	{
		if (_snapMovingId != moving.Model.Id)
		{
			ResetSnapLatch(moving.Model.Id);
		}
		PageModel currentPage = CurrentPage;
		double num = Math.Max(1.0, _settings.Current.SnapStartPixels) / Math.Max(0.25, _zoom);
		double releaseThreshold = Math.Max(num + 1.0, _settings.Current.SnapReleasePixels / Math.Max(0.25, _zoom));
		double size = Math.Max(1.0, moving.ActualWidth);
		double size2 = Math.Max(1.0, moving.ActualHeight);
		List<SnapCandidate> list = new List<SnapCandidate>();
		List<SnapCandidate> list2 = new List<SnapCandidate>();
		bool flag = _settings.Current.SnapToGrid && string.Equals(_settings.Current.SnapPriorityMode, "グリッド優先", StringComparison.Ordinal);
		if (_settings.Current.SnapToPageEdges)
		{
			AddAxisCandidates(list, position.X, size, new double[2] { 0.0, PageCanvas.ActualWidth }, num, "台紙端");
			AddAxisCandidates(list2, position.Y, size2, new double[2] { 0.0, PageCanvas.ActualHeight }, num, "台紙端");
		}
		if (_settings.Current.SnapToVerticalCenterGuide)
		{
			AddAxisCandidates(list, position.X, size, new double[1] { PageCanvas.ActualWidth / 2.0 }, num, "縦の正中線");
		}
		if (_settings.Current.SnapToHorizontalCenterGuide)
		{
			AddAxisCandidates(list2, position.Y, size2, new double[1] { PageCanvas.ActualHeight / 2.0 }, num, "横の正中線");
		}
		if (_settings.Current.SnapToSafeArea)
		{
			double num2 = currentPage.SafeMarginMm * 3.7795275590551185;
			AddAxisCandidates(list, position.X, size, new double[2]
			{
				num2,
				PageCanvas.ActualWidth - num2
			}, num, "安全領域");
			AddAxisCandidates(list2, position.Y, size2, new double[2]
			{
				num2,
				PageCanvas.ActualHeight - num2
			}, num, "安全領域");
		}
		if (_settings.Current.SnapToObjects)
		{
			foreach (KeyValuePair<Guid, DesignerItem> visual in _visuals)
			{
				if (!(visual.Key == moving.Model.Id) && visual.Value.Model.IsVisible && !visual.Value.Model.IsLocked)
				{
					double num3 = Canvas.GetLeft(visual.Value);
					double num4 = Canvas.GetTop(visual.Value);
					if (double.IsNaN(num3))
					{
						num3 = 0.0;
					}
					if (double.IsNaN(num4))
					{
						num4 = 0.0;
					}
					AddAxisCandidates(list, position.X, size, new double[3]
					{
						num3,
						num3 + visual.Value.ActualWidth / 2.0,
						num3 + visual.Value.ActualWidth
					}, num, visual.Value.Model.Name);
					AddAxisCandidates(list2, position.Y, size2, new double[3]
					{
						num4,
						num4 + visual.Value.ActualHeight / 2.0,
						num4 + visual.Value.ActualHeight
					}, num, visual.Value.Model.Name);
				}
			}
		}
		if (_settings.Current.SnapToGrid && _settings.Current.GridSizeMm > 0.0)
		{
			double num5 = _settings.Current.GridSizeMm * 3.7795275590551185;
			double num6 = Math.Round(position.X / num5) * num5;
			double num7 = Math.Round(position.Y / num5) * num5;
			if (flag || Math.Abs(num6 - position.X) <= num)
			{
				list.Add(new SnapCandidate(num6 - position.X, num6, "グリッド"));
			}
			if (flag || Math.Abs(num7 - position.Y) <= num)
			{
				list2.Add(new SnapCandidate(num7 - position.Y, num7, "グリッド"));
			}
		}
		double num8 = ResolveSnapAxis(position.X, list, releaseThreshold, ref _snapLatchX, ref _snapLatchXGuide, ref _snapLatchXLabel);
		double num9 = ResolveSnapAxis(position.Y, list2, releaseThreshold, ref _snapLatchY, ref _snapLatchYGuide, ref _snapLatchYLabel);
		bool hasValue = _snapLatchX.HasValue;
		bool hasValue2 = _snapLatchY.HasValue;
		string label = string.Join(" / ", new string[2]
		{
			hasValue ? _snapLatchXLabel : null,
			hasValue2 ? _snapLatchYLabel : null
		}.Where((string value) => !string.IsNullOrWhiteSpace(value)).Distinct());
		ShowSnapGuides(hasValue ? _snapLatchXGuide : num8, hasValue2 ? _snapLatchYGuide : num9, hasValue, hasValue2, label);
		return new Point(num8, num9);
		static void AddAxisCandidates(List<SnapCandidate> candidates, double start, double num10, IEnumerable<double> targets, double threshold, string label2)
		{
			double[] array = new double[3]
			{
				start,
				start + num10 / 2.0,
				start + num10
			};
			foreach (double num11 in array)
			{
				foreach (double target in targets)
				{
					double num12 = target - num11;
					if (Math.Abs(num12) <= threshold)
					{
						candidates.Add(new SnapCandidate(num12, target, label2));
					}
				}
			}
		}
		static double ResolveSnapAxis(double raw, List<SnapCandidate> candidates, double num10, ref double? latch, ref double latchGuide, ref string latchLabel)
		{
			if (latch.HasValue)
			{
				if (Math.Abs(raw - latch.Value) <= num10)
				{
					return latch.Value;
				}
				latch = null;
				latchLabel = string.Empty;
			}
			SnapCandidate snapCandidate = (from candidate in candidates
				orderby (candidate.Label == "グリッド") ? 1 : 0, Math.Abs(candidate.Offset)
				select candidate).FirstOrDefault();
			if (snapCandidate == null)
			{
				return raw;
			}
			latch = raw + snapCandidate.Offset;
			latchGuide = snapCandidate.Guide;
			latchLabel = snapCandidate.Label;
			return latch.Value;
		}
	}

	private void ShowSnapGuides(double x, double y, bool vertical, bool horizontal, string label)
	{
		ClearSnapGuides();
		SolidColorBrush stroke = new SolidColorBrush(Color.FromRgb(43, 182, 200));
		if (vertical)
		{
			_snapGuideVertical = new Line
			{
				X1 = x,
				X2 = x,
				Y1 = 0.0,
				Y2 = PageCanvas.Height,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 },
				IsHitTestVisible = false
			};
			GuideOverlayCanvas.Children.Add(_snapGuideVertical);
		}
		if (horizontal)
		{
			_snapGuideHorizontal = new Line
			{
				X1 = 0.0,
				X2 = PageCanvas.Width,
				Y1 = y,
				Y2 = y,
				Stroke = stroke,
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 2.0 },
				IsHitTestVisible = false
			};
			GuideOverlayCanvas.Children.Add(_snapGuideHorizontal);
		}
		if ((vertical || horizontal) && !string.IsNullOrWhiteSpace(label))
		{
			_snapGuideLabel = new TextBlock
			{
				Text = label,
				Foreground = Brushes.White,
				Background = new SolidColorBrush(Color.FromArgb(220, 24, 133, 152)),
				Padding = new Thickness(5.0, 2.0, 5.0, 2.0),
				FontSize = 10.0,
				IsHitTestVisible = false
			};
			Canvas.SetLeft(_snapGuideLabel, Math.Clamp(x + 6.0, 0.0, Math.Max(0.0, PageCanvas.Width - 100.0)));
			Canvas.SetTop(_snapGuideLabel, Math.Clamp(y + 6.0, 0.0, Math.Max(0.0, PageCanvas.Height - 24.0)));
			Panel.SetZIndex(_snapGuideLabel, int.MaxValue);
			GuideOverlayCanvas.Children.Add(_snapGuideLabel);
		}
	}

	private void ClearSnapGuides()
	{
		if (_snapGuideVertical != null)
		{
			GuideOverlayCanvas.Children.Remove(_snapGuideVertical);
		}
		if (_snapGuideHorizontal != null)
		{
			GuideOverlayCanvas.Children.Remove(_snapGuideHorizontal);
		}
		if (_snapGuideLabel != null)
		{
			GuideOverlayCanvas.Children.Remove(_snapGuideLabel);
		}
		_snapGuideVertical = null;
		_snapGuideHorizontal = null;
		_snapGuideLabel = null;
	}

	private void ResetSnapLatch(Guid? movingId = null)
	{
		_snapMovingId = movingId;
		_snapLatchX = null;
		_snapLatchY = null;
		_snapLatchXLabel = string.Empty;
		_snapLatchYLabel = string.Empty;
		ClearSnapGuides();
	}

	private static Brush BrushFrom(string value, Brush fallback)
	{
		try
		{
			SolidColorBrush obj = (SolidColorBrush)new BrushConverter().ConvertFromString(value);
			obj.Freeze();
			return obj;
		}
		catch
		{
			return fallback;
		}
	}

	private void DesignerItem_SelectionRequested(object? sender, DesignerItemSelectionEventArgs e)
	{
		if (sender is DesignerItem designerItem)
		{
			if (!e.Additive && (_selectedIds.Count <= 1 || !_selectedIds.Contains(designerItem.Model.Id)))
			{
				_selectedIds.Clear();
			}
			if (e.Additive && _selectedIds.Contains(designerItem.Model.Id))
			{
				_selectedIds.Remove(designerItem.Model.Id);
			}
			else
			{
				_selectedIds.Add(designerItem.Model.Id);
			}
			UpdateSelectionVisuals();
		}
	}

	private void DesignerItem_MoveStarted(object? sender, EventArgs e)
	{
		_groupMoveOrigins.Clear();
		DesignerItem leader = sender as DesignerItem;
		if (leader != null)
		{
			ResetSnapLatch(leader.Model.Id);
		}
		if (leader == null || _selectedIds.Count < 2 || !_selectedIds.Contains(leader.Model.Id))
		{
			return;
		}
		foreach (KeyValuePair<Guid, DesignerItem> item in _visuals.Where((KeyValuePair<Guid, DesignerItem> x) => _selectedIds.Contains(x.Key) && x.Key != leader.Model.Id))
		{
			double num = Canvas.GetLeft(item.Value);
			if (double.IsNaN(num))
			{
				num = 0.0;
			}
			double num2 = Canvas.GetTop(item.Value);
			if (double.IsNaN(num2))
			{
				num2 = 0.0;
			}
			_groupMoveOrigins[item.Key] = new Point(num, num2);
		}
	}

	private void DesignerItem_MovePreview(object? sender, DesignerItemMoveEventArgs e)
	{
		foreach (KeyValuePair<Guid, Point> groupMoveOrigin in _groupMoveOrigins)
		{
			if (_visuals.TryGetValue(groupMoveOrigin.Key, out DesignerItem value))
			{
				Canvas.SetLeft(value, groupMoveOrigin.Value.X + e.DeltaX);
				Canvas.SetTop(value, groupMoveOrigin.Value.Y + e.DeltaY);
				SyncOverflowVisual(groupMoveOrigin.Key);
			}
		}
		AutoScrollDuringObjectDrag();
	}

	private void DesignerItem_MoveFinished(object? sender, EventArgs e)
	{
		ResetSnapLatch();
		foreach (Guid id in _groupMoveOrigins.Keys)
		{
			if (_visuals.TryGetValue(id, out DesignerItem value))
			{
				CanvasElementModel canvasElementModel = CurrentPage.Elements.FirstOrDefault((CanvasElementModel x) => x.Id == id);
				if (canvasElementModel != null)
				{
					canvasElementModel.Xmm = Canvas.GetLeft(value) / 3.7795275590551185;
					canvasElementModel.Ymm = Canvas.GetTop(value) / 3.7795275590551185;
				}
			}
		}
		_groupMoveOrigins.Clear();
	}

	private void DesignerItem_InteractionCanceled(object? sender, EventArgs e)
	{
		foreach (KeyValuePair<Guid, Point> groupMoveOrigin in _groupMoveOrigins)
		{
			if (_visuals.TryGetValue(groupMoveOrigin.Key, out DesignerItem value))
			{
				Canvas.SetLeft(value, groupMoveOrigin.Value.X);
				Canvas.SetTop(value, groupMoveOrigin.Value.Y);
				SyncOverflowVisual(groupMoveOrigin.Key);
			}
		}
		_groupMoveOrigins.Clear();
		ResetSnapLatch();
		StatusText.Text = "操作をキャンセルしました";
	}

	private void AutoScrollDuringObjectDrag()
	{
		Point position = Mouse.GetPosition(CanvasScroll);
		double viewportWidth = CanvasScroll.ViewportWidth;
		double viewportHeight = CanvasScroll.ViewportHeight;
		if (!(viewportWidth <= 0.0) && !(viewportHeight <= 0.0))
		{
			double num = EdgeSpeed(position.X, viewportWidth, 40.0);
			double num2 = EdgeSpeed(position.Y, viewportHeight, 40.0);
			if (Math.Abs(num) > 0.01)
			{
				CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(CanvasScroll.HorizontalOffset + num, 0.0, CanvasScroll.ScrollableWidth));
			}
			if (Math.Abs(num2) > 0.01)
			{
				CanvasScroll.ScrollToVerticalOffset(Math.Clamp(CanvasScroll.VerticalOffset + num2, 0.0, CanvasScroll.ScrollableHeight));
			}
		}
		static double EdgeSpeed(double num3, double length, double threshold)
		{
			if (num3 >= 0.0 && num3 < threshold)
			{
				return 0.0 - Math.Min(10.0, 1.5 + (threshold - num3) / threshold * 8.5);
			}
			if (num3 <= length && num3 > length - threshold)
			{
				return Math.Min(10.0, 1.5 + (num3 - (length - threshold)) / threshold * 8.5);
			}
			return 0.0;
		}
	}

	private void UpdateSelectionVisuals()
	{
		foreach (KeyValuePair<Guid, DesignerItem> visual in _visuals)
		{
			visual.Value.IsSelected = _selectedIds.Contains(visual.Key);
		}
		SelectionMiniToolbar.Visibility = ((_selectedIds.Count <= 0) ? Visibility.Collapsed : Visibility.Visible);
		_updatingProperties = true;
		try
		{
			CanvasElementModel activeElement = ActiveElement;
			LayerList.SelectedItem = activeElement;
		}
		finally
		{
			_updatingProperties = false;
		}
		UpdatePropertyPanel();
		StatusText.Text = ((_selectedIds.Count == 0) ? "要素を選択してください" : $"{_selectedIds.Count}個の要素を選択中");
	}

	private void QuickColor_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null)
		{
			return;
		}
		string text = ShowProjectColor(activeElement.Kind switch
		{
			ElementKind.Text => activeElement.TextColor, 
			ElementKind.Shape => activeElement.FillColor, 
			ElementKind.QrCode => activeElement.QrForeground, 
			_ => CurrentPage.Background, 
		});
		if (text == null)
		{
			return;
		}
		PushUndo();
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)))
		{
			if (item.Kind == ElementKind.Text)
			{
				item.TextColor = text;
			}
			else if (item.Kind == ElementKind.Shape)
			{
				item.FillColor = text;
			}
			else if (item.Kind == ElementKind.QrCode)
			{
				item.QrForeground = text;
			}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private string? ShowProjectColor(string initial)
	{
		IEnumerable<string> designColors = CurrentPage.Elements.SelectMany((CanvasElementModel element) => new string[11]
		{
			element.TextColor, element.TextBackground, element.TextOutlineColor, element.TextExtrusionColor, element.FillColor, element.StrokeColor, element.ShapeExtrusionColor, element.PanelDividerColor, element.ImageExtrusionColor, element.QrForeground,
			element.QrBackground
		}).Append(CurrentPage.Background).Where(IsColor)
			.Distinct<string>(StringComparer.OrdinalIgnoreCase);
		ColorPickerDialog.SetContext((!_project.BrandName.Contains("JBL", StringComparison.OrdinalIgnoreCase)) ? ((IEnumerable<string>?)Array.Empty<string>()) : ((IEnumerable<string>?)new string[5] { "#FFFF3300", "#FF000000", "#FFFFFFFF", "#FF202733", "#FF00A5B5" }), designColors);
		return ColorPickerDialog.Show(this, initial);
	}

	private void QuickLock_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null)
		{
			return;
		}
		PushUndo();
		bool isLocked = !activeElement.IsLocked;
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)))
		{
			item.IsLocked = isLocked;
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private void PageCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource == PageCanvas && !Keyboard.IsKeyDown(Key.Space))
		{
			if (_freehandMode)
			{
				_freehandDrawing = true;
				_freehandPoints.Clear();
				Point point = SnapFreehandPoint(e.GetPosition(PageCanvas));
				_freehandPoints.Add(point);
				_freehandPreview = new Polyline
				{
					Stroke = new SolidColorBrush(Color.FromRgb(242, 106, 33)),
					StrokeThickness = 2.0,
					StrokeLineJoin = PenLineJoin.Round,
					IsHitTestVisible = false
				};
				_freehandPreview.Points.Add(point);
				Panel.SetZIndex(_freehandPreview, int.MaxValue);
				PageCanvas.Children.Add(_freehandPreview);
				PageCanvas.CaptureMouse();
				e.Handled = true;
			}
			else
			{
				BeginMarquee(e.GetPosition(PageCanvas));
				e.Handled = true;
			}
		}
	}

	private void PageCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (_eyedropperMode)
		{
			ApplyEyedropper(e.GetPosition(PageCanvas));
			e.Handled = true;
		}
		else if (!_freehandMode && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && !Keyboard.IsKeyDown(Key.Space) && FindVisualAncestor<DesignerItem>(e.OriginalSource as DependencyObject) != null)
		{
			Point position = e.GetPosition(PageCanvas);
			IReadOnlyList<DesignerItem> elementsAt = GetElementsAt(position);
			if (elementsAt.Count > 0)
			{
				int num = elementsAt.ToList().FindIndex((DesignerItem item) => _selectedIds.Contains(item.Model.Id));
				int index = ((num >= 0) ? ((num + 1) % elementsAt.Count) : 0);
				SelectOnly(elementsAt[index].Model.Id);
				UpdateSelectionVisuals();
				StatusText.Text = ((elementsAt.Count > 1) ? ("重なりから選択：" + elementsAt[index].Model.Name + "（Altのままドラッグで移動）") : (elementsAt[index].Model.Name + "を選択しました"));
				if (!elementsAt[index].Model.IsLocked)
				{
					_overlapDragArmed = true;
					_overlapDragging = false;
					_overlapDragStart = position;
					_overlapDragLeaderId = elementsAt[index].Model.Id;
					_overlapDragOrigins.Clear();
					foreach (Guid selectedId in _selectedIds)
					{
						if (_visuals.TryGetValue(selectedId, out DesignerItem value))
						{
							double left = Canvas.GetLeft(value);
							double top = Canvas.GetTop(value);
							_overlapDragOrigins[selectedId] = new Point(double.IsNaN(left) ? 0.0 : left, double.IsNaN(top) ? 0.0 : top);
						}
					}
					PageCanvas.CaptureMouse();
				}
			}
			e.Handled = true;
		}
		else if (!_freehandMode && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && !Keyboard.IsKeyDown(Key.Space))
		{
			BeginMarquee(e.GetPosition(PageCanvas));
			e.Handled = true;
		}
	}

	private IReadOnlyList<DesignerItem> GetElementsAt(Point canvasPoint)
	{
		List<DesignerItem> list = new List<DesignerItem>();
		foreach (DesignerItem item in _visuals.Values.Where((DesignerItem item) => item.Model.IsVisible && item.Visibility == Visibility.Visible))
		{
			try
			{
				GeneralTransform inverse = item.TransformToAncestor(PageCanvas).Inverse;
				if (inverse != null)
				{
					Point point = inverse.Transform(canvasPoint);
					if (new Rect(0.0, 0.0, Math.Max(1.0, item.ActualWidth), Math.Max(1.0, item.ActualHeight)).Contains(point))
					{
						list.Add(item);
					}
				}
			}
			catch
			{
			}
		}
		return list.OrderByDescending((DesignerItem item) => item.Model.ZIndex).ToList();
	}

	private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
	{
		while (source != null)
		{
			if (source is T result)
			{
				return result;
			}
			try
			{
				source = VisualTreeHelper.GetParent(source);
			}
			catch
			{
				return null;
			}
		}
		return null;
	}

	private void BeginMarquee(Point start)
	{
		if (!_marqueeSelecting)
		{
			bool flag = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
			_marqueeBaseSelection = (flag ? new HashSet<Guid>(_selectedIds) : new HashSet<Guid>());
			if (!flag)
			{
				_selectedIds.Clear();
			}
			_marqueeStart = start;
			_marqueeRectangle = new Rectangle
			{
				Stroke = new SolidColorBrush(Color.FromRgb(43, 182, 200)),
				StrokeThickness = 1.0,
				StrokeDashArray = new DoubleCollection { 4.0, 3.0 },
				Fill = new SolidColorBrush(Color.FromArgb(35, 43, 182, 200)),
				IsHitTestVisible = false
			};
			Panel.SetZIndex(_marqueeRectangle, int.MaxValue);
			Canvas.SetLeft(_marqueeRectangle, _marqueeStart.X);
			Canvas.SetTop(_marqueeRectangle, _marqueeStart.Y);
			PageCanvas.Children.Add(_marqueeRectangle);
			_marqueeSelecting = true;
			PageCanvas.CaptureMouse();
			UpdateSelectionVisuals();
		}
	}

	private void PageCanvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (_overlapDragArmed && e.LeftButton == MouseButtonState.Pressed && _overlapDragLeaderId.HasValue && _visuals.TryGetValue(_overlapDragLeaderId.Value, out DesignerItem value) && _overlapDragOrigins.TryGetValue(_overlapDragLeaderId.Value, out var value2))
		{
			Vector vector = e.GetPosition(PageCanvas) - _overlapDragStart;
			if (!_overlapDragging && Math.Abs(vector.X) < 4.0 && Math.Abs(vector.Y) < 4.0)
			{
				e.Handled = true;
				return;
			}
			if (!_overlapDragging)
			{
				_overlapDragging = true;
				PushUndo();
				BeginLightweightPreview();
				ResetSnapLatch(value.Model.Id);
			}
			Point point = new Point(value2.X + vector.X, value2.Y + vector.Y);
			if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
			{
				point = SnapPosition(value, point);
			}
			Vector vector2 = point - value2;
			foreach (KeyValuePair<Guid, Point> overlapDragOrigin in _overlapDragOrigins)
			{
				if (_visuals.TryGetValue(overlapDragOrigin.Key, out DesignerItem value3))
				{
					Canvas.SetLeft(value3, overlapDragOrigin.Value.X + vector2.X);
					Canvas.SetTop(value3, overlapDragOrigin.Value.Y + vector2.Y);
					SyncOverflowVisual(overlapDragOrigin.Key);
				}
			}
			AutoScrollDuringObjectDrag();
			e.Handled = true;
			return;
		}
		if (_freehandDrawing && _freehandPreview != null && e.LeftButton == MouseButtonState.Pressed)
		{
			Point point2 = SnapFreehandPoint(e.GetPosition(PageCanvas));
			if (_freehandPoints.Count != 0)
			{
				List<Point> freehandPoints = _freehandPoints;
				if (!((point2 - freehandPoints[freehandPoints.Count - 1]).Length >= 1.5))
				{
					goto IL_0456;
				}
			}
			_freehandPoints.Add(point2);
			_freehandPreview.Points.Add(point2);
			goto IL_0456;
		}
		if (!_marqueeSelecting || _marqueeRectangle == null || e.LeftButton != MouseButtonState.Pressed)
		{
			return;
		}
		Point position = e.GetPosition(PageCanvas);
		Rect rect = new Rect(_marqueeStart, position);
		Canvas.SetLeft(_marqueeRectangle, rect.Left);
		Canvas.SetTop(_marqueeRectangle, rect.Top);
		_marqueeRectangle.Width = rect.Width;
		_marqueeRectangle.Height = rect.Height;
		HashSet<Guid> hashSet = new HashSet<Guid>(_marqueeBaseSelection);
		if (rect.Width >= 2.0 || rect.Height >= 2.0)
		{
			foreach (KeyValuePair<Guid, DesignerItem> visual in _visuals)
			{
				if (visual.Value.Model.IsVisible)
				{
					double num = Canvas.GetLeft(visual.Value);
					if (double.IsNaN(num))
					{
						num = 0.0;
					}
					double num2 = Canvas.GetTop(visual.Value);
					if (double.IsNaN(num2))
					{
						num2 = 0.0;
					}
					Rect rect2 = new Rect(num, num2, visual.Value.ActualWidth, visual.Value.ActualHeight);
					if (rect.IntersectsWith(rect2))
					{
						hashSet.Add(visual.Key);
					}
				}
			}
		}
		if (!_selectedIds.SetEquals(hashSet))
		{
			_selectedIds.Clear();
			foreach (Guid item in hashSet)
			{
				_selectedIds.Add(item);
			}
			UpdateSelectionVisuals();
		}
		e.Handled = true;
		return;
		IL_0456:
		e.Handled = true;
	}

	private void PageCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_overlapDragArmed)
		{
			PageCanvas.ReleaseMouseCapture();
			if (_overlapDragging)
			{
				foreach (Guid id in _overlapDragOrigins.Keys)
				{
					if (_visuals.TryGetValue(id, out DesignerItem value))
					{
						CanvasElementModel canvasElementModel = CurrentPage.Elements.FirstOrDefault((CanvasElementModel element) => element.Id == id);
						if (canvasElementModel != null)
						{
							canvasElementModel.Xmm = Canvas.GetLeft(value) / 3.7795275590551185;
							canvasElementModel.Ymm = Canvas.GetTop(value) / 3.7795275590551185;
						}
					}
				}
				EndLightweightPreview();
				ResetSnapLatch();
				MarkDirty();
				UpdateSelectionVisuals();
			}
			_overlapDragArmed = false;
			_overlapDragging = false;
			_overlapDragLeaderId = null;
			_overlapDragOrigins.Clear();
			e.Handled = true;
		}
		else if (_freehandDrawing)
		{
			_freehandDrawing = false;
			PageCanvas.ReleaseMouseCapture();
			if (_freehandPreview != null)
			{
				PageCanvas.Children.Remove(_freehandPreview);
			}
			_freehandPreview = null;
			if (_freehandPoints.Count >= 2)
			{
				double minX = _freehandPoints.Min((Point x) => x.X);
				double minY = _freehandPoints.Min((Point x) => x.Y);
				double num = _freehandPoints.Max((Point x) => x.X);
				double num2 = _freehandPoints.Max((Point x) => x.Y);
				double width = Math.Max(4.0, num - minX);
				double height = Math.Max(4.0, num2 - minY);
				PushUndo();
				CanvasElementModel canvasElementModel2 = new CanvasElementModel
				{
					Kind = ElementKind.Shape,
					ShapeType = "Freehand",
					Name = UniqueName("フリーハンド"),
					Xmm = minX / 3.7795275590551185,
					Ymm = minY / 3.7795275590551185,
					WidthMm = width / 3.7795275590551185,
					HeightMm = height / 3.7795275590551185,
					FillColor = "#00FFFFFF",
					StrokeColor = "#FF172033",
					StrokeThicknessPt = 2.0,
					PreserveAspectRatio = false,
					ShapePoints = _freehandPoints.Select((Point x) => new ShapePointModel
					{
						X = (x.X - minX) / width * 100.0,
						Y = (x.Y - minY) / height * 100.0
					}).ToList(),
					ZIndex = CurrentPage.Elements.Count
				};
				CurrentPage.Elements.Add(canvasElementModel2);
				SelectOnly(canvasElementModel2.Id);
				MarkDirty();
				RebuildCanvas();
				RefreshLayers();
			}
			e.Handled = true;
		}
		else if (_marqueeSelecting)
		{
			_marqueeSelecting = false;
			PageCanvas.ReleaseMouseCapture();
			if (_marqueeRectangle != null)
			{
				PageCanvas.Children.Remove(_marqueeRectangle);
			}
			_marqueeRectangle = null;
			_marqueeBaseSelection.Clear();
			UpdateSelectionVisuals();
			e.Handled = true;
		}
	}

	private Point SnapFreehandPoint(Point point)
	{
		if (!_settings.Current.SnapToGrid || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
		{
			return point;
		}
		double num = Math.Max(0.1, _settings.Current.GridSizeMm) * 3.7795275590551185;
		return new Point(Math.Round(point.X / num) * num, Math.Round(point.Y / num) * num);
	}

	private void Freehand_Click(object sender, RoutedEventArgs e)
	{
		if (_freehandMode)
		{
			ReturnToSelectionMode("フリーハンドを終了しました");
			SetActiveTopTool("選択");
			return;
		}
		ReturnToSelectionMode();
		_freehandMode = true;
		SetActiveTopTool("手描き");
		FreehandButton.Background = (_freehandMode ? new SolidColorBrush(Color.FromRgb(242, 106, 33)) : Brushes.Transparent);
		FreehandButton.Foreground = (_freehandMode ? Brushes.White : Brushes.White);
		base.Cursor = (_freehandMode ? Cursors.Pen : Cursors.Arrow);
		StatusText.Text = (_freehandMode ? "フリーハンド: 台紙上をドラッグ（Shiftでグリッド吸着解除）" : "フリーハンドを終了しました");
	}

	private void SelectTool_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode("選択ツール");
		SetActiveTopTool("選択");
	}

	private void Eyedropper_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode();
		_eyedropperMode = true;
		SetActiveTopTool("スポイト");
		base.Cursor = Cursors.Cross;
		StatusText.Text = "スポイト: 台紙上の色をクリック（Escで終了）";
	}

	private void ReturnToSelectionMode(string? status = null)
	{
		_freehandMode = false;
		_eyedropperMode = false;
		_freehandDrawing = false;
		if (_freehandPreview != null)
		{
			PageCanvas.Children.Remove(_freehandPreview);
		}
		_freehandPreview = null;
		_freehandPoints.Clear();
		PageCanvas.ReleaseMouseCapture();
		FreehandButton.Background = Brushes.Transparent;
		EyedropperButton.Background = Brushes.Transparent;
		base.Cursor = Cursors.Arrow;
		if (status != null)
		{
			StatusText.Text = status;
		}
	}

	private void ApplyEyedropper(Point point)
	{
		try
		{
			int num = Math.Max(1, (int)Math.Ceiling(PageCanvas.ActualWidth));
			int num2 = Math.Max(1, (int)Math.Ceiling(PageCanvas.ActualHeight));
			int x = Math.Clamp((int)point.X, 0, num - 1);
			int y = Math.Clamp((int)point.Y, 0, num2 - 1);
			RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(num, num2, 96.0, 96.0, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(PageCanvas);
			byte[] array = new byte[4];
			renderTargetBitmap.CopyPixels(new Int32Rect(x, y, 1, 1), array, 4, 0);
			byte b = array[3];
			int value = ((b == 0) ? array[2] : Math.Clamp(array[2] * 255 / b, 0, 255));
			int value2 = ((b == 0) ? array[1] : Math.Clamp(array[1] * 255 / b, 0, 255));
			int value3 = ((b == 0) ? array[0] : Math.Clamp(array[0] * 255 / b, 0, 255));
			string text = $"#FF{value:X2}{value2:X2}{value3:X2}";
			PushUndo();
			CanvasElementModel activeElement = ActiveElement;
			if (activeElement != null)
			{
				if (activeElement.Kind == ElementKind.Text)
				{
					activeElement.TextColor = text;
				}
				else if (activeElement.Kind == ElementKind.Shape)
				{
					activeElement.FillColor = text;
				}
				else if (activeElement.Kind == ElementKind.QrCode)
				{
					activeElement.QrForeground = text;
				}
			}
			else
			{
				CurrentPage.Background = text;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
			ReturnToSelectionMode("スポイトで " + text + " を取得しました");
			SetActiveTopTool("選択");
		}
		catch (Exception ex)
		{
			ReturnToSelectionMode("スポイトを終了しました");
			SetActiveTopTool("選択");
			MessageBox.Show("色を取得できませんでした。\n" + ex.Message, "スポイト");
		}
	}

	private void RefreshLayers()
	{
		List<CanvasElementModel> itemsSource = CurrentPage.Elements.OrderByDescending((CanvasElementModel x) => x.ZIndex).ToList();
		_updatingProperties = true;
		LayerList.ItemsSource = null;
		LayerList.ItemsSource = itemsSource;
		LayerList.SelectedItem = ActiveElement;
		_updatingProperties = false;
	}

	private void LayerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_refreshing && !_updatingProperties && LayerList.SelectedItem is CanvasElementModel canvasElementModel)
		{
			_selectedIds.Clear();
			_selectedIds.Add(canvasElementModel.Id);
			UpdateSelectionVisuals();
		}
	}

	private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_refreshing && PageList.SelectedIndex >= 0 && PageList.SelectedIndex != _pageIndex)
		{
			_pageIndex = PageList.SelectedIndex;
			_selectedIds.Clear();
			RefreshAll();
		}
	}

	private void UpdateStatus()
	{
		PageModel currentPage = CurrentPage;
		string text = (_dirty ? " *" : string.Empty);
		ProjectTitleText.Text = _project.ProjectName + text;
		base.Title = _project.ProjectName + text + " - MISE";
		PageInfoOverlay.Text = $"{_project.PaperName}  {currentPage.WidthMm:0.#}×{currentPage.HeightMm:0.#}mm";
		PageStatusText.Text = $"{_pageIndex + 1} / {_project.Pages.Count}ページ";
	}

	private void ProjectTitleText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount >= 2)
		{
			ProjectTitleEditor.Text = _project.ProjectName;
			ProjectTitleText.Visibility = Visibility.Collapsed;
			ProjectTitleEditor.Visibility = Visibility.Visible;
			ProjectTitleEditor.Focus();
			ProjectTitleEditor.SelectAll();
			e.Handled = true;
		}
	}

	private void ProjectTitleEditor_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Return)
		{
			CommitProjectTitle();
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			ProjectTitleEditor.Visibility = Visibility.Collapsed;
			ProjectTitleText.Visibility = Visibility.Visible;
			PageCanvas.Focus();
			e.Handled = true;
		}
	}

	private void ProjectTitleEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		if (ProjectTitleEditor.Visibility == Visibility.Visible)
		{
			CommitProjectTitle();
		}
	}

	private void CommitProjectTitle()
	{
		string text = ProjectTitleEditor.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "無題の販促物";
		}
		string text2 = SafeFileName(text);
		if (_filePath != null)
		{
			string text3 = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_filePath), text2 + ".rcanvas");
			if (!string.Equals(text3, _filePath, StringComparison.OrdinalIgnoreCase))
			{
				if (File.Exists(text3))
				{
					MessageBox.Show("同じ名前のプロジェクトファイルが既にあります。", "ファイル名の変更", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					ProjectTitleEditor.Focus();
					ProjectTitleEditor.SelectAll();
					return;
				}
				string oldPath = _filePath;
				try
				{
					File.Move(oldPath, text3);
					_filePath = text3;
					_settings.Current.RecentProjects.RemoveAll((RecentProjectInfo x) => string.Equals(x.FilePath, oldPath, StringComparison.OrdinalIgnoreCase));
				}
				catch (Exception ex)
				{
					MessageBox.Show("ファイル名を変更できませんでした。\n\n" + ex.Message, "ファイル名の変更", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					ProjectTitleEditor.Focus();
					ProjectTitleEditor.SelectAll();
					return;
				}
			}
		}
		if (_project.ProjectName != text)
		{
			PushUndo();
			_project.ProjectName = text;
			MarkDirty();
			if (_filePath != null)
			{
				_settings.AddRecent(_project, _filePath);
			}
		}
		ProjectTitleEditor.Visibility = Visibility.Collapsed;
		ProjectTitleText.Visibility = Visibility.Visible;
		UpdateStatus();
		PageCanvas.Focus();
	}

	private void UpdateValidationCount()
	{
		List<ValidationIssue> source = _validator.Validate(CurrentPage);
		int num = source.Count((ValidationIssue x) => x.Severity == IssueSeverity.Error);
		int num2 = source.Count((ValidationIssue x) => x.Severity == IssueSeverity.Warning);
		ErrorCountText.Text = ((num + num2 == 0) ? "チェック: 問題なし" : $"チェック: 赤{num} / 黄{num2}");
		ErrorCountText.Foreground = ((num > 0) ? Brushes.Firebrick : ((num2 > 0) ? Brushes.DarkOrange : Brushes.ForestGreen));
	}

	private void MarkDirty()
	{
		_dirty = true;
		_project.UpdatedAt = DateTime.Now;
		UpdateStatus();
	}

	private void PushUndo()
	{
		if (_refreshing)
		{
			return;
		}
		ProjectSnapshot projectSnapshot = CreateSnapshot();
		if (_undo.Count == 0 || !SnapshotsEqual(_undo.Peek(), projectSnapshot))
		{
			_undo.Push(projectSnapshot);
		}
		if (_undo.Count > 50)
		{
			ProjectSnapshot[] array = _undo.Take(50).Reverse().ToArray();
			_undo.Clear();
			ProjectSnapshot[] array2 = array;
			foreach (ProjectSnapshot item in array2)
			{
				_undo.Push(item);
			}
		}
		_redo.Clear();
	}

	private void Undo_Click(object sender, RoutedEventArgs e)
	{
		if (_undo.Count != 0)
		{
			_redo.Push(CreateSnapshot());
			ApplySnapshot(_undo.Pop());
			StatusText.Text = "元に戻しました";
		}
	}

	private void Redo_Click(object sender, RoutedEventArgs e)
	{
		if (_redo.Count != 0)
		{
			_undo.Push(CreateSnapshot());
			ApplySnapshot(_redo.Pop());
			StatusText.Text = "やり直しました";
		}
	}

	private ProjectSnapshot CreateSnapshot()
	{
		Dictionary<Guid, ImageSnapshot> dictionary = new Dictionary<Guid, ImageSnapshot>();
		List<CanvasElementModel> list = (from element in _project.Pages.SelectMany((PageModel page) => page.Elements)
			where element.Kind == ElementKind.Image && (!string.IsNullOrWhiteSpace(element.ImageDataBase64) || !string.IsNullOrWhiteSpace(element.ImageOriginalDataBase64))
			select element).ToList();
		foreach (CanvasElementModel item in list)
		{
			dictionary[item.Id] = new ImageSnapshot(item.ImageDataBase64, item.ImageOriginalDataBase64, item.ImagePreTrimDataBase64);
			item.ImageDataBase64 = null;
			item.ImageOriginalDataBase64 = null;
			item.ImagePreTrimDataBase64 = null;
		}
		try
		{
			return new ProjectSnapshot(_projectService.Serialize(_project), dictionary);
		}
		finally
		{
			foreach (CanvasElementModel item2 in list)
			{
				if (dictionary.TryGetValue(item2.Id, out var value))
				{
					item2.ImageDataBase64 = value.DataBase64;
					item2.ImageOriginalDataBase64 = value.OriginalDataBase64;
					item2.ImagePreTrimDataBase64 = value.PreTrimDataBase64;
				}
			}
		}
	}

	private static bool SnapshotsEqual(ProjectSnapshot left, ProjectSnapshot right)
	{
		if (!string.Equals(left.Json, right.Json, StringComparison.Ordinal) || left.Images.Count != right.Images.Count)
		{
			return false;
		}
		foreach (KeyValuePair<Guid, ImageSnapshot> image in left.Images)
		{
			if (!right.Images.TryGetValue(image.Key, out ImageSnapshot value))
			{
				return false;
			}
			if (!string.Equals(image.Value.DataBase64, value.DataBase64, StringComparison.Ordinal) || !string.Equals(image.Value.OriginalDataBase64, value.OriginalDataBase64, StringComparison.Ordinal) || !string.Equals(image.Value.PreTrimDataBase64, value.PreTrimDataBase64, StringComparison.Ordinal))
			{
				return false;
			}
		}
		return true;
	}

	private void ApplySnapshot(ProjectSnapshot snapshot)
	{
		_project = _projectService.Deserialize(snapshot.Json);
		foreach (CanvasElementModel item in _project.Pages.SelectMany((PageModel page) => page.Elements))
		{
			if (snapshot.Images.TryGetValue(item.Id, out ImageSnapshot value))
			{
				item.ImageDataBase64 = value.DataBase64;
				item.ImageOriginalDataBase64 = value.OriginalDataBase64;
				item.ImagePreTrimDataBase64 = value.PreTrimDataBase64;
			}
		}
		ActivateEmbeddedFonts();
		_pageIndex = Math.Clamp(_pageIndex, 0, _project.Pages.Count - 1);
		_selectedIds.Clear();
		_dirty = true;
		RefreshAll();
	}

	private void NormalizeZ()
	{
		List<CanvasElementModel> list = CurrentPage.Elements.OrderBy((CanvasElementModel x) => x.ZIndex).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			list[num].ZIndex = num;
		}
	}

	private void ShowHome_Click(object sender, RoutedEventArgs e)
	{
		RefreshRecent();
		HomeOverlay.Visibility = Visibility.Visible;
	}

	private void NewProject_Click(object sender, RoutedEventArgs e)
	{
		if (!ConfirmDiscardOrSave())
		{
			return;
		}
		NewProjectDialog newProjectDialog = new NewProjectDialog
		{
			Owner = this
		};
		if (newProjectDialog.ShowDialog() == true && newProjectDialog.Result != null)
		{
			NewProjectOptions result = newProjectDialog.Result;
			CreateBlankProject(result.PaperName, result.Landscape, hideHome: true, result.CustomWidthMm, result.CustomHeightMm);
			_project.ProjectName = result.ProjectName;
			_project.Purpose = result.Purpose;
			_project.BrandName = result.Brand;
			_project.StoreName = result.Store;
			_project.Author = result.Author;
			_project.PrintMode = result.PrintMode;
			_project.Pages[0].Background = result.Background;
			for (int i = 1; i < result.PageCount; i++)
			{
				PageModel pageModel = PageModel.Create(result.PaperName, result.Landscape);
				pageModel.Name = $"ページ {i + 1}";
				pageModel.Background = result.Background;
				pageModel.SafeMarginMm = _settings.Current.DefaultSafeMarginMm;
				pageModel.ShowGrid = _settings.Current.ShowGridOnNewProjects;
				pageModel.ShowSafeArea = _settings.Current.ShowSafeAreaOnNewProjects;
				_project.Pages.Add(pageModel);
			}
			_dirty = true;
			RefreshAll();
		}
	}

	private void QuickNew_Click(object sender, RoutedEventArgs e)
	{
		if (ConfirmDiscardOrSave() && sender is FrameworkElement frameworkElement)
		{
			CreateBlankProject(frameworkElement.Tag?.ToString() ?? "A4", landscape: false);
			_project.ProjectName = (frameworkElement.Tag?.ToString() ?? "A4") + " 販促物";
			_dirty = true;
			RefreshAll();
		}
	}

	private void OpenProject_Click(object sender, RoutedEventArgs e)
	{
		if (ConfirmDiscardOrSave())
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "MISEプロジェクトを開く",
				Filter = "MISEプロジェクト (*.rcanvas;*.rtemplate)|*.rcanvas;*.rtemplate|すべてのファイル|*.*",
				InitialDirectory = AppPaths.Projects
			};
			if (openFileDialog.ShowDialog(this) == true)
			{
				OpenProject(openFileDialog.FileName);
			}
		}
	}

	private async void OpenProject(string path)
	{
		try
		{
			_project = _projectService.Load(path);
			ActivateEmbeddedFonts();
			_filePath = (path.EndsWith(".rtemplate", StringComparison.OrdinalIgnoreCase) ? null : path);
			if (_filePath == null)
			{
				_project.ProjectId = Guid.NewGuid();
			}
			_pageIndex = 0;
			_dirty = false;
			_undo.Clear();
			_redo.Clear();
			_selectedIds.Clear();
			HomeOverlay.Visibility = Visibility.Collapsed;
			if (_filePath != null)
			{
				_settings.AddRecent(_project, _filePath);
			}
			RefreshAll();
			await UpgradePdfPreviewsAsync();
		}
		catch (Exception ex)
		{
			LogService.Error("Project open failed", ex);
			MessageBox.Show("プロジェクトを開けませんでした。\n\n" + ex.Message, "MISE", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private async Task UpgradePdfPreviewsAsync()
	{
		List<CanvasElementModel> pdfElements = (from canvasElementModel in _project.Pages.SelectMany((PageModel page) => page.Elements)
			where canvasElementModel.Kind == ElementKind.Image && !string.IsNullOrWhiteSpace(canvasElementModel.PdfSourcePath) && canvasElementModel.PdfPageIndex.HasValue && File.Exists(canvasElementModel.PdfSourcePath) && Math.Max(canvasElementModel.ImagePixelWidth, canvasElementModel.ImagePixelHeight) < 2400
			select canvasElementModel).ToList();
		if (pdfElements.Count == 0)
		{
			return;
		}
		int updated = 0;
		for (int i = 0; i < pdfElements.Count; i++)
		{
			CanvasElementModel element = pdfElements[i];
			StatusText.Text = $"PDFプレビューを高精細化中… {i + 1}/{pdfElements.Count}";
			try
			{
				PdfRenderedPage pdfRenderedPage = await PdfImportService.RenderPageAsync(element.PdfSourcePath, element.PdfPageIndex.Value);
				BitmapSource bitmapSource = LoadBitmap(pdfRenderedPage.PngBytes);
				element.ImageDataBase64 = Convert.ToBase64String(pdfRenderedPage.PngBytes);
				element.ImagePixelWidth = bitmapSource.PixelWidth;
				element.ImagePixelHeight = bitmapSource.PixelHeight;
				updated++;
			}
			catch (Exception ex)
			{
				LogService.Error("PDF preview upgrade failed", ex);
			}
		}
		if (updated > 0)
		{
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
			StatusText.Text = $"PDFプレビューを高精細化しました（{updated}ページ）";
		}
	}

	private void SaveProject_Click(object sender, RoutedEventArgs e)
	{
		SaveProject(saveAs: false);
	}

	private void SaveAsProject_Click(object sender, RoutedEventArgs e)
	{
		SaveProject(saveAs: true);
	}

	private bool SaveProject(bool saveAs)
	{
		if (saveAs || string.IsNullOrWhiteSpace(_filePath))
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog
			{
				Title = "プロジェクトを保存",
				Filter = "MISEプロジェクト (*.rcanvas)|*.rcanvas",
				DefaultExt = ".rcanvas",
				AddExtension = true,
				FileName = SafeFileName(_project.ProjectName),
				InitialDirectory = AppPaths.Projects
			};
			if (saveFileDialog.ShowDialog(this) != true)
			{
				return false;
			}
			_filePath = saveFileDialog.FileName;
		}
		try
		{
			_projectService.Save(_project, _filePath);
			_projectService.ClearAutoSave(_project);
			_settings.AddRecent(_project, _filePath);
			_dirty = false;
			UpdateStatus();
			StatusText.Text = "保存しました";
			return true;
		}
		catch (Exception ex)
		{
			LogService.Error("Project save failed", ex);
			return MessageBox.Show($"保存できませんでした。\n\n保存先: {_filePath}\n理由: {ex.Message}\n\n別名で保存しますか？", "保存エラー", MessageBoxButton.YesNo, MessageBoxImage.Hand) == MessageBoxResult.Yes && SaveProject(saveAs: true);
		}
	}

	private void AutoSave()
	{
		if (!_dirty)
		{
			return;
		}
		try
		{
			_projectService.AutoSave(_project);
			AutoSaveText.Text = $"自動保存済み {DateTime.Now:HH:mm}";
		}
		catch (Exception ex)
		{
			AutoSaveText.Text = "自動保存に失敗";
			LogService.Error("Auto save failed", ex);
		}
	}

	private bool ConfirmDiscardOrSave()
	{
		if (!_dirty)
		{
			return true;
		}
		return MessageBox.Show("現在の変更を保存しますか？", "MISE", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
		{
			MessageBoxResult.Yes => SaveProject(saveAs: false), 
			MessageBoxResult.No => true, 
			_ => false, 
		};
	}

	private void RecentList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!(RecentList.SelectedItem is RecentProjectInfo recentProjectInfo))
		{
			return;
		}
		if (!File.Exists(recentProjectInfo.FilePath))
		{
			MessageBox.Show("ファイルが移動または削除されています。", "リンク切れ", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		else if (ConfirmDiscardOrSave())
		{
			if (recentProjectInfo.IsAutoSave)
			{
				OpenAutoSave(recentProjectInfo.FilePath);
			}
			else
			{
				OpenProject(recentProjectInfo.FilePath);
			}
		}
	}

	private async void OpenAutoSave(string path)
	{
		try
		{
			_project = _projectService.Load(path);
			ActivateEmbeddedFonts();
			if (!_project.ProjectName.EndsWith("（復元）", StringComparison.Ordinal))
			{
				_project.ProjectName += "（復元）";
			}
			_filePath = null;
			_pageIndex = 0;
			_dirty = true;
			_undo.Clear();
			_redo.Clear();
			_selectedIds.Clear();
			HomeOverlay.Visibility = Visibility.Collapsed;
			RefreshAll();
			StatusText.Text = "自動保存データを復元しました。保存時に保存先を指定してください";
			await UpgradePdfPreviewsAsync();
		}
		catch (Exception ex)
		{
			LogService.Error("Recovery open failed", ex);
			MessageBox.Show("自動保存データを開けませんでした。\n\n" + ex.Message, "MISE", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private static string SafeFileName(string value)
	{
		string text = string.Join("_", value.Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "無題の販促物";
	}

	private void AddHeading_Click(object sender, RoutedEventArgs e)
	{
		AddText("大見出し", "心を動かす、ひとこと。", 28.0, 120.0, 26.0, bold: true);
	}

	private void AddSubheading_Click(object sender, RoutedEventArgs e)
	{
		AddText("中見出し", "商品の魅力を分かりやすく", 20.0, 110.0, 18.0, bold: true);
	}

	private void AddBody_Click(object sender, RoutedEventArgs e)
	{
		AddText("本文", "ここに説明文を入力してください。", 12.0, 110.0, 30.0, bold: false);
	}

	private void AddNote_Click(object sender, RoutedEventArgs e)
	{
		AddText("注釈", "※条件・注意事項を入力", 7.0, 110.0, 15.0, bold: false);
	}

	private void AddPrice_Click(object sender, RoutedEventArgs e)
	{
		AddText("価格", "￥00,000", 30.0, 100.0, 28.0, bold: true, "#FFF26A21");
	}

	private void AddProductName_Click(object sender, RoutedEventArgs e)
	{
		AddText("製品名", "製品名", 22.0, 110.0, 22.0, bold: true);
	}

	private Point VisibleViewportCenterMm()
	{
		try
		{
			double num = ((CanvasScroll.ViewportWidth > 0.0) ? CanvasScroll.ViewportWidth : CanvasScroll.ActualWidth);
			double num2 = ((CanvasScroll.ViewportHeight > 0.0) ? CanvasScroll.ViewportHeight : CanvasScroll.ActualHeight);
			Point point = CanvasScroll.TranslatePoint(new Point(num / 2.0, num2 / 2.0), PageCanvas);
			if (double.IsFinite(point.X) && double.IsFinite(point.Y))
			{
				return new Point(Math.Clamp(point.X / 3.7795275590551185, 0.0, CurrentPage.WidthMm), Math.Clamp(point.Y / 3.7795275590551185, 0.0, CurrentPage.HeightMm));
			}
		}
		catch
		{
		}
		return new Point(CurrentPage.WidthMm / 2.0, CurrentPage.HeightMm / 2.0);
	}

	private Point VisibleInsertionTopLeft(double widthMm, double heightMm, double offsetMm = 0.0)
	{
		Point point = VisibleViewportCenterMm();
		double min = Math.Min(0.0, CurrentPage.WidthMm - widthMm);
		double max = Math.Max(0.0, CurrentPage.WidthMm - widthMm);
		double min2 = Math.Min(0.0, CurrentPage.HeightMm - heightMm);
		double max2 = Math.Max(0.0, CurrentPage.HeightMm - heightMm);
		return new Point(Math.Clamp(point.X - widthMm / 2.0 + offsetMm, min, max), Math.Clamp(point.Y - heightMm / 2.0 + offsetMm, min2, max2));
	}

	private void CenterElementsInVisibleViewport(IReadOnlyCollection<CanvasElementModel> elements)
	{
		if (elements.Count == 0)
		{
			return;
		}
		double num = elements.Min((CanvasElementModel x) => x.Xmm);
		double num2 = elements.Min((CanvasElementModel x) => x.Ymm);
		double num3 = elements.Max((CanvasElementModel x) => x.Xmm + x.WidthMm);
		double num4 = elements.Max((CanvasElementModel x) => x.Ymm + x.HeightMm);
		Point point = VisibleInsertionTopLeft(num3 - num, num4 - num2);
		double num5 = point.X - num;
		double num6 = point.Y - num2;
		foreach (CanvasElementModel element in elements)
		{
			element.Xmm += num5;
			element.Ymm += num6;
		}
	}

	private void AddText(string name, string text, double pt, double widthMm, double heightMm, bool bold, string color = "#FF172033")
	{
		ReturnToSelectionMode();
		PushUndo();
		double widthMm2 = Math.Min(widthMm, Math.Max(5.0, CurrentPage.WidthMm - 10.0));
		Point point = VisibleInsertionTopLeft(widthMm2, heightMm);
		CanvasElementModel canvasElementModel = new CanvasElementModel
		{
			Kind = ElementKind.Text,
			Name = UniqueName(name),
			Text = text,
			FontSizePt = pt,
			Bold = bold,
			FontWeightValue = (bold ? 700 : 400),
			TextColor = color,
			WidthMm = widthMm2,
			HeightMm = heightMm,
			Xmm = point.X,
			Ymm = point.Y,
			ZIndex = CurrentPage.Elements.Count
		};
		CurrentPage.Elements.Add(canvasElementModel);
		SelectOnly(canvasElementModel.Id);
		MarkDirty();
		RebuildCanvas();
		if (canvasElementModel.Kind == ElementKind.Text)
		{
			base.Dispatcher.BeginInvoke(new Action(() => FitTextFrameToGlyphBounds(canvasElementModel)), DispatcherPriority.Loaded);
		}
		RefreshLayers();
		UpdatePropertyPanel();
	}

	private void FitTextFrameToGlyphBounds(CanvasElementModel model)
	{
		if (!_visuals.TryGetValue(model.Id, out DesignerItem item) || string.IsNullOrEmpty(model.Text))
			return;
		FontWeight weight = FontWeight.FromOpenTypeWeight(Math.Clamp(model.Bold ? Math.Max(700, model.FontWeightValue) : model.FontWeightValue, 100, 900));
		Typeface typeface = new Typeface(ResolveFontFamily(model.FontFamily), model.Italic ? FontStyles.Italic : FontStyles.Normal, weight, FontStretches.Normal);
		double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
		double size = Math.Max(1.0, model.FontSizePt * 96.0 / 72.0);
		string[] lines = model.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
		double width = 0.0;
		double lineHeight = 0.0;
		foreach (string line in lines)
		{
			FormattedText measured = new FormattedText(line, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, dip);
			width = Math.Max(width, measured.WidthIncludingTrailingWhitespace);
			lineHeight = Math.Max(lineHeight, measured.Height);
		}
		if (width <= 0.0 || lineHeight <= 0.0) return;
		double height = lineHeight * Math.Max(1, lines.Length);
		const double pxPerMm = 3.7795275590551185;
		model.WidthMm = Math.Max(1.0, (width + 2.0) / pxPerMm);
		model.HeightMm = Math.Max(1.0, (height + 2.0) / pxPerMm);
		item.Width = width + 2.0;
		item.Height = height + 2.0;
		UpdatePropertyPanel();
	}

	private async void AddImage_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode();
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "画像またはPDFを選択",
			Filter = "画像・PDF (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp;*.pdf)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp;*.pdf|すべてのファイル|*.*",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		string[] fileNames = openFileDialog.FileNames;
		foreach (string path in fileNames)
		{
			if (System.IO.Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
			{
				await AddPdfFileAsync(path);
			}
			else
			{
				AddImageFile(path);
			}
		}
	}

	private async void AddPdf_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode();
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "PDFを選択",
			Filter = "PDF (*.pdf)|*.pdf",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog(this) == true)
		{
			string[] fileNames = openFileDialog.FileNames;
			foreach (string path in fileNames)
			{
				await AddPdfFileAsync(path);
			}
		}
	}

	private async Task AddPdfFileAsync(string path)
	{
		_ = 1;
		try
		{
			PdfPageSelectionDialog selector = new PdfPageSelectionDialog(await PdfImportService.GetPageCountAsync(path))
			{
				Owner = this
			};
			if (selector.ShowDialog() != true)
			{
				return;
			}
			PushUndo();
			foreach (int pageIndex in selector.SelectedPages)
			{
				PdfRenderedPage pdfRenderedPage = await PdfImportService.RenderPageAsync(path, pageIndex);
				BitmapImage bitmapImage = LoadBitmap(pdfRenderedPage.PngBytes);
				double num = Math.Min(Math.Max(20.0, CurrentPage.WidthMm - 30.0), 100.0);
				double num2 = num * (double)bitmapImage.PixelHeight / Math.Max(1.0, bitmapImage.PixelWidth);
				if (num2 > CurrentPage.HeightMm - 30.0)
				{
					num2 = CurrentPage.HeightMm - 30.0;
					num = num2 * (double)bitmapImage.PixelWidth / Math.Max(1.0, bitmapImage.PixelHeight);
				}
				double offsetMm = (double)selector.SelectedPages.ToList().IndexOf(pageIndex) * 3.0;
				Point point = VisibleInsertionTopLeft(num, num2, offsetMm);
				CanvasElementModel canvasElementModel = new CanvasElementModel
				{
					Kind = ElementKind.Image,
					Name = UniqueName($"PDF_{System.IO.Path.GetFileNameWithoutExtension(path)}_{pageIndex + 1}"),
					WidthMm = num,
					HeightMm = num2,
					Xmm = point.X,
					Ymm = point.Y,
					PreserveAspectRatio = true,
					ZIndex = CurrentPage.Elements.Count,
					PdfSourcePath = path,
					PdfPageIndex = pageIndex,
					ImageDataBase64 = Convert.ToBase64String(pdfRenderedPage.PngBytes),
					ImagePixelWidth = bitmapImage.PixelWidth,
					ImagePixelHeight = bitmapImage.PixelHeight,
					ImageSourcePath = path,
					ImageSourceBytes = new FileInfo(path).Length
				};
				CurrentPage.Elements.Add(canvasElementModel);
				SelectOnly(canvasElementModel.Id);
			}
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
			UpdatePropertyPanel();
			StatusText.Text = $"PDFから{selector.SelectedPages.Count}ページを配置しました";
		}
		catch (Exception ex)
		{
			MessageBox.Show("PDFを読み込めませんでした。\n\n" + ex.Message, "PDF読込", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void AddImageFile(string path)
	{
		try
		{
			byte[] array = File.ReadAllBytes(path);
			BitmapImage bitmapImage = LoadBitmap(array);
			double num = Math.Min(Math.Max(20.0, CurrentPage.WidthMm - 30.0), 100.0);
			double num2 = ((bitmapImage.PixelWidth > 0) ? (num * (double)bitmapImage.PixelHeight / (double)bitmapImage.PixelWidth) : 70.0);
			if (num2 > CurrentPage.HeightMm - 30.0)
			{
				num2 = CurrentPage.HeightMm - 30.0;
				num = num2 * (double)bitmapImage.PixelWidth / (double)Math.Max(1, bitmapImage.PixelHeight);
			}
			PushUndo();
			Point point = VisibleInsertionTopLeft(num, num2);
			CanvasElementModel canvasElementModel = new CanvasElementModel
			{
				Kind = ElementKind.Image,
				Name = UniqueName(System.IO.Path.GetFileNameWithoutExtension(path)),
				WidthMm = num,
				HeightMm = num2,
				Xmm = point.X,
				Ymm = point.Y,
				PreserveAspectRatio = true,
				ZIndex = CurrentPage.Elements.Count
			};
			BitmapSource bitmapSource = StoreImageForEditing(canvasElementModel, array, bitmapImage, path);
			if (!canvasElementModel.ImageUsesLinkedOriginal && (bitmapSource.PixelWidth != bitmapImage.PixelWidth || bitmapSource.PixelHeight != bitmapImage.PixelHeight))
			{
				num2 = (canvasElementModel.HeightMm = num * (double)bitmapSource.PixelHeight / Math.Max(1.0, bitmapSource.PixelWidth));
				Point point2 = VisibleInsertionTopLeft(num, num2);
				canvasElementModel.Xmm = point2.X;
				canvasElementModel.Ymm = point2.Y;
			}
			CurrentPage.Elements.Add(canvasElementModel);
			SelectOnly(canvasElementModel.Id);
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
			UpdatePropertyPanel();
			if (canvasElementModel.ImageUsesLinkedOriginal)
			{
				StatusText.Text = $"大容量画像を軽量プレビューで配置しました（元画像 {(double)canvasElementModel.ImageSourceBytes / 1024.0 / 1024.0:0.0}MB）";
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("画像を読み込めませんでした。\n\n" + ex.Message, "画像エラー", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void EditImageExtrusion_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Image)
		{
			ImageExtrusionDialog imageExtrusionDialog = new ImageExtrusionDialog(activeElement)
			{
				Owner = this
			};
			if (imageExtrusionDialog.ShowDialog() == true)
			{
				PushUndo();
				activeElement.ImageExtrusionDepthPt = imageExtrusionDialog.DepthPt;
				activeElement.ImageExtrusionAngle = imageExtrusionDialog.Angle;
				activeElement.ImageExtrusionColor = imageExtrusionDialog.ColorValue;
				activeElement.ImageExtrusionSmoothness = imageExtrusionDialog.Smoothness;
				MarkDirty();
				RebuildCanvas();
				UpdatePropertyPanel();
			}
		}
	}

	private void EditTransparentImageTrim_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Image || string.IsNullOrWhiteSpace(activeElement.ImageDataBase64))
		{
			return;
		}
		ImageTrimDialog imageTrimDialog = new ImageTrimDialog(activeElement)
		{
			Owner = this
		};
		if (imageTrimDialog.ShowDialog() != true)
		{
			return;
		}
		try
		{
			PushUndo();
			byte[] array = Convert.FromBase64String(activeElement.ImagePreTrimDataBase64 ?? activeElement.ImageDataBase64);
			BitmapSource bitmapSource = LoadBitmap(array);
			double num = activeElement.Xmm + activeElement.WidthMm / 2.0;
			double num2 = activeElement.Ymm + activeElement.HeightMm / 2.0;
			if (imageTrimDialog.RestoreOriginal)
			{
				activeElement.ImageDataBase64 = Convert.ToBase64String(array);
				activeElement.ImagePixelWidth = bitmapSource.PixelWidth;
				activeElement.ImagePixelHeight = bitmapSource.PixelHeight;
				activeElement.ImageTransparentTrimApplied = false;
				activeElement.HeightMm = activeElement.WidthMm * (double)bitmapSource.PixelHeight / Math.Max(1.0, bitmapSource.PixelWidth);
			}
			else
			{
				CanvasElementModel canvasElementModel = activeElement;
				if (canvasElementModel.ImagePreTrimDataBase64 == null)
				{
					string text = (canvasElementModel.ImagePreTrimDataBase64 = Convert.ToBase64String(array));
				}
				activeElement.ImageTransparentTrimThreshold = imageTrimDialog.AlphaThreshold;
				activeElement.ImageTransparentTrimPaddingPixels = imageTrimDialog.PaddingPixels;
				if (!TryTrimTransparentEdges(bitmapSource, imageTrimDialog.AlphaThreshold, imageTrimDialog.PaddingPixels, out BitmapSource result))
				{
					MessageBox.Show("トリミングできる透明余白が見つかりませんでした。", "透明余白");
					return;
				}
				activeElement.ImageDataBase64 = Convert.ToBase64String(EncodeBitmap(result, "PNG", 100));
				activeElement.ImagePixelWidth = result.PixelWidth;
				activeElement.ImagePixelHeight = result.PixelHeight;
				activeElement.ImageTransparentTrimApplied = true;
				activeElement.HeightMm = activeElement.WidthMm * (double)result.PixelHeight / Math.Max(1.0, result.PixelWidth);
			}
			activeElement.Xmm = num - activeElement.WidthMm / 2.0;
			activeElement.Ymm = num2 - activeElement.HeightMm / 2.0;
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
		catch (Exception ex)
		{
			MessageBox.Show("透明余白を処理できませんでした。\n\n" + ex.Message, "透明余白", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void AddShape_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode();
		string requested = (sender as FrameworkElement)?.Tag?.ToString() ?? _settings.Current.LastShapeType;
		string text = (requested.StartsWith("Line:", StringComparison.Ordinal) ? "Line" : requested);
		_settings.Current.LastShapeType = requested;
		_settings.Current.RecentShapeTypes.RemoveAll((string x) => string.Equals(x, requested, StringComparison.OrdinalIgnoreCase));
		_settings.Current.RecentShapeTypes.Insert(0, requested);
		if (_settings.Current.RecentShapeTypes.Count > 12)
		{
			_settings.Current.RecentShapeTypes.RemoveRange(12, _settings.Current.RecentShapeTypes.Count - 12);
		}
		_settings.Save();
		PushUndo();
		double val = ((text == "Line") ? 100.0 : 65.0);
		double heightMm = ((text == "Line") ? 5.0 : 45.0);
		bool flag;
		switch (text)
		{
		case "Circle":
		case "Square":
		case "Ring":
		case "Star":
		case "Badge":
		case "Heart":
		case "Diamond":
		case "Polygon":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			val = 50.0;
			heightMm = 50.0;
		}
		double widthMm = Math.Min(val, Math.Max(5.0, CurrentPage.WidthMm - 10.0));
		Point point = VisibleInsertionTopLeft(widthMm, heightMm);
		CanvasElementModel canvasElementModel = new CanvasElementModel();
		canvasElementModel.Kind = ElementKind.Shape;
		CanvasElementModel canvasElementModel2 = canvasElementModel;
		canvasElementModel2.Name = UniqueName(text switch
		{
			"Ellipse" => "楕円", 
			"Circle" => "円", 
			"Square" => "正方形", 
			"SemiCircle" => "半円", 
			"Ring" => "リング", 
			"RoundedRectangle" => "角丸長方形", 
			"Panel" => "パネル", 
			"Triangle" => "三角形", 
			"Star" => "星", 
			"Heart" => "ハート", 
			"Diamond" => "ひし形", 
			"Badge" => "バッジ", 
			"SpeechBubble" => "吹き出し", 
			"Label" => "ラベル", 
			"Polygon" => "多角形", 
			"Line" => "区切り線", 
			_ => "長方形", 
		});
		canvasElementModel.ShapeType = text;
		canvasElementModel.WidthMm = widthMm;
		canvasElementModel.HeightMm = heightMm;
		canvasElementModel.Xmm = point.X;
		canvasElementModel.Ymm = point.Y;
		canvasElementModel.FillColor = ((text == "Line") ? "#00FFFFFF" : "#FFF26A21");
		canvasElementModel.StrokeColor = "#FF172033";
		canvasElementModel.StrokeThicknessPt = ((!(text == "Line")) ? 1 : 2);
		if (requested == "Line:Dash")
		{
			canvasElementModel.LineStyle = "破線";
		}
		else if (requested == "Line:Dot")
		{
			canvasElementModel.LineStyle = "点線";
		}
		else if (requested == "Line:Arrow")
		{
			canvasElementModel.LineEndCap = "三角矢印";
		}
		else if (requested == "Line:BothArrow")
		{
			canvasElementModel.LineStartCap = "三角矢印";
			canvasElementModel.LineEndCap = "三角矢印";
		}
		else if (requested == "Line:OpenArrow")
		{
			canvasElementModel.LineEndCap = "開き矢印";
		}
		CanvasElementModel canvasElementModel3 = canvasElementModel;
		switch (text)
		{
		case "Circle":
		case "Square":
		case "Ring":
		case "Star":
		case "Badge":
		case "Heart":
		case "Diamond":
		case "Polygon":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		bool preserveAspectRatio = flag;
		canvasElementModel3.PreserveAspectRatio = preserveAspectRatio;
		canvasElementModel.CornerRadiusMm = ((text == "Panel") ? 6 : 2);
		canvasElementModel.PanelEnabled = text == "Panel";
		canvasElementModel.PanelRows = ((!(text == "Panel")) ? 1 : 2);
		canvasElementModel.PanelColumns = 1;
		canvasElementModel.ZIndex = CurrentPage.Elements.Count;
		CanvasElementModel canvasElementModel4 = canvasElementModel;
		CurrentPage.Elements.Add(canvasElementModel4);
		SelectOnly(canvasElementModel4.Id);
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
	}

	private void AddQr_Click(object sender, RoutedEventArgs e)
	{
		ReturnToSelectionMode();
		QrDialog qrDialog = new QrDialog
		{
			Owner = this
		};
		if (qrDialog.ShowDialog() == true && !(qrDialog.Result == null))
		{
			PushUndo();
			double num = Math.Max(5.0, Math.Min(45.0, Math.Min(CurrentPage.WidthMm, CurrentPage.HeightMm) - 10.0));
			Point point = VisibleInsertionTopLeft(num, num);
			CanvasElementModel canvasElementModel = new CanvasElementModel
			{
				Kind = ElementKind.QrCode,
				Name = UniqueName("QRコード"),
				QrContent = qrDialog.Result.Content,
				QrErrorCorrection = qrDialog.Result.ErrorCorrection,
				QrForeground = qrDialog.Result.Foreground,
				QrBackground = qrDialog.Result.Background,
				QrLabel = qrDialog.Result.Label,
				WidthMm = num,
				HeightMm = num,
				Xmm = point.X,
				Ymm = point.Y,
				PreserveAspectRatio = true,
				ZIndex = CurrentPage.Elements.Count
			};
			CurrentPage.Elements.Add(canvasElementModel);
			SelectOnly(canvasElementModel.Id);
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
		}
	}

	private string UniqueName(string baseName)
	{
		if (CurrentPage.Elements.All((CanvasElementModel x) => x.Name != baseName))
		{
			return baseName;
		}
		int i = 2;
		while (CurrentPage.Elements.Any((CanvasElementModel x) => x.Name == $"{baseName}_{i:00}"))
		{
			i++;
		}
		return $"{baseName}_{i:00}";
	}

	private void SelectOnly(Guid id)
	{
		_selectedIds.Clear();
		_selectedIds.Add(id);
	}

	private void Delete_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedIds.Count != 0)
		{
			PushUndo();
			CurrentPage.Elements.RemoveAll((CanvasElementModel x) => _selectedIds.Contains(x.Id));
			_selectedIds.Clear();
			NormalizeZ();
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
			UpdatePropertyPanel();
		}
	}

	private void Duplicate_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedIds.Count == 0)
		{
			return;
		}
		PushUndo();
		List<CanvasElementModel> list = CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)).Select(CloneElement).ToList();
		_selectedIds.Clear();
		foreach (CanvasElementModel item in list)
		{
			item.Id = Guid.NewGuid();
			item.Name = UniqueName(item.Name);
			item.Xmm += 5.0;
			item.Ymm += 5.0;
			item.ZIndex = CurrentPage.Elements.Count;
			CurrentPage.Elements.Add(item);
			_selectedIds.Add(item.Id);
		}
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
	}

	private static CanvasElementModel CloneElement(CanvasElementModel item)
	{
		return JsonSerializer.Deserialize<CanvasElementModel>(JsonSerializer.Serialize(item, ProjectService.JsonOptions), ProjectService.JsonOptions);
	}

	private void Copy_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedIds.Count == 0)
		{
			return;
		}
		List<CanvasElementModel> list = CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)).ToList();
		DataObject dataObject = new DataObject();
		dataObject.SetData("RetailCanvas.Elements", JsonSerializer.Serialize(list, ProjectService.JsonOptions));
		if (list.Count == 1 && list[0].Kind == ElementKind.Image && !string.IsNullOrWhiteSpace(list[0].ImageDataBase64))
		{
			try
			{
				dataObject.SetImage(LoadBitmap(Convert.FromBase64String(list[0].ImageDataBase64)));
			}
			catch
			{
			}
		}
		Clipboard.SetDataObject(dataObject, copy: true);
		StatusText.Text = $"{list.Count}個の要素をコピーしました";
	}

	private void Paste_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (Clipboard.ContainsData("RetailCanvas.Elements"))
			{
				List<CanvasElementModel> list = JsonSerializer.Deserialize<List<CanvasElementModel>>(Clipboard.GetData("RetailCanvas.Elements")?.ToString() ?? string.Empty, ProjectService.JsonOptions);
				if (list == null)
				{
					return;
				}
				PushUndo();
				_selectedIds.Clear();
				foreach (CanvasElementModel item in list)
				{
					item.Id = Guid.NewGuid();
					item.Xmm += 5.0;
					item.Ymm += 5.0;
					item.Name = UniqueName(item.Name);
					item.ZIndex = CurrentPage.Elements.Count;
					CurrentPage.Elements.Add(item);
					_selectedIds.Add(item.Id);
				}
				MarkDirty();
				RebuildCanvas();
				RefreshLayers();
			}
			else
			{
				if (!Clipboard.ContainsImage())
				{
					return;
				}
				BitmapSource image = Clipboard.GetImage();
				if (image != null)
				{
					PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
					pngBitmapEncoder.Frames.Add(BitmapFrame.Create(image));
					using MemoryStream memoryStream = new MemoryStream();
					pngBitmapEncoder.Save(memoryStream);
					string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RetailCanvasClipboard.png");
					File.WriteAllBytes(path, memoryStream.ToArray());
					AddImageFile(path);
					return;
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("貼り付けできませんでした。\n\n" + ex.Message, "貼り付け", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void SelectAll_Click(object sender, RoutedEventArgs e)
	{
		_selectedIds.Clear();
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel x) => x.IsVisible))
		{
			_selectedIds.Add(item.Id);
		}
		UpdateSelectionVisuals();
	}

	private void AddPage_Click(object sender, RoutedEventArgs e)
	{
		PushUndo();
		PageModel pageModel = PageModel.Create(_project.PaperName, _project.Landscape);
		pageModel.Name = $"ページ {_project.Pages.Count + 1}";
		pageModel.Background = CurrentPage.Background;
		pageModel.SafeMarginMm = _settings.Current.DefaultSafeMarginMm;
		pageModel.ShowGrid = _settings.Current.ShowGridOnNewProjects;
		pageModel.ShowSafeArea = _settings.Current.ShowSafeAreaOnNewProjects;
		_project.Pages.Add(pageModel);
		_pageIndex = _project.Pages.Count - 1;
		_selectedIds.Clear();
		MarkDirty();
		RefreshAll();
	}

	private void DuplicatePage_Click(object sender, RoutedEventArgs e)
	{
		PushUndo();
		PageModel pageModel = JsonSerializer.Deserialize<PageModel>(JsonSerializer.Serialize(CurrentPage, ProjectService.JsonOptions), ProjectService.JsonOptions);
		pageModel.PageId = Guid.NewGuid();
		pageModel.Name = $"ページ {_project.Pages.Count + 1}";
		foreach (CanvasElementModel element in pageModel.Elements)
		{
			element.Id = Guid.NewGuid();
		}
		_project.Pages.Insert(_pageIndex + 1, pageModel);
		_pageIndex++;
		_selectedIds.Clear();
		MarkDirty();
		RefreshAll();
	}

	private void DeletePage_Click(object sender, RoutedEventArgs e)
	{
		if (_project.Pages.Count <= 1)
		{
			MessageBox.Show("最低1ページは必要です。", "ページ削除", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		PushUndo();
		_project.Pages.RemoveAt(_pageIndex);
		_pageIndex = Math.Min(_pageIndex, _project.Pages.Count - 1);
		_selectedIds.Clear();
		MarkDirty();
		RefreshAll();
	}

	private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
	{
		if (TemplateCombo.SelectedItem is string templateName && (CurrentPage.Elements.Count <= 0 || MessageBox.Show("現在のページ内容をテンプレートで置き換えますか？", "テンプレート", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
		{
			PushUndo();
			ApplyTemplateByName(templateName);
			_selectedIds.Clear();
			MarkDirty();
			RefreshAll();
		}
	}

	private void SmartTemplate_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			SmartTemplateDialog smartTemplateDialog = new SmartTemplateDialog(new DatabaseService(), _templates)
			{
				Owner = this
			};
			if (smartTemplateDialog.ShowDialog() == true && smartTemplateDialog.SelectedProduct != null && (CurrentPage.Elements.Count <= 0 || MessageBox.Show("現在のページを商品データ連動テンプレートで置き換えますか？", "商品データからPOPを作成", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes))
			{
				PushUndo();
				ApplyTemplateByName(smartTemplateDialog.TemplateName);
				int value = ApplyProductToTemplate(smartTemplateDialog.SelectedProduct, smartTemplateDialog.TemplateName);
				_project.BrandName = smartTemplateDialog.SelectedProduct.BrandName;
				_selectedIds.Clear();
				MarkDirty();
				RefreshAll();
				HomeOverlay.Visibility = Visibility.Collapsed;
				StatusText.Text = $"{smartTemplateDialog.SelectedProduct.ProductName}をテンプレートへ反映しました（{value}項目）";
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("商品データからPOPを作成できませんでした。\n\n" + ex.Message, "商品データ連動テンプレート", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ApplyTemplateByName(string templateName)
	{
		if (_templates.BuiltInNames.Contains(templateName))
		{
			_templates.ApplyBuiltIn(CurrentPage, templateName);
			return;
		}
		PageModel pageModel = _templates.LoadUserTemplate(templateName).Pages.First();
		CurrentPage.Background = pageModel.Background;
		CurrentPage.BackgroundTextureName = pageModel.BackgroundTextureName;
		CurrentPage.BackgroundTextureDataBase64 = pageModel.BackgroundTextureDataBase64;
		CurrentPage.BackgroundTextureOpacity = pageModel.BackgroundTextureOpacity;
		CurrentPage.BackgroundTextureScale = pageModel.BackgroundTextureScale;
		CurrentPage.Elements = pageModel.Elements;
	}

	private int ApplyProductToTemplate(ProductModel product, string templateName)
	{
		int num = 0;
		List<CanvasElementModel> list = CurrentPage.Elements.Where((CanvasElementModel element) => element.Kind == ElementKind.Text && PlaceholderName(element).Contains("製品画像", StringComparison.OrdinalIgnoreCase)).ToList();
		foreach (CanvasElementModel element in CurrentPage.Elements)
		{
			string text = PlaceholderName(element);
			if (element.Kind == ElementKind.QrCode && (text.Contains("QR", StringComparison.OrdinalIgnoreCase) || element.Name.Contains("QR", StringComparison.OrdinalIgnoreCase)))
			{
				if (!string.IsNullOrWhiteSpace(product.Url))
				{
					element.QrContent = product.Url;
					num++;
				}
			}
			else if (element.Kind == ElementKind.Text && !list.Contains(element))
			{
				string text2 = ProductTemplateValue(text, product, templateName);
				if (!string.IsNullOrWhiteSpace(text2))
				{
					element.Text = text2;
					num++;
				}
			}
		}
		string text3 = ResolveProductMainImage(product);
		if (!string.IsNullOrWhiteSpace(text3) && File.Exists(text3))
		{
			byte[] array = File.ReadAllBytes(text3);
			BitmapImage bitmapImage = LoadBitmap(array);
			foreach (CanvasElementModel item in list)
			{
				double num2 = Math.Max(5.0, item.WidthMm);
				double num3 = Math.Max(5.0, item.HeightMm);
				double num4 = (double)Math.Max(1, bitmapImage.PixelWidth) / (double)Math.Max(1, bitmapImage.PixelHeight);
				double num5 = num2;
				double num6 = num5 / num4;
				if (num6 > num3)
				{
					num6 = num3;
					num5 = num6 * num4;
				}
				CanvasElementModel canvasElementModel = new CanvasElementModel
				{
					Kind = ElementKind.Image,
					Name = UniqueName(product.ProductName + "画像"),
					PlaceholderKey = item.PlaceholderKey,
					Xmm = item.Xmm + (num2 - num5) / 2.0,
					Ymm = item.Ymm + (num3 - num6) / 2.0,
					WidthMm = num5,
					HeightMm = num6,
					Rotation = item.Rotation,
					Opacity = item.Opacity,
					PreserveAspectRatio = true,
					ZIndex = item.ZIndex
				};
				StoreImageForEditing(canvasElementModel, array, bitmapImage, text3);
				int index = CurrentPage.Elements.IndexOf(item);
				CurrentPage.Elements[index] = canvasElementModel;
				num++;
			}
		}
		for (int num7 = 0; num7 < CurrentPage.Elements.Count; num7++)
		{
			CurrentPage.Elements[num7].ZIndex = num7;
		}
		return num;
	}

	private static string PlaceholderName(CanvasElementModel element)
	{
		if (!string.IsNullOrWhiteSpace(element.PlaceholderKey))
		{
			return element.PlaceholderKey.Trim();
		}
		string text = element.Name.Trim();
		bool flag;
		switch (text)
		{
		case "URL":
		case "製品名":
		case "製品A":
		case "製品B":
		case "注意事項":
		case "主要仕様":
		case "主な特徴":
		case "製品特徴":
		case "製品画像":
		case "販売トーク":
		case "QRコード":
		case "ブランド名":
		case "製品画像A":
		case "製品画像B":
		case "キャッチコピー":
		case "価格":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return string.Empty;
		}
		return text;
	}

	private static string ProductTemplateValue(string key, ProductModel product, string templateName)
	{
		string result = CleanMultiline(product.Features);
		string result2 = BuildProductSpecifications(product);
		bool flag = key.Contains("製品名", StringComparison.OrdinalIgnoreCase);
		if (!flag)
		{
			bool flag2 = ((key == "製品A" || key == "製品B") ? true : false);
			flag = flag2;
		}
		if (flag)
		{
			return string.Join(" / ", new string[2] { product.ProductName, product.ModelNumber }.Where((string value) => !string.IsNullOrWhiteSpace(value)));
		}
		if (key.Contains("ブランド", StringComparison.OrdinalIgnoreCase))
		{
			return product.BrandName;
		}
		if (key.Contains("キャッチ", StringComparison.OrdinalIgnoreCase) || key.Contains("機能コピー", StringComparison.OrdinalIgnoreCase))
		{
			return product.CatchCopy;
		}
		if (key.Contains("価格", StringComparison.OrdinalIgnoreCase))
		{
			if (!product.Price.HasValue)
			{
				return string.Empty;
			}
			return $"￥{product.Price.Value:N0}";
		}
		if (key.Contains("発売日", StringComparison.OrdinalIgnoreCase))
		{
			return product.ReleaseDate?.ToString("yyyy年M月d日") ?? string.Empty;
		}
		if (key.Contains("URL", StringComparison.OrdinalIgnoreCase))
		{
			return product.Url;
		}
		if (key.Contains("販売トーク", StringComparison.OrdinalIgnoreCase))
		{
			return product.SalesTalk;
		}
		if (key.Contains("注意", StringComparison.OrdinalIgnoreCase))
		{
			return product.Notes;
		}
		if (key.Contains("説明文", StringComparison.OrdinalIgnoreCase) || key.Contains("ブランド説明", StringComparison.OrdinalIgnoreCase))
		{
			return product.Features;
		}
		if (key.Contains("主要仕様", StringComparison.OrdinalIgnoreCase) || key.Contains("比較情報", StringComparison.OrdinalIgnoreCase))
		{
			return result2;
		}
		if (key.Contains("仕様説明", StringComparison.OrdinalIgnoreCase))
		{
			return product.Specifications;
		}
		if (key.Contains("機能名", StringComparison.OrdinalIgnoreCase))
		{
			return templateName switch
			{
				"防水訴求" => "WATERPROOF", 
				"バッテリー訴求" => "LONG BATTERY", 
				"ノイズキャンセリング訴求" => "NOISE CANCELING", 
				_ => product.Category, 
			};
		}
		if (key.Contains("主な特徴", StringComparison.OrdinalIgnoreCase) || key.Contains("製品特徴", StringComparison.OrdinalIgnoreCase))
		{
			return result;
		}
		if (key.StartsWith("特徴", StringComparison.OrdinalIgnoreCase) && int.TryParse(new string(key.Where(char.IsDigit).ToArray()), out var result3))
		{
			string[] array = SplitProductFeatures(product.Features);
			if (result3 <= 0 || result3 > array.Length)
			{
				return string.Empty;
			}
			return array[result3 - 1];
		}
		if (key.Contains("主要", StringComparison.OrdinalIgnoreCase))
		{
			if (templateName == "防水訴求")
			{
				return product.Waterproof;
			}
			if (templateName == "バッテリー訴求")
			{
				return product.Battery;
			}
			return product.Specifications;
		}
		return string.Empty;
	}

	private static string[] SplitProductFeatures(string value)
	{
		return (from part in value.Split(new char[5] { '\r', '\n', '・', '●', '■' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			where !string.IsNullOrWhiteSpace(part)
			select part).Take(6).ToArray();
	}

	private static string CleanMultiline(string value)
	{
		string[] array = SplitProductFeatures(value);
		if (array.Length <= 1)
		{
			return value;
		}
		return string.Join("\n", array.Select((string part) => "✓ " + part));
	}

	private static string BuildProductSpecifications(ProductModel product)
	{
		return string.Join("\n", new string[5]
		{
			product.Specifications,
			string.IsNullOrWhiteSpace(product.Codec) ? "" : ("コーデック：" + product.Codec),
			string.IsNullOrWhiteSpace(product.Waterproof) ? "" : ("防水・防塵：" + product.Waterproof),
			string.IsNullOrWhiteSpace(product.Battery) ? "" : ("バッテリー：" + product.Battery),
			string.IsNullOrWhiteSpace(product.Weight) ? "" : ("重量：" + product.Weight)
		}.Where((string value) => !string.IsNullOrWhiteSpace(value)));
	}

	private void SaveTemplate_Click(object sender, RoutedEventArgs e)
	{
		string text = TextPromptDialog.Show(this, "テンプレートとして保存", "テンプレート名", _project.ProjectName);
		if (!string.IsNullOrWhiteSpace(text))
		{
			_templates.SaveTemplate(_project, text);
			TemplateCombo.ItemsSource = _templates.BuiltInNames.Concat(_templates.UserTemplates()).ToList();
			StatusText.Text = "テンプレートを保存しました";
		}
	}

	private void SaveReusableBlock_Click(object sender, RoutedEventArgs e)
	{
		List<CanvasElementModel> list = (from x in CurrentPage.Elements
			where _selectedIds.Contains(x.Id)
			orderby x.ZIndex
			select x).ToList();
		if (list.Count == 0)
		{
			MessageBox.Show("保存するオブジェクトを選択してください。", "再利用ブロック", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		string initial = ((list.Count == 1) ? list[0].Name : $"{list[0].Name}ほか{list.Count - 1}点");
		string text = TextPromptDialog.Show(this, "再利用ブロックとして保存", "ブロック名", initial);
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}
		try
		{
			_reusableBlocks.Save(text, list);
			StatusText.Text = "再利用ブロック「" + text + "」を保存しました";
		}
		catch (Exception ex)
		{
			MessageBox.Show("ブロックを保存できませんでした。\n\n" + ex.Message, "再利用ブロック", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void OpenReusableBlocks_Click(object sender, RoutedEventArgs e)
	{
		OpenReusableBlocksAt(null);
	}

	private void OpenReusableBlocksAt(Point? requestedTopLeft)
	{
		ReusableBlockPickerDialog reusableBlockPickerDialog = new ReusableBlockPickerDialog(_reusableBlocks)
		{
			Owner = this
		};
		if (reusableBlockPickerDialog.ShowDialog() == true && reusableBlockPickerDialog.SelectedBlock != null)
		{
			InsertReusableBlock(reusableBlockPickerDialog.SelectedBlock, requestedTopLeft);
		}
	}

	private void InsertReusableBlock(ReusableBlockModel block, Point? requestedTopLeft)
	{
		if (block.Elements.Count == 0)
		{
			return;
		}
		PushUndo();
		Point point = VisibleInsertionTopLeft(block.WidthMm, block.HeightMm);
		double value = requestedTopLeft?.X ?? point.X;
		double value2 = requestedTopLeft?.Y ?? point.Y;
		value = Math.Clamp(value, Math.Min(0.0, CurrentPage.WidthMm - block.WidthMm), Math.Max(0.0, CurrentPage.WidthMm - Math.Min(block.WidthMm, 5.0)));
		value2 = Math.Clamp(value2, Math.Min(0.0, CurrentPage.HeightMm - block.HeightMm), Math.Max(0.0, CurrentPage.HeightMm - Math.Min(block.HeightMm, 5.0)));
		List<CanvasElementModel>? source = JsonSerializer.Deserialize<List<CanvasElementModel>>(JsonSerializer.Serialize(block.Elements, ProjectService.JsonOptions), ProjectService.JsonOptions) ?? new List<CanvasElementModel>();
		_selectedIds.Clear();
		foreach (CanvasElementModel item in source.OrderBy((CanvasElementModel x) => x.ZIndex))
		{
			item.Id = Guid.NewGuid();
			item.Name = UniqueName(item.Name);
			item.Xmm += value;
			item.Ymm += value2;
			item.ZIndex = CurrentPage.Elements.Count;
			CurrentPage.Elements.Add(item);
			_selectedIds.Add(item.Id);
		}
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
		UpdateSelectionVisuals();
		StatusText.Text = "再利用ブロック「" + block.Name + "」を挿入しました";
	}

	private void TemplateGallery_Click(object sender, RoutedEventArgs e)
	{
		HomeOverlay.Visibility = Visibility.Collapsed;
		if (LeftColumn.Width.Value == 0.0)
		{
			ToggleLeftPanel_Click(sender, e);
		}
		ShowInsertPalette("テンプレート");
		TemplateCombo.Focus();
	}

	private void UpdatePropertyPanel()
	{
		CanvasElementModel item = ActiveElement;
		_updatingProperties = true;
		try
		{
			PropertyFields.IsEnabled = item != null;
			UpdatePropertyTabs(item);
			NoSelectionText.Visibility = ((item != null) ? Visibility.Collapsed : Visibility.Visible);
			StackPanel textProperties = TextProperties;
			CanvasElementModel canvasElementModel = item;
			textProperties.Visibility = ((canvasElementModel == null || canvasElementModel.Kind != ElementKind.Text) ? Visibility.Collapsed : Visibility.Visible);
			StackPanel shapeProperties = ShapeProperties;
			CanvasElementModel canvasElementModel2 = item;
			shapeProperties.Visibility = ((canvasElementModel2 == null || canvasElementModel2.Kind != ElementKind.Shape) ? Visibility.Collapsed : Visibility.Visible);
			StackPanel imageProperties = ImageProperties;
			CanvasElementModel canvasElementModel3 = item;
			imageProperties.Visibility = ((canvasElementModel3 == null || canvasElementModel3.Kind != ElementKind.Image) ? Visibility.Collapsed : Visibility.Visible);
			StackPanel qrProperties = QrProperties;
			CanvasElementModel canvasElementModel4 = item;
			qrProperties.Visibility = ((canvasElementModel4 == null || canvasElementModel4.Kind != ElementKind.QrCode) ? Visibility.Collapsed : Visibility.Visible);
			if (item == null)
			{
				return;
			}
			NameBox.Text = item.Name;
			XBox.Text = item.Xmm.ToString("0.##");
			YBox.Text = item.Ymm.ToString("0.##");
			WidthBox.Text = item.WidthMm.ToString("0.##");
			HeightBox.Text = item.HeightMm.ToString("0.##");
			RotationBox.Text = item.Rotation.ToString("0.#");
			SkewXBox.Text = item.SkewX.ToString("0.#");
			SkewYBox.Text = item.SkewY.ToString("0.#");
			OpacityBox.Text = (item.Opacity * 100.0).ToString("0");
			AspectCheck.IsChecked = item.PreserveAspectRatio;
			LockCheck.IsChecked = item.IsLocked;
			VisibleCheck.IsChecked = item.IsVisible;
			if (item.Kind == ElementKind.Text)
			{
				TextContentBox.Text = item.Text;
				FontCombo.SelectedItem = item.FontFamily;
				FontCombo.Text = item.FontFamily;
				FavoriteFontButton.Content = (_settings.Current.FavoriteFonts.Any((string x) => string.Equals(x, item.FontFamily, StringComparison.OrdinalIgnoreCase)) ? "★" : "☆");
				FontSizeBox.Text = item.FontSizePt.ToString("0.#");
				int displayedWeight = NormalizeFontWeight((item.Bold && item.FontWeightValue < 700) ? 700 : item.FontWeightValue);
				if (_fontWeightSlider != null)
				{
					_fontWeightSlider.Value = displayedWeight;
				}
				if (_fontWeightValueText != null)
				{
					_fontWeightValueText.Text = displayedWeight.ToString(CultureInfo.InvariantCulture);
				}
				if (_fontWeightCombo != null)
				{
					_fontWeightCombo.SelectedItem = _fontWeightCombo.Items.OfType<ComboBoxItem>().FirstOrDefault((ComboBoxItem option) => option.Tag is int num && num == displayedWeight);
				}
				if (_characterSpacingBox != null)
				{
					_characterSpacingBox.Text = item.CharacterSpacing.ToString("0.##");
				}
				if (_lineSpacingBox != null)
				{
					_lineSpacingBox.Text = item.LineSpacingPt.ToString("0.##");
				}
				TextColorBox.Text = item.TextColor;
				TextColorButton.Background = BrushFrom(item.TextColor, Brushes.Transparent);
				TextBackgroundBox.Text = item.TextBackground;
				TextBackgroundButton.Background = BrushFrom(item.TextBackground, Brushes.Transparent);
				TextOutlineColorBox.Text = item.TextOutlineColor;
				TextOutlineColorButton.Background = BrushFrom(item.TextOutlineColor, Brushes.Transparent);
				TextOutlineThicknessBox.Text = item.TextOutlineThicknessPt.ToString("0.#");
				TextExtrusionColorBox.Text = item.TextExtrusionColor;
				TextExtrusionColorButton.Background = BrushFrom(item.TextExtrusionColor, Brushes.Transparent);
				TextExtrusionDepthBox.Text = item.TextExtrusionDepthPt.ToString("0.#");
				TextExtrusionAngleBox.Text = item.TextExtrusionAngle.ToString("0.#");
				BoldToggle.IsChecked = item.Bold;
				ItalicToggle.IsChecked = item.Italic;
				UnderlineToggle.IsChecked = item.Underline;
			}
			else if (item.Kind == ElementKind.Shape)
			{
				FillColorBox.Text = item.FillColor;
				StrokeColorBox.Text = item.StrokeColor;
				FillColorButton.Background = BrushFrom(item.FillColor, Brushes.Transparent);
				StrokeColorButton.Background = BrushFrom(item.StrokeColor, Brushes.Transparent);
				StrokeThicknessBox.Text = item.StrokeThicknessPt.ToString("0.#");
				CornerRadiusBox.Text = item.CornerRadiusMm.ToString("0.#");
				CornerLeftBox.Text = ((item.CornerRadiusTopLeftMm < 0.0) ? string.Empty : item.CornerRadiusTopLeftMm.ToString("0.#"));
				CornerRightBox.Text = ((item.CornerRadiusTopRightMm < 0.0) ? string.Empty : item.CornerRadiusTopRightMm.ToString("0.#"));
				PanelRowsBox.Text = item.PanelRows.ToString();
				PanelColumnsBox.Text = item.PanelColumns.ToString();
				PanelRowSplitsBox.Text = string.Join(", ", item.PanelRowSplits.Select((double x) => x.ToString("0.#")));
				PanelColumnSplitsBox.Text = string.Join(", ", item.PanelColumnSplits.Select((double x) => x.ToString("0.#")));
				ShapeExtrusionColorBox.Text = item.ShapeExtrusionColor;
				ShapeExtrusionColorButton.Background = BrushFrom(item.ShapeExtrusionColor, Brushes.Transparent);
				ShapeExtrusionDepthBox.Text = item.ShapeExtrusionDepthPt.ToString("0.#");
				ShapeExtrusionAngleBox.Text = item.ShapeExtrusionAngle.ToString("0.#");
			}
			else if (item.Kind == ElementKind.Image)
			{
				ImageDpiText.Text = $"実効DPI: {item.EffectiveDpi:0} dpi";
				ImageDpiText.Foreground = ((item.EffectiveDpi < 150.0) ? Brushes.Firebrick : ((item.EffectiveDpi < 200.0) ? Brushes.DarkOrange : Brushes.ForestGreen));
				ImageSizeText.Text = $"画像サイズ: {item.ImagePixelWidth} × {item.ImagePixelHeight}px";
			}
			else if (item.Kind == ElementKind.QrCode)
			{
				QrContentBox.Text = item.QrContent;
				QrForegroundBox.Text = item.QrForeground;
				QrBackgroundBox.Text = item.QrBackground;
				QrForegroundButton.Background = BrushFrom(item.QrForeground, Brushes.Transparent);
				QrBackgroundButton.Background = BrushFrom(item.QrBackground, Brushes.Transparent);
				ComboBox qrLevelCombo = QrLevelCombo;
				qrLevelCombo.SelectedIndex = item.QrErrorCorrection switch
				{
					"L" => 0, 
					"Q" => 2, 
					"H" => 3, 
					_ => 1, 
				};
			}
		}
		finally
		{
			_updatingProperties = false;
		}
	}

	private void GeneralProperty_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		switch (frameworkElement.Tag?.ToString())
		{
		case "Name":
			activeElement.Name = (string.IsNullOrWhiteSpace(NameBox.Text) ? activeElement.Name : NameBox.Text.Trim());
			break;
		case "X":
		{
			if (TryNumber(XBox.Text, out var value5))
			{
				activeElement.Xmm = value5;
			}
			break;
		}
		case "Y":
		{
			if (TryNumber(YBox.Text, out var value7))
			{
				activeElement.Ymm = value7;
			}
			break;
		}
		case "Width":
		{
			if (TryNumber(WidthBox.Text, out var value3))
			{
				activeElement.WidthMm = Math.Max(1.0, value3);
			}
			break;
		}
		case "Height":
		{
			if (TryNumber(HeightBox.Text, out var value8))
			{
				activeElement.HeightMm = Math.Max(1.0, value8);
			}
			break;
		}
		case "Rotation":
		{
			if (TryNumber(RotationBox.Text, out var value6))
			{
				activeElement.Rotation = value6 % 360.0;
			}
			break;
		}
		case "SkewX":
		{
			if (TryNumber(SkewXBox.Text, out var value4))
			{
				activeElement.SkewX = Math.Clamp(value4, -80.0, 80.0);
			}
			break;
		}
		case "SkewY":
		{
			if (TryNumber(SkewYBox.Text, out var value2))
			{
				activeElement.SkewY = Math.Clamp(value2, -80.0, 80.0);
			}
			break;
		}
		case "Opacity":
		{
			if (TryNumber(OpacityBox.Text, out var value))
			{
				activeElement.Opacity = Math.Clamp(value / 100.0, 0.0, 1.0);
			}
			break;
		}
		}
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
		UpdatePropertyPanel();
		UpdateValidationCount();
		_generalPropertyUndoCaptured = false;
	}

	private void CheckProperty_Click(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && sender is CheckBox checkBox)
		{
			PushUndo();
			switch (checkBox.Tag?.ToString())
			{
			case "Aspect":
				activeElement.PreserveAspectRatio = checkBox.IsChecked == true;
				break;
			case "Lock":
				activeElement.IsLocked = checkBox.IsChecked == true;
				break;
			case "Visible":
				activeElement.IsVisible = checkBox.IsChecked == true;
				break;
			}
			MarkDirty();
			RebuildCanvas();
			RefreshLayers();
			UpdatePropertyPanel();
		}
	}

	private void TextProperty_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Text || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		switch (frameworkElement.Tag?.ToString())
		{
		case "Text":
			activeElement.Text = TextContentBox.Text;
			break;
		case "FontSize":
		{
			if (TryNumber(FontSizeBox.Text, out var value6))
			{
				activeElement.FontSizePt = Math.Clamp(value6, 1.0, 300.0);
			}
			break;
		}
		case "CharacterSpacing":
		{
			if (_characterSpacingBox != null && TryNumber(_characterSpacingBox.Text, out var value3))
			{
				activeElement.CharacterSpacing = Math.Clamp(value3, -100.0, 300.0);
			}
			break;
		}
		case "LineSpacing":
		{
			if (_lineSpacingBox != null && TryNumber(_lineSpacingBox.Text, out var value5))
			{
				activeElement.LineSpacingPt = Math.Clamp(value5, -100.0, 300.0);
			}
			break;
		}
		case "TextColor":
			if (IsColor(TextColorBox.Text))
			{
				activeElement.TextColor = NormalizeColor(TextColorBox.Text);
			}
			break;
		case "TextBackground":
			if (IsColor(TextBackgroundBox.Text))
			{
				activeElement.TextBackground = NormalizeColor(TextBackgroundBox.Text);
			}
			break;
		case "OutlineColor":
			if (IsColor(TextOutlineColorBox.Text))
			{
				activeElement.TextOutlineColor = NormalizeColor(TextOutlineColorBox.Text);
			}
			break;
		case "OutlineThickness":
		{
			if (TryNumber(TextOutlineThicknessBox.Text, out var value4))
			{
				activeElement.TextOutlineThicknessPt = Math.Clamp(value4, 0.0, 8.0);
			}
			break;
		}
		case "ExtrusionColor":
			if (IsColor(TextExtrusionColorBox.Text))
			{
				activeElement.TextExtrusionColor = NormalizeColor(TextExtrusionColorBox.Text);
			}
			break;
		case "ExtrusionDepth":
		{
			if (TryNumber(TextExtrusionDepthBox.Text, out var value2))
			{
				activeElement.TextExtrusionDepthPt = Math.Clamp(value2, 0.0, 24.0);
			}
			break;
		}
		case "ExtrusionAngle":
		{
			if (TryNumber(TextExtrusionAngleBox.Text, out var value))
			{
				activeElement.TextExtrusionAngle = (value % 360.0 + 360.0) % 360.0;
			}
			break;
		}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
		UpdateValidationCount();
		if (string.Equals(frameworkElement.Tag?.ToString(), "FontSize", StringComparison.Ordinal))
		{
			base.Dispatcher.BeginInvoke(new Action(() => FitTextFrameToGlyphBounds(activeElement)), DispatcherPriority.Loaded);
		}
		if (string.Equals(frameworkElement.Tag?.ToString(), "Text", StringComparison.Ordinal))
		{
			_textContentUndoCaptured = false;
		}
	}

	private void TextContentBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_updatingProperties || ActiveElement is not { Kind: ElementKind.Text } activeElement)
		{
			return;
		}
		if (!_textContentUndoCaptured)
		{
			PushUndo();
			_textContentUndoCaptured = true;
		}
		activeElement.Text = TextContentBox.Text;
		MarkDirty();
		RebuildCanvas();
		UpdateValidationCount();
		_generalPropertyUndoCaptured = false;
	}

	private void GeneralProperty_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (_updatingProperties || ActiveElement is not CanvasElementModel activeElement || sender is not FrameworkElement field)
		{
			return;
		}
		string tag = field.Tag?.ToString() ?? string.Empty;
		if (!new[] { "X", "Y", "SkewX", "SkewY", "Width", "Height", "Rotation", "Opacity" }.Contains(tag))
		{
			return;
		}
		if (!_generalPropertyUndoCaptured)
		{
			PushUndo();
			_generalPropertyUndoCaptured = true;
		}
		if (!TryNumber(((TextBox)field).Text, out double value))
		{
			return;
		}
		switch (tag)
		{
		case "X": activeElement.Xmm = value; break;
		case "Y": activeElement.Ymm = value; break;
		case "SkewX": activeElement.SkewX = Math.Clamp(value, -80.0, 80.0); break;
		case "SkewY": activeElement.SkewY = Math.Clamp(value, -80.0, 80.0); break;
		case "Width": activeElement.WidthMm = Math.Max(1.0, value); break;
		case "Height": activeElement.HeightMm = Math.Max(1.0, value); break;
		case "Rotation": activeElement.Rotation = (value % 360.0 + 360.0) % 360.0; break;
		case "Opacity": activeElement.Opacity = Math.Clamp(value / 100.0, 0.0, 1.0); break;
		}
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
		UpdateStatus();
		UpdateValidationCount();
	}

	private void TextColorPicker_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Text)
		{
			string text = ShowProjectColor(activeElement.TextColor);
			if (text != null)
			{
				PushUndo();
				activeElement.TextColor = text;
				MarkDirty();
				RebuildCanvas();
				UpdatePropertyPanel();
				UpdateValidationCount();
			}
		}
	}

	private void TextEffectColorPicker_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Text || !(sender is FrameworkElement { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString();
		string text2 = ShowProjectColor(text switch
		{
			"TextBackground" => activeElement.TextBackground, 
			"OutlineColor" => activeElement.TextOutlineColor, 
			"ExtrusionColor" => activeElement.TextExtrusionColor, 
			_ => activeElement.TextColor, 
		});
		if (text2 != null)
		{
			PushUndo();
			switch (text)
			{
			case "TextBackground":
				activeElement.TextBackground = text2;
				break;
			case "OutlineColor":
				activeElement.TextOutlineColor = text2;
				break;
			case "ExtrusionColor":
				activeElement.TextExtrusionColor = text2;
				break;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
			UpdateValidationCount();
		}
	}

	private void TextBackgroundTransparent_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Text)
		{
			PushUndo();
			activeElement.TextBackground = "#00FFFFFF";
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void RefreshFontList()
	{
		List<string> list = (from x in Fonts.SystemFontFamilies.Select((FontFamily x) => x.Source).Concat(_embeddedFontFamilies.Keys).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			orderby x
			select x).ToList();
		List<string> priority = _settings.Current.FavoriteFonts.Concat(_settings.Current.RecentFonts).Where(list.Contains).Distinct<string>(StringComparer.OrdinalIgnoreCase)
			.ToList();
		FontCombo.ItemsSource = priority.Concat(list.Where((string x) => !priority.Contains<string>(x, StringComparer.OrdinalIgnoreCase))).ToList();
	}

	private FontFamily ResolveFontFamily(string family)
	{
		if (!_embeddedFontFamilies.TryGetValue(family, out FontFamily value))
		{
			return new FontFamily(family);
		}
		return value;
	}

	private void ActivateEmbeddedFonts()
	{
		_embeddedFontFamilies.Clear();
		string text = System.IO.Path.Combine(AppPaths.Assets, "EmbeddedFonts", _project.ProjectId.ToString("N"));
		Directory.CreateDirectory(text);
		foreach (EmbeddedFontModel embeddedFont in _project.EmbeddedFonts)
		{
			try
			{
				string text2 = ((System.IO.Path.GetExtension(embeddedFont.FileName).ToLowerInvariant() == ".otf") ? ".otf" : ".ttf");
				string path = System.IO.Path.Combine(text, embeddedFont.Sha256 + text2);
				if (!File.Exists(path))
				{
					File.WriteAllBytes(path, Convert.FromBase64String(embeddedFont.DataBase64));
				}
				Uri baseUri = new Uri(text.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, UriKind.Absolute);
				_embeddedFontFamilies[embeddedFont.FamilyName] = new FontFamily(baseUri, "./#" + embeddedFont.FamilyName);
			}
			catch (Exception ex)
			{
				LogService.Error("Embedded font activation failed", ex);
			}
		}
		if (FontCombo != null)
		{
			RefreshFontList();
		}
	}

	private void AddFont_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Title = "埋め込むフォントを選択",
			Filter = "フォント (*.ttf;*.otf)|*.ttf;*.otf",
			Multiselect = true
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		List<string> list = new List<string>();
		string[] fileNames = openFileDialog.FileNames;
		foreach (string text in fileNames)
		{
			try
			{
				GlyphTypeface glyphTypeface = new GlyphTypeface(new Uri(text));
				if (glyphTypeface.EmbeddingRights.ToString().Contains("Restricted", StringComparison.OrdinalIgnoreCase))
				{
					MessageBox.Show(System.IO.Path.GetFileName(text) + " はライセンスにより埋め込みできません。", "フォント埋め込み", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					continue;
				}
				string text2 = glyphTypeface.Win32FamilyNames.Values.FirstOrDefault() ?? glyphTypeface.FamilyNames.Values.FirstOrDefault() ?? System.IO.Path.GetFileNameWithoutExtension(text);
				byte[] array = File.ReadAllBytes(text);
				string hash = Convert.ToHexString(SHA256.HashData(array));
				if (!_project.EmbeddedFonts.Any((EmbeddedFontModel x) => x.Sha256 == hash))
				{
					_project.EmbeddedFonts.Add(new EmbeddedFontModel
					{
						FamilyName = text2,
						FileName = System.IO.Path.GetFileName(text),
						DataBase64 = Convert.ToBase64String(array),
						Sha256 = hash
					});
					list.Add(text2);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(System.IO.Path.GetFileName(text) + " を追加できませんでした。\n" + ex.Message, "フォント埋め込み");
			}
		}
		if (list.Count != 0)
		{
			PushUndo();
			ActivateEmbeddedFonts();
			MarkDirty();
			CanvasElementModel activeElement = ActiveElement;
			if (activeElement != null && activeElement.Kind == ElementKind.Text)
			{
				activeElement.FontFamily = list[0];
				FontCombo.Text = list[0];
				RebuildCanvas();
				UpdatePropertyPanel();
			}
			StatusText.Text = $"{list.Count}個のフォントをプロジェクトへ埋め込みました";
		}
	}

	private void FontCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Text && !string.IsNullOrWhiteSpace(FontCombo.SelectedItem?.ToString()))
		{
			string font = FontCombo.SelectedItem.ToString();
			PushUndo();
			activeElement.FontFamily = font;
			_settings.Current.RecentFonts.RemoveAll((string x) => string.Equals(x, font, StringComparison.OrdinalIgnoreCase));
			_settings.Current.RecentFonts.Insert(0, font);
			if (_settings.Current.RecentFonts.Count > 12)
			{
				_settings.Current.RecentFonts.RemoveRange(12, _settings.Current.RecentFonts.Count - 12);
			}
			_settings.Save();
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void FavoriteFontButton_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		string font = ((activeElement != null && activeElement.Kind == ElementKind.Text) ? activeElement.FontFamily : FontCombo.Text);
		if (!string.IsNullOrWhiteSpace(font))
		{
			string text = _settings.Current.FavoriteFonts.FirstOrDefault((string x) => string.Equals(x, font, StringComparison.OrdinalIgnoreCase));
			if (text != null)
			{
				_settings.Current.FavoriteFonts.Remove(text);
			}
			else
			{
				_settings.Current.FavoriteFonts.Insert(0, font);
			}
			_settings.Save();
			FavoriteFontButton.Content = ((text == null) ? "★" : "☆");
			RefreshFontList();
			FontCombo.Text = font;
		}
	}

	private void TextToggle_Click(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Text && sender is ToggleButton toggleButton)
		{
			PushUndo();
			switch (toggleButton.Tag?.ToString())
			{
			case "Bold":
				activeElement.Bold = toggleButton.IsChecked == true;
				activeElement.FontWeightValue = (activeElement.Bold ? Math.Max(700, activeElement.FontWeightValue) : 400);
				break;
			case "Italic":
				activeElement.Italic = toggleButton.IsChecked == true;
				break;
			case "Underline":
				activeElement.Underline = toggleButton.IsChecked == true;
				break;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void TextAlign_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Text && sender is FrameworkElement frameworkElement)
		{
			PushUndo();
			activeElement.TextAlignment = frameworkElement.Tag?.ToString() ?? "Center";
			MarkDirty();
			RebuildCanvas();
		}
	}

	private void ShapeProperty_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Shape || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		switch (frameworkElement.Tag?.ToString())
		{
		case "Fill":
			if (IsColor(FillColorBox.Text))
			{
				activeElement.FillColor = NormalizeColor(FillColorBox.Text);
			}
			break;
		case "Stroke":
			if (IsColor(StrokeColorBox.Text))
			{
				activeElement.StrokeColor = NormalizeColor(StrokeColorBox.Text);
			}
			break;
		case "StrokeThickness":
		{
			if (TryNumber(StrokeThicknessBox.Text, out var value5))
			{
				activeElement.StrokeThicknessPt = Math.Clamp(value5, 0.0, 30.0);
			}
			break;
		}
		case "CornerRadius":
		{
			if (TryNumber(CornerRadiusBox.Text, out var value3))
			{
				activeElement.CornerRadiusMm = Math.Clamp(value3, 0.0, 100.0);
			}
			break;
		}
		case "CornerLeft":
		{
			double value4;
			double num = (activeElement.CornerRadiusBottomLeftMm = (TryNumber(CornerLeftBox.Text, out value4) ? Math.Clamp(value4, 0.0, 100.0) : (-1.0)));
			double cornerRadiusTopLeftMm = num;
			activeElement.CornerRadiusTopLeftMm = cornerRadiusTopLeftMm;
			break;
		}
		case "CornerRight":
		{
			double value6;
			double num = (activeElement.CornerRadiusBottomRightMm = (TryNumber(CornerRightBox.Text, out value6) ? Math.Clamp(value6, 0.0, 100.0) : (-1.0)));
			double cornerRadiusTopRightMm = num;
			activeElement.CornerRadiusTopRightMm = cornerRadiusTopRightMm;
			break;
		}
		case "PanelRows":
		{
			if (int.TryParse(PanelRowsBox.Text, out var result2))
			{
				activeElement.PanelRows = Math.Clamp(result2, 1, 12);
			}
			break;
		}
		case "PanelColumns":
		{
			if (int.TryParse(PanelColumnsBox.Text, out var result))
			{
				activeElement.PanelColumns = Math.Clamp(result, 1, 12);
			}
			break;
		}
		case "PanelRowSplits":
			activeElement.PanelRowSplits = ParseSplits(PanelRowSplitsBox.Text);
			activeElement.PanelRows = Math.Max(1, activeElement.PanelRowSplits.Count + 1);
			break;
		case "PanelColumnSplits":
			activeElement.PanelColumnSplits = ParseSplits(PanelColumnSplitsBox.Text);
			activeElement.PanelColumns = Math.Max(1, activeElement.PanelColumnSplits.Count + 1);
			break;
		case "ShapeExtrusionColor":
			if (IsColor(ShapeExtrusionColorBox.Text))
			{
				activeElement.ShapeExtrusionColor = NormalizeColor(ShapeExtrusionColorBox.Text);
			}
			break;
		case "ShapeExtrusionDepth":
		{
			if (TryNumber(ShapeExtrusionDepthBox.Text, out var value2))
			{
				activeElement.ShapeExtrusionDepthPt = Math.Clamp(value2, 0.0, 24.0);
			}
			break;
		}
		case "ShapeExtrusionAngle":
		{
			if (TryNumber(ShapeExtrusionAngleBox.Text, out var value))
			{
				activeElement.ShapeExtrusionAngle = (value % 360.0 + 360.0) % 360.0;
			}
			break;
		}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private static List<double> ParseSplits(string text)
	{
		double result;
		return (from x in (from x in (from x in text.Split(',', '、', ';')
					select double.TryParse(x.Trim(), out result) ? result : double.NaN).Where(double.IsFinite)
				select Math.Clamp(x, 0.1, 99.9)).Distinct()
			orderby x
			select x).ToList();
	}

	private void ShapeColorPicker_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Shape || !(sender is FrameworkElement { Tag: var tag }))
		{
			return;
		}
		string? obj = tag?.ToString();
		bool flag = obj == "Fill";
		bool flag2 = obj == "ShapeExtrusionColor";
		string text = ShowProjectColor(flag2 ? activeElement.ShapeExtrusionColor : (flag ? activeElement.FillColor : activeElement.StrokeColor));
		if (text != null)
		{
			PushUndo();
			if (flag2)
			{
				activeElement.ShapeExtrusionColor = text;
			}
			else if (flag)
			{
				activeElement.FillColor = text;
			}
			else
			{
				activeElement.StrokeColor = text;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void EditShapePoints_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Shape)
		{
			return;
		}
		ShapePointEditorDialog shapePointEditorDialog = new ShapePointEditorDialog(activeElement, _settings.Current.GridSizeMm, _settings.Current.VertexSnapMode)
		{
			Owner = this
		};
		if (shapePointEditorDialog.ShowDialog() != true || shapePointEditorDialog.Result == null)
		{
			return;
		}
		PushUndo();
		if (shapePointEditorDialog.RestoreOriginalShape && !shapePointEditorDialog.HadOriginalPoints && shapePointEditorDialog.OriginalShapeType != "CustomPath")
		{
			activeElement.ShapePoints.Clear();
			activeElement.ShapeClosed = shapePointEditorDialog.InitialClosedPath;
			activeElement.ShapeType = shapePointEditorDialog.OriginalShapeType;
		}
		else
		{
			activeElement.ShapePoints = shapePointEditorDialog.Result;
			activeElement.ShapeClosed = shapePointEditorDialog.IsClosedPath;
			if (!IsPanelElement(activeElement))
			{
				activeElement.ShapeType = "CustomPath";
			}
		}
		activeElement.PreserveAspectRatio = false;
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private void EditCorners_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Shape)
		{
			CornerEditorDialog cornerEditorDialog = new CornerEditorDialog(activeElement)
			{
				Owner = this
			};
			if (cornerEditorDialog.ShowDialog() == true)
			{
				PushUndo();
				activeElement.CornerRadiusTopLeftMm = cornerEditorDialog.TopLeft;
				activeElement.CornerRadiusTopRightMm = cornerEditorDialog.TopRight;
				activeElement.CornerRadiusBottomRightMm = cornerEditorDialog.BottomRight;
				activeElement.CornerRadiusBottomLeftMm = cornerEditorDialog.BottomLeft;
				activeElement.CornerRadiusMm = (cornerEditorDialog.TopLeft + cornerEditorDialog.TopRight + cornerEditorDialog.BottomRight + cornerEditorDialog.BottomLeft) / 4.0;
				MarkDirty();
				RebuildCanvas();
				UpdatePropertyPanel();
			}
		}
	}

	private void EditPanelDividers_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Shape || activeElement.ShapeType == "Line")
		{
			MessageBox.Show("線以外の図形を選択してください。", "パネル分割線");
			return;
		}
		bool num = IsPanelElement(activeElement);
		CanvasElementModel canvasElementModel = (num ? activeElement : CloneElement(activeElement));
		if (!num)
		{
			canvasElementModel.PanelEnabled = true;
			canvasElementModel.PanelRows = 2;
			canvasElementModel.PanelColumns = 1;
			canvasElementModel.PanelRowSplits = new List<double> { 50.0 };
			canvasElementModel.PanelColumnSplits = new List<double>();
		}
		PanelDividerEditorDialog panelDividerEditorDialog = new PanelDividerEditorDialog(canvasElementModel, _settings.Current.GridSizeMm)
		{
			Owner = this
		};
		if (panelDividerEditorDialog.ShowDialog() == true)
		{
			PushUndo();
			activeElement.PanelEnabled = true;
			activeElement.PanelRowSplits = panelDividerEditorDialog.RowSplits.OrderBy((double x) => x).ToList();
			activeElement.PanelColumnSplits = panelDividerEditorDialog.ColumnSplits.OrderBy((double x) => x).ToList();
			activeElement.PanelRows = activeElement.PanelRowSplits.Count + 1;
			activeElement.PanelColumns = activeElement.PanelColumnSplits.Count + 1;
			activeElement.PanelDividerColor = panelDividerEditorDialog.DividerColor;
			activeElement.PanelDividerThicknessPt = panelDividerEditorDialog.DividerThicknessPt;
			activeElement.PanelDividerOpacity = panelDividerEditorDialog.DividerOpacity;
			activeElement.PanelDividerStyle = panelDividerEditorDialog.DividerStyle;
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void EditPanelCellColors_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Shape || !IsPanelElement(activeElement))
		{
			MessageBox.Show("パネル図形を選択してください。", "区画ごとの色");
			return;
		}
		PanelCellColorDialog panelCellColorDialog = new PanelCellColorDialog(activeElement.PanelRows, activeElement.PanelColumns, activeElement.PanelCellColors, activeElement.PanelCellRoles, activeElement.FillColor)
		{
			Owner = this
		};
		if (panelCellColorDialog.ShowDialog() == true)
		{
			PushUndo();
			activeElement.PanelCellColors = panelCellColorDialog.Result.ToList();
			activeElement.PanelCellRoles = panelCellColorDialog.Roles.ToList();
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void EditElementTexture_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null)
		{
			return;
		}
		TexturePickerDialog texturePickerDialog = new TexturePickerDialog(activeElement.TextureName, activeElement.TextureDataBase64, activeElement.TextureOpacity, activeElement.TextureScale)
		{
			Owner = this
		};
		if (texturePickerDialog.ShowDialog() != true)
		{
			return;
		}
		PushUndo();
		foreach (CanvasElementModel item in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)))
		{
			item.TextureName = texturePickerDialog.TextureName;
			item.TextureDataBase64 = texturePickerDialog.TextureDataBase64;
			item.TextureOpacity = texturePickerDialog.TextureOpacity;
			item.TextureScale = texturePickerDialog.TextureScale;
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private void EditPageTexture_Click(object sender, RoutedEventArgs e)
	{
		PageModel currentPage = CurrentPage;
		TexturePickerDialog texturePickerDialog = new TexturePickerDialog(currentPage.BackgroundTextureName, currentPage.BackgroundTextureDataBase64, currentPage.BackgroundTextureOpacity, currentPage.BackgroundTextureScale)
		{
			Owner = this
		};
		if (texturePickerDialog.ShowDialog() == true)
		{
			PushUndo();
			currentPage.BackgroundTextureName = texturePickerDialog.TextureName;
			currentPage.BackgroundTextureDataBase64 = texturePickerDialog.TextureDataBase64;
			currentPage.BackgroundTextureOpacity = texturePickerDialog.TextureOpacity;
			currentPage.BackgroundTextureScale = texturePickerDialog.TextureScale;
			MarkDirty();
			RefreshAll();
		}
	}

	private void QrProperty_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_updatingProperties)
		{
			return;
		}
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.QrCode || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		switch (frameworkElement.Tag?.ToString())
		{
		case "Content":
			activeElement.QrContent = QrContentBox.Text.Trim();
			break;
		case "Foreground":
			if (IsColor(QrForegroundBox.Text))
			{
				activeElement.QrForeground = NormalizeColor(QrForegroundBox.Text);
			}
			break;
		case "Background":
			if (IsColor(QrBackgroundBox.Text))
			{
				activeElement.QrBackground = NormalizeColor(QrBackgroundBox.Text);
			}
			break;
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
		UpdateValidationCount();
	}

	private void QrColorPicker_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.QrCode || !(sender is FrameworkElement { Tag: var tag }))
		{
			return;
		}
		bool flag = tag?.ToString() == "Foreground";
		string text = ShowProjectColor(flag ? activeElement.QrForeground : activeElement.QrBackground);
		if (text != null)
		{
			PushUndo();
			if (flag)
			{
				activeElement.QrForeground = text;
			}
			else
			{
				activeElement.QrBackground = text;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
			UpdateValidationCount();
		}
	}

	private void QrLevelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_updatingProperties)
		{
			CanvasElementModel activeElement = ActiveElement;
			if (activeElement != null && activeElement.Kind == ElementKind.QrCode && QrLevelCombo.SelectedItem is ComboBoxItem comboBoxItem)
			{
				PushUndo();
				activeElement.QrErrorCorrection = comboBoxItem.Content?.ToString() ?? "M";
				MarkDirty();
				RebuildCanvas();
			}
		}
	}

	private void UpdateQr_Click(object sender, RoutedEventArgs e)
	{
		QrProperty_LostFocus(QrContentBox, e);
		QrProperty_LostFocus(QrForegroundBox, e);
		QrProperty_LostFocus(QrBackgroundBox, e);
	}

	private void ReplaceImage_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Image)
		{
			return;
		}
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "画像|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|すべて|*.*"
		};
		if (openFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			byte[] array = File.ReadAllBytes(openFileDialog.FileName);
			BitmapImage bitmapImage = LoadBitmap(array);
			PushUndo();
			StoreImageForEditing(activeElement, array, bitmapImage, openFileDialog.FileName);
			activeElement.ImageOriginalDataBase64 = null;
			activeElement.ImageCutoutSettingsJson = null;
			if (activeElement.PreserveAspectRatio && bitmapImage.PixelWidth > 0)
			{
				activeElement.HeightMm = activeElement.WidthMm * (double)bitmapImage.PixelHeight / (double)bitmapImage.PixelWidth;
			}
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
			UpdateValidationCount();
			if (activeElement.ImageUsesLinkedOriginal)
			{
				StatusText.Text = $"大容量画像を軽量プレビューで差し替えました（元画像 {(double)activeElement.ImageSourceBytes / 1024.0 / 1024.0:0.0}MB）";
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("画像を差し替えできませんでした。\n" + ex.Message);
		}
	}

	private void RemoveImageBackground_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement == null || activeElement.Kind != ElementKind.Image)
		{
			return;
		}
		try
		{
			byte[] array;
			if (!string.IsNullOrWhiteSpace(activeElement.ImageOriginalDataBase64))
			{
				array = Convert.FromBase64String(activeElement.ImageOriginalDataBase64);
			}
			else if (!string.IsNullOrWhiteSpace(activeElement.ImageDataBase64))
			{
				array = Convert.FromBase64String(activeElement.ImageDataBase64);
			}
			else
			{
				if (string.IsNullOrWhiteSpace(activeElement.ImageSourcePath) || !File.Exists(activeElement.ImageSourcePath))
				{
					MessageBox.Show("元画像が見つかりません。", "背景を抜く");
					return;
				}
				array = File.ReadAllBytes(activeElement.ImageSourcePath);
			}
			BackgroundRemovalDialog backgroundRemovalDialog = new BackgroundRemovalDialog(array, activeElement.ImageCutoutSettingsJson)
			{
				Owner = this
			};
			if (backgroundRemovalDialog.ShowDialog() == true && backgroundRemovalDialog.ResultBytes != null)
			{
				PushUndo();
				activeElement.ImageOriginalDataBase64 = Convert.ToBase64String(array);
				activeElement.ImageDataBase64 = Convert.ToBase64String(backgroundRemovalDialog.ResultBytes);
				activeElement.ImageCutoutSettingsJson = backgroundRemovalDialog.ResultSettingsJson;
				activeElement.ImageSourcePath = (string.IsNullOrWhiteSpace(activeElement.ImageSourcePath) ? "非破壊パス抜き.png" : activeElement.ImageSourcePath);
				MarkDirty();
				RebuildCanvas();
				UpdatePropertyPanel();
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("背景を抜けませんでした。\n" + ex.Message, "背景を抜く", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ResetImageCutout_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Image && !string.IsNullOrWhiteSpace(activeElement.ImageOriginalDataBase64))
		{
			PushUndo();
			activeElement.ImageDataBase64 = activeElement.ImageOriginalDataBase64;
			activeElement.ImageOriginalDataBase64 = null;
			activeElement.ImageCutoutSettingsJson = null;
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private void ResetImageRatio_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel activeElement = ActiveElement;
		if (activeElement != null && activeElement.Kind == ElementKind.Image && activeElement.ImagePixelWidth > 0 && activeElement.ImagePixelHeight > 0)
		{
			PushUndo();
			activeElement.HeightMm = activeElement.WidthMm * (double)activeElement.ImagePixelHeight / (double)activeElement.ImagePixelWidth;
			activeElement.PreserveAspectRatio = true;
			MarkDirty();
			RebuildCanvas();
			UpdatePropertyPanel();
		}
	}

	private static bool TryNumber(string text, out double value)
	{
		if (!double.TryParse(text, out value))
		{
			return double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
		}
		return true;
	}

	private static bool IsColor(string text)
	{
		try
		{
			ColorConverter.ConvertFromString(text);
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string NormalizeColor(string text)
	{
		Color color = (Color)ColorConverter.ConvertFromString(text);
		return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
	}

	private void Align_Click(object sender, RoutedEventArgs e)
	{
		List<CanvasElementModel> list = CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id)).ToList();
		if (list.Count == 0 || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		string text = frameworkElement.Tag?.ToString();
		if (text == "PageCenterX")
		{
			foreach (CanvasElementModel item in list)
			{
				item.Xmm = (CurrentPage.WidthMm - item.WidthMm) / 2.0;
			}
		}
		else if (text == "PageCenterY")
		{
			foreach (CanvasElementModel item2 in list)
			{
				item2.Ymm = (CurrentPage.HeightMm - item2.HeightMm) / 2.0;
			}
		}
		else
		{
			CanvasElementModel canvasElementModel = list.Last();
			foreach (CanvasElementModel item3 in list)
			{
				switch (text)
				{
				case "Left":
					item3.Xmm = canvasElementModel.Xmm;
					break;
				case "Center":
					item3.Xmm = canvasElementModel.Xmm + (canvasElementModel.WidthMm - item3.WidthMm) / 2.0;
					break;
				case "Right":
					item3.Xmm = canvasElementModel.Xmm + canvasElementModel.WidthMm - item3.WidthMm;
					break;
				case "Top":
					item3.Ymm = canvasElementModel.Ymm;
					break;
				case "Middle":
					item3.Ymm = canvasElementModel.Ymm + (canvasElementModel.HeightMm - item3.HeightMm) / 2.0;
					break;
				case "Bottom":
					item3.Ymm = canvasElementModel.Ymm + canvasElementModel.HeightMm - item3.HeightMm;
					break;
				}
			}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
	}

	private void LayerOrder_Click(object sender, RoutedEventArgs e)
	{
		CanvasElementModel active = ActiveElement;
		if (active == null || !(sender is FrameworkElement frameworkElement))
		{
			return;
		}
		PushUndo();
		List<CanvasElementModel> list = CurrentPage.Elements.OrderBy((CanvasElementModel x) => x.ZIndex).ToList();
		int num = list.IndexOf(active);
		string text = frameworkElement.Tag?.ToString();
		if (text == "AboveSelected" || text == "BelowSelected")
		{
			CanvasElementModel canvasElementModel = list.LastOrDefault((CanvasElementModel x) => x.Id != active.Id && _selectedIds.Contains(x.Id));
			if (canvasElementModel == null)
			{
				MessageBox.Show("基準にするオブジェクトを含めて2つ選択してください。", "レイヤー順序");
				return;
			}
			list.Remove(active);
			int num2 = list.IndexOf(canvasElementModel);
			list.Insert((text == "AboveSelected") ? (num2 + 1) : num2, active);
		}
		else
		{
			switch (text)
			{
			case "Front":
				list.Remove(active);
				list.Add(active);
				break;
			case "Back":
				list.Remove(active);
				list.Insert(0, active);
				break;
			case "Forward":
				if (num < list.Count - 1)
				{
					int index3 = num;
					int index4 = num + 1;
					CanvasElementModel value3 = list[num + 1];
					CanvasElementModel value4 = list[num];
					list[index3] = value3;
					list[index4] = value4;
				}
				break;
			case "Backward":
				if (num > 0)
				{
					int index = num;
					int index2 = num - 1;
					CanvasElementModel value = list[num - 1];
					CanvasElementModel value2 = list[num];
					list[index] = value;
					list[index2] = value2;
				}
				break;
			}
		}
		for (int num3 = 0; num3 < list.Count; num3++)
		{
			list[num3].ZIndex = num3;
		}
		MarkDirty();
		RebuildCanvas();
		RefreshLayers();
	}

	private void Validate_Click(object sender, RoutedEventArgs e)
	{
		ValidationDialog validationDialog = new ValidationDialog(_validator.Validate(CurrentPage))
		{
			Owner = this
		};
		if (validationDialog.ShowDialog() == true && validationDialog.SelectedElementId.HasValue)
		{
			SelectOnly(validationDialog.SelectedElementId.Value);
			UpdateSelectionVisuals();
		}
		UpdateValidationCount();
	}

	private void ErrorCountText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		Validate_Click(sender, e);
	}

	private async void Export_Click(object sender, RoutedEventArgs e)
	{
		if (_outputInProgress)
		{
			StatusText.Text = "書き出し・印刷処理が進行中です";
			return;
		}
		List<ValidationIssue> source = _validator.Validate(CurrentPage);
		if (_settings.Current.WarnBeforeExportOnErrors && source.Any((ValidationIssue x) => x.Severity == IssueSeverity.Error) && MessageBox.Show("赤色のエラーが残っています。内容を確認せずに書き出しますか？", "書き出し前チェック", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			Validate_Click(sender, e);
			return;
		}
		ExportDialog exportDialog = new ExportDialog(_project.ExportSettings)
		{
			Owner = this
		};
		if (exportDialog.ShowDialog() != true || exportDialog.Result == null)
		{
			return;
		}
		ExportDialogResult result = exportDialog.Result;
		_project.ExportSettings.Dpi = result.Dpi;
		_project.ExportSettings.Format = result.Format;
		_project.ExportSettings.JpegQuality = result.JpegQuality;
		_project.ExportSettings.TransparentBackground = result.Transparent;
		_project.ExportSettings.ExportAllPages = result.AllPages;
		string format = result.Format;
		string text = ((format == "PNG") ? ".png" : ((!(format == "JPEG")) ? ".pdf" : ".jpg"));
		string defaultExt = text;
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Title = "販促物を書き出す";
		text = result.Format;
		format = ((text == "PNG") ? "PNG画像 (*.png)|*.png" : ((!(text == "JPEG")) ? "PDF (*.pdf)|*.pdf" : "JPEG画像 (*.jpg)|*.jpg"));
		saveFileDialog.Filter = format;
		saveFileDialog.DefaultExt = defaultExt;
		saveFileDialog.AddExtension = true;
		saveFileDialog.InitialDirectory = AppPaths.Exports;
		saveFileDialog.FileName = BuildExportName();
		SaveFileDialog saveFileDialog3 = saveFileDialog;
		if (saveFileDialog3.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			_outputInProgress = true;
			Mouse.OverrideCursor = Cursors.Wait;
			StatusText.Text = "書き出し準備中…";
			ExportRunResult exportRunResult = await DoExportAsync(saveFileDialog3.FileName, result);
			if (!exportRunResult.Saved)
			{
				StatusText.Text = "QR出力検査で書き出しを中止しました";
				return;
			}
			StatusText.Text = "書き出しました: " + saveFileDialog3.FileName;
			if (exportRunResult.QrChecks.Count > 0)
			{
				StatusText.Text += $"（QR {exportRunResult.QrChecks.Count}件を最終画像で照合済み）";
			}
			LogService.Info("Export completed: " + saveFileDialog3.FileName);
			if (_settings.Current.ExportCompletionAction == "自動で保存先を開く" || (_settings.Current.ExportCompletionAction == "確認する" && MessageBox.Show("書き出しが完了しました。保存先を開きますか？", "書き出し完了", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) == MessageBoxResult.Yes))
			{
				Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + saveFileDialog3.FileName + "\"")
				{
					UseShellExecute = true
				});
			}
		}
		catch (Exception ex)
		{
			LogService.Error("Export failed", ex);
			MessageBox.Show("書き出しに失敗しました。\n\n" + ex.Message, "書き出しエラー", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			_outputInProgress = false;
			Mouse.OverrideCursor = null;
		}
	}

	private async Task<ExportRunResult> DoExportAsync(string path, ExportDialogResult options)
	{
		List<int> list = (options.AllPages ? Enumerable.Range(0, _project.Pages.Count).ToList() : new List<int> { _pageIndex });
		List<QrOutputCheckResult> checks = new List<QrOutputCheckResult>();
		List<RenderedPage> renderedPages = new List<RenderedPage>();
		List<(string Path, byte[] Bytes)> outputs = new List<(string, byte[])>();
		for (int i = 0; i < list.Count; i++)
		{
			int pageNumber = i + 1;
			StatusText.Text = $"高精細素材を準備中… {pageNumber}/{list.Count}";
			PageModel preparedPage = await PreparePageForFinalRenderAsync(_project.Pages[list[i]]);
			StatusText.Text = $"ページを描画中… {pageNumber}/{list.Count}";
			(byte[], IReadOnlyList<QrOutputCheckResult>) tuple = await RunStaAsync(delegate
			{
				string format = ((options.Format == "PDF") ? "PNG" : options.Format);
				byte[] array = EncodeBitmap(RenderPage(preparedPage, options.Dpi, options.Transparent && options.Format == "PNG"), format, options.JpegQuality);
				IReadOnlyList<QrOutputCheckResult> item2 = QrOutputVerificationService.Verify(LoadBitmap(array), preparedPage);
				return (Bytes: array, Checks: item2);
			});
			checks.AddRange(tuple.Item2);
			if (options.Format == "PDF")
			{
				renderedPages.Add(new RenderedPage(tuple.Item1, preparedPage.WidthMm, preparedPage.HeightMm, preparedPage.Name));
				continue;
			}
			string item = ((list.Count == 1) ? path : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + $"_{pageNumber:00}" + System.IO.Path.GetExtension(path)));
			outputs.Add((item, tuple.Item1));
		}
		StatusText.Text = "最終出力を検査中…";
		if (!ConfirmQrOutput(checks))
		{
			return new ExportRunResult(Saved: false, checks);
		}
		StatusText.Text = "ファイルを保存中…";
		if (!(options.Format == "PDF"))
		{
			await Task.Run(delegate
			{
				foreach (var item3 in outputs)
				{
					File.WriteAllBytes(item3.Path, item3.Bytes);
				}
			});
		}
		else
		{
			await Task.Run(delegate
			{
				ExportService.SavePdf(path, renderedPages);
			});
		}
		return new ExportRunResult(Saved: true, checks);
	}

	private bool ConfirmQrOutput(IReadOnlyList<QrOutputCheckResult> checks)
	{
		if (checks.Count == 0)
		{
			return true;
		}
		List<QrOutputCheckResult> list = checks.Where((QrOutputCheckResult x) => !x.Passed).ToList();
		if (list.Count == 0)
		{
			LogService.Info($"QR output verification passed: {checks.Count} item(s)");
			return true;
		}
		string text = string.Join("\n", from x in list.Take(8)
			select "・" + x.ElementName + ": " + x.Detail);
		if (list.Count > 8)
		{
			text += $"\n・ほか{list.Count - 8}件";
		}
		MessageBoxResult messageBoxResult = MessageBox.Show("最終出力画像からQRパターンを読み取れない項目があります。\n\n" + text + "\n\nこのまま強制的に出力しますか？", "QR実読取テスト", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No);
		LogService.Info($"QR output verification: {checks.Count - list.Count} passed / {list.Count} failed; force={messageBoxResult == MessageBoxResult.Yes}");
		return messageBoxResult == MessageBoxResult.Yes;
	}

	private void SwitchPageForRender(int index)
	{
		_pageIndex = index;
		_selectedIds.Clear();
		RefreshAll();
		PageCanvas.UpdateLayout();
		base.Dispatcher.Invoke(delegate
		{
		}, DispatcherPriority.Render);
	}

	private RenderTargetBitmap RenderCurrentPage(int dpi, bool transparent)
	{
		return RenderPage(CurrentPage, dpi, transparent);
	}

	private RenderTargetBitmap RenderPage(PageModel currentPage, int dpi, bool transparent)
	{
		double width = currentPage.WidthMm * 3.7795275590551185;
		double height = currentPage.HeightMm * 3.7795275590551185;
		Canvas canvas = new Canvas
		{
			Width = width,
			Height = height,
			Background = (transparent ? Brushes.Transparent : TextureCatalogService.Blend(BrushFrom(currentPage.Background, Brushes.White), currentPage.BackgroundTextureDataBase64, currentPage.BackgroundTextureOpacity, currentPage.BackgroundTextureScale)),
			ClipToBounds = true,
			SnapsToDevicePixels = false
		};
		TextOptions.SetTextFormattingMode(canvas, TextFormattingMode.Ideal);
		TextOptions.SetTextRenderingMode(canvas, TextRenderingMode.Grayscale);
		foreach (CanvasElementModel item in from x in currentPage.Elements
			where x.IsVisible
			orderby x.ZIndex
			select x)
		{
			CanvasElementModel canvasElementModel = PrepareForFinalRender(item);
			ContentControl contentControl = new ContentControl
			{
				Content = BuildVisual(canvasElementModel, inverted: false),
				Width = Math.Max(4.0, canvasElementModel.WidthMm * 3.7795275590551185),
				Height = Math.Max(4.0, canvasElementModel.HeightMm * 3.7795275590551185),
				Opacity = Math.Clamp(canvasElementModel.Opacity, 0.0, 1.0),
				HorizontalContentAlignment = HorizontalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Stretch,
				RenderTransformOrigin = new Point(0.5, 0.5),
				RenderTransform = CreateElementTransform(canvasElementModel)
			};
			Canvas.SetLeft(contentControl, canvasElementModel.Xmm * 3.7795275590551185);
			Canvas.SetTop(contentControl, canvasElementModel.Ymm * 3.7795275590551185);
			Panel.SetZIndex(contentControl, canvasElementModel.ZIndex);
			RenderOptions.SetBitmapScalingMode(contentControl, BitmapScalingMode.HighQuality);
			canvas.Children.Add(contentControl);
		}
		canvas.Measure(new Size(width, height));
		canvas.Arrange(new Rect(0.0, 0.0, width, height));
		canvas.UpdateLayout();
		int pixelWidth = Math.Max(1, (int)Math.Round(currentPage.WidthMm / 25.4 * (double)dpi));
		int pixelHeight = Math.Max(1, (int)Math.Round(currentPage.HeightMm / 25.4 * (double)dpi));
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(canvas);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}

	private static async Task<PageModel> PreparePageForFinalRenderAsync(PageModel source)
	{
		PageModel page = JsonSerializer.Deserialize<PageModel>(JsonSerializer.Serialize(source, ProjectService.JsonOptions), ProjectService.JsonOptions) ?? throw new InvalidOperationException("ページデータを複製できませんでした。");
		foreach (CanvasElementModel element in page.Elements.Where((CanvasElementModel canvasElementModel) => canvasElementModel.Kind == ElementKind.Image && !string.IsNullOrWhiteSpace(canvasElementModel.PdfSourcePath) && canvasElementModel.PdfPageIndex.HasValue && File.Exists(canvasElementModel.PdfSourcePath)))
		{
			try
			{
				element.ImageDataBase64 = Convert.ToBase64String((await PdfImportService.RenderPageAsync(element.PdfSourcePath, element.PdfPageIndex.Value, 5200)).PngBytes);
			}
			catch (Exception ex)
			{
				LogService.Error("High resolution PDF preparation failed", ex);
			}
		}
		return page;
	}

	private static Task<T> RunStaAsync<T>(Func<T> action)
	{
		TaskCompletionSource<T> completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				completion.SetResult(action());
			}
			catch (Exception exception)
			{
				completion.SetException(exception);
			}
		});
		thread.IsBackground = true;
		thread.Name = "MISE High Resolution Renderer";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return completion.Task;
	}

	private static CanvasElementModel PrepareForFinalRender(CanvasElementModel item)
	{
		if (item.Kind != ElementKind.Image)
		{
			return item;
		}
		try
		{
			if (!string.IsNullOrWhiteSpace(item.PdfSourcePath) && item.PdfPageIndex.HasValue && File.Exists(item.PdfSourcePath))
			{
				return item;
			}
			if (item.ImageUsesLinkedOriginal && !string.IsNullOrWhiteSpace(item.ImageSourcePath) && File.Exists(item.ImageSourcePath))
			{
				CanvasElementModel canvasElementModel = CloneElement(item);
				canvasElementModel.ImageOriginalDataBase64 = null;
				if (!string.IsNullOrWhiteSpace(item.ImageCutoutSettingsJson))
				{
					ImageCutoutSettings imageCutoutSettings = JsonSerializer.Deserialize<ImageCutoutSettings>(item.ImageCutoutSettingsJson, ProjectService.JsonOptions);
					if (imageCutoutSettings != null)
					{
						byte[] source = File.ReadAllBytes(item.ImageSourcePath);
						canvasElementModel.ImageDataBase64 = Convert.ToBase64String(BackgroundRemovalService.Apply(source, imageCutoutSettings));
						return canvasElementModel;
					}
				}
				canvasElementModel.ImageDataBase64 = null;
				return canvasElementModel;
			}
			if (string.IsNullOrWhiteSpace(item.ImageOriginalDataBase64) || string.IsNullOrWhiteSpace(item.ImageCutoutSettingsJson))
			{
				return item;
			}
			ImageCutoutSettings imageCutoutSettings2 = JsonSerializer.Deserialize<ImageCutoutSettings>(item.ImageCutoutSettingsJson, ProjectService.JsonOptions);
			if (imageCutoutSettings2 == null)
			{
				return item;
			}
			CanvasElementModel canvasElementModel2 = CloneElement(item);
			byte[] source2 = Convert.FromBase64String(item.ImageOriginalDataBase64);
			canvasElementModel2.ImageDataBase64 = Convert.ToBase64String(BackgroundRemovalService.Apply(source2, imageCutoutSettings2));
			return canvasElementModel2;
		}
		catch
		{
			return item;
		}
	}

	private static byte[] EncodeBitmap(BitmapSource bitmap, string format, int jpegQuality)
	{
		BitmapEncoder bitmapEncoder = ((format == "JPEG") ? ((BitmapEncoder)new JpegBitmapEncoder
		{
			QualityLevel = Math.Clamp(jpegQuality, 1, 100)
		}) : ((BitmapEncoder)new PngBitmapEncoder()));
		bitmapEncoder.Frames.Add(BitmapFrame.Create(bitmap));
		using MemoryStream memoryStream = new MemoryStream();
		bitmapEncoder.Save(memoryStream);
		return memoryStream.ToArray();
	}

	private string BuildExportName()
	{
		string[] source = new string[5]
		{
			_project.BrandName,
			_project.ProjectName,
			_project.StoreName,
			_project.PaperName,
			DateTime.Now.ToString("yyyyMMdd")
		};
		return SafeFileName(string.Join("_", source.Where((string x) => !string.IsNullOrWhiteSpace(x))));
	}

	private async void Print_Click(object sender, RoutedEventArgs e)
	{
		if (_outputInProgress)
		{
			StatusText.Text = "書き出し・印刷処理が進行中です";
			return;
		}
		PrintWizardDialog printWizardDialog = new PrintWizardDialog(_project.PrintMode)
		{
			Owner = this
		};
		if (printWizardDialog.ShowDialog() != true || printWizardDialog.Result == null)
		{
			return;
		}
		PrintWizardResult options = printWizardDialog.Result;
		try
		{
			int dpi = (options.Quality.Contains("600") ? 600 : (options.Quality.Contains("200") ? 200 : 300));
			if (options.OutputMethod == "PDFで保管")
			{
				SaveFileDialog save = new SaveFileDialog
				{
					Title = "印刷用PDFを保存",
					Filter = "PDF (*.pdf)|*.pdf",
					DefaultExt = ".pdf",
					AddExtension = true,
					InitialDirectory = AppPaths.Exports,
					FileName = BuildExportName() + "_印刷用"
				};
				if (save.ShowDialog(this) != true)
				{
					return;
				}
				_outputInProgress = true;
				Mouse.OverrideCursor = Cursors.Wait;
				StatusText.Text = "印刷用の高精細素材を準備中…";
				PageModel preparedPage = await PreparePageForFinalRenderAsync(CurrentPage);
				StatusText.Text = "印刷用ページを描画中…";
				(byte[] Bytes, IReadOnlyList<QrOutputCheckResult> Checks) rendered = await RunStaAsync(delegate
				{
					BitmapSource bitmapSource = ConvertForPrint(RenderPage(preparedPage, dpi, transparent: false), options);
					IReadOnlyList<QrOutputCheckResult> item = QrOutputVerificationService.Verify(bitmapSource, preparedPage);
					return (Bytes: options.RequestK100 ? EncodeK100Image(bitmapSource) : EncodeBitmap(bitmapSource, "PNG", 100), Checks: item);
				});
				if (ConfirmQrOutput(rendered.Checks))
				{
					StatusText.Text = "印刷用PDFを保存中…";
					await Task.Run(delegate
					{
						ExportService.SavePdf(save.FileName, new RenderedPage[1]
						{
							new RenderedPage(rendered.Bytes, preparedPage.WidthMm, preparedPage.HeightMm, preparedPage.Name)
						});
					});
					StatusText.Text = (options.RequestK100 ? "K100%向け純黒処理を適用した印刷用PDFを保存しました" : "印刷用PDFを保存しました");
				}
				return;
			}
			PrintDialog printDialog = new PrintDialog();
			if (printDialog.ShowDialog() != true)
			{
				return;
			}
			bool driverAcceptedBlackOnly = !options.PreferBlackInk || !(options.ColorMode != "カラー") || TrySetPrintTicketOption(printDialog, "OutputColor", "Monochrome");
			if (options.Duplex.Contains("長辺"))
			{
				TrySetPrintTicketOption(printDialog, "Duplexing", "TwoSidedLongEdge");
			}
			else if (options.Duplex.Contains("短辺"))
			{
				TrySetPrintTicketOption(printDialog, "Duplexing", "TwoSidedShortEdge");
			}
			_outputInProgress = true;
			Mouse.OverrideCursor = Cursors.Wait;
			StatusText.Text = "印刷用の高精細素材を準備中…";
			PageModel printPage = await PreparePageForFinalRenderAsync(CurrentPage);
			StatusText.Text = "印刷用ページを描画中…";
			(BitmapSource, IReadOnlyList<QrOutputCheckResult>) tuple = await RunStaAsync(delegate
			{
				BitmapSource bitmapSource = ConvertForPrint(RenderPage(printPage, dpi, transparent: false), options);
				IReadOnlyList<QrOutputCheckResult> item = QrOutputVerificationService.Verify(bitmapSource, printPage);
				bitmapSource.Freeze();
				return (Source: bitmapSource, Checks: item);
			});
			if (ConfirmQrOutput(tuple.Item2))
			{
				double num = printPage.WidthMm * 96.0 / 25.4;
				double num2 = printPage.HeightMm * 96.0 / 25.4;
				double num3 = Math.Max(1.0, printDialog.PrintableAreaWidth);
				double num4 = Math.Max(1.0, printDialog.PrintableAreaHeight);
				double num5 = ((options.ScaleMode == "印刷可能範囲に合わせる") ? Math.Min(num3 / num, num4 / num2) : 1.0);
				Image image = new Image
				{
					Source = tuple.Item1,
					Width = num * num5,
					Height = num2 * num5,
					Stretch = Stretch.Fill
				};
				FixedPage fixedPage = new FixedPage
				{
					Width = num3,
					Height = num4,
					Background = Brushes.White
				};
				fixedPage.Children.Add(image);
				FixedPage.SetLeft(image, Math.Max(0.0, (num3 - image.Width) / 2.0));
				FixedPage.SetTop(image, Math.Max(0.0, (num4 - image.Height) / 2.0));
				StatusText.Text = "プリンタードライバーへ送信中…";
				Mouse.OverrideCursor = null;
				printDialog.PrintVisual(fixedPage, _project.ProjectName + " [" + options.PaperType + "]");
				if (!driverAcceptedBlackOnly)
				{
					MessageBox.Show("画像はモノクロ変換済みですが、このプリンタードライバーへ『黒インクのみ』を自動指定できませんでした。\nプリンターのプロパティで［モノクロ］［グレースケール］または［黒インクのみ］を選んでください。", "黒インク設定", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				}
				LogService.Info("Printed project: " + _project.ProjectName);
				StatusText.Text = "印刷データを送信しました";
			}
		}
		catch (Exception ex)
		{
			LogService.Error("Print failed", ex);
			MessageBox.Show("印刷できませんでした。\n\n" + ex.Message, "印刷エラー", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			_outputInProgress = false;
			Mouse.OverrideCursor = null;
		}
	}

	private static BitmapSource ConvertForPrint(BitmapSource source, PrintWizardResult options)
	{
		if (options.ColorMode == "カラー")
		{
			return source;
		}
		FormatConvertedBitmap converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0.0);
		int num = converted.PixelWidth * 4;
		byte[] array = new byte[num * converted.PixelHeight];
		converted.CopyPixels(array, num, 0);
		int num2 = converted.PixelWidth * converted.PixelHeight;
		double[] luminance = new double[num2];
		double num3 = Math.Clamp(options.Contrast / 100.0, 0.1, 3.0);
		double num4 = Math.Clamp(options.BlackDensity / 100.0, 0.1, 2.5);
		double num5 = Math.Clamp(options.Gamma, 0.25, 4.0);
		for (int i = 0; i < num2; i++)
		{
			int num6 = i * 4;
			double value = ((double)(int)array[num6 + 2] * 0.2126 + (double)(int)array[num6 + 1] * 0.7152 + (double)(int)array[num6] * 0.0722) / 255.0;
			value = Math.Pow(Math.Clamp(value, 0.0, 1.0), 1.0 / num5);
			value = (value - 0.5) * num3 + 0.5;
			value = 1.0 - (1.0 - value) * num4;
			luminance[i] = Math.Clamp(value, 0.0, 1.0);
		}
		double num7 = Math.Clamp(options.BlackThreshold / 100.0, 0.01, 0.99);
		string colorMode = options.ColorMode;
		bool flag = ((colorMode == "白黒2値化" || colorMode == "印刷会社向けK100%") ? true : false);
		bool flag2 = flag;
		if (flag2 && options.Dithering)
		{
			for (int j = 0; j < converted.PixelHeight; j++)
			{
				for (int k = 0; k < converted.PixelWidth; k++)
				{
					int num8 = j * converted.PixelWidth + k;
					double num9 = luminance[num8];
					double num10 = ((num9 <= num7) ? 0.0 : 1.0);
					double num11 = num9 - num10;
					luminance[num8] = num10;
					AddError(k + 1, j, num11 * 7.0 / 16.0);
					AddError(k - 1, j + 1, num11 * 3.0 / 16.0);
					AddError(k, j + 1, num11 * 5.0 / 16.0);
					AddError(k + 1, j + 1, num11 / 16.0);
				}
			}
		}
		for (int l = 0; l < num2; l++)
		{
			double num12 = luminance[l];
			if (flag2 && !options.Dithering)
			{
				num12 = ((!(num12 <= num7)) ? 1 : 0);
			}
			else if (options.ColorMode == "純黒（文字・枠線・QR）")
			{
				num12 = ((num12 <= num7) ? 0.0 : (options.PreservePhotoTones ? num12 : 1.0));
			}
			else if (options.ColorMode == "販促物向けモノクロ" && num12 < 0.42)
			{
				num12 = 0.0;
			}
			else if (options.ColorMode == "黒インク優先" && num12 < 0.32)
			{
				num12 = 0.0;
			}
			byte b = (byte)Math.Clamp(Math.Round(num12 * 255.0), 0.0, 255.0);
			int num13 = l * 4;
			array[num13] = (array[num13 + 1] = (array[num13 + 2] = b));
		}
		BitmapSource bitmapSource = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, converted.DpiX, converted.DpiY, PixelFormats.Bgra32, null, array, num);
		bitmapSource.Freeze();
		return bitmapSource;
		void AddError(int targetX, int targetY, double amount)
		{
			if (targetX >= 0 && targetX < converted.PixelWidth && targetY >= 0 && targetY < converted.PixelHeight)
			{
				int num14 = targetY * converted.PixelWidth + targetX;
				luminance[num14] = Math.Clamp(luminance[num14] + amount, 0.0, 1.0);
			}
		}
	}

	private static bool TrySetPrintTicketOption(PrintDialog dialog, string propertyName, string enumValue)
	{
		try
		{
			PrintTicket printTicket = dialog.PrintTicket;
			if (printTicket == null)
			{
				return false;
			}
			PropertyInfo property = printTicket.GetType().GetProperty(propertyName);
			if (property == null)
			{
				return false;
			}
			object value = Enum.Parse(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType, enumValue, ignoreCase: true);
			property.SetValue(printTicket, value);
			dialog.PrintTicket = printTicket;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static byte[] EncodeK100Image(BitmapSource source)
	{
		try
		{
			FormatConvertedBitmap source2 = new FormatConvertedBitmap(source, PixelFormats.Cmyk32, null, 0.0);
			JpegBitmapEncoder jpegBitmapEncoder = new JpegBitmapEncoder
			{
				QualityLevel = 100
			};
			jpegBitmapEncoder.Frames.Add(BitmapFrame.Create(source2));
			using MemoryStream memoryStream = new MemoryStream();
			jpegBitmapEncoder.Save(memoryStream);
			return memoryStream.ToArray();
		}
		catch
		{
			return EncodeBitmap(source, "PNG", 100);
		}
	}

	private void Imposition_Click(object sender, RoutedEventArgs e)
	{
		ImpositionDialog impositionDialog = new ImpositionDialog(CurrentPage.WidthMm, CurrentPage.HeightMm)
		{
			Owner = this
		};
		if (impositionDialog.ShowDialog() != true || impositionDialog.Result == null)
		{
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "PDF (*.pdf)|*.pdf|PNG (*.png)|*.png",
			DefaultExt = ".pdf",
			FileName = BuildExportName() + "_面付け",
			InitialDirectory = AppPaths.Exports
		};
		if (saveFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			Mouse.OverrideCursor = Cursors.Wait;
			ImpositionOutput impositionOutput = BuildImposition(impositionDialog.Result, 300);
			byte[] array = EncodeBitmap(impositionOutput.Bitmap, "PNG", 100);
			if (System.IO.Path.GetExtension(saveFileDialog.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
			{
				File.WriteAllBytes(saveFileDialog.FileName, array);
			}
			else
			{
				ExportService.SavePdf(saveFileDialog.FileName, new RenderedPage[1]
				{
					new RenderedPage(array, impositionOutput.WidthMm, impositionOutput.HeightMm, "面付け")
				});
			}
			StatusText.Text = $"{impositionOutput.Copies}面を配置して書き出しました";
		}
		catch (Exception ex)
		{
			MessageBox.Show("面付け出力に失敗しました。\n\n" + ex.Message, "自動面付け", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		finally
		{
			Mouse.OverrideCursor = null;
		}
	}

	private ImpositionOutput BuildImposition(ImpositionDialogResult options, int dpi)
	{
		RenderTargetBitmap imageSource = RenderCurrentPage(180, transparent: false);
		PaperSizeDefinition paperSizeDefinition = PaperCatalog.Get(options.PaperName);
		double num = (options.Landscape ? paperSizeDefinition.HeightMm : paperSizeDefinition.WidthMm);
		double num2 = (options.Landscape ? paperSizeDefinition.WidthMm : paperSizeDefinition.HeightMm);
		double widthMm = CurrentPage.WidthMm;
		double heightMm = CurrentPage.HeightMm;
		double num3 = num - options.MarginMm * 2.0;
		double num4 = num2 - options.MarginMm * 2.0;
		double num5 = Math.Min(1.0, Math.Min(num3 / widthMm, num4 / heightMm));
		widthMm *= num5;
		heightMm *= num5;
		int num6 = Math.Max(1, (int)Math.Floor((num3 + options.GapMm) / (widthMm + options.GapMm)));
		int num7 = Math.Max(1, (int)Math.Floor((num4 + options.GapMm) / (heightMm + options.GapMm)));
		int num8 = Math.Min((options.Copies <= 0) ? (num6 * num7) : options.Copies, num6 * num7);
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap((int)Math.Round(num / 25.4 * (double)dpi), (int)Math.Round(num2 / 25.4 * (double)dpi), dpi, dpi, PixelFormats.Pbgra32);
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawRectangle(Brushes.White, null, new Rect(0.0, 0.0, num * 3.7795275590551185, num2 * 3.7795275590551185));
			Pen pen = new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 150)), 0.5);
			for (int i = 0; i < num8; i++)
			{
				int num9 = i % num6;
				int num10 = i / num6;
				double num11 = options.MarginMm + (double)num9 * (widthMm + options.GapMm);
				double num12 = options.MarginMm + (double)num10 * (heightMm + options.GapMm);
				Rect rect = new Rect(num11 * 3.7795275590551185, num12 * 3.7795275590551185, widthMm * 3.7795275590551185, heightMm * 3.7795275590551185);
				drawingContext.DrawImage(imageSource, rect);
				if (options.CropMarks)
				{
					DrawCropMarks(drawingContext, rect, pen);
				}
			}
		}
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		return new ImpositionOutput(renderTargetBitmap, num, num2, num8);
	}

	private static void DrawCropMarks(DrawingContext dc, Rect rect, Pen pen)
	{
		dc.DrawLine(pen, new Point(rect.Left - 12.0, rect.Top), new Point(rect.Left, rect.Top));
		dc.DrawLine(pen, new Point(rect.Left, rect.Top - 12.0), new Point(rect.Left, rect.Top));
		dc.DrawLine(pen, new Point(rect.Right, rect.Top), new Point(rect.Right + 12.0, rect.Top));
		dc.DrawLine(pen, new Point(rect.Right, rect.Top - 12.0), new Point(rect.Right, rect.Top));
		dc.DrawLine(pen, new Point(rect.Left - 12.0, rect.Bottom), new Point(rect.Left, rect.Bottom));
		dc.DrawLine(pen, new Point(rect.Left, rect.Bottom), new Point(rect.Left, rect.Bottom + 12.0));
		dc.DrawLine(pen, new Point(rect.Right, rect.Bottom), new Point(rect.Right + 12.0, rect.Bottom));
		dc.DrawLine(pen, new Point(rect.Right, rect.Bottom), new Point(rect.Right, rect.Bottom + 12.0));
	}

	private void ProductDatabase_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			ProductDatabaseDialog productDatabaseDialog = new ProductDatabaseDialog(new DatabaseService())
			{
				Owner = this
			};
			if (productDatabaseDialog.ShowDialog() == true && productDatabaseDialog.SelectedProduct != null)
			{
				HomeOverlay.Visibility = Visibility.Collapsed;
				PlaceProduct(productDatabaseDialog.SelectedProduct);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("商品データベースを開けませんでした。\n\n" + ex.Message, "商品データベース", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void PlaceProduct(ProductModel product)
	{
		PushUndo();
		List<CanvasElementModel> list = new List<CanvasElementModel>();
		CanvasElementModel canvasElementModel = new CanvasElementModel
		{
			Kind = ElementKind.Text,
			Name = UniqueName("製品名"),
			Text = product.ProductName,
			FontSizePt = 22.0,
			Bold = true,
			Xmm = 15.0,
			Ymm = 20.0,
			WidthMm = CurrentPage.WidthMm - 30.0,
			HeightMm = 22.0,
			ZIndex = CurrentPage.Elements.Count
		};
		CurrentPage.Elements.Add(canvasElementModel);
		list.Add(canvasElementModel);
		if (!string.IsNullOrWhiteSpace(product.CatchCopy))
		{
			CanvasElementModel item = new CanvasElementModel
			{
				Kind = ElementKind.Text,
				Name = UniqueName("キャッチコピー"),
				Text = product.CatchCopy,
				FontSizePt = 16.0,
				Bold = true,
				TextColor = "#FFF26A21",
				Xmm = 15.0,
				Ymm = 47.0,
				WidthMm = CurrentPage.WidthMm - 30.0,
				HeightMm = 20.0,
				ZIndex = CurrentPage.Elements.Count
			};
			CurrentPage.Elements.Add(item);
			list.Add(item);
		}
		if (product.Price.HasValue)
		{
			CanvasElementModel item2 = new CanvasElementModel
			{
				Kind = ElementKind.Text,
				Name = UniqueName("価格"),
				Text = $"￥{product.Price:N0}",
				FontSizePt = 28.0,
				Bold = true,
				TextColor = "#FFF26A21",
				Xmm = 15.0,
				Ymm = CurrentPage.HeightMm - 55.0,
				WidthMm = CurrentPage.WidthMm - 30.0,
				HeightMm = 28.0,
				ZIndex = CurrentPage.Elements.Count
			};
			CurrentPage.Elements.Add(item2);
			list.Add(item2);
		}
		string text = ResolveProductMainImage(product);
		if (!string.IsNullOrWhiteSpace(text) && File.Exists(text))
		{
			try
			{
				byte[] array = File.ReadAllBytes(text);
				BitmapImage bitmapImage = LoadBitmap(array);
				double num = Math.Min(100.0, CurrentPage.WidthMm - 40.0);
				CanvasElementModel canvasElementModel2 = new CanvasElementModel
				{
					Kind = ElementKind.Image,
					Name = UniqueName(product.ProductName + "画像"),
					WidthMm = num,
					HeightMm = num * (double)bitmapImage.PixelHeight / (double)Math.Max(1, bitmapImage.PixelWidth),
					Xmm = (CurrentPage.WidthMm - num) / 2.0,
					Ymm = 75.0,
					ZIndex = CurrentPage.Elements.Count
				};
				StoreImageForEditing(canvasElementModel2, array, bitmapImage, text);
				CurrentPage.Elements.Add(canvasElementModel2);
				list.Add(canvasElementModel2);
			}
			catch
			{
			}
		}
		if (!string.IsNullOrWhiteSpace(product.Url))
		{
			CanvasElementModel item3 = new CanvasElementModel
			{
				Kind = ElementKind.QrCode,
				Name = UniqueName("製品QR"),
				QrContent = product.Url,
				WidthMm = 25.0,
				HeightMm = 25.0,
				Xmm = CurrentPage.WidthMm - 38.0,
				Ymm = CurrentPage.HeightMm - 38.0,
				ZIndex = CurrentPage.Elements.Count
			};
			CurrentPage.Elements.Add(item3);
			list.Add(item3);
		}
		CenterElementsInVisibleViewport(list);
		_selectedIds.Clear();
		_selectedIds.Add(canvasElementModel.Id);
		MarkDirty();
		RefreshAll();
	}

	private static string ResolveProductMainImage(ProductModel product)
	{
		if (!string.IsNullOrWhiteSpace(product.ImagePath) && File.Exists(product.ImagePath))
		{
			return product.ImagePath;
		}
		try
		{
			string text = JsonSerializer.Deserialize<Dictionary<string, string>>(product.AssetRoleData)?.FirstOrDefault((KeyValuePair<string, string> pair) => pair.Value == "メイン画像" && File.Exists(pair.Key)).Key;
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
		}
		catch
		{
		}
		if (!Directory.Exists(product.AssetFolderPath))
		{
			return string.Empty;
		}
		return Directory.EnumerateFiles(product.AssetFolderPath, "*", SearchOption.AllDirectories).FirstOrDefault((string path) => new string[6] { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".webp" }.Contains<string>(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) ?? string.Empty;
	}

	private void AssetLibrary_Click(object sender, RoutedEventArgs e)
	{
		AssetLibraryDialog assetLibraryDialog = new AssetLibraryDialog((_settings.Current.AssetFolders.Count > 0) ? ((IEnumerable<string>)_settings.Current.AssetFolders) : ((IEnumerable<string>)new List<string> { _settings.Current.AssetFolder }))
		{
			Owner = this
		};
		if (assetLibraryDialog.ShowDialog() == true)
		{
			if (assetLibraryDialog.SelectedFiles.Count > 0)
			{
				HomeOverlay.Visibility = Visibility.Collapsed;
				foreach (string selectedFile in assetLibraryDialog.SelectedFiles)
				{
					AddImageFile(selectedFile);
				}
			}
			if (!string.IsNullOrWhiteSpace(assetLibraryDialog.SelectedFolder))
			{
				_settings.Current.AssetFolder = assetLibraryDialog.SelectedFolder;
				_settings.Save();
			}
		}
		_settings.Current.AssetFolders = assetLibraryDialog.FolderRoots.ToList();
		_settings.Save();
	}

	private void Settings_Click(object sender, RoutedEventArgs e)
	{
		if (new SettingsDialog(_settings.Current)
		{
			Owner = this
		}.ShowDialog() == true)
		{
			_settings.Save();
			ConfigureAutoSave();
			WindowSizing.ApplyMainWindow(this, _settings.Current);
			ApplyUiPreferences();
			ApplyStartupZoom();
			CurrentPage.SafeMarginMm = _settings.Current.DefaultSafeMarginMm;
			RefreshAll();
		}
	}

	private void CreateBackup_Click(object sender, RoutedEventArgs e)
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "ZIPバックアップ (*.zip)|*.zip",
			DefaultExt = ".zip",
			AddExtension = true,
			InitialDirectory = AppPaths.Backups,
			FileName = $"MISE_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
		};
		if (saveFileDialog.ShowDialog(this) != true)
		{
			return;
		}
		try
		{
			BackupService.CreateBackup(saveFileDialog.FileName);
			MessageBox.Show("バックアップを作成しました。", "バックアップ", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("バックアップを作成できませんでした。\n" + ex.Message, "バックアップ", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void RestoreBackup_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "ZIPバックアップ (*.zip)|*.zip",
			InitialDirectory = AppPaths.Backups
		};
		if (openFileDialog.ShowDialog(this) != true || MessageBox.Show("現在の設定・データベース・テンプレートをバックアップ内容で置き換えます。続行しますか？", "バックアップ復元", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) != MessageBoxResult.Yes)
		{
			return;
		}
		try
		{
			BackupService.Restore(openFileDialog.FileName);
			MessageBox.Show("復元しました。アプリを再起動してください。", "バックアップ復元");
		}
		catch (Exception ex)
		{
			MessageBox.Show("復元できませんでした。\n" + ex.Message, "バックアップ復元", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void Help_Click(object sender, RoutedEventArgs e)
	{
		HelpDialog helpDialog = new HelpDialog();
		helpDialog.Owner = this;
		helpDialog.ShowDialog();
	}

	private void ShowCommandPalette()
	{
		RoutedEventArgs e = new RoutedEventArgs();
		List<CommandPaletteItem> list = new List<CommandPaletteItem>
		{
			Command("新規プロジェクト", "ファイル", "Ctrl+N", "新規 作成", delegate
			{
				NewProject_Click(this, e);
			}),
			Command("プロジェクトを開く", "ファイル", "Ctrl+O", "読込", delegate
			{
				OpenProject_Click(this, e);
			}),
			Command("保存", "ファイル", "Ctrl+S", "上書き", delegate
			{
				SaveProject_Click(this, e);
			}),
			Command("名前を付けて保存", "ファイル", "Ctrl+Shift+S", "別名", delegate
			{
				SaveAsProject_Click(this, e);
			}),
			Command("書き出し", "出力", "Ctrl+E", "PDF PNG JPEG", delegate
			{
				Export_Click(this, e);
			}),
			Command("印刷", "出力", "Ctrl+P", "プリンター PDF", delegate
			{
				Print_Click(this, e);
			}),
			Command("レイアウトチェック", "確認", string.Empty, "エラー 検査", delegate
			{
				Validate_Click(this, e);
			}),
			Command("大見出しを追加", "挿入", string.Empty, "文字 テキスト", delegate
			{
				AddHeading_Click(this, e);
			}),
			Command("本文を追加", "挿入", string.Empty, "文字 テキスト", delegate
			{
				AddBody_Click(this, e);
			}),
			Command("価格を追加", "挿入", string.Empty, "文字 値札", delegate
			{
				AddPrice_Click(this, e);
			}),
			Command("画像を追加", "挿入", string.Empty, "写真 素材", delegate
			{
				AddImage_Click(this, e);
			}),
			Command("角丸図形を追加", "挿入", string.Empty, "シェイプ", delegate
			{
				AddShape_Click(new Button
				{
					Tag = "RoundedRectangle"
				}, e);
			}),
			Command("パネルを追加", "挿入", string.Empty, "区画 見出し 本文", delegate
			{
				AddShape_Click(new Button
				{
					Tag = "Panel"
				}, e);
			}),
			Command("QRコードを追加", "挿入", string.Empty, "URL 二次元", delegate
			{
				AddQr_Click(this, e);
			}),
			Command("再利用ブロックを挿入", "挿入", string.Empty, "部品 パーツ", delegate
			{
				OpenReusableBlocks_Click(this, e);
			}),
			Command("素材ライブラリ", "管理", string.Empty, "画像 フォルダ", delegate
			{
				AssetLibrary_Click(this, e);
			}),
			Command("商品データベース", "管理", string.Empty, "製品 セールスポイント", delegate
			{
				ProductDatabase_Click(this, e);
			}),
			Command("実寸表示", "表示", string.Empty, "原寸 モニター", delegate
			{
				ApplyZoom(GetActualSizeZoom());
			}),
			Command("全体表示", "表示", "Ctrl+0", "ズーム", FitPage),
			Command("100%表示", "表示", "Ctrl+1", "ズーム", delegate
			{
				ApplyZoom(1.0);
			}),
			Command("環境設定", "設定", "Ctrl+,", "画面 性能 リリースノート", delegate
			{
				Settings_Click(this, e);
			}),
			Command("操作ガイド", "ヘルプ", string.Empty, "使い方", delegate
			{
				Help_Click(this, e);
			})
		};
		if (_selectedIds.Count > 0)
		{
			list.Add(Command("選択項目を再利用ブロックとして保存", "選択", string.Empty, "部品 パーツ", delegate
			{
				SaveReusableBlock_Click(this, e);
			}));
		}
		CommandPaletteDialog commandPaletteDialog = new CommandPaletteDialog(list)
		{
			Owner = this
		};
		if (commandPaletteDialog.ShowDialog() == true && commandPaletteDialog.SelectedCommand != null)
		{
			base.Dispatcher.BeginInvoke(commandPaletteDialog.SelectedCommand.Execute, DispatcherPriority.Input);
		}
		static CommandPaletteItem Command(string name, string category, string shortcut, string keywords, Action action)
		{
			return new CommandPaletteItem
			{
				Name = name,
				Category = category,
				Shortcut = shortcut,
				Keywords = keywords,
				Execute = action
			};
		}
	}

	private void About_Click(object sender, RoutedEventArgs e)
	{
		MessageBox.Show("MISE（マイズ） 1.1.12\n\nWindows向け販促物作成ソフト\n\n© 2026 MISE", "MISEについて", MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (base.IsLoaded)
		{
			ApplyZoom(e.NewValue / 100.0);
		}
	}

	private void ApplyZoom(double zoom)
	{
		_zoom = Math.Clamp(zoom, 0.25, 4.0);
		PageCanvas.LayoutTransform = Transform.Identity;
		OverflowCanvas.LayoutTransform = Transform.Identity;
		GuideOverlayCanvas.LayoutTransform = Transform.Identity;
		CanvasOuter.LayoutTransform = new ScaleTransform(_zoom, _zoom);
		ZoomText.Text = $"{_zoom * 100.0:0}%";
		if (Math.Abs(ZoomSlider.Value - _zoom * 100.0) > 0.1)
		{
			ZoomSlider.Value = _zoom * 100.0;
		}
	}

	private void ZoomPreset_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is FrameworkElement { Tag: var tag }))
		{
			return;
		}
		string text = tag?.ToString();
		if (!(text == "100"))
		{
			if (text == "Actual")
			{
				ApplyZoom(GetActualSizeZoom());
			}
			else
			{
				FitPage();
			}
		}
		else
		{
			ApplyZoom(1.0);
		}
	}

	private double GetActualSizeZoom()
	{
		string description;
		double actualSizeZoom = PhysicalDisplayService.GetActualSizeZoom(this, _settings.Current.ActualSizeCalibrationPercent, out description);
		StatusText.Text = "実寸表示: " + description;
		return actualSizeZoom;
	}

	private void FitPage()
	{
		double num = Math.Max(100.0, CanvasScroll.ViewportWidth - 100.0);
		double value = Math.Min(val2: Math.Max(100.0, CanvasScroll.ViewportHeight - 100.0) / (CurrentPage.HeightMm * 3.7795275590551185), val1: num / (CurrentPage.WidthMm * 3.7795275590551185));
		ApplyZoom(Math.Clamp(value, 0.25, 2.0));
	}

	private void CanvasScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
		{
			ApplyZoom(_zoom * ((e.Delta > 0) ? 1.1 : 0.9));
			e.Handled = true;
		}
	}

	private void CanvasWorkspace_ManipulationDelta(object sender, ManipulationDeltaEventArgs e)
	{
		double x = e.DeltaManipulation.Scale.X;
		Vector translation = e.DeltaManipulation.Translation;
		bool flag = false;
		if (double.IsFinite(x) && x > 0.0 && Math.Abs(x - 1.0) > 0.005)
		{
			ApplyZoom(_zoom * x);
			flag = true;
		}
		if (double.IsFinite(translation.X) && Math.Abs(translation.X) > 0.01)
		{
			CanvasScroll.ScrollToHorizontalOffset(Math.Clamp(CanvasScroll.HorizontalOffset - translation.X, 0.0, CanvasScroll.ScrollableWidth));
			flag = true;
		}
		if (double.IsFinite(translation.Y) && Math.Abs(translation.Y) > 0.01)
		{
			CanvasScroll.ScrollToVerticalOffset(Math.Clamp(CanvasScroll.VerticalOffset - translation.Y, 0.0, CanvasScroll.ScrollableHeight));
			flag = true;
		}
		if (flag)
		{
			e.Handled = true;
		}
	}

	private void CanvasScroll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space)))
		{
			_panning = true;
			_panStart = e.GetPosition(CanvasScroll);
			_panHorizontal = CanvasScroll.HorizontalOffset;
			_panVertical = CanvasScroll.VerticalOffset;
			CanvasScroll.CaptureMouse();
			base.Cursor = Cursors.ScrollAll;
			e.Handled = true;
		}
	}

	private void CanvasScroll_PreviewMouseMove(object sender, MouseEventArgs e)
	{
		if (_panning)
		{
			Point position = e.GetPosition(CanvasScroll);
			CanvasScroll.ScrollToHorizontalOffset(_panHorizontal - (position.X - _panStart.X));
			CanvasScroll.ScrollToVerticalOffset(_panVertical - (position.Y - _panStart.Y));
			e.Handled = true;
		}
	}

	private void CanvasScroll_PreviewMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (_panning)
		{
			_panning = false;
			CanvasScroll.ReleaseMouseCapture();
			base.Cursor = Cursors.Arrow;
			e.Handled = true;
		}
	}

	private void ToggleGrid_Click(object sender, RoutedEventArgs e)
	{
		CurrentPage.ShowGrid = sender is MenuItem menuItem && menuItem.IsChecked;
		GuideOverlayCanvas.ShowGrid = CurrentPage.ShowGrid;
		GuideOverlayCanvas.RefreshGuides();
		MarkDirty();
	}

	private void ToggleSafe_Click(object sender, RoutedEventArgs e)
	{
		CurrentPage.ShowSafeArea = sender is MenuItem menuItem && menuItem.IsChecked;
		GuideOverlayCanvas.ShowSafeArea = CurrentPage.ShowSafeArea;
		GuideOverlayCanvas.RefreshGuides();
		MarkDirty();
	}

	private void ToggleBleed_Click(object sender, RoutedEventArgs e)
	{
		CurrentPage.ShowBleed = sender is MenuItem menuItem && menuItem.IsChecked;
		GuideOverlayCanvas.ShowBleed = CurrentPage.ShowBleed;
		GuideOverlayCanvas.RefreshGuides();
		MarkDirty();
	}

	private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
	{
		bool flag = LeftColumn.Width.Value == 0.0;
		SetLeftPanelVisible(flag);
		_leftManuallyHidden = !flag;
	}

	private void ToggleRightPanel_Click(object sender, RoutedEventArgs e)
	{
		bool flag = RightColumn.Width.Value == 0.0;
		SetRightPanelVisible(flag);
		_rightManuallyHidden = !flag;
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (base.IsLoaded)
		{
			UpdateToolbarForWidth(base.ActualWidth);
		}
		if (!base.IsLoaded || !_settings.Current.AutoCollapsePanels)
		{
			return;
		}
		if (base.ActualWidth < 920.0)
		{
			if (RightColumn.Width.Value > 0.0)
			{
				SetRightPanelVisible(show: false);
			}
			RightPanelToggleButton.Visibility = Visibility.Visible;
		}
		else if (!_rightManuallyHidden && RightColumn.Width.Value == 0.0)
		{
			SetRightPanelVisible(show: true);
		}
		if (base.ActualWidth < 760.0)
		{
			if (LeftColumn.Width.Value > 0.0)
			{
				SetLeftPanelVisible(show: false);
			}
			LeftPanelToggleButton.Visibility = Visibility.Visible;
		}
		else if (!_leftManuallyHidden && LeftColumn.Width.Value == 0.0)
		{
			SetLeftPanelVisible(show: true);
		}
	}

	private void UpdateToolbarForWidth(double width)
	{
		if (!(RootGrid.Children.OfType<Border>().FirstOrDefault((Border x) => Grid.GetRow(x) == 1)?.Child is Grid))
		{
			return;
		}
		bool flag = width < 1120.0;
		foreach (KeyValuePair<Button, (string, string, string)> topCommandButton in _topCommandButtons)
		{
			var (icon, label, text) = topCommandButton.Value;
			topCommandButton.Key.Content = TopButtonContent(icon, label, flag);
			topCommandButton.Key.MinWidth = (flag ? 38 : ((text == "Export") ? 84 : 42));
			topCommandButton.Key.Padding = (flag ? new Thickness(8.0, 5.0, 8.0, 5.0) : new Thickness(10.0, 5.0, 10.0, 5.0));
			bool flag2 = width < 720.0;
			if (flag2)
			{
				bool flag3 = ((text == "Paper" || text == "Check") ? true : false);
				flag2 = flag3 || (text == "Properties" && RightColumn.Width.Value > 0.0);
			}
			bool flag4 = flag2;
			topCommandButton.Key.Visibility = (flag4 ? Visibility.Collapsed : Visibility.Visible);
		}
		if (LeftPanelToggleButton != null)
		{
			LeftPanelToggleButton.Visibility = ((!(LeftColumn.Width.Value <= 0.0)) ? Visibility.Collapsed : Visibility.Visible);
		}
		ProjectTitleText.Visibility = ((width < 1080.0) ? Visibility.Collapsed : Visibility.Visible);
	}

	private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Escape)
		{
			bool flag = false;
			foreach (DesignerItem value2 in _visuals.Values)
			{
				flag |= value2.CancelInteraction();
			}
			if (flag)
			{
				e.Handled = true;
			}
			else if (_overlapDragArmed)
			{
				foreach (KeyValuePair<Guid, Point> overlapDragOrigin in _overlapDragOrigins)
				{
					if (_visuals.TryGetValue(overlapDragOrigin.Key, out DesignerItem value))
					{
						Canvas.SetLeft(value, overlapDragOrigin.Value.X);
						Canvas.SetTop(value, overlapDragOrigin.Value.Y);
						SyncOverflowVisual(overlapDragOrigin.Key);
					}
				}
				if (_overlapDragging && _undo.Count > 0)
				{
					_undo.Pop();
				}
				EndLightweightPreview();
				ResetSnapLatch();
				PageCanvas.ReleaseMouseCapture();
				_overlapDragArmed = false;
				_overlapDragging = false;
				_overlapDragLeaderId = null;
				_overlapDragOrigins.Clear();
				StatusText.Text = "重なり選択の移動をキャンセルしました";
				e.Handled = true;
			}
			else if (_marqueeSelecting)
			{
				_marqueeSelecting = false;
				PageCanvas.ReleaseMouseCapture();
				if (_marqueeRectangle != null)
				{
					PageCanvas.Children.Remove(_marqueeRectangle);
				}
				_marqueeRectangle = null;
				_selectedIds.Clear();
				foreach (Guid item in _marqueeBaseSelection)
				{
					_selectedIds.Add(item);
				}
				UpdateSelectionVisuals();
				e.Handled = true;
			}
			else if (_freehandDrawing)
			{
				_freehandDrawing = false;
				PageCanvas.ReleaseMouseCapture();
				if (_freehandPreview != null)
				{
					PageCanvas.Children.Remove(_freehandPreview);
				}
				_freehandPreview = null;
				_freehandPoints.Clear();
				ReturnToSelectionMode("手描きをキャンセルしました");
				e.Handled = true;
			}
			else
			{
				IInputElement focusedElement = Keyboard.FocusedElement;
				if ((focusedElement is TextBox || focusedElement is ComboBox) ? true : false)
				{
					Keyboard.ClearFocus();
					e.Handled = true;
					return;
				}
				ReturnToSelectionMode("選択ツール");
				SetActiveTopTool("選択");
				_selectedIds.Clear();
				UpdateSelectionVisuals();
				e.Handled = true;
			}
			return;
		}
		if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && e.Key == Key.K)
		{
			ShowCommandPalette();
			e.Handled = true;
			return;
		}
		string text;
		Key key;
		bool flag2;
		if (Keyboard.FocusedElement is TextBox { Tag: var tag } textBox)
		{
			text = tag?.ToString();
			HashSet<string> hashSet = new HashSet<string>
			{
				"X", "Y", "Width", "Height", "Rotation", "Opacity", "SkewX", "SkewY", "FontSize", "OutlineThickness",
				"CharacterSpacing", "LineSpacing", "ExtrusionDepth", "ExtrusionAngle", "StrokeThickness", "CornerRadius", "CornerLeft", "CornerRight", "PanelRows", "PanelColumns",
				"ShapeExtrusionDepth", "ShapeExtrusionAngle"
			};
			flag2 = text != null && hashSet.Contains(text);
			if (flag2)
			{
				key = e.Key;
				flag2 = ((key == Key.Up || key == Key.Down) ? true : false);
			}
			if (!flag2 || !double.TryParse(textBox.Text, out var result))
			{
				return;
			}
			flag2 = ((text == "PanelRows" || text == "PanelColumns") ? true : false);
			bool flag3 = flag2;
			double num = (flag3 ? 1.0 : (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? 0.1 : ((double)((!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ? 1 : 10))));
			result += ((e.Key == Key.Up) ? num : (0.0 - num));
			textBox.Text = (flag3 ? Math.Round(result).ToString("0") : result.ToString("0.##"));
			textBox.SelectAll();
			if (text != null)
			{
				switch (text.Length)
				{
				case 1:
				{
					char c2 = text[0];
					if (c2 != 'X' && c2 != 'Y')
					{
						break;
					}
					goto IL_05ee;
				}
				case 5:
				{
					char c = text[4];
					if (c != 'X')
					{
						if (c != 'Y')
						{
							if (c != 'h' || !(text == "Width"))
							{
								break;
							}
						}
						else if (!(text == "SkewY"))
						{
							break;
						}
					}
					else if (!(text == "SkewX"))
					{
						break;
					}
					goto IL_05ee;
				}
				case 6:
					if (!(text == "Height"))
					{
						break;
					}
					goto IL_05ee;
				case 8:
					if (!(text == "Rotation"))
					{
						break;
					}
					goto IL_05ee;
				case 7:
					{
						if (!(text == "Opacity"))
						{
							break;
						}
						goto IL_05ee;
					}
					IL_05ee:
					flag2 = true;
					goto IL_0a39;
				}
			}
			flag2 = false;
			goto IL_0a39;
		}
		if (Keyboard.FocusedElement is ComboBox)
		{
			return;
		}
		if (e.Key == Key.Tab && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
		{
			List<CanvasElementModel> list = (from item in CurrentPage.Elements
				where item.IsVisible && !item.IsLocked
				orderby item.ZIndex
				select item).ToList();
			if (list.Count > 0)
			{
				int num2 = list.FindIndex((CanvasElementModel item) => _selectedIds.Contains(item.Id));
				int num3 = ((!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) ? 1 : (-1));
				int index = ((num2 >= 0) ? ((num2 + num3 + list.Count) % list.Count) : ((num3 <= 0) ? (list.Count - 1) : 0));
				SelectOnly(list[index].Id);
				UpdateSelectionVisuals();
				StatusText.Text = "選択：" + list[index].Name;
			}
			e.Handled = true;
			return;
		}
		bool num4 = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
		bool flag4 = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
		if (num4)
		{
			switch (e.Key)
			{
			case Key.N:
				NewProject_Click(sender, e);
				e.Handled = true;
				return;
			case Key.O:
				OpenProject_Click(sender, e);
				e.Handled = true;
				return;
			case Key.S:
				if (flag4)
				{
					SaveAsProject_Click(sender, e);
					e.Handled = true;
				}
				else
				{
					SaveProject_Click(sender, e);
					e.Handled = true;
				}
				return;
			case Key.Z:
				Undo_Click(sender, e);
				e.Handled = true;
				return;
			case Key.Y:
				Redo_Click(sender, e);
				e.Handled = true;
				return;
			case Key.C:
				Copy_Click(sender, e);
				e.Handled = true;
				return;
			case Key.V:
				Paste_Click(sender, e);
				e.Handled = true;
				return;
			case Key.D:
				Duplicate_Click(sender, e);
				e.Handled = true;
				return;
			case Key.A:
				SelectAll_Click(sender, e);
				e.Handled = true;
				return;
			case Key.P:
				Print_Click(sender, e);
				e.Handled = true;
				return;
			case Key.E:
				Export_Click(sender, e);
				e.Handled = true;
				return;
			case Key.D0:
				FitPage();
				e.Handled = true;
				return;
			case Key.D1:
				ApplyZoom(1.0);
				e.Handled = true;
				return;
			case Key.H:
				ShowHome_Click(sender, e);
				e.Handled = true;
				return;
			case Key.OemComma:
				Settings_Click(sender, e);
				e.Handled = true;
				return;
			}
		}
		if (e.Key == Key.Delete)
		{
			Delete_Click(sender, e);
			e.Handled = true;
			return;
		}
		key = e.Key;
		if ((uint)(key - 23) > 3u || _selectedIds.Count <= 0)
		{
			return;
		}
		PushUndo();
		double num5 = (flag4 ? 10.0 : 1.0);
		foreach (CanvasElementModel item2 in CurrentPage.Elements.Where((CanvasElementModel x) => _selectedIds.Contains(x.Id) && !x.IsLocked))
		{
			if (e.Key == Key.Left)
			{
				item2.Xmm -= num5;
			}
			if (e.Key == Key.Right)
			{
				item2.Xmm += num5;
			}
			if (e.Key == Key.Up)
			{
				item2.Ymm -= num5;
			}
			if (e.Key == Key.Down)
			{
				item2.Ymm += num5;
			}
		}
		MarkDirty();
		RebuildCanvas();
		UpdatePropertyPanel();
		e.Handled = true;
		return;
		IL_0a39:
		if (flag2)
		{
			GeneralProperty_LostFocus(textBox, new RoutedEventArgs());
		}
		else
		{
			switch (text)
			{
			case "FontSize":
			case "CharacterSpacing":
			case "LineSpacing":
			case "OutlineThickness":
			case "ExtrusionDepth":
			case "ExtrusionAngle":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			if (flag2)
			{
				TextProperty_LostFocus(textBox, new RoutedEventArgs());
			}
			else
			{
				ShapeProperty_LostFocus(textBox, new RoutedEventArgs());
			}
		}
		e.Handled = true;
	}

	private async void Window_Drop(object sender, DragEventArgs e)
	{
		if (e.Data.GetDataPresent("MISE.ShapeType") && e.Data.GetData("MISE.ShapeType") is string tag)
		{
			e.Handled = true;
			Point position = e.GetPosition(PageCanvas);
			AddShape_Click(new Button
			{
				Tag = tag
			}, new RoutedEventArgs());
			CanvasElementModel activeElement = ActiveElement;
			if (activeElement != null)
			{
				activeElement.Xmm = Math.Clamp(position.X / 3.7795275590551185 - activeElement.WidthMm / 2.0, 0.0 - activeElement.WidthMm + 2.0, CurrentPage.WidthMm - 2.0);
				activeElement.Ymm = Math.Clamp(position.Y / 3.7795275590551185 - activeElement.HeightMm / 2.0, 0.0 - activeElement.HeightMm + 2.0, CurrentPage.HeightMm - 2.0);
				MarkDirty();
				RebuildCanvas();
				RefreshLayers();
				UpdatePropertyPanel();
			}
		}
		else
		{
			if (!(e.Data.GetData(DataFormats.FileDrop) is string[] array))
			{
				return;
			}
			e.Handled = true;
			string[] array2 = array;
			string[] array3 = array2;
			foreach (string text in array3)
			{
				if (System.IO.Path.GetExtension(text).Equals(".rcanvas", StringComparison.OrdinalIgnoreCase))
				{
					if (ConfirmDiscardOrSave())
					{
						OpenProject(text);
					}
					break;
				}
				if (IsImageFile(text))
				{
					AddImageFile(text);
				}
				else if (System.IO.Path.GetExtension(text).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
				{
					await AddPdfFileAsync(text);
				}
			}
		}
	}

	private void PageCanvas_Drop(object sender, DragEventArgs e)
	{
	}

	private static bool IsImageFile(string file)
	{
		return new string[7] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp" }.Contains<string>(System.IO.Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
	}

	private void Window_Closing(object? sender, CancelEventArgs e)
	{
		if (!ConfirmDiscardOrSave())
		{
			e.Cancel = true;
			return;
		}
		WindowSizing.SaveMainWindowPlacement(this, _settings.Current);
		try
		{
			_settings.Save();
		}
		catch
		{
		}
		try
		{
			if (_settings.Current.AutoSaveMinutes > 0)
			{
				_projectService.ClearAutoSave(_project);
			}
		}
		catch
		{
		}
	}

	private void Exit_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.25.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/RetailCanvas;component/mainwindow.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.25.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.25.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((MainWindow)target).PreviewKeyDown += Window_PreviewKeyDown;
			((MainWindow)target).Closing += Window_Closing;
			((MainWindow)target).SizeChanged += Window_SizeChanged;
			((MainWindow)target).Drop += Window_Drop;
			break;
		case 2:
			RootGrid = (Grid)target;
			break;
		case 3:
			ToolbarRow = (RowDefinition)target;
			break;
		case 4:
			StatusRow = (RowDefinition)target;
			break;
		case 5:
			((MenuItem)target).Click += ShowHome_Click;
			break;
		case 6:
			((MenuItem)target).Click += NewProject_Click;
			break;
		case 7:
			((MenuItem)target).Click += OpenProject_Click;
			break;
		case 8:
			((MenuItem)target).Click += SaveProject_Click;
			break;
		case 9:
			((MenuItem)target).Click += SaveAsProject_Click;
			break;
		case 10:
			((MenuItem)target).Click += SaveTemplate_Click;
			break;
		case 11:
			((MenuItem)target).Click += CreateBackup_Click;
			break;
		case 12:
			((MenuItem)target).Click += RestoreBackup_Click;
			break;
		case 13:
			((MenuItem)target).Click += Print_Click;
			break;
		case 14:
			((MenuItem)target).Click += Export_Click;
			break;
		case 15:
			((MenuItem)target).Click += Settings_Click;
			break;
		case 16:
			((MenuItem)target).Click += Exit_Click;
			break;
		case 17:
			((MenuItem)target).Click += Undo_Click;
			break;
		case 18:
			((MenuItem)target).Click += Redo_Click;
			break;
		case 19:
			((MenuItem)target).Click += Copy_Click;
			break;
		case 20:
			((MenuItem)target).Click += Paste_Click;
			break;
		case 21:
			((MenuItem)target).Click += Duplicate_Click;
			break;
		case 22:
			((MenuItem)target).Click += Delete_Click;
			break;
		case 23:
			((MenuItem)target).Click += SelectAll_Click;
			break;
		case 24:
			((MenuItem)target).Click += AddHeading_Click;
			break;
		case 25:
			((MenuItem)target).Click += AddBody_Click;
			break;
		case 26:
			((MenuItem)target).Click += AddPrice_Click;
			break;
		case 27:
			((MenuItem)target).Click += AddImage_Click;
			break;
		case 28:
			((MenuItem)target).Click += AddShape_Click;
			break;
		case 29:
			((MenuItem)target).Click += AddShape_Click;
			break;
		case 30:
			((MenuItem)target).Click += AddQr_Click;
			break;
		case 31:
			((MenuItem)target).Click += Align_Click;
			break;
		case 32:
			((MenuItem)target).Click += Align_Click;
			break;
		case 33:
			((MenuItem)target).Click += Align_Click;
			break;
		case 34:
			((MenuItem)target).Click += Align_Click;
			break;
		case 35:
			((MenuItem)target).Click += Align_Click;
			break;
		case 36:
			((MenuItem)target).Click += Align_Click;
			break;
		case 37:
			((MenuItem)target).Click += Align_Click;
			break;
		case 38:
			((MenuItem)target).Click += Align_Click;
			break;
		case 39:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 40:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 41:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 42:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 43:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 44:
			((MenuItem)target).Click += LayerOrder_Click;
			break;
		case 45:
			((MenuItem)target).Click += ZoomPreset_Click;
			break;
		case 46:
			((MenuItem)target).Click += ZoomPreset_Click;
			break;
		case 47:
			((MenuItem)target).Click += ZoomPreset_Click;
			break;
		case 48:
			((MenuItem)target).Click += ToggleGrid_Click;
			break;
		case 49:
			((MenuItem)target).Click += ToggleSafe_Click;
			break;
		case 50:
			((MenuItem)target).Click += ToggleBleed_Click;
			break;
		case 51:
			((MenuItem)target).Click += ToggleLeftPanel_Click;
			break;
		case 52:
			((MenuItem)target).Click += ToggleRightPanel_Click;
			break;
		case 53:
			((MenuItem)target).Click += ProductDatabase_Click;
			break;
		case 54:
			((MenuItem)target).Click += AssetLibrary_Click;
			break;
		case 55:
			((MenuItem)target).Click += Imposition_Click;
			break;
		case 56:
			((MenuItem)target).Click += Validate_Click;
			break;
		case 57:
			((MenuItem)target).Click += Settings_Click;
			break;
		case 58:
			((MenuItem)target).Click += Help_Click;
			break;
		case 59:
			((MenuItem)target).Click += About_Click;
			break;
		case 60:
			((Button)target).Click += SaveProject_Click;
			break;
		case 61:
			((Button)target).Click += Undo_Click;
			break;
		case 62:
			((Button)target).Click += Redo_Click;
			break;
		case 63:
			SelectToolButton = (Button)target;
			SelectToolButton.Click += SelectTool_Click;
			break;
		case 64:
			((Button)target).Click += AddHeading_Click;
			break;
		case 65:
			((Button)target).Click += AddImage_Click;
			break;
		case 66:
			((Button)target).Click += AddShape_Click;
			break;
		case 67:
			FreehandButton = (Button)target;
			FreehandButton.Click += Freehand_Click;
			break;
		case 68:
			EyedropperButton = (Button)target;
			EyedropperButton.Click += Eyedropper_Click;
			break;
		case 69:
			((Button)target).Click += AddQr_Click;
			break;
		case 70:
			ProjectTitleText = (TextBlock)target;
			ProjectTitleText.MouseLeftButtonDown += ProjectTitleText_MouseLeftButtonDown;
			break;
		case 71:
			ProjectTitleEditor = (TextBox)target;
			ProjectTitleEditor.KeyDown += ProjectTitleEditor_KeyDown;
			ProjectTitleEditor.LostKeyboardFocus += ProjectTitleEditor_LostKeyboardFocus;
			break;
		case 72:
			LeftPanelToggleButton = (Button)target;
			LeftPanelToggleButton.Click += ToggleLeftPanel_Click;
			break;
		case 73:
			RightPanelToggleButton = (Button)target;
			RightPanelToggleButton.Click += ToggleRightPanel_Click;
			break;
		case 74:
			((Button)target).Click += Validate_Click;
			break;
		case 75:
			((Button)target).Click += Export_Click;
			break;
		case 76:
			EditorGrid = (Grid)target;
			break;
		case 77:
			LeftColumn = (ColumnDefinition)target;
			break;
		case 78:
			LeftSplitterColumn = (ColumnDefinition)target;
			break;
		case 79:
			RightSplitterColumn = (ColumnDefinition)target;
			break;
		case 80:
			RightColumn = (ColumnDefinition)target;
			break;
		case 81:
			LeftPanel = (Border)target;
			break;
		case 82:
			TemplateCombo = (ComboBox)target;
			break;
		case 83:
			((Button)target).Click += ApplyTemplate_Click;
			break;
		case 84:
			((Button)target).Click += AddHeading_Click;
			break;
		case 85:
			((Button)target).Click += AddSubheading_Click;
			break;
		case 86:
			((Button)target).Click += AddBody_Click;
			break;
		case 87:
			((Button)target).Click += AddNote_Click;
			break;
		case 88:
			((Button)target).Click += AddPrice_Click;
			break;
		case 89:
			((Button)target).Click += AddProductName_Click;
			break;
		case 90:
			((Button)target).Click += AddImage_Click;
			break;
		case 91:
			((Button)target).Click += AssetLibrary_Click;
			break;
		case 92:
			((Button)target).Click += ProductDatabase_Click;
			break;
		case 93:
			((Button)target).Click += AddShape_Click;
			break;
		case 94:
			((Button)target).Click += AddShape_Click;
			break;
		case 95:
			((Button)target).Click += AddShape_Click;
			break;
		case 96:
			((Button)target).Click += AddShape_Click;
			break;
		case 97:
			((Button)target).Click += AddShape_Click;
			break;
		case 98:
			((Button)target).Click += AddShape_Click;
			break;
		case 99:
			((Button)target).Click += AddShape_Click;
			break;
		case 100:
			((Button)target).Click += Freehand_Click;
			break;
		case 101:
			((Button)target).Click += AddQr_Click;
			break;
		case 102:
			((Button)target).Click += AddPage_Click;
			break;
		case 103:
			((Button)target).Click += DuplicatePage_Click;
			break;
		case 104:
			((Button)target).Click += DeletePage_Click;
			break;
		case 105:
			PageList = (ListBox)target;
			PageList.SelectionChanged += PageList_SelectionChanged;
			break;
		case 106:
			((Button)target).Click += LayerOrder_Click;
			break;
		case 107:
			((Button)target).Click += LayerOrder_Click;
			break;
		case 108:
			((Button)target).Click += Duplicate_Click;
			break;
		case 109:
			((Button)target).Click += Delete_Click;
			break;
		case 110:
			LayerList = (ListBox)target;
			LayerList.SelectionChanged += LayerList_SelectionChanged;
			break;
		case 111:
			LeftSplitter = (GridSplitter)target;
			break;
		case 112:
			CanvasWorkspace = (Grid)target;
			CanvasWorkspace.ManipulationDelta += CanvasWorkspace_ManipulationDelta;
			break;
		case 113:
			CanvasScroll = (ScrollViewer)target;
			CanvasScroll.PreviewMouseWheel += CanvasScroll_PreviewMouseWheel;
			CanvasScroll.PreviewMouseDown += CanvasScroll_PreviewMouseDown;
			CanvasScroll.PreviewMouseMove += CanvasScroll_PreviewMouseMove;
			CanvasScroll.PreviewMouseUp += CanvasScroll_PreviewMouseUp;
			break;
		case 114:
			CanvasOuter = (Grid)target;
			break;
		case 115:
			PaperBorder = (Border)target;
			break;
		case 116:
			PageCanvas = (DesignCanvas)target;
			break;
		case 117:
			OverflowCanvas = (Canvas)target;
			break;
		case 118:
			GuideOverlayCanvas = (DesignCanvas)target;
			break;
		case 119:
			PageInfoOverlay = (TextBlock)target;
			break;
		case 120:
			SelectionMiniToolbar = (Border)target;
			break;
		case 121:
			((Button)target).Click += QuickColor_Click;
			break;
		case 122:
			((Button)target).Click += LayerOrder_Click;
			break;
		case 123:
			((Button)target).Click += LayerOrder_Click;
			break;
		case 124:
			((Button)target).Click += Duplicate_Click;
			break;
		case 125:
			((Button)target).Click += QuickLock_Click;
			break;
		case 126:
			((Button)target).Click += Delete_Click;
			break;
		case 127:
			RightSplitter = (GridSplitter)target;
			break;
		case 128:
			RightPanel = (Border)target;
			break;
		case 129:
			((Button)target).Click += ToggleRightPanel_Click;
			break;
		case 130:
			PropertyPanel = (StackPanel)target;
			break;
		case 131:
			NoSelectionText = (TextBlock)target;
			break;
		case 132:
			PropertyFields = (StackPanel)target;
			break;
		case 133:
			NameBox = (TextBox)target;
			NameBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 134:
			XBox = (TextBox)target;
			XBox.TextChanged += GeneralProperty_TextChanged;
			XBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 135:
			YBox = (TextBox)target;
			YBox.TextChanged += GeneralProperty_TextChanged;
			YBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 136:
			SkewXBox = (TextBox)target;
			SkewXBox.TextChanged += GeneralProperty_TextChanged;
			SkewXBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 137:
			SkewYBox = (TextBox)target;
			SkewYBox.TextChanged += GeneralProperty_TextChanged;
			SkewYBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 138:
			WidthBox = (TextBox)target;
			WidthBox.TextChanged += GeneralProperty_TextChanged;
			WidthBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 139:
			HeightBox = (TextBox)target;
			HeightBox.TextChanged += GeneralProperty_TextChanged;
			HeightBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 140:
			RotationBox = (TextBox)target;
			RotationBox.TextChanged += GeneralProperty_TextChanged;
			RotationBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 141:
			OpacityBox = (TextBox)target;
			OpacityBox.TextChanged += GeneralProperty_TextChanged;
			OpacityBox.LostFocus += GeneralProperty_LostFocus;
			break;
		case 142:
			AspectCheck = (CheckBox)target;
			AspectCheck.Click += CheckProperty_Click;
			break;
		case 143:
			LockCheck = (CheckBox)target;
			LockCheck.Click += CheckProperty_Click;
			break;
		case 144:
			VisibleCheck = (CheckBox)target;
			VisibleCheck.Click += CheckProperty_Click;
			break;
		case 145:
			TextProperties = (StackPanel)target;
			break;
		case 146:
			TextContentBox = (TextBox)target;
			TextContentBox.TextChanged += TextContentBox_TextChanged;
			TextContentBox.LostFocus += TextProperty_LostFocus;
			break;
		case 147:
			FontCombo = (ComboBox)target;
			FontCombo.SelectionChanged += FontCombo_SelectionChanged;
			break;
		case 148:
			FavoriteFontButton = (Button)target;
			FavoriteFontButton.Click += FavoriteFontButton_Click;
			break;
		case 149:
			((Button)target).Click += AddFont_Click;
			break;
		case 150:
			FontSizeBox = (TextBox)target;
			FontSizeBox.LostFocus += TextProperty_LostFocus;
			break;
		case 151:
			TextColorBox = (TextBox)target;
			TextColorBox.LostFocus += TextProperty_LostFocus;
			break;
		case 152:
			TextColorButton = (Button)target;
			TextColorButton.Click += TextColorPicker_Click;
			break;
		case 153:
			TextBackgroundBox = (TextBox)target;
			TextBackgroundBox.LostFocus += TextProperty_LostFocus;
			break;
		case 154:
			TextBackgroundButton = (Button)target;
			TextBackgroundButton.Click += TextEffectColorPicker_Click;
			break;
		case 155:
			((Button)target).Click += TextBackgroundTransparent_Click;
			break;
		case 156:
			BoldToggle = (ToggleButton)target;
			BoldToggle.Click += TextToggle_Click;
			break;
		case 157:
			ItalicToggle = (ToggleButton)target;
			ItalicToggle.Click += TextToggle_Click;
			break;
		case 158:
			UnderlineToggle = (ToggleButton)target;
			UnderlineToggle.Click += TextToggle_Click;
			break;
		case 159:
			((Button)target).Click += TextAlign_Click;
			break;
		case 160:
			((Button)target).Click += TextAlign_Click;
			break;
		case 161:
			((Button)target).Click += TextAlign_Click;
			break;
		case 162:
			TextOutlineColorBox = (TextBox)target;
			TextOutlineColorBox.LostFocus += TextProperty_LostFocus;
			break;
		case 163:
			TextOutlineColorButton = (Button)target;
			TextOutlineColorButton.Click += TextEffectColorPicker_Click;
			break;
		case 164:
			TextOutlineThicknessBox = (TextBox)target;
			TextOutlineThicknessBox.LostFocus += TextProperty_LostFocus;
			break;
		case 165:
			TextExtrusionColorBox = (TextBox)target;
			TextExtrusionColorBox.LostFocus += TextProperty_LostFocus;
			break;
		case 166:
			TextExtrusionColorButton = (Button)target;
			TextExtrusionColorButton.Click += TextEffectColorPicker_Click;
			break;
		case 167:
			TextExtrusionDepthBox = (TextBox)target;
			TextExtrusionDepthBox.LostFocus += TextProperty_LostFocus;
			break;
		case 168:
			TextExtrusionAngleBox = (TextBox)target;
			TextExtrusionAngleBox.LostFocus += TextProperty_LostFocus;
			break;
		case 169:
			ShapeProperties = (StackPanel)target;
			break;
		case 170:
			FillColorBox = (TextBox)target;
			FillColorBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 171:
			FillColorButton = (Button)target;
			FillColorButton.Click += ShapeColorPicker_Click;
			break;
		case 172:
			StrokeColorBox = (TextBox)target;
			StrokeColorBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 173:
			StrokeColorButton = (Button)target;
			StrokeColorButton.Click += ShapeColorPicker_Click;
			break;
		case 174:
			StrokeThicknessBox = (TextBox)target;
			StrokeThicknessBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 175:
			CornerRadiusBox = (TextBox)target;
			CornerRadiusBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 176:
			CornerLeftBox = (TextBox)target;
			CornerLeftBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 177:
			CornerRightBox = (TextBox)target;
			CornerRightBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 178:
			PanelRowsBox = (TextBox)target;
			PanelRowsBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 179:
			PanelColumnsBox = (TextBox)target;
			PanelColumnsBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 180:
			PanelRowSplitsBox = (TextBox)target;
			PanelRowSplitsBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 181:
			PanelColumnSplitsBox = (TextBox)target;
			PanelColumnSplitsBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 182:
			((Button)target).Click += EditShapePoints_Click;
			break;
		case 183:
			((Button)target).Click += EditPanelDividers_Click;
			break;
		case 184:
			((Button)target).Click += EditPanelCellColors_Click;
			break;
		case 185:
			ShapeExtrusionColorBox = (TextBox)target;
			ShapeExtrusionColorBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 186:
			ShapeExtrusionColorButton = (Button)target;
			ShapeExtrusionColorButton.Click += ShapeColorPicker_Click;
			break;
		case 187:
			ShapeExtrusionDepthBox = (TextBox)target;
			ShapeExtrusionDepthBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 188:
			ShapeExtrusionAngleBox = (TextBox)target;
			ShapeExtrusionAngleBox.LostFocus += ShapeProperty_LostFocus;
			break;
		case 189:
			ImageProperties = (StackPanel)target;
			break;
		case 190:
			ImageDpiText = (TextBlock)target;
			break;
		case 191:
			ImageSizeText = (TextBlock)target;
			break;
		case 192:
			((Button)target).Click += ReplaceImage_Click;
			break;
		case 193:
			((Button)target).Click += ResetImageRatio_Click;
			break;
		case 194:
			QrProperties = (StackPanel)target;
			break;
		case 195:
			QrContentBox = (TextBox)target;
			QrContentBox.LostFocus += QrProperty_LostFocus;
			break;
		case 196:
			QrForegroundBox = (TextBox)target;
			QrForegroundBox.LostFocus += QrProperty_LostFocus;
			break;
		case 197:
			QrForegroundButton = (Button)target;
			QrForegroundButton.Click += QrColorPicker_Click;
			break;
		case 198:
			QrBackgroundBox = (TextBox)target;
			QrBackgroundBox.LostFocus += QrProperty_LostFocus;
			break;
		case 199:
			QrBackgroundButton = (Button)target;
			QrBackgroundButton.Click += QrColorPicker_Click;
			break;
		case 200:
			QrLevelCombo = (ComboBox)target;
			QrLevelCombo.SelectionChanged += QrLevelCombo_SelectionChanged;
			break;
		case 201:
			((Button)target).Click += UpdateQr_Click;
			break;
		case 202:
			StatusText = (TextBlock)target;
			break;
		case 203:
			ErrorCountText = (TextBlock)target;
			ErrorCountText.MouseLeftButtonUp += ErrorCountText_MouseLeftButtonUp;
			break;
		case 204:
			AutoSaveText = (TextBlock)target;
			break;
		case 205:
			PageStatusText = (TextBlock)target;
			break;
		case 206:
			ZoomSlider = (Slider)target;
			ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;
			break;
		case 207:
			ZoomText = (TextBlock)target;
			break;
		case 208:
			HomeOverlay = (Grid)target;
			break;
		case 209:
			((Button)target).Click += NewProject_Click;
			break;
		case 210:
			((Button)target).Click += QuickNew_Click;
			break;
		case 211:
			((Button)target).Click += QuickNew_Click;
			break;
		case 212:
			((Button)target).Click += OpenProject_Click;
			break;
		case 213:
			RecentList = (ListBox)target;
			RecentList.MouseDoubleClick += RecentList_MouseDoubleClick;
			break;
		case 214:
			((Button)target).Click += TemplateGallery_Click;
			break;
		case 215:
			((Button)target).Click += ProductDatabase_Click;
			break;
		case 216:
			((Button)target).Click += AssetLibrary_Click;
			break;
		case 217:
			((Button)target).Click += CreateBackup_Click;
			break;
		case 218:
			((Button)target).Click += Settings_Click;
			break;
		case 219:
			VersionText = (TextBlock)target;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
