using System;
using System.Collections.Generic;

namespace RetailCanvas.Models;

public sealed class ProjectModel
{
	public int FileFormatVersion { get; set; } = 1;

	public Guid ProjectId { get; set; } = Guid.NewGuid();

	public string ProjectName { get; set; } = "無題の販促物";

	public string Purpose { get; set; } = "製品単品POP";

	public string BrandName { get; set; } = string.Empty;

	public string StoreName { get; set; } = string.Empty;

	public string Author { get; set; } = string.Empty;

	public string PaperName { get; set; } = "A4";

	public bool Landscape { get; set; }

	public string PrintMode { get; set; } = "家庭用プリンタ";

	public string CategoryProfile { get; set; } = "JBL Audio";

	public List<string> EnabledProductCategories { get; set; } = new List<string> { "TWS", "ヘッドホン", "スピーカー", "サウンドバー" };

	public DateTime CreatedAt { get; set; } = DateTime.Now;

	public DateTime UpdatedAt { get; set; } = DateTime.Now;

	public List<PageModel> Pages { get; set; } = new List<PageModel> { PageModel.Create("A4", landscape: false) };

	public ExportSettings ExportSettings { get; set; } = new ExportSettings();

	public List<EmbeddedFontModel> EmbeddedFonts { get; set; } = new List<EmbeddedFontModel>();
}
