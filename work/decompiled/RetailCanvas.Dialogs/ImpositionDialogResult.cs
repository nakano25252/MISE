namespace RetailCanvas.Dialogs;

public sealed record ImpositionDialogResult(string PaperName, bool Landscape, double MarginMm, double GapMm, int Copies, bool CropMarks);
