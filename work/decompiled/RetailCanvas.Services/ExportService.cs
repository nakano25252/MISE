using System.Collections.Generic;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace RetailCanvas.Services;

public static class ExportService
{
	public static void SavePdf(string path, IReadOnlyList<RenderedPage> pages)
	{
		using PdfDocument pdfDocument = new PdfDocument();
		pdfDocument.Info.Title = Path.GetFileNameWithoutExtension(path);
		pdfDocument.Info.Creator = "MISE " + AppInfo.Version;
		foreach (RenderedPage page in pages)
		{
			PdfPage pdfPage = pdfDocument.AddPage();
			pdfPage.Width = XUnit.FromMillimeter(page.WidthMm);
			pdfPage.Height = XUnit.FromMillimeter(page.HeightMm);
			using XGraphics xGraphics = XGraphics.FromPdfPage(pdfPage);
			using MemoryStream stream = new MemoryStream(page.PngBytes, writable: false);
			using XImage image = XImage.FromStream(stream);
			xGraphics.DrawImage(image, 0.0, 0.0, pdfPage.Width.Point, pdfPage.Height.Point);
		}
		pdfDocument.Save(path);
		LogService.Info("PDF exported: " + path);
	}
}
