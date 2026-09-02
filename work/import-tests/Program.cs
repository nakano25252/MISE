using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using RetailCanvas.Services;

PdfDocumentBuilder builder = new PdfDocumentBuilder();
PdfPageBuilder page = builder.AddPage(595, 842);
PdfDocumentBuilder.AddedFont font = builder.AddStandard14Font(Standard14Font.Helvetica);
page.AddText("Hello MISE", 24, new PdfPoint(72, 720), font);
byte[] testPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAIAAAACAQMAAABIeJ9nAAAAIGNIUk0AAHomAACAhAAA+gAAAIDoAAB1MAAA6mAAADqYAAAXcJy6UTwAAAAGUExURf8AAP///0EdNBEAAAABYktHRAH/Ai3eAAAAB3RJTUUH6ggcFgUYJwSCcwAAAAxJREFUCNdjYGBgAAAABAABJzQnCgAAAABJRU5ErkJggg==");
page.AddPng(testPng, new PdfRectangle(200, 600, 260, 660));
string pdfPath = Path.Combine(AppContext.BaseDirectory, "editable-test.pdf");
string aiPath = Path.Combine(AppContext.BaseDirectory, "editable-test.ai");
await File.WriteAllBytesAsync(pdfPath, builder.Build());
File.Copy(pdfPath, aiPath, true);

EditableDesignDocument pdf = await EditableDesignImportService.ReadAsync(pdfPath);
EditableDesignDocument ai = await EditableDesignImportService.ReadAsync(aiPath);
if (pdf.Pages.Count != 1 || ai.Pages.Count != 1 || pdf.Pages[0].TextBlocks.All(block => !block.Text.Contains("Hello")) || pdf.Pages[0].Images.Count != 1 || ai.Pages[0].Images.Count != 1)
{
    throw new InvalidOperationException("Editable PDF/AI import test failed.");
}

string legacyAiPath = Path.Combine(AppContext.BaseDirectory, "legacy-test.ai");
await File.WriteAllTextAsync(legacyAiPath, "%!PS-Adobe-3.0");
if (EditableDesignImportService.IsPdfCompatible(legacyAiPath))
{
    throw new InvalidOperationException("Legacy AI compatibility detection failed.");
}

Console.WriteLine($"PASS pages={pdf.Pages.Count} text={string.Join("|", pdf.Pages[0].TextBlocks.Select(block => block.Text))} images={pdf.Pages[0].Images.Count}");
