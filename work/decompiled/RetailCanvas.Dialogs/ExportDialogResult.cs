namespace RetailCanvas.Dialogs;

public sealed record ExportDialogResult(string Format, int Dpi, bool AllPages, bool Transparent, int JpegQuality);
