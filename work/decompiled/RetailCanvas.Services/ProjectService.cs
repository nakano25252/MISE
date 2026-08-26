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
		return JsonSerializer.Deserialize<ProjectModel>(json, JsonOptions) ?? throw new InvalidDataException("プロジェクトデータを読み取れませんでした。");
	}

	public void Save(ProjectModel project, string path, bool createHistory = true)
	{
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
