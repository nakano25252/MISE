namespace RetailCanvas.Dialogs;

public sealed record PrintWizardResult(string OutputMethod, string PaperType, string Quality, string ScaleMode, bool Borderless, string ColorMode, double BlackDensity, double Contrast, double Gamma, double BlackThreshold, bool Dithering, bool PreservePhotoTones, bool PreferBlackInk, bool RequestK100, string Duplex);
