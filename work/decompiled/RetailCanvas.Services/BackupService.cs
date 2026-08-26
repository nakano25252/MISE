using System;
using System.IO;
using System.IO.Compression;

namespace RetailCanvas.Services;

public static class BackupService
{
	public static string CreateBackup(string? destination = null)
	{
		AppPaths.EnsureCreated();
		if (destination == null)
		{
			destination = Path.Combine(AppPaths.Backups, $"MISE_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
		}
		string text = destination + ".tmp";
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		using (ZipArchive archive = ZipFile.Open(text, ZipArchiveMode.Create))
		{
			AddFile(archive, AppPaths.SettingsFile, "Settings/settings.json");
			AddFile(archive, AppPaths.DatabaseFile, "Database/retailcanvas.db");
			AddDirectory(archive, AppPaths.Projects, "Projects");
			AddDirectory(archive, AppPaths.Templates, "Templates");
			AddDirectory(archive, AppPaths.Blocks, "ReusableBlocks");
			AddDirectory(archive, AppPaths.Assets, "Assets");
		}
		File.Move(text, destination, overwrite: true);
		LogService.Info("Backup created: " + destination);
		return destination;
	}

	public static void Restore(string backupFile)
	{
		string text = Path.Combine(Path.GetTempPath(), "RetailCanvasRestore_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(text);
		try
		{
			ZipFile.ExtractToDirectory(backupFile, text, overwriteFiles: true);
			CopyIfExists(Path.Combine(text, "Settings", "settings.json"), AppPaths.SettingsFile);
			CopyIfExists(Path.Combine(text, "Database", "retailcanvas.db"), AppPaths.DatabaseFile);
			CopyDirectory(Path.Combine(text, "Projects"), AppPaths.Projects);
			CopyDirectory(Path.Combine(text, "Templates"), AppPaths.Templates);
			CopyDirectory(Path.Combine(text, "ReusableBlocks"), AppPaths.Blocks);
			CopyDirectory(Path.Combine(text, "Assets"), AppPaths.Assets);
			LogService.Info("Backup restored: " + backupFile);
		}
		finally
		{
			try
			{
				Directory.Delete(text, recursive: true);
			}
			catch
			{
			}
		}
	}

	private static void AddFile(ZipArchive archive, string source, string entry)
	{
		if (File.Exists(source))
		{
			archive.CreateEntryFromFile(source, entry, CompressionLevel.Optimal);
		}
	}

	private static void AddDirectory(ZipArchive archive, string source, string prefix)
	{
		if (!Directory.Exists(source))
		{
			return;
		}
		foreach (string item in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			string text = Path.GetRelativePath(source, item).Replace('\\', '/');
			archive.CreateEntryFromFile(item, prefix + "/" + text, CompressionLevel.Optimal);
		}
	}

	private static void CopyIfExists(string source, string destination)
	{
		if (File.Exists(source))
		{
			Directory.CreateDirectory(Path.GetDirectoryName(destination));
			File.Copy(source, destination, overwrite: true);
		}
	}

	private static void CopyDirectory(string source, string destination)
	{
		if (!Directory.Exists(source))
		{
			return;
		}
		foreach (string item in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
		{
			string text = Path.Combine(destination, Path.GetRelativePath(source, item));
			Directory.CreateDirectory(Path.GetDirectoryName(text));
			File.Copy(item, text, overwrite: true);
		}
	}
}
