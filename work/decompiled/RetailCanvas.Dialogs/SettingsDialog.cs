using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class SettingsDialog : Window
{
	private readonly AppSettings _settings;

	private readonly ComboBox _windowMode = new ComboBox();

	private readonly TextBox _windowWidth = new TextBox();

	private readonly TextBox _windowHeight = new TextBox();

	private readonly CheckBox _rememberWindow = new CheckBox
	{
		Content = "終了時の位置・サイズを記憶する"
	};

	private readonly ComboBox _density = new ComboBox();

	private readonly ComboBox _zoomMode = new ComboBox();

	private readonly TextBox _zoomPercent = new TextBox();

	private readonly CheckBox _showHome = new CheckBox
	{
		Content = "起動時にホーム画面を表示"
	};

	private readonly CheckBox _showLeftPanel = new CheckBox
	{
		Content = "起動時に左パネルを表示"
	};

	private readonly CheckBox _showRightPanel = new CheckBox
	{
		Content = "起動時に右パネルを表示"
	};

	private readonly CheckBox _autoCollapse = new CheckBox
	{
		Content = "ウインドウが狭いときパネルを自動収納"
	};

	private readonly TextBox _leftPanelWidth = new TextBox();

	private readonly TextBox _rightPanelWidth = new TextBox();

	private readonly ComboBox _autosave = new ComboBox();

	private readonly TextBox _safe = new TextBox();

	private readonly TextBox _grid = new TextBox();

	private readonly CheckBox _snapGrid = new CheckBox
	{
		Content = "グリッドに吸着"
	};

	private readonly CheckBox _snapSafe = new CheckBox
	{
		Content = "安全領域に吸着"
	};

	private readonly CheckBox _snapObjects = new CheckBox
	{
		Content = "ほかのオブジェクトの端・中心に吸着"
	};

	private readonly CheckBox _snapPage = new CheckBox
	{
		Content = "台紙の端に吸着"
	};

	private readonly TextBox _snapDistance = new TextBox();

	private readonly TextBox _snapStartPixels = new TextBox();

	private readonly TextBox _snapReleasePixels = new TextBox();

	private readonly ComboBox _snapPriorityMode = new ComboBox();

	private readonly CheckBox _showVerticalCenterGuide = new CheckBox
	{
		Content = "縦の正中線を表示"
	};

	private readonly CheckBox _showHorizontalCenterGuide = new CheckBox
	{
		Content = "横の正中線を表示"
	};

	private readonly CheckBox _snapVerticalCenterGuide = new CheckBox
	{
		Content = "縦の正中線へ吸着"
	};

	private readonly CheckBox _snapHorizontalCenterGuide = new CheckBox
	{
		Content = "横の正中線へ吸着"
	};

	private readonly ComboBox _vertexSnapMode = new ComboBox();

	private readonly TextBox _actualSizeCalibration = new TextBox();

	private readonly ComboBox _performanceMode = new ComboBox();

	private readonly CheckBox _lightweightDrag = new CheckBox
	{
		Content = "移動・拡大縮小中は軽量プレビューを使う"
	};

	private readonly CheckBox _invertOutOfBounds = new CheckBox
	{
		Content = "台紙からはみ出した部分をネガ反転で表示"
	};

	private readonly CheckBox _showGrid = new CheckBox
	{
		Content = "新規プロジェクトでグリッドを表示"
	};

	private readonly CheckBox _showSafe = new CheckBox
	{
		Content = "新規プロジェクトで安全領域を表示"
	};

	private readonly ComboBox _dpi = new ComboBox();

	private readonly ComboBox _printMode = new ComboBox();

	private readonly CheckBox _warnExport = new CheckBox
	{
		Content = "エラーがある場合、書き出し前に警告"
	};

	private readonly ComboBox _exportAction = new ComboBox();

	private readonly TextBox _assetFolder = new TextBox();

	public SettingsDialog(AppSettings settings)
	{
		_settings = settings;
		base.Title = "環境設定";
		base.Width = 720.0;
		base.Height = 720.0;
		base.ResizeMode = ResizeMode.CanResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 720.0, 560.0, 420.0);
		_windowMode.ItemsSource = new string[4] { "画面に合わせる", "最大化", "前回の状態", "カスタム" };
		_windowMode.SelectedItem = settings.StartupWindowMode;
		if (_windowMode.SelectedIndex < 0)
		{
			_windowMode.SelectedIndex = 0;
		}
		_windowMode.SelectionChanged += delegate
		{
			UpdateCustomSizeEnabled();
		};
		_windowWidth.Text = settings.CustomWindowWidth.ToString("0");
		_windowHeight.Text = settings.CustomWindowHeight.ToString("0");
		_rememberWindow.IsChecked = settings.RememberWindowPlacement;
		_density.ItemsSource = new string[3] { "コンパクト", "標準", "ゆったり" };
		_density.SelectedItem = settings.UiDensity;
		_zoomMode.ItemsSource = new string[5] { "全体表示", "50%", "75%", "100%", "カスタム" };
		_zoomMode.SelectedItem = settings.StartupZoomMode;
		if (_zoomMode.SelectedIndex < 0)
		{
			_zoomMode.SelectedIndex = 0;
		}
		_zoomPercent.Text = settings.DefaultZoomPercent.ToString();
		_showHome.IsChecked = settings.ShowHomeOnStartup;
		_showLeftPanel.IsChecked = settings.ShowLeftPanelOnStartup;
		_showRightPanel.IsChecked = settings.ShowRightPanelOnStartup;
		_autoCollapse.IsChecked = settings.AutoCollapsePanels;
		_leftPanelWidth.Text = settings.LeftPanelWidth.ToString("0");
		_rightPanelWidth.Text = settings.RightPanelWidth.ToString("0");
		_autosave.ItemsSource = new int[5] { 0, 1, 3, 5, 10 };
		_autosave.SelectedItem = new int[5] { 0, 1, 3, 5, 10 }.OrderBy((int x) => Math.Abs(x - settings.AutoSaveMinutes)).First();
		_safe.Text = settings.DefaultSafeMarginMm.ToString("0.#");
		_grid.Text = settings.GridSizeMm.ToString("0.#");
		_snapGrid.IsChecked = settings.SnapToGrid;
		_snapSafe.IsChecked = settings.SnapToSafeArea;
		_snapObjects.IsChecked = settings.SnapToObjects;
		_snapPage.IsChecked = settings.SnapToPageEdges;
		_snapDistance.Text = settings.SnapDistanceMm.ToString("0.#");
		_snapStartPixels.Text = settings.SnapStartPixels.ToString("0.#");
		_snapReleasePixels.Text = settings.SnapReleasePixels.ToString("0.#");
		_snapPriorityMode.ItemsSource = new string[2] { "グリッド優先", "スマート吸着" };
		_snapPriorityMode.SelectedItem = settings.SnapPriorityMode;
		if (_snapPriorityMode.SelectedIndex < 0)
		{
			_snapPriorityMode.SelectedIndex = 0;
		}
		_showVerticalCenterGuide.IsChecked = settings.ShowCenterGuides && settings.ShowVerticalCenterGuide;
		_showHorizontalCenterGuide.IsChecked = settings.ShowCenterGuides && settings.ShowHorizontalCenterGuide;
		_snapVerticalCenterGuide.IsChecked = settings.SnapToVerticalCenterGuide;
		_snapHorizontalCenterGuide.IsChecked = settings.SnapToHorizontalCenterGuide;
		_vertexSnapMode.ItemsSource = new string[2] { "交点のみ", "線上も許可" };
		_vertexSnapMode.SelectedItem = settings.VertexSnapMode;
		if (_vertexSnapMode.SelectedIndex < 0)
		{
			_vertexSnapMode.SelectedIndex = 0;
		}
		_actualSizeCalibration.Text = settings.ActualSizeCalibrationPercent.ToString("0.#");
		_performanceMode.ItemsSource = new string[3] { "自動", "画質優先", "軽快さ優先" };
		_performanceMode.SelectedItem = settings.PerformanceMode;
		if (_performanceMode.SelectedIndex < 0)
		{
			_performanceMode.SelectedIndex = 0;
		}
		_lightweightDrag.IsChecked = settings.UseLightweightDragPreview;
		_invertOutOfBounds.IsChecked = settings.InvertOutOfBoundsObjects;
		_showGrid.IsChecked = settings.ShowGridOnNewProjects;
		_showSafe.IsChecked = settings.ShowSafeAreaOnNewProjects;
		_dpi.ItemsSource = new int[4] { 150, 200, 300, 600 };
		_dpi.SelectedItem = new int[4] { 150, 200, 300, 600 }.OrderBy((int x) => Math.Abs(x - settings.DefaultExportDpi)).First();
		_printMode.ItemsSource = new string[5] { "家庭用プリンタ", "コンビニ印刷", "業務用プリンタ", "印刷会社入稿", "PDF閲覧用" };
		_printMode.SelectedItem = settings.DefaultPrintMode;
		_warnExport.IsChecked = settings.WarnBeforeExportOnErrors;
		_exportAction.ItemsSource = new string[3] { "確認する", "自動で保存先を開く", "何もしない" };
		_exportAction.SelectedItem = settings.ExportCompletionAction;
		if (_exportAction.SelectedIndex < 0)
		{
			_exportAction.SelectedIndex = 0;
		}
		_assetFolder.Text = settings.AssetFolder;
		Build();
		UpdateCustomSizeEnabled();
	}

	private void Build()
	{
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
			Content = "保存して適用",
			MinWidth = 130.0,
			Style = (FindResource("PrimaryButton") as Style)
		};
		button2.Click += Save_Click;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(4.0, 0.0, 0.0, 12.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "環境設定",
			FontSize = 24.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush)
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "MISE 1.1.12",
			Foreground = Brushes.SlateGray,
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		dockPanel.Children.Add(stackPanel2);
		TabControl tabControl = new TabControl();
		tabControl.Items.Add(new TabItem
		{
			Header = "画面・起動",
			Content = Scroll(BuildDisplayTab())
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "編集・操作",
			Content = Scroll(BuildEditingTab())
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "出力・保存",
			Content = Scroll(BuildOutputTab())
		});
		tabControl.Items.Add(new TabItem
		{
			Header = "リリースノート",
			Content = Scroll(BuildReleaseNotes())
		});
		dockPanel.Children.Add(tabControl);
		base.Content = dockPanel;
	}

	private Panel BuildDisplayTab()
	{
		StackPanel stackPanel = TabPanel();
		Section(stackPanel, "ウインドウ");
		Add(stackPanel, "起動時の画面サイズ", _windowMode);
		Grid grid = TwoColumns("幅", _windowWidth, "高さ", _windowHeight);
		grid.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		stackPanel.Children.Add(grid);
		_rememberWindow.Margin = new Thickness(0.0, 0.0, 0.0, 14.0);
		stackPanel.Children.Add(_rememberWindow);
		Section(stackPanel, "表示");
		Add(stackPanel, "UI表示密度", _density);
		Add(stackPanel, "起動時のズーム", _zoomMode);
		Add(stackPanel, "カスタムズーム（25～400%）", _zoomPercent);
		_showHome.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showHome);
		_showLeftPanel.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showLeftPanel);
		_showRightPanel.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showRightPanel);
		_autoCollapse.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel.Children.Add(_autoCollapse);
		stackPanel.Children.Add(TwoColumns("左パネル幅", _leftPanelWidth, "右パネル幅", _rightPanelWidth));
		Section(stackPanel, "原寸大表示");
		Add(stackPanel, "実寸表示の補正率（%）", _actualSizeCalibration);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "MISEはディスプレイ情報から物理サイズを推定します。定規とのずれがある場合だけ補正してください。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		});
		Section(stackPanel, "動作と画質");
		Add(stackPanel, "動作モード", _performanceMode);
		_lightweightDrag.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		stackPanel.Children.Add(_lightweightDrag);
		return stackPanel;
	}

	private Panel BuildEditingTab()
	{
		StackPanel stackPanel = TabPanel();
		Section(stackPanel, "保存と復元");
		Add(stackPanel, "自動保存間隔（分、0で無効）", _autosave);
		Section(stackPanel, "ガイドと吸着");
		stackPanel.Children.Add(TwoColumns("安全領域（mm）", _safe, "グリッド間隔（mm）", _grid));
		_snapGrid.Margin = new Thickness(0.0, 10.0, 0.0, 8.0);
		stackPanel.Children.Add(_snapGrid);
		Add(stackPanel, "頂点編集の吸着先", _vertexSnapMode);
		_snapSafe.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_snapSafe);
		_snapObjects.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_snapObjects);
		_snapPage.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_snapPage);
		Add(stackPanel, "吸着対象の優先方法", _snapPriorityMode);
		stackPanel.Children.Add(TwoColumns("吸着開始距離（画面px）", _snapStartPixels, "吸着解除距離（画面px）", _snapReleasePixels));
		Section(stackPanel, "正中線");
		_showVerticalCenterGuide.Margin = new Thickness(0.0, 4.0, 0.0, 8.0);
		_showHorizontalCenterGuide.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		_snapVerticalCenterGuide.Margin = new Thickness(16.0, 0.0, 0.0, 8.0);
		_snapHorizontalCenterGuide.Margin = new Thickness(16.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showVerticalCenterGuide);
		stackPanel.Children.Add(_showHorizontalCenterGuide);
		stackPanel.Children.Add(_snapVerticalCenterGuide);
		stackPanel.Children.Add(_snapHorizontalCenterGuide);
		_snapDistance.Visibility = Visibility.Collapsed;
		stackPanel.Children.Add(new TextBlock
		{
			Text = "移動中にShiftを押すと、一時的にすべての吸着を解除できます。",
			Foreground = Brushes.SlateGray,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		});
		_invertOutOfBounds.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_invertOutOfBounds);
		_showGrid.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showGrid);
		_showSafe.Margin = new Thickness(0.0, 0.0, 0.0, 8.0);
		stackPanel.Children.Add(_showSafe);
		return stackPanel;
	}

	private Panel BuildOutputTab()
	{
		StackPanel stackPanel = TabPanel();
		Section(stackPanel, "書き出し・印刷");
		Add(stackPanel, "標準書き出し解像度（dpi）", _dpi);
		Add(stackPanel, "標準印刷モード", _printMode);
		_warnExport.Margin = new Thickness(0.0, 0.0, 0.0, 12.0);
		stackPanel.Children.Add(_warnExport);
		Add(stackPanel, "書き出し完了後", _exportAction);
		Section(stackPanel, "素材");
		Grid grid = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = GridLength.Auto
				}
			},
			Children = { (UIElement)_assetFolder }
		};
		Button button = new Button
		{
			Content = "参照"
		};
		button.Click += Browse_Click;
		Grid.SetColumn(button, 1);
		grid.Children.Add(button);
		Add(stackPanel, "素材フォルダ", grid);
		stackPanel.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(244, 246, 249)),
			CornerRadius = new CornerRadius(5.0),
			Padding = new Thickness(10.0),
			Child = new TextBlock
			{
				Text = "設定・商品データ・テンプレートはPC内に保存され、許可なく外部送信されません。",
				TextWrapping = TextWrapping.Wrap,
				Foreground = new SolidColorBrush(Color.FromRgb(88, 99, 116))
			}
		});
		return stackPanel;
	}

	private static Panel BuildReleaseNotes()
	{
		StackPanel stackPanel = TabPanel();
		TextBlock element = new TextBlock
		{
			Text = "MISE 1.1.12 – 2026/07/29\n・起動時の「自動保存から復元」確認ウィンドウを廃止\n・復元可能な自動保存データをホームの「最近使用したプロジェクト」先頭へ表示\n・自動保存項目はプロジェクト名へ明記し、更新日時順で複数候補を確認可能\n・自動保存から開いたプロジェクトは未保存状態として扱い、元データへの誤上書きを防止\n・通常保存が完了するまで自動保存データを保持し、復元失敗時の再試行を可能に変更\n・一時的なリンク切れが解消した通常プロジェクトの表示名を自動で正常化\n\nMISE 1.1.11 – 2026/07/29\n・文字プロパティへ「字間・左右」と「行間・上下」の個別調整を追加\n・間隔は0を標準とし、プラス値で広げ、マイナス値で詰める操作へ統一\n・日本語、英数字、絵文字、結合文字を文字単位で安全に処理\n・字間変更後も左揃え、中央揃え、右揃え、折り返し、上下配置を維持\n・縁取り、下線、立体効果を字間・行間調整後の文字形状へ連動\n・文字間隔をプロジェクト保存、スタイルコピー、画像、PDF、印刷へ共通反映\n\nMISE 1.1.10 – 2026/07/29\n・書き出し・印刷の高精細描画を編集画面から分離し、処理中の応答停止を改善\n・読み込んだPDFを編集時は長辺2600px、最終出力時は長辺最大5200pxで再描画\n・ハート、星、リング、バッジ等を正方形基準で生成し、自然な縦横比を維持\n・角設定の「角なし」を実際の動作に合う「丸みなし（直角）」へ変更\n・台紙と塗り面のテクスチャ選択・ライブプレビューを改善\n・素材ライブラリへ容量、拡張子、更新日時、画像寸法による並び替えを追加\n・商品データベースの選択チェックを1クリック操作とID保持方式へ修正\n・文字ウェイトを100～900で指定できる編集項目を追加\n・2本指操作の上下・左右パンと、キャンバス端の自動スクロールを改善\n・旧バージョンの環境設定値を自動補正し、無変更で確定した際の警告を防止\n\nMISE 1.1.9 – 2026/07/22\n・1.1.8で起動時に「RetailCanvas」内部アセンブリを検索してXAML読込が停止する不具合を修正\n・既存BAMLとの互換性に必要な内部アセンブリ識別子をRetailCanvasへ戻し、起動リソース参照を整合\n・利用者が起動する公開ファイル名は従来どおりOutput\\MISE.exeに統一\n・1.1.8で追加した左ツールレール、階層式プロパティ、商品データ連動テンプレート、選択操作改善をすべて継承\n\nMISE 1.1.8 – 2026/07/22\n・通常クリックとドラッグを最前面オブジェクトの選択・移動専用にし、連続クリック判定による移動干渉を解消\n・重なったオブジェクトの選択をAlt＋クリック、右クリックの対象一覧、Tab／Shift＋Tabへ分離し、Altのまま背面をドラッグ移動可能\n・上部に集中していた作成機能を、スタイリッシュな縦型ツールレールを備えた左パネルへ再配置\n・左側を選択、テンプレート、文字、画像、図形、パネル、線、手描き、スポイト、QR、商品、素材、ページ、レイヤーの目的別構成へ刷新\n・四角形、円形、特殊図形、線・矢印を検索、最近使用、お気に入り、右クリック選択から素早く呼び出せる構成を維持\n・右プロパティの固定タブ切替を廃止し、親項目を開くと子設定が現れる階層式フォルダへ変更\n・位置とサイズ、文字とフォント、縁取り、立体、図形の塗りと線、パネルと頂点、画像、QRを必要に応じて複数同時展開可能\n・テンプレート機能を左側へ復帰し、登録済み商品を選ぶだけで商品名、型番、画像、価格、特徴、仕様、URL／QRを自動反映\n・テンプレートのプレースホルダーへ商品データを割り当て、商品単品訴求、新製品、防水、バッテリー、店員ガイド等を即時作成\n・ユーザーテンプレートにもプレースホルダー名が設定されていれば商品データを自動反映\n・左パネル標準幅を新しいツールレールに合わせて最適化し、狭い画面では従来どおり自動収納\n・Windows向け公開EXE名をMISE.exeへ統一し、OutputにはMISE.exeを生成\n\nMISE 1.1.7 – 2026/07/22\n・上部ツールバーを保存／履歴、作成ツール、台紙／ガイド、確認／書き出しの順に再設計\n・通常幅はアイコン＋名称、狭い幅はアイコン表示へ切り替え、隠れた機能を［その他］へ集約\n・上部で選んだ文字・画像・図形・パネル・線・手描き・QR・ライブラリの詳細を左パネルへ連動表示\n・左パネルを［作成］［ページ］［レイヤー］の固定構成へ整理し、目的別のプリセットを表示\n・図形を四角形／円形／特殊図形へ整理し、長方形・正方形・角丸、円・楕円・半円・リング、星・ハート等を実装\n・線・矢印とパネルを独立カテゴリにし、図形の検索、最近使用、お気に入りへ対応\n・図形タイルはクリックで表示範囲中央へ追加、キャンバスへドラッグして任意位置へ配置可能\n・右プロパティのカテゴリ位置を固定し、クリックするたび並び順が変わる問題を解消\n・数値入力欄をコンパクトな一行表示へ戻し、上下スピンボタンと↑↓キー操作は維持\n・縦の正中線と横の正中線を個別に表示／非表示、個別に吸着オン／オフできるガイドメニューを追加\n・設定画面にも縦横正中線の表示と吸着を個別設定する項目を追加\n・Windows向け出力ファイル名をOutput\\MISE.exeへ変更し、既存のRetailCanvas.exeは適用時に退避\n・既存.rcanvas、商品DB、素材、テンプレート、設定、内部リソース識別子との互換性を維持\n\nMISE 1.1.6 – 2026/07/22\n・ズーム中に文字・画像・図形・QR・PDF・商品・再利用ブロックを追加した際、台紙全体ではなく現在表示している範囲の中央へ配置\n・上部の［台紙］から、編集途中でも用紙プリセット・自由サイズ・縦横・背景色・透明・テクスチャを変更可能に改善\n・台紙変更時の要素処理を「位置維持」「中央位置維持」「比率を保って拡縮」「台紙と一緒に回転」から選択可能に改善\n・安全領域、塗り足し、印刷余白、グリッド間隔と各ガイド表示を台紙設定でまとめて編集\n・台紙だけ／オブジェクトだけ／両方を90度または180度回転する機能を追加\n・カラーパレットのカラーサークルと明度、RGB、HSV、カラーコード、透明度を相互連動\n・変更前／変更後色の比較、最近色の永続保存、ブランド色、現在のデザイン内で使用中の色を追加\n・スポイト取得色をカラーサークル・RGB／HSV・変更後色へ即時反映\n・台紙、文字背景、図形、縁取り、立体色など色の指定をカラーパレットへ統一\n・パネルの分割線を＋／－で増減し、2／3／4分割、見出し＋本文、見出し＋2列本文のプリセットを追加\n・分割線の色・太さ・透明度・線種と区画ごとの色、ドラッグ位置、台紙グリッド単位の吸着を維持\n・削除対象の分割線をオレンジで事前表示し、任意多角形のパネルにも分割線を配置可能\n・図形を四角形／円形／線・矢印／特殊図形へ再整理し、リング、ひし形、バッジ、ラベルを追加\n・線の始点と終点を個別に、三角・細型・幅広・中抜き・V字・山形・ひし形・丸・四角から選択可能\n・線の矢印サイズを小／標準／大／特大から選択可能\n・PNGなどの透明余白トリミングを非破壊化し、透明判定しきい値、余白量、再トリミング、元の範囲へ戻す機能を追加\n・透明PNGやパス抜き画像の輪郭に沿った立体効果に、色・深さ・角度・滑らかさを追加\n・素材ライブラリの30MB以上を警告アイコン付き赤太字で表示し、クラウド素材はプレビュー前に取得確認\n・素材ライブラリでファイル／フォルダを複数選択、表示中全選択、選択解除、選択数表示、一括ごみ箱移動へ対応\n・素材ごみ箱から復元／完全削除でき、元フォルダと商品データは初期値で保持\n・複数の親フォルダを階層スキャンし、製品名／型番とフォルダ名の類似度で複数商品へ一括紐付け\n・素材フォルダ内の画像へメイン、色違い、パッケージ、背面、装着、機能、その他の役割を指定\n・商品配置時は紐付けたメイン画像を優先し、次に指定画像、素材フォルダ内の先頭画像を自動使用\n・商品CSVは正式19項目を既定としたまま、旧13項目／正式19項目／発売日・カラー・注意事項・訴求・素材役割・情報源を含む拡張25項目を選択出力可能\n・旧13列、正式19列、拡張25列をすべて正規化ヘッダー名で自動認識し、列順入替え・追加列・相対パスに対応\n・商品ライブラリをCtrl追加選択、Shift範囲選択、チェック選択、検索結果全選択、選択件数表示、一括ごみ箱移動に対応\n・商品ごみ箱から復元／完全削除でき、商品削除時も元画像・素材フォルダは初期値で削除しない仕様に改善\n・プロパティを「位置・サイズ」「文字・フォント」「文字の縁取り」「文字の立体」「図形・色・線」「図形の立体」「パネル・頂点」「画像」「QR」に整理\n・数値欄にスピンボタンを追加し、↑↓とShiftを使った微調整／大きな調整へ対応\n・旧製品名をホーム・ヘルプ・環境設定の表示から除去し、互換用の実行ファイル名・保存先・.rcanvasは維持\n\nMISE 1.1.5 － 2026/07/22\n・50～400%ズーム時のクリック選択とドラッグ移動を分離し、選択だけで座標が変わる不具合を修正\n・ドラッグ開始位置を基準にキャンバス座標を計算し、倍率による移動量の二重加算と開始時の飛びを修正\n・キャンバス端40px以内だけで動く速度制限付きオートスクロールへ変更し、表示範囲の暴走を抑止\n・表示グリッドと吸着計算を同じ原点・間隔へ統一し、開始10px／解除14pxのヒステリシスを追加\n・台紙中央線を専用色で常時表示し、中央・端・他要素へ吸着した位置と対象名をガイド表示\n・Escで作成中操作の取消、ドラッグ前位置への復元、選択解除を状態別に実行\n・連続クリック、右クリックの「重なりから選択」、Tab／Shift+Tabで重なった要素を選択可能に変更\n・商品CSVを正式19項目へ統一し、列番号依存を廃止して正規化ヘッダー名で割り当て\n・UTF-8 BOM／UTF-8／CP932、引用符内のカンマ・改行、旧ヘッダー別名、相対パス、列順変更へ対応\n・CSV登録前プレビュー、未知列・行別警告、重複時の更新／追加／スキップ、空欄上書き設定を追加\n・CSV出力も同じ19項目・同じ順序へ統一し、商品一覧の複数選択と一括削除を追加\n・PDFをページ選択して画像要素として配置し、移動・拡大縮小・回転・保存・書き出しに対応\n・図形を四角形／円形／線・矢印／特殊図形へ整理し、半円・ハート・吹き出し・多角形を追加\n・通常線、破線、点線、片側／両側／開き矢印、丸端を右クリックから切替可能に変更\n・パネル分割線の横線／縦線をボタンで増減し、台紙グリッド原点へ正確に吸着\n・カラーパレットへカラーサークルを追加し、既存の画面全域スポイト・透明度・最近色と統合\n・上部の「台紙」から用紙プリセット、自由サイズ、背景色、テクスチャ、台紙／要素／全体90度回転を設定可能に変更\n・PNG取込時に完全透明な外周を自動トリミングし、画像にも輪郭準拠の立体効果を追加\n・素材ライブラリで複数画像・複数親フォルダを選択可能にし、30MB以上を赤太字表示して読込前確認\n・30MB以上のTIFF等はプロジェクトへ原寸データをJSON埋め込みせず、軽量プレビューと元画像参照を併用\n・ホーム、ヘルプ、タイトルに残っていた旧製品名をMISEへ統一\n\nMISE 1.1.4 － 2026/07/21\n・完全透明なスポイト取得面がWindows環境によってクリック透過になる不具合を修正\n・仮想デスクトップ全体をほぼ不可視の取得面で覆い、MISE外や別アプリを含む画面全域から色を取得可能に変更\n・スポイト中のカラーパレットを透明化せず画面外へ一時退避し、黒い空ウィンドウが残る問題を修正\n・スポイト終了後はカラーパレットの位置とモーダル状態をそのまま復元\n・互換用BAMLから生成されるホーム画面の旧製品名・誤記を描画完了後にMISEへ確実に置換\n\nMISE 1.1.3 － 2026/07/21\n・スポイト使用時にカラーパレットをHideせず透明化し、モーダル状態を維持する方式へ変更\n・スポイト取得後に「この色を使う」または「キャンセル」を押すとDialogResult例外になる不具合を修正\n・カラーパレットの確定／取消処理をDialogResult非依存の安全な終了処理へ変更\n・スタート画面、タイトル、ヘルプメニューに残っていた旧製品名をMISEへ統一\n\nMISE 1.1.2 － 2026/07/21\n・スポイトを専用選択モードへ変更し、スポイトカーソル表示後の次の左クリックで画面色を取得\n・透明な全画面取得レイヤーで元画面への誤クリックを防止し、Esc／右クリックで中止可能に変更\n・角形状エディターへ元オブジェクト比率を維持したライブプレビューを追加\n・パネル分割線と頂点編集のグリッド表示・吸着を台紙左上の共通原点へ統一\n・頂点編集を既定で「パスを閉じて塗りつぶす」に変更\n・「四角形に戻す」を「元の形に戻す」へ変更し、三角形・星・円・既存カスタム形状を保持\n・パネル属性を外周形状から分離し、パネルを三角形・台形・多角形へ頂点変形しても分割線と区画色を維持\n・長方形以外の図形にも分割線を追加し、任意形状のパネルとして利用可能に変更\n・回転ハンドルを通常45度単位の吸着、Shiftを押しながら自由回転へ変更\n・素材ライブラリを親フォルダ単位の再帰スキャン登録へ変更し、カテゴリ／製品名／画像の階層を自動認識\n・クラウドのオンライン専用素材を一覧のまま扱い、プレビュー／配置対象だけをオンデマンド取得\n・製品名フォルダを素材セットとして登録する操作を追加\n・用途別モノクロ8プリセット、黒の濃さ、コントラスト、ガンマ、しきい値、ディザリング、写真階調設定を追加\n・対応プリンターへ黒インクのみを自動要求し、未対応時はドライバー設定を案内\n・印刷会社向けK100%モードで純黒処理とCMYK画像化を行うPDF出力を追加\n・パス抜きを非破壊化し、元画像と編集設定をプロジェクト内へ保持\n・クリック色除去、残す／消すブラシ、多角形切り抜き、頂点調整、境界拡張・縮小、ぼかし、滑らかさを追加\n・切り抜き前後比較、透明PNG保存、元画像へ戻す操作を追加\n・印刷／PDF出力時は元解像度画像と保存したパス抜き設定から再描画\n\nMISE 1.1.1 － 2026/07/21\n・起動リソースapp.xamlがパッケージに正しく収録されず、1.1.0が起動直後に終了する不具合を修正\n・WPF起動リソースを標準リソース形式で格納するよう修正\n・起動初期化前の例外も記録する緊急起動ログを追加\n・テクスチャ初期配置に失敗しても、本体は起動を継続できるよう改善\n\nMISE 1.1.0 － 2026/07/20\n・製品名と画面デザインをMISE（マイズ）へ刷新（既存.rcanvasファイルと互換用実行ファイル名は維持）\n・丸角パネルの区画色が外枠を覆う描画不具合を修正し、外枠を常に最前面で描画\n・パネル分割線の色、太さ、透明度、実線／破線／点線を調整可能に変更\n・文字の縁取りをベクター輪郭描画へ変更し、太い縁取りのぼやけ・ギザつきを改善\n・文字の立体効果を縁取りにも連続して適用し、飛び出し量・角度・色を維持\n・図形の立体効果を外枠まで含めて描画し、丸角パネルでも自然な奥行きに改善\n・カラーパレットを刷新し、透明色、最近使用した色、配色セット、画面スポイトを一つの画面へ統合\n・頂点編集で「グリッド交点のみ／グリッド線上も許可」を選択可能に変更\n・吸着をオンにした瞬間とグリッド間隔変更時に、全頂点を最寄りグリッドへ再吸着\n・頂点編集プレビューを元オブジェクトの縦横比で表示し、直接ドラッグ・追加・削除に対応\n・台紙外ネガ表示の座標系とクリップを再構成し、右側に現れる謎の囲いを抑止\n・移動／拡大縮小中だけ選択要素を軽量キャッシュし、確定後に高品質描画へ戻すことで揺れと引っ掛かりを低減\n・実寸表示にモニター物理サイズの自動推定と補正率を追加\n・設定画面に頂点吸着方式、実寸補正、動作モード、軽量ドラッグ表示を追加\n・水面、和紙、ブラッシュドメタル、ゴールド粒子のテクスチャ素材パックを同梱\n・図形／文字背景／台紙へテクスチャを埋め込み、濃さと大きさを調整可能に変更\n・角丸設定を「どこを→どのように→どれくらい」の3段階とプリセット／四隅別設定へ刷新\n・パネルの行列数・百分率入力を整理し、分割線を直接操作する画面へ一本化\n・印刷を「プリンター／PDF保管」から始める選択フローへ変更\n・普通紙、光沢紙、マットフォト、写真用紙、厚紙などの用紙種類を選択可能に変更\n・写真向けグレースケール、文字・図形向け高コントラスト、純黒変換、白黒2値化、黒インク優先、印刷会社向けK100%を採用予定\n・黒の濃さ、コントラスト、ガンマ、2値化しきい値、ディザリング、写真階調の保持を調整可能にする予定\n・文字、枠線、QR、ロゴを純黒にし、写真だけ階調を残す「販促物向けモノクロ」を標準プリセットとする予定\n・対応プリンターには黒インクのみを自動指定し、未対応時はドライバー設定を案内する予定\n・素材ライブラリを複数ルート・階層フォルダ・プレビュー・拡張子／容量／DPI確認に対応\n・大容量TIFFの警告と、製品名／型番に近い素材フォルダ候補の提案・手動選択を追加\n・JBL案件向けにTWS／ヘッドホン／スピーカー／サウンドバーのセールスポイントカードを追加\n・ライト／標準／詳しいの情報レベルで、コーデック・bit/kHz・出力・音声形式まで選択可能\n・元画像を変更しない非破壊パス抜きを採用予定\n・背景自動削除、クリック色削除、残す／消すブラシ、多角形切り抜き、頂点輪郭調整を採用予定\n・境界の拡張・縮小、ぼかし、滑らかさ、髪や細部の境界調整を採用予定\n・切り抜き前後比較、透明PNG保存、編集可能なパスのプロジェクト保持を採用予定\n・印刷／PDF時は元解像度画像と保存パスから再描画する予定\n・環境設定のリリースノートから1.0.0以降の更新履歴を確認可能\n・Ctrl+Kの操作検索を追加し、機能名・用途・ショートカットから目的の操作を即実行可能\n・複数オブジェクトを位置関係と埋め込み素材ごと保存・再挿入できる「再利用ブロック」を追加\n・PDF／PNG／JPEG／印刷の最終レンダリング画像からQR全モジュールを照合する実読取テストを追加\n・書き出しメタデータとヘルプ／バージョン表示をMISE 1.1.0へ更新\n\nMISE 1.0.4 － 2026/07/18\n・対象に応じて内容が変わる右クリックメニューを追加\n・選択／手描き／スポイトのツールバーとEscによる選択ツール復帰を追加\n・カラーパレットに最近使用した色とキャンバススポイトを追加\n・パネル区画ごとの配色と、見出し／本文／画像／価格／QRなどの役割指定を追加\n・頂点編集を直接ドラッグ、ダブルクリック追加、右クリック削除へ刷新\n・頂点編集プレビューで元図形の縦横比を維持\n・Altドラッグでも開始できる囲み選択を追加\n・グリッド、安全領域、塗り足しをオブジェクトより前面に表示\n・吸着時の水色ガイド、選択ミニツールバー、選択項目の分離表示を追加\n・右クリックからスタイルコピー、同種選択、前後関係、整列、精密編集を実行可能に変更\n\nMISE 1.0.3 － 2026/07/18\n・フリーハンド描画とグリッド吸着、Shift吸着解除を追加\n・図形のパス変換、頂点座標、精密グリッド編集を追加\n・横方向／縦方向の傾斜による自由変形を追加\n・パネルの横線／縦線位置を百分率で自由指定可能に変更\n・パネル分割線を直接ドラッグ、Shift自由移動、ダブルクリック追加、右クリック削除に対応\n・TTF／OTFフォントの追加とプロジェクト埋め込みに対応\n・数値欄を↑↓、Ctrl＋↑↓、Shift＋↑↓で増減可能に変更\n\nMISE 1.0.2 － 2026/07/18\n・台紙外ネガ表示の倍率・原点ずれと右側の謎の囲いを修正\n・上部のプロジェクト名をダブルクリックしてファイル名を変更可能に変更\n・文字背景色に透明ボタンを追加\n・最近使用したフォントとお気に入りフォントを一覧上部へ表示\n・白紙テンプレートを追加\n・2つの選択要素を基準に前面／背面を指定する機能を追加\n・左右別角丸と行列分割に対応したパネル図形を追加\n・図形の立体色、飛び出し量、角度を追加\n・PDF／PNG／JPEG書き出しの二重拡大を修正\n\nMISE 1.0.1 － 2026/07/18\n・［ファイル］メニューに環境設定を追加\n・起動時の画面サイズ、最大化、カスタム幅・高さ、前回サイズ復元に対応\n・初期ズーム、左右パネル、UI密度、パネル自動収納を設定可能に変更\n・オブジェクト／台紙／安全領域への磁石吸着、Shift自由移動、吸着距離設定を追加\n・台紙からはみ出したオブジェクト部分のネガ反転表示を追加\n・PDF／PNG／JPEG書き出しが拡大されて欠ける問題を修正\n・画面ズームや選択状態に影響されない書き出し専用描画へ変更\n・新規プロジェクトのグリッド／安全領域、書き出し後動作などを設定可能に変更\n・最大化時に右端・下端へ隙間が残る問題を修正\n\nMISE 1.0.0 修正履歴 － 2026/07/18\n・オブジェクトの移動、拡大縮小、回転時の揺れを改善\n・囲み選択、複数選択、複数オブジェクトの一括移動を追加\n・文字、図形、QR、背景色をカラーパレット対応\n・文字の縁取り色／太さ、立体色／飛び出し量／角度を追加\n・標準テンプレート10種類を実務向けデザインへ刷新\n・テンプレート装飾に対するレイアウトチェックの誤検出を修正\n\nMISE 1.0.0 － 2026/07/17\n・Windows向け初期版を公開\n・文字、画像、図形、QR、保存、PDF／PNG出力、テンプレート、商品DB、面付けに対応",
			TextWrapping = TextWrapping.Wrap,
			LineHeight = 22.0,
			FontSize = 13.0
		};
		stackPanel.Children.Add(element);
		return stackPanel;
	}

	private void UpdateCustomSizeEnabled()
	{
		bool isEnabled = _windowMode.SelectedItem?.ToString() == "カスタム";
		_windowWidth.IsEnabled = isEnabled;
		_windowHeight.IsEnabled = isEnabled;
	}

	private void Browse_Click(object sender, RoutedEventArgs e)
	{
		OpenFolderDialog openFolderDialog = new OpenFolderDialog
		{
			Title = "素材フォルダを選択",
			InitialDirectory = (Directory.Exists(_assetFolder.Text) ? _assetFolder.Text : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures))
		};
		if (openFolderDialog.ShowDialog(this) == true)
		{
			_assetFolder.Text = openFolderDialog.FolderName;
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		if (!double.TryParse(_windowWidth.Text, out var result) || !double.TryParse(_windowHeight.Text, out var result2) || !double.TryParse(_leftPanelWidth.Text, out var result3) || !double.TryParse(_rightPanelWidth.Text, out var result4) || !int.TryParse(_zoomPercent.Text, out var result5) || !double.TryParse(_safe.Text, out var result6) || !double.TryParse(_grid.Text, out var result7) || !double.TryParse(_snapDistance.Text, out var result8) || !double.TryParse(_snapStartPixels.Text, out var result9) || !double.TryParse(_snapReleasePixels.Text, out var result10) || !double.TryParse(_actualSizeCalibration.Text, out var result11) || result < 560.0 || result2 < 320.0 || result3 < 285.0 || result3 > 500.0 || result4 < 200.0 || result4 > 600.0 || result5 < 25 || result5 > 400 || result6 < 0.0 || result6 > 50.0 || result7 <= 0.0 || result7 > 100.0 || result8 < 0.2 || result8 > 10.0 || result9 < 2.0 || result9 > 40.0 || result10 <= result9 || result10 > 60.0 || result11 < 50.0 || result11 > 200.0)
		{
			MessageBox.Show("入力値を確認してください。\n\n画面: 560×320以上、パネル: 左285～500／右200～600、ズーム: 25～400%、グリッド: 0より大きい値", "入力確認");
			return;
		}
		_settings.StartupWindowMode = _windowMode.SelectedItem?.ToString() ?? "画面に合わせる";
		_settings.CustomWindowWidth = result;
		_settings.CustomWindowHeight = result2;
		_settings.RememberWindowPlacement = _rememberWindow.IsChecked == true;
		_settings.UiDensity = _density.SelectedItem?.ToString() ?? "標準";
		_settings.StartupZoomMode = _zoomMode.SelectedItem?.ToString() ?? "全体表示";
		_settings.DefaultZoomPercent = result5;
		_settings.ShowHomeOnStartup = _showHome.IsChecked == true;
		_settings.ShowLeftPanelOnStartup = _showLeftPanel.IsChecked == true;
		_settings.ShowRightPanelOnStartup = _showRightPanel.IsChecked == true;
		_settings.AutoCollapsePanels = _autoCollapse.IsChecked == true;
		_settings.LeftPanelWidth = result3;
		_settings.RightPanelWidth = result4;
		_settings.AutoSaveMinutes = ((_autosave.SelectedItem is int num) ? num : 3);
		_settings.DefaultSafeMarginMm = result6;
		_settings.GridSizeMm = result7;
		_settings.SnapToGrid = _snapGrid.IsChecked == true;
		_settings.VertexSnapMode = _vertexSnapMode.SelectedItem?.ToString() ?? "交点のみ";
		_settings.SnapToSafeArea = _snapSafe.IsChecked == true;
		_settings.SnapToObjects = _snapObjects.IsChecked == true;
		_settings.SnapToPageEdges = _snapPage.IsChecked == true;
		_settings.SnapDistanceMm = result8;
		_settings.SnapStartPixels = result9;
		_settings.SnapReleasePixels = result10;
		_settings.SnapPriorityMode = _snapPriorityMode.SelectedItem?.ToString() ?? "グリッド優先";
		_settings.ShowVerticalCenterGuide = _showVerticalCenterGuide.IsChecked == true;
		_settings.ShowHorizontalCenterGuide = _showHorizontalCenterGuide.IsChecked == true;
		_settings.ShowCenterGuides = _settings.ShowVerticalCenterGuide || _settings.ShowHorizontalCenterGuide;
		_settings.SnapToVerticalCenterGuide = _snapVerticalCenterGuide.IsChecked == true;
		_settings.SnapToHorizontalCenterGuide = _snapHorizontalCenterGuide.IsChecked == true;
		_settings.InvertOutOfBoundsObjects = _invertOutOfBounds.IsChecked == true;
		_settings.ShowGridOnNewProjects = _showGrid.IsChecked == true;
		_settings.ShowSafeAreaOnNewProjects = _showSafe.IsChecked == true;
		_settings.DefaultExportDpi = ((_dpi.SelectedItem is int num2) ? num2 : 300);
		_settings.DefaultPrintMode = _printMode.SelectedItem?.ToString() ?? "家庭用プリンタ";
		_settings.WarnBeforeExportOnErrors = _warnExport.IsChecked == true;
		_settings.ExportCompletionAction = _exportAction.SelectedItem?.ToString() ?? "確認する";
		_settings.AssetFolder = _assetFolder.Text.Trim();
		_settings.ActualSizeCalibrationPercent = result11;
		_settings.PerformanceMode = _performanceMode.SelectedItem?.ToString() ?? "自動";
		_settings.UseLightweightDragPreview = _lightweightDrag.IsChecked == true;
		base.DialogResult = true;
	}

	private static StackPanel TabPanel()
	{
		return new StackPanel
		{
			Margin = new Thickness(18.0)
		};
	}

	private static ScrollViewer Scroll(object content)
	{
		return new ScrollViewer
		{
			Content = content,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
	}

	private static void Section(Panel panel, string title)
	{
		panel.Children.Add(new TextBlock
		{
			Text = title,
			FontSize = 16.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 51)),
			Margin = new Thickness(0.0, 2.0, 0.0, 12.0)
		});
	}

	private static void Add(Panel panel, string label, UIElement control)
	{
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = label,
			Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
		});
		stackPanel.Children.Add(control);
		panel.Children.Add(stackPanel);
	}

	private static Grid TwoColumns(string leftLabel, Control left, string rightLabel, Control right)
	{
		Grid obj = new Grid
		{
			ColumnDefinitions = 
			{
				new ColumnDefinition(),
				new ColumnDefinition
				{
					Width = new GridLength(12.0)
				},
				new ColumnDefinition()
			}
		};
		StackPanel element = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = leftLabel,
					Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
				},
				(UIElement)left
			}
		};
		obj.Children.Add(element);
		StackPanel element2 = new StackPanel
		{
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = rightLabel,
					Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
				},
				(UIElement)right
			}
		};
		Grid.SetColumn(element2, 2);
		obj.Children.Add(element2);
		return obj;
	}
}
