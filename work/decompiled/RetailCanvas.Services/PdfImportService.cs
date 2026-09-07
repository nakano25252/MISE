using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RetailCanvas.Services;

public static class PdfImportService
{
	public static async Task<int> GetPageCountAsync(string path)
	{
		string loadPath = PrepareLoadPath(path, out string? temporaryPath);
		try
		{
			return checked((int)(await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(loadPath))).PageCount);
		}
		finally
		{
			DeleteTemporaryFile(temporaryPath);
		}
	}

	public static async Task<PdfRenderedPage> RenderPageAsync(string path, int pageIndex, int maxLongSidePixels = 2600)
	{
		string loadPath = PrepareLoadPath(path, out string? temporaryPath);
		try
		{
			PdfDocument pdfDocument = await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(loadPath));
			if (pageIndex < 0 || pageIndex >= checked((int)pdfDocument.PageCount))
			{
				throw new ArgumentOutOfRangeException("pageIndex");
			}
			using PdfPage page = pdfDocument.GetPage((uint)pageIndex);
			double width = page.Size.Width;
			double height = page.Size.Height;
			double num = (double)maxLongSidePixels / Math.Max(width, height);
			uint destinationWidth;
			uint destinationHeight;
			checked
			{
				destinationWidth = Math.Max(1u, (uint)Math.Round(width * num));
				destinationHeight = Math.Max(1u, (uint)Math.Round(height * num));
			}
			using InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
			await page.RenderToStreamAsync(stream, new PdfPageRenderOptions
			{
				DestinationWidth = destinationWidth,
				DestinationHeight = destinationHeight
			});
			if (stream.Size > uint.MaxValue)
			{
				throw new InvalidOperationException("PDFページのプレビューが大きすぎます。");
			}
			byte[] bytes = new byte[(uint)stream.Size];
			stream.Seek(0uL);
			using DataReader reader = new DataReader(stream.GetInputStreamAt(0uL));
			await reader.LoadAsync((uint)stream.Size);
			reader.ReadBytes(bytes);
			return new PdfRenderedPage(bytes, width, height, pageIndex);
		}
		finally
		{
			DeleteTemporaryFile(temporaryPath);
		}
	}

	private static string PrepareLoadPath(string path, out string? temporaryPath)
	{
		temporaryPath = null;
		if (!System.IO.Path.GetExtension(path).Equals(".ai", StringComparison.OrdinalIgnoreCase))
		{
			return path;
		}
		if (!EditableDesignImportService.IsPdfCompatible(path))
		{
			throw new InvalidDataException("このAIファイルにはPDF互換データがありません。Illustratorで『PDF互換ファイルを作成』を有効にして保存し直してください。");
		}
		temporaryPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MISE_AI_" + Guid.NewGuid().ToString("N") + ".pdf");
		File.Copy(path, temporaryPath, overwrite: false);
		return temporaryPath;
	}

	private static void DeleteTemporaryFile(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}
}
