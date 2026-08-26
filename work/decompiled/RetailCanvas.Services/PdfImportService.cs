using System;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RetailCanvas.Services;

public static class PdfImportService
{
	public static async Task<int> GetPageCountAsync(string path)
	{
		return checked((int)(await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(path))).PageCount);
	}

	public static async Task<PdfRenderedPage> RenderPageAsync(string path, int pageIndex, int maxLongSidePixels = 2600)
	{
		PdfDocument pdfDocument = await PdfDocument.LoadFromFileAsync(await StorageFile.GetFileFromPathAsync(path));
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
}
