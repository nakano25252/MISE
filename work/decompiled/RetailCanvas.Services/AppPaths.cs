using System;
using System.IO;

namespace RetailCanvas.Services;

public static class AppPaths
{
	public static string Root
	{
		get
		{
			string environmentVariable = Environment.GetEnvironmentVariable("RETAILCANVAS_DATA_ROOT");
			if (environmentVariable == null || environmentVariable.Length <= 0)
			{
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetailCanvas");
			}
			return environmentVariable;
		}
	}

	public static string Projects => Path.Combine(Root, "Projects");

	public static string Templates => Path.Combine(Root, "Templates");

	public static string Blocks => Path.Combine(Root, "ReusableBlocks");

	public static string Assets => Path.Combine(Root, "Assets");

	public static string AssetTrash => Path.Combine(Root, "AssetTrash");

	public static string Textures => Path.Combine(Root, "Textures");

	public static string Exports => Path.Combine(Root, "Exports");

	public static string Backups => Path.Combine(Root, "Backups");

	public static string AutoSave => Path.Combine(Root, "AutoSave");

	public static string History => Path.Combine(Root, "History");

	public static string Logs => Path.Combine(Root, "Logs");

	public static string SettingsFile => Path.Combine(Root, "settings.json");

	public static string DatabaseFile => Path.Combine(Root, "retailcanvas.db");

	public static void EnsureCreated()
	{
		string[] array = new string[12]
		{
			Root, Projects, Templates, Blocks, Assets, AssetTrash, Textures, Exports, Backups, AutoSave,
			History, Logs
		};
		for (int i = 0; i < array.Length; i++)
		{
			Directory.CreateDirectory(array[i]);
		}
	}
}
