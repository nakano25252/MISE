using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class ProjectService
{
	public const int CurrentFileFormatVersion = 2;

	public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public string Serialize(ProjectModel project)
	{
		return JsonSerializer.Serialize(project, JsonOptions);
	}

	public ProjectModel Deserialize(string json)
	{
		ProjectModel project = JsonSerializer.Deserialize<ProjectModel>(json, JsonOptions) ?? throw new InvalidDataException("プロジェクトデータを読み取れませんでした。");
		return Normalize(project);
	}

	public void Save(ProjectModel project, string path, bool createHistory = true)
	{
		project.FileFormatVersion = CurrentFileFormatVersion;
		project.UpdatedAt = DateTime.Now;
		Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppPaths.Projects);
		string text = path + ".tmp";
		File.WriteAllText(text, Serialize(project));
		using (FileStream fileStream = new FileStream(text, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
		{
			fileStream.Flush(flushToDisk: true);
		}
		File.Move(text, path, overwrite: true);
		if (createHistory)
		{
			CreateHistorySnapshot(project, path);
		}
		LogService.Info("Project saved: " + path);
	}

	public ProjectModel Load(string path)
	{
		ProjectModel projectModel = Deserialize(File.ReadAllText(path));
		if (projectModel.Pages.Count == 0)
		{
			projectModel.Pages.Add(PageModel.Create("A4", landscape: false));
		}
		LogService.Info("Project opened: " + path);
		return projectModel;
	}

	private static ProjectModel Normalize(ProjectModel project)
	{
		if (project.FileFormatVersion > CurrentFileFormatVersion)
		{
			throw new InvalidDataException($"このプロジェクトは新しいMISE用です（形式 {project.FileFormatVersion}）。アプリを更新してください。");
		}
		project.Pages ??= new List<PageModel>();
		project.EmbeddedFonts ??= new List<EmbeddedFontModel>();
		project.ExportSettings ??= new ExportSettings();
		foreach (PageModel page in project.Pages)
		{
			page.WidthMm = PositiveFinite(page.WidthMm, 210.0, 1.0);
			page.HeightMm = PositiveFinite(page.HeightMm, 297.0, 1.0);
			page.SafeMarginMm = ClampFinite(page.SafeMarginMm, 5.0, 0.0, Math.Min(page.WidthMm, page.HeightMm) / 2.0);
			page.BleedMm = ClampFinite(page.BleedMm, 3.0, 0.0, 100.0);
			page.PrintMarginMm = ClampFinite(page.PrintMarginMm, 5.0, 0.0, 100.0);
			page.Elements ??= new List<CanvasElementModel>();
			foreach (CanvasElementModel element in page.Elements)
			{
				element.Xmm = FiniteOr(element.Xmm, 0.0);
				element.Ymm = FiniteOr(element.Ymm, 0.0);
				element.WidthMm = PositiveFinite(element.WidthMm, 10.0, 0.1);
				element.HeightMm = PositiveFinite(element.HeightMm, 10.0, 0.1);
				element.Rotation = ((FiniteOr(element.Rotation, 0.0) % 360.0) + 360.0) % 360.0;
				element.SkewX = ClampFinite(element.SkewX, 0.0, -80.0, 80.0);
				element.SkewY = ClampFinite(element.SkewY, 0.0, -80.0, 80.0);
				element.Opacity = ClampFinite(element.Opacity, 1.0, 0.0, 1.0);
				element.FontSizePt = ClampFinite(element.FontSizePt, 18.0, 3.0, 300.0);
				element.FontWeightValue = Math.Clamp(element.FontWeightValue, 100, 900);
				element.ImagePixelWidth = Math.Max(0, element.ImagePixelWidth);
				element.ImagePixelHeight = Math.Max(0, element.ImagePixelHeight);
				if (element.Kind == ElementKind.QrCode)
				{
					double side = Math.Max(5.0, Math.Max(element.WidthMm, element.HeightMm));
					element.WidthMm = side;
					element.HeightMm = side;
					element.PreserveAspectRatio = true;
				}
				else if (element.Kind == ElementKind.Text && element.TextFrameTight)
				{
					element.PreserveAspectRatio = true;
				}
			}
		}
		project.FileFormatVersion = CurrentFileFormatVersion;
		return project;
	}

	private static double FiniteOr(double value, double fallback) => double.IsFinite(value) ? value : fallback;

	private static double PositiveFinite(double value, double fallback, double minimum) => Math.Max(minimum, FiniteOr(value, fallback));

	private static double ClampFinite(double value, double fallback, double minimum, double maximum) => Math.Clamp(FiniteOr(value, fallback), minimum, maximum);

	public string AutoSavePath(ProjectModel project)
	{
		return Path.Combine(AppPaths.AutoSave, project.ProjectId.ToString("N") + ".rcanvas");
	}

	public void AutoSave(ProjectModel project)
	{
		Save(project, AutoSavePath(project), createHistory: false);
	}

	public IEnumerable<string> FindRecoveryFiles()
	{
		if (!Directory.Exists(AppPaths.AutoSave))
		{
			return Enumerable.Empty<string>();
		}
		return Directory.EnumerateFiles(AppPaths.AutoSave, "*.rcanvas").OrderByDescending(File.GetLastWriteTime);
	}

	public void ClearAutoSave(ProjectModel project)
	{
		string path = AutoSavePath(project);
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private void CreateHistorySnapshot(ProjectModel project, string savedPath)
	{
		try
		{
			string text = Path.Combine(AppPaths.History, project.ProjectId.ToString("N"));
			Directory.CreateDirectory(text);
			string path = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".rcanvas";
			File.Copy(savedPath, Path.Combine(text, path), overwrite: true);
			foreach (string item in Directory.EnumerateFiles(text, "*.rcanvas").OrderByDescending(File.GetCreationTimeUtc).ToList()
				.Skip(50))
			{
				File.Delete(item);
			}
		}
		catch (Exception ex)
		{
			LogService.Error("History snapshot failed", ex);
		}
	}
}
