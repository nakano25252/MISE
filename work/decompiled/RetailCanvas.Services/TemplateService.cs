using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public sealed class TemplateService
{
	private readonly ProjectService _projects = new ProjectService();

	public IReadOnlyList<string> BuiltInNames { get; } = new string[11]
	{
		"白紙", "製品単品訴求", "ブランド訴求", "新製品発売", "比較表", "防水訴求", "ノイズキャンセリング訴求", "バッテリー訴求", "セール", "QRコード誘導",
		"店員向け製品ガイド"
	};

	public List<string> UserTemplates()
	{
		if (!Directory.Exists(AppPaths.Templates))
		{
			return new List<string>();
		}
		return (from x in Directory.EnumerateFiles(AppPaths.Templates, "*.rtemplate")
			select Path.GetFileNameWithoutExtension(x) ?? "ユーザーテンプレート").Order().ToList();
	}

	public void SaveTemplate(ProjectModel project, string name)
	{
		string text = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "ユーザーテンプレート";
		}
		_projects.Save(project, Path.Combine(AppPaths.Templates, text + ".rtemplate"), createHistory: false);
	}

	public ProjectModel LoadUserTemplate(string name)
	{
		ProjectModel projectModel = _projects.Load(Path.Combine(AppPaths.Templates, name + ".rtemplate"));
		projectModel.ProjectId = Guid.NewGuid();
		projectModel.ProjectName = name + "から作成";
		DateTime dateTime = (projectModel.UpdatedAt = DateTime.Now);
		DateTime createdAt = dateTime;
		projectModel.CreatedAt = createdAt;
		foreach (PageModel page in projectModel.Pages)
		{
			page.PageId = Guid.NewGuid();
			foreach (CanvasElementModel element in page.Elements)
			{
				element.Id = Guid.NewGuid();
			}
		}
		return projectModel;
	}

	public void ApplyBuiltIn(PageModel page, string templateName)
	{
		page.Elements.Clear();
		if (templateName == "白紙")
		{
			page.Background = "#FFFFFFFF";
			return;
		}
		string text = "#FFF26A21";
		string text2 = "#FF172033";
		string text3 = "#FF2BB6C8";
		string text4 = "#FFFFFFFF";
		string text5 = "#FFF3F5F8";
		string color = "#FF687386";
		double widthMm = page.WidthMm;
		double heightMm = page.HeightMm;
		double num = Math.Clamp(Math.Min(widthMm, heightMm) * 0.07, 5.0, 14.0);
		double num2 = widthMm - num * 2.0;
		AddShape(page, "背景", "Rectangle", 0.0, 0.0, widthMm, heightMm, text5, text5, 0.0);
		switch (templateName)
		{
		case "ブランド訴求":
			AddShape(page, "濃紺背景", "Rectangle", 0.0, 0.0, widthMm, heightMm, text2, text2, 0.0);
			AddShape(page, "アクセント円1", "Ellipse", widthMm * 0.66, (0.0 - heightMm) * 0.08, widthMm * 0.48, widthMm * 0.48, "#262BB6C8", "#002BB6C8", 0.0);
			AddShape(page, "アクセント円2", "Ellipse", (0.0 - widthMm) * 0.18, heightMm * 0.7, widthMm * 0.52, widthMm * 0.52, "#22F26A21", "#00F26A21", 0.0);
			AddText(page, "ブランド名", "BRAND / LOGO", num, num, num2, heightMm * 0.08, 15.0, text3, bold: true, "ブランド名");
			AddText(page, "ブランドコピー", "選ばれる理由を、ひと目で。", num, heightMm * 0.18, num2, heightMm * 0.17, Math.Clamp(widthMm * 0.15, 22.0, 38.0), text4, bold: true, "キャッチコピー", 1.2, text2, 3.0, "#FF0B101C");
			AddText(page, "ブランド説明", "ブランドが大切にしている価値と、売場で伝えたい魅力を2〜3行で入力します。", num, heightMm * 0.38, num2 * 0.78, heightMm * 0.13, 12.0, "#FFD8DEEA", bold: false, "ブランド説明");
			AddShape(page, "価値カード", "RoundedRectangle", num, heightMm * 0.57, num2, heightMm * 0.25, "#F0FFFFFF", "#33FFFFFF", 1.0);
			AddText(page, "価値1", "01  QUALITY\n確かな品質", num * 1.5, heightMm * 0.61, num2 * 0.27, heightMm * 0.14, 12.0, text2, bold: true, "特徴1");
			AddText(page, "価値2", "02  DESIGN\n使うほど好きになる", num + num2 * 0.34, heightMm * 0.61, num2 * 0.29, heightMm * 0.14, 12.0, text2, bold: true, "特徴2");
			AddText(page, "価値3", "03  SUPPORT\n購入後も安心", num + num2 * 0.69, heightMm * 0.61, num2 * 0.27, heightMm * 0.14, 12.0, text2, bold: true, "特徴3");
			break;
		case "セール":
			AddShape(page, "セール背景", "Rectangle", 0.0, 0.0, widthMm, heightMm, "#FFFFD836", "#FFFFD836", 0.0);
			AddShape(page, "上帯", "Rectangle", 0.0, 0.0, widthMm, heightMm * 0.12, text2, text2, 0.0);
			AddText(page, "期間", "期間限定  /  LIMITED OFFER", num, heightMm * 0.025, num2, heightMm * 0.06, 12.0, text4, bold: true, "開催期間");
			AddText(page, "セール見出し", "SPECIAL\nSALE", num, heightMm * 0.14, num2, heightMm * 0.2, Math.Clamp(widthMm * 0.19, 28.0, 48.0), text, bold: true, "セール見出し", 1.6, text4, 4.0, text2, 48.0);
			AddShape(page, "商品カード", "RoundedRectangle", num, heightMm * 0.39, num2, heightMm * 0.48, text4, "#22000000", 1.0);
			AddText(page, "製品画像枠", "PRODUCT IMAGE", num * 1.5, heightMm * 0.44, num2 * 0.44, heightMm * 0.22, 12.0, "#FF9AA4B4", bold: true, "製品画像");
			AddText(page, "製品名", "製品名を入力", num + num2 * 0.48, heightMm * 0.45, num2 * 0.45, heightMm * 0.1, 18.0, text2, bold: true, "製品名");
			AddText(page, "特典", "今だけの購入特典\nポイント還元・プレゼント", num + num2 * 0.48, heightMm * 0.56, num2 * 0.44, heightMm * 0.12, 11.0, color, bold: false, "キャンペーン内容");
			AddText(page, "価格", "￥00,000", num * 1.5, heightMm * 0.73, num2 - num, heightMm * 0.1, 32.0, text, bold: true, "価格", 1.0, text4);
			break;
		case "比較表":
		{
			AddText(page, "見出し", "あなたに合うのは、どっち？", num, num, num2, heightMm * 0.1, 24.0, text2, bold: true, "見出し");
			AddText(page, "サブ見出し", "使い方で選べる2つのモデル", num, heightMm * 0.11, num2, heightMm * 0.05, 11.0, color, bold: false);
			double num3 = num * 0.65;
			double num4 = (num2 - num3) / 2.0;
			AddShape(page, "製品Aカード", "RoundedRectangle", num, heightMm * 0.2, num4, heightMm * 0.68, text4, "#33F26A21", 1.2);
			AddShape(page, "製品Bカード", "RoundedRectangle", num + num4 + num3, heightMm * 0.2, num4, heightMm * 0.68, text4, "#332BB6C8", 1.2);
			AddShape(page, "Aラベル", "RoundedRectangle", num + num * 0.5, heightMm * 0.18, num4 - num, heightMm * 0.07, text, text, 0.0);
			AddShape(page, "Bラベル", "RoundedRectangle", num + num4 + num3 + num * 0.5, heightMm * 0.18, num4 - num, heightMm * 0.07, text3, text3, 0.0);
			AddText(page, "製品A", "MODEL A", num + num * 0.7, heightMm * 0.19, num4 - num * 1.4, heightMm * 0.04, 13.0, text4, bold: true, "製品A");
			AddText(page, "製品B", "MODEL B", num + num4 + num3 + num * 0.7, heightMm * 0.19, num4 - num * 1.4, heightMm * 0.04, 13.0, text4, bold: true, "製品B");
			AddText(page, "画像A", "PRODUCT IMAGE", num + num * 0.8, heightMm * 0.28, num4 - num * 1.6, heightMm * 0.16, 10.0, "#FF9AA4B4", bold: true, "製品画像A");
			AddText(page, "画像B", "PRODUCT IMAGE", num + num4 + num3 + num * 0.8, heightMm * 0.28, num4 - num * 1.6, heightMm * 0.16, 10.0, "#FF9AA4B4", bold: true, "製品画像B");
			AddText(page, "比較項目A", "✓ 強み・特徴\n✓ 利用シーン\n✓ おすすめユーザー\n\n価格  ￥00,000", num + num * 0.8, heightMm * 0.49, num4 - num * 1.6, heightMm * 0.28, 11.0, text2, bold: false, "比較情報A");
			AddText(page, "比較項目B", "✓ 強み・特徴\n✓ 利用シーン\n✓ おすすめユーザー\n\n価格  ￥00,000", num + num4 + num3 + num * 0.8, heightMm * 0.49, num4 - num * 1.6, heightMm * 0.28, 11.0, text2, bold: false, "比較情報B");
			break;
		}
		case "バッテリー訴求":
		case "防水訴求":
		case "ノイズキャンセリング訴求":
		{
			string text6 = ((templateName == "防水訴求") ? "WATERPROOF" : ((templateName == "バッテリー訴求") ? "LONG BATTERY" : "NOISE CANCELING"));
			string text7 = ((templateName == "防水訴求") ? "水を気にせず、音楽をもっと自由に。" : ((templateName == "バッテリー訴求") ? "朝から夜まで、充電を気にしない。" : "周囲の音を抑え、聴きたい音へ。"));
			AddShape(page, "機能背景", "Rectangle", 0.0, 0.0, widthMm, heightMm, text2, text2, 0.0);
			AddShape(page, "シアン面", "RoundedRectangle", widthMm * 0.56, heightMm * 0.1, widthMm * 0.55, heightMm * 0.72, "#FF173A49", "#00173A49", 0.0);
			AddText(page, "機能ラベル", text6, num, num, num2, heightMm * 0.06, 12.0, text3, bold: true, "機能名");
			AddText(page, "機能コピー", text7, num, heightMm * 0.16, num2 * 0.72, heightMm * 0.2, 26.0, text4, bold: true, "機能コピー", 1.0, text2, 2.5, "#FF0B101C");
			AddText(page, "製品画像枠", "PRODUCT\nIMAGE", widthMm * 0.47, heightMm * 0.37, widthMm * 0.43, heightMm * 0.25, 16.0, "#FF7A8A9F", bold: true, "製品画像");
			AddShape(page, "数値カード", "RoundedRectangle", num, heightMm * 0.48, widthMm * 0.4, heightMm * 0.2, text4, text4, 0.0);
			string text8 = ((templateName == "防水訴求") ? "IPX7" : ((templateName == "バッテリー訴求") ? "最大 40h" : "-30dB"));
			AddText(page, "主要数値", text8, num * 1.4, heightMm * 0.51, widthMm * 0.34, heightMm * 0.09, 24.0, text, bold: true, "主要仕様");
			AddText(page, "数値説明", "強みが伝わる仕様と\n根拠情報を入力", num * 1.4, heightMm * 0.6, widthMm * 0.34, heightMm * 0.06, 10.0, text2, bold: false, "仕様説明");
			AddText(page, "補足", "※測定条件・対応等級・使用環境などの注意事項を入力してください。", num, heightMm * 0.82, num2, heightMm * 0.08, 8.0, "#FFC5CDDA", bold: false, "注意事項");
			break;
		}
		case "QRコード誘導":
			AddShape(page, "左背景", "RoundedRectangle", 0.0, 0.0, widthMm * 0.52, heightMm, text2, text2, 0.0);
			AddText(page, "ラベル", "SCAN TO DISCOVER", num, heightMm * 0.11, widthMm * 0.4, heightMm * 0.06, 11.0, text3, bold: true);
			AddText(page, "見出し", "続きは\nスマホで。", num, heightMm * 0.22, widthMm * 0.4, heightMm * 0.2, 30.0, text4, bold: true, "見出し", 1.0, text2, 3.0, "#FF080D16", 48.0);
			AddText(page, "説明", "製品情報・動画・キャンペーン詳細へ、すぐアクセスできます。", num, heightMm * 0.5, widthMm * 0.39, heightMm * 0.14, 11.0, "#FFD6DDEA", bold: false, "説明文");
			AddShape(page, "QRカード", "RoundedRectangle", widthMm * 0.57, heightMm * 0.2, widthMm * 0.35, widthMm * 0.43, text4, "#22000000", 1.0);
			page.Elements.Add(new CanvasElementModel
			{
				Kind = ElementKind.QrCode,
				Name = "QRコード",
				QrContent = "https://example.com",
				Xmm = widthMm * 0.61,
				Ymm = heightMm * 0.27,
				WidthMm = widthMm * 0.27,
				HeightMm = widthMm * 0.27,
				ZIndex = page.Elements.Count + 1,
				PlaceholderKey = "QRコード"
			});
			AddText(page, "URL", "example.com/product", widthMm * 0.57, heightMm * 0.68, widthMm * 0.35, heightMm * 0.05, 9.0, color, bold: false, "URL");
			AddText(page, "誘導文", "カメラをかざしてチェック", widthMm * 0.55, heightMm * 0.76, widthMm * 0.39, heightMm * 0.06, 11.0, text2, bold: true);
			break;
		case "店員向け製品ガイド":
			AddShape(page, "ガイドヘッダー", "Rectangle", 0.0, 0.0, widthMm, heightMm * 0.16, text2, text2, 0.0);
			AddText(page, "資料種別", "STAFF QUICK GUIDE", num, heightMm * 0.025, num2, heightMm * 0.04, 10.0, text3, bold: true);
			AddText(page, "製品名", "製品名 / 型番", num, heightMm * 0.065, num2, heightMm * 0.07, 22.0, text4, bold: true, "製品名");
			AddShape(page, "左カード", "RoundedRectangle", num, heightMm * 0.21, num2 * 0.47, heightMm * 0.28, text4, "#1F172033", 1.0);
			AddShape(page, "右カード", "RoundedRectangle", num + num2 * 0.51, heightMm * 0.21, num2 * 0.49, heightMm * 0.28, text4, "#1F172033", 1.0);
			AddText(page, "3つの特徴", "3つの推しポイント\n\n01  特徴を短く\n02  数値で強く\n03  利用場面で伝える", num * 1.4, heightMm * 0.24, num2 * 0.39, heightMm * 0.21, 11.0, text2, bold: true, "製品特徴");
			AddText(page, "販売トーク", "おすすめトーク\n\n『こんなお客様に最適です』\n比較時の一言を入力", num + num2 * 0.55, heightMm * 0.24, num2 * 0.4, heightMm * 0.21, 11.0, text2, bold: true, "販売トーク");
			AddShape(page, "仕様カード", "RoundedRectangle", num, heightMm * 0.54, num2, heightMm * 0.22, "#FFE9F7F9", "#332BB6C8", 1.0);
			AddText(page, "主要仕様", "主要仕様  ｜  バッテリー：--h  ｜  重量：--g  ｜  防水：IPX-\n対応機能・同梱品・注意点を簡潔に記載", num * 1.4, heightMm * 0.58, num2 - num, heightMm * 0.14, 11.0, text2, bold: false, "主要仕様");
			AddText(page, "注意事項", "※価格・仕様・キャンペーン条件は最新情報をご確認ください。", num, heightMm * 0.82, num2, heightMm * 0.05, 8.0, color, bold: false, "注意事項");
			break;
		case "新製品発売":
			AddShape(page, "新製品背景", "Rectangle", 0.0, 0.0, widthMm, heightMm, text2, text2, 0.0);
			AddShape(page, "発光円", "Ellipse", widthMm * 0.35, heightMm * 0.19, widthMm * 0.58, widthMm * 0.58, "#252BB6C8", "#002BB6C8", 0.0);
			AddText(page, "新製品ラベル", "NEW PRODUCT / 2026", num, num, num2, heightMm * 0.05, 11.0, text3, bold: true, "発売日");
			AddText(page, "キャッチコピー", "新しい体験が、\nここから始まる。", num, heightMm * 0.15, num2 * 0.7, heightMm * 0.2, 29.0, text4, bold: true, "キャッチコピー", 1.2, text2, 3.0, "#FF080D16");
			AddText(page, "製品画像枠", "PRODUCT IMAGE", widthMm * 0.42, heightMm * 0.38, widthMm * 0.48, heightMm * 0.25, 14.0, "#FF7F8CA0", bold: true, "製品画像");
			AddText(page, "製品名", "製品名 / MODEL", num, heightMm * 0.48, widthMm * 0.4, heightMm * 0.1, 20.0, text4, bold: true, "製品名");
			AddText(page, "特徴", "01  圧倒的な特徴\n02  進化した使いやすさ\n03  毎日使いたくなる設計", num, heightMm * 0.62, num2 * 0.54, heightMm * 0.16, 11.0, "#FFD8DFEB", bold: false, "主な特徴");
			AddText(page, "価格", "￥00,000", num, heightMm * 0.81, num2, heightMm * 0.08, 26.0, text, bold: true, "価格");
			break;
		default:
			AddShape(page, "ヘッダー", "Rectangle", 0.0, 0.0, widthMm, heightMm * 0.18, text, text, 0.0);
			AddText(page, "カテゴリ", "FEATURED PRODUCT", num, heightMm * 0.025, num2, heightMm * 0.04, 10.0, text4, bold: true);
			AddText(page, "キャッチコピー", "売場で伝わる、\nひとつ上の選択。", num, heightMm * 0.065, num2, heightMm * 0.1, 24.0, text4, bold: true, "キャッチコピー", 1.0, text, 2.5, text2);
			AddText(page, "製品名", "製品名 / MODEL", num, heightMm * 0.22, num2, heightMm * 0.08, 20.0, text2, bold: true, "製品名");
			AddShape(page, "製品ステージ", "RoundedRectangle", num, heightMm * 0.32, num2 * 0.58, heightMm * 0.35, text4, "#1F172033", 1.0);
			AddText(page, "製品画像枠", "PRODUCT\nIMAGE", num * 1.5, heightMm * 0.41, num2 * 0.5, heightMm * 0.15, 14.0, "#FF9AA4B4", bold: true, "製品画像");
			AddShape(page, "特徴カード", "RoundedRectangle", num + num2 * 0.62, heightMm * 0.32, num2 * 0.38, heightMm * 0.35, text2, text2, 0.0);
			AddText(page, "主な特徴", "KEY FEATURES\n\n✓ 特徴を短く\n✓ 数値で具体的に\n✓ 使用場面で訴求", num + num2 * 0.67, heightMm * 0.36, num2 * 0.29, heightMm * 0.24, 11.0, text4, bold: false, "主な特徴");
			AddText(page, "価格ラベル", "店頭販売価格", num, heightMm * 0.73, num2 * 0.35, heightMm * 0.04, 9.0, color, bold: false);
			AddText(page, "価格", "￥00,000", num, heightMm * 0.77, num2 * 0.64, heightMm * 0.1, 30.0, text, bold: true, "価格", 1.0, text4);
			AddShape(page, "QR枠", "RoundedRectangle", num + num2 * 0.76, heightMm * 0.73, num2 * 0.24, num2 * 0.24, text4, "#22172033", 1.0);
			break;
		}
		NormalizeZ(page);
	}

	private static void AddText(PageModel p, string name, string text, double x, double y, double w, double h, double pt, string color, bool bold, string placeholder = "", double outline = 0.0, string? outlineColor = null, double depth = 0.0, string? depthColor = null, double depthAngle = 45.0)
	{
		p.Elements.Add(new CanvasElementModel
		{
			Kind = ElementKind.Text,
			Name = name,
			Text = text,
			Xmm = x,
			Ymm = y,
			WidthMm = w,
			HeightMm = h,
			FontSizePt = pt,
			TextColor = color,
			Bold = bold,
			PlaceholderKey = placeholder,
			TextOutlineThicknessPt = outline,
			TextOutlineColor = (outlineColor ?? "#FFFFFFFF"),
			TextExtrusionDepthPt = depth,
			TextExtrusionColor = (depthColor ?? "#FF172033"),
			TextExtrusionAngle = depthAngle,
			ZIndex = p.Elements.Count + 1
		});
	}

	private static void AddShape(PageModel p, string name, string type, double x, double y, double w, double h, string fill, string stroke, double thickness)
	{
		p.Elements.Add(new CanvasElementModel
		{
			Kind = ElementKind.Shape,
			Name = name,
			ShapeType = type,
			Xmm = x,
			Ymm = y,
			WidthMm = w,
			HeightMm = h,
			FillColor = fill,
			StrokeColor = stroke,
			StrokeThicknessPt = thickness,
			IsDecoration = true,
			ZIndex = p.Elements.Count
		});
	}

	private static void NormalizeZ(PageModel page)
	{
		for (int i = 0; i < page.Elements.Count; i++)
		{
			page.Elements[i].ZIndex = i;
		}
	}
}
