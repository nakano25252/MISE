using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class ReusableBlockService
{
	public IReadOnlyList<ReusableBlockInfo> List()
	{
		Directory.CreateDirectory(AppPaths.Blocks);
		List<ReusableBlockInfo> list = new List<ReusableBlockInfo>();
		foreach (string item in Directory.EnumerateFiles(AppPaths.Blocks, "*.rblock").OrderByDescending(File.GetLastWriteTime))
		{
			try
			{
				ReusableBlockModel reusableBlockModel = Load(item);
				list.Add(new ReusableBlockInfo
				{
					Name = reusableBlockModel.Name,
					FilePath = item,
					ElementCount = reusableBlockModel.Elements.Count,
					UpdatedAt = reusableBlockModel.UpdatedAt
				});
			}
			catch (Exception ex)
			{
				LogService.Error("Reusable block list failed: " + item, ex);
			}
		}
		return list;
	}

	public string Save(string name, IReadOnlyList<CanvasElementModel> source)
	{
		if (source.Count == 0)
		{
			throw new InvalidOperationException("保存する要素が選択されていません。");
		}
		Directory.CreateDirectory(AppPaths.Blocks);
		List<CanvasElementModel> list = JsonSerializer.Deserialize<List<CanvasElementModel>>(JsonSerializer.Serialize(source, ProjectService.JsonOptions), ProjectService.JsonOptions) ?? new List<CanvasElementModel>();
		double num = list.Min((CanvasElementModel x) => x.Xmm);
		double num2 = list.Min((CanvasElementModel x) => x.Ymm);
		double num3 = list.Max((CanvasElementModel x) => x.Xmm + x.WidthMm);
		double num4 = list.Max((CanvasElementModel x) => x.Ymm + x.HeightMm);
		foreach (CanvasElementModel item in list)
		{
			item.Xmm -= num;
			item.Ymm -= num2;
		}
		ReusableBlockModel reusableBlockModel = new ReusableBlockModel
		{
			Name = (string.IsNullOrWhiteSpace(name) ? "再利用ブロック" : name.Trim()),
			WidthMm = Math.Max(0.1, num3 - num),
			HeightMm = Math.Max(0.1, num4 - num2),
			Elements = list,
			UpdatedAt = DateTime.Now
		};
		string text = Path.Combine(AppPaths.Blocks, SafeFileName(reusableBlockModel.Name) + ".rblock");
		File.WriteAllText(text, JsonSerializer.Serialize(reusableBlockModel, ProjectService.JsonOptions));
		LogService.Info("Reusable block saved: " + text);
		return text;
	}

	public ReusableBlockModel Load(string path)
	{
		return JsonSerializer.Deserialize<ReusableBlockModel>(File.ReadAllText(path), ProjectService.JsonOptions) ?? throw new InvalidDataException("再利用ブロックを読み込めませんでした。");
	}

	public void Delete(string path)
	{
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	private static string SafeFileName(string value)
	{
		string text = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		return "再利用ブロック";
	}
}
