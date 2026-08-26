using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RetailCanvas.Services;

public sealed class AssetTrashService
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private static string Manifest => Path.Combine(AppPaths.AssetTrash, "manifest.json");

	public List<AssetTrashEntry> Load()
	{
		try
		{
			return File.Exists(Manifest) ? (JsonSerializer.Deserialize<List<AssetTrashEntry>>(File.ReadAllText(Manifest), Options) ?? new List<AssetTrashEntry>()) : new List<AssetTrashEntry>();
		}
		catch
		{
			return new List<AssetTrashEntry>();
		}
	}

	public IReadOnlyList<AssetTrashEntry> MoveToTrash(IEnumerable<string> paths)
	{
		AppPaths.EnsureCreated();
		List<AssetTrashEntry> list = Load();
		List<AssetTrashEntry> list2 = new List<AssetTrashEntry>();
		foreach (string item in paths.Distinct<string>(StringComparer.OrdinalIgnoreCase))
		{
			if (File.Exists(item))
			{
				AssetTrashEntry assetTrashEntry = new AssetTrashEntry
				{
					OriginalPath = Path.GetFullPath(item)
				};
				assetTrashEntry.TrashPath = Path.Combine(AppPaths.AssetTrash, assetTrashEntry.Id.ToString("N") + Path.GetExtension(item));
				File.Move(item, assetTrashEntry.TrashPath);
				list.Add(assetTrashEntry);
				list2.Add(assetTrashEntry);
			}
		}
		Save(list);
		return list2;
	}

	public void Restore(IEnumerable<Guid> ids)
	{
		HashSet<Guid> selected = ids.ToHashSet();
		List<AssetTrashEntry> list = Load();
		foreach (AssetTrashEntry item in list.Where((AssetTrashEntry x) => selected.Contains(x.Id)).ToList())
		{
			if (!File.Exists(item.TrashPath))
			{
				list.Remove(item);
				continue;
			}
			Directory.CreateDirectory(Path.GetDirectoryName(item.OriginalPath) ?? AppPaths.Assets);
			string text = item.OriginalPath;
			if (File.Exists(text))
			{
				text = Path.Combine(Path.GetDirectoryName(text), Path.GetFileNameWithoutExtension(text) + "_復元" + Path.GetExtension(text));
			}
			File.Move(item.TrashPath, text);
			list.Remove(item);
		}
		Save(list);
	}

	public void Purge(IEnumerable<Guid> ids)
	{
		HashSet<Guid> selected = ids.ToHashSet();
		List<AssetTrashEntry> list = Load();
		foreach (AssetTrashEntry item in list.Where((AssetTrashEntry x) => selected.Contains(x.Id)).ToList())
		{
			try
			{
				if (File.Exists(item.TrashPath))
				{
					File.Delete(item.TrashPath);
				}
			}
			catch
			{
				continue;
			}
			list.Remove(item);
		}
		Save(list);
	}

	private static void Save(List<AssetTrashEntry> entries)
	{
		AppPaths.EnsureCreated();
		File.WriteAllText(Manifest, JsonSerializer.Serialize(entries, Options));
	}
}
