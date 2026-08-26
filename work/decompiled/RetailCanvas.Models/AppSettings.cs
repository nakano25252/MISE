using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class AppSettings
{
	public int AutoSaveMinutes { get; set; } = 3;

	public int DefaultExportDpi { get; set; } = 300;

	public double DefaultSafeMarginMm { get; set; } = 5.0;

	public double GridSizeMm { get; set; } = 5.0;

	public bool SnapToGrid { get; set; } = true;

	public string VertexSnapMode { get; set; } = "交点のみ";

	public bool SnapToSafeArea { get; set; } = true;

	public bool SnapToObjects { get; set; } = true;

	public bool SnapToPageEdges { get; set; } = true;

	public double SnapDistanceMm { get; set; } = 2.0;

	public double SnapStartPixels { get; set; } = 10.0;

	public double SnapReleasePixels { get; set; } = 14.0;

	public string SnapPriorityMode { get; set; } = "グリッド優先";

	public bool ShowCenterGuides { get; set; } = true;

	public bool ShowVerticalCenterGuide { get; set; } = true;

	public bool ShowHorizontalCenterGuide { get; set; } = true;

	public bool SnapToVerticalCenterGuide { get; set; } = true;

	public bool SnapToHorizontalCenterGuide { get; set; } = true;

	public string LastShapeType { get; set; } = "RoundedRectangle";

	public List<string> RecentShapeTypes { get; set; } = new List<string>();

	public List<string> FavoriteShapeTypes { get; set; } = new List<string>();

	public bool InvertOutOfBoundsObjects { get; set; } = true;

	public bool DarkMode { get; set; }

	public string DefaultPrintMode { get; set; } = "家庭用プリンタ";

	public string AssetFolder { get; set; } = string.Empty;

	public List<string> AssetFolders { get; set; } = new List<string>();

	public double ActualSizeCalibrationPercent { get; set; } = 100.0;

	public string PerformanceMode { get; set; } = "自動";

	public bool UseLightweightDragPreview { get; set; } = true;

	public string StartupWindowMode { get; set; } = "画面に合わせる";

	public double CustomWindowWidth { get; set; } = 1280.0;

	public double CustomWindowHeight { get; set; } = 800.0;

	public bool RememberWindowPlacement { get; set; } = true;

	public double? LastWindowLeft { get; set; }

	public double? LastWindowTop { get; set; }

	public double LastWindowWidth { get; set; } = 1280.0;

	public double LastWindowHeight { get; set; } = 800.0;

	public bool LastWindowMaximized { get; set; }

	public string UiDensity { get; set; } = "標準";

	public string StartupZoomMode { get; set; } = "全体表示";

	public int DefaultZoomPercent { get; set; } = 75;

	public bool ShowHomeOnStartup { get; set; } = true;

	public bool ShowLeftPanelOnStartup { get; set; } = true;

	public bool ShowRightPanelOnStartup { get; set; } = true;

	public bool AutoCollapsePanels { get; set; } = true;

	public double LeftPanelWidth { get; set; } = 310.0;

	public double RightPanelWidth { get; set; } = 300.0;

	public bool ShowGridOnNewProjects { get; set; } = true;

	public bool ShowSafeAreaOnNewProjects { get; set; } = true;

	public bool WarnBeforeExportOnErrors { get; set; } = true;

	public string ExportCompletionAction { get; set; } = "確認する";

	public List<string> RecentFonts { get; set; } = new List<string>();

	public List<string> FavoriteFonts { get; set; } = new List<string>();

	public List<RecentProjectInfo> RecentProjects { get; set; } = new List<RecentProjectInfo>();

	public Dictionary<string, double> MinimumFontSizes { get; set; } = new Dictionary<string, double>
	{
		["A4_Heading"] = 24.0,
		["A4_Product"] = 18.0,
		["A4_Body"] = 10.0,
		["A4_Note"] = 7.0,
		["Card_Heading"] = 10.0,
		["Card_Body"] = 7.0,
		["Card_Note"] = 5.5
	};
}
