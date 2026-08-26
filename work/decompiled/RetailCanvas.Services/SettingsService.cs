using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class SettingsService
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public AppSettings Current { get; private set; }

	public SettingsService()
	{
		Current = Load();
	}

	public AppSettings Load()
	{
		try
		{
			if (File.Exists(AppPaths.SettingsFile))
			{
				return Normalize(JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options) ?? new AppSettings());
			}
		}
		catch (Exception ex)
		{
			LogService.Error("Settings load failed", ex);
		}
		return Normalize(new AppSettings());
	}

	private static AppSettings Normalize(AppSettings settings)
	{
		settings.CustomWindowWidth = Math.Clamp(double.IsFinite(settings.CustomWindowWidth) ? settings.CustomWindowWidth : 1280.0, 560.0, 7680.0);
		settings.CustomWindowHeight = Math.Clamp(double.IsFinite(settings.CustomWindowHeight) ? settings.CustomWindowHeight : 800.0, 320.0, 4320.0);
		settings.LeftPanelWidth = Math.Clamp(double.IsFinite(settings.LeftPanelWidth) ? settings.LeftPanelWidth : 310.0, 285.0, 500.0);
		settings.RightPanelWidth = Math.Clamp(double.IsFinite(settings.RightPanelWidth) ? settings.RightPanelWidth : 300.0, 200.0, 600.0);
		settings.DefaultZoomPercent = Math.Clamp(settings.DefaultZoomPercent, 25, 400);
		settings.DefaultSafeMarginMm = Math.Clamp(double.IsFinite(settings.DefaultSafeMarginMm) ? settings.DefaultSafeMarginMm : 5.0, 0.0, 50.0);
		settings.GridSizeMm = Math.Clamp(double.IsFinite(settings.GridSizeMm) ? settings.GridSizeMm : 5.0, 0.1, 100.0);
		settings.SnapDistanceMm = Math.Clamp(double.IsFinite(settings.SnapDistanceMm) ? settings.SnapDistanceMm : 2.0, 0.2, 10.0);
		settings.SnapStartPixels = Math.Clamp(double.IsFinite(settings.SnapStartPixels) ? settings.SnapStartPixels : 10.0, 2.0, 40.0);
		settings.SnapReleasePixels = Math.Clamp(double.IsFinite(settings.SnapReleasePixels) ? settings.SnapReleasePixels : 14.0, settings.SnapStartPixels + 1.0, 60.0);
		settings.ActualSizeCalibrationPercent = Math.Clamp(double.IsFinite(settings.ActualSizeCalibrationPercent) ? settings.ActualSizeCalibrationPercent : 100.0, 50.0, 200.0);
		settings.DefaultExportDpi = new int[4] { 150, 200, 300, 600 }.OrderBy((int value) => Math.Abs(value - settings.DefaultExportDpi)).First();
		AppSettings appSettings = settings;
		if (appSettings.AssetFolders == null)
		{
			List<string> list = (appSettings.AssetFolders = new List<string>());
		}
		appSettings = settings;
		if (appSettings.RecentFonts == null)
		{
			List<string> list = (appSettings.RecentFonts = new List<string>());
		}
		appSettings = settings;
		if (appSettings.FavoriteFonts == null)
		{
			List<string> list = (appSettings.FavoriteFonts = new List<string>());
		}
		appSettings = settings;
		if (appSettings.RecentProjects == null)
		{
			List<RecentProjectInfo> list5 = (appSettings.RecentProjects = new List<RecentProjectInfo>());
		}
		appSettings = settings;
		if (appSettings.RecentShapeTypes == null)
		{
			List<string> list = (appSettings.RecentShapeTypes = new List<string>());
		}
		appSettings = settings;
		if (appSettings.FavoriteShapeTypes == null)
		{
			List<string> list = (appSettings.FavoriteShapeTypes = new List<string>());
		}
		appSettings = settings;
		if (appSettings.MinimumFontSizes == null)
		{
			Dictionary<string, double> dictionary = (appSettings.MinimumFontSizes = new Dictionary<string, double>());
		}
		return settings;
	}

	public void Save()
	{
		try
		{
			AppPaths.EnsureCreated();
			File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Options));
		}
		catch (Exception ex)
		{
			LogService.Error("Settings save failed", ex);
		}
	}

	public void AddRecent(ProjectModel project, string filePath)
	{
		Current.RecentProjects.RemoveAll((RecentProjectInfo x) => string.Equals(x.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
		Current.RecentProjects.Insert(0, new RecentProjectInfo
		{
			FilePath = filePath,
			ProjectName = project.ProjectName,
			PaperName = project.PaperName,
			BrandName = project.BrandName,
			StoreName = project.StoreName,
			LastOpenedAt = DateTime.Now
		});
		if (Current.RecentProjects.Count > 12)
		{
			Current.RecentProjects.RemoveRange(12, Current.RecentProjects.Count - 12);
		}
		Save();
	}
}
