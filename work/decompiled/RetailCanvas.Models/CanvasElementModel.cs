using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetailCanvas.Models;

public sealed class CanvasElementModel
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public ElementKind Kind { get; set; }

	public string Name { get; set; } = "要素";

	public double Xmm { get; set; } = 20.0;

	public double Ymm { get; set; } = 20.0;

	public double WidthMm { get; set; } = 80.0;

	public double HeightMm { get; set; } = 20.0;

	public double Rotation { get; set; }

	public double SkewX { get; set; }

	public double SkewY { get; set; }

	public double Opacity { get; set; } = 1.0;

	public int ZIndex { get; set; }

	public bool IsVisible { get; set; } = true;

	public bool IsLocked { get; set; }

	public bool PreserveAspectRatio { get; set; } = true;

	public bool IsDecoration { get; set; }

	public string Text { get; set; } = string.Empty;

	public string FontFamily { get; set; } = "Yu Gothic UI";

	public double FontSizePt { get; set; } = 18.0;

	public int FontWeightValue { get; set; } = 400;

	public bool Bold { get; set; }

	public bool Italic { get; set; }

	public bool Underline { get; set; }

	public string TextColor { get; set; } = "#FF172033";

	public string TextBackground { get; set; } = "#00FFFFFF";

	public string TextOutlineColor { get; set; } = "#FFFFFFFF";

	public double TextOutlineThicknessPt { get; set; }

	public string TextOutlinePosition { get; set; } = "外側";

	public string TextExtrusionColor { get; set; } = "#FF172033";

	public double TextExtrusionDepthPt { get; set; }

	public double TextExtrusionAngle { get; set; } = 45.0;

	public bool TextExtrudeOutline { get; set; } = true;

	public string TextAlignment { get; set; } = "Center";

	public string VerticalAlignment { get; set; } = "Center";

	public double LineHeight { get; set; }

	public double LineSpacingPt { get; set; }

	public double CharacterSpacing { get; set; }

	public string ShapeType { get; set; } = "Rectangle";

	public string LineStyle { get; set; } = "実線";

	public string LineStartCap { get; set; } = "なし";

	public string LineEndCap { get; set; } = "なし";

	public double ArrowSize { get; set; } = 8.0;

	public string FillColor { get; set; } = "#FFF26A21";

	public string StrokeColor { get; set; } = "#FF172033";

	public double StrokeThicknessPt { get; set; } = 1.0;

	public double CornerRadiusMm { get; set; } = 2.0;

	public double CornerRadiusTopLeftMm { get; set; } = -1.0;

	public double CornerRadiusTopRightMm { get; set; } = -1.0;

	public double CornerRadiusBottomRightMm { get; set; } = -1.0;

	public double CornerRadiusBottomLeftMm { get; set; } = -1.0;

	public string ShapeExtrusionColor { get; set; } = "#FF172033";

	public double ShapeExtrusionDepthPt { get; set; }

	public double ShapeExtrusionAngle { get; set; } = 45.0;

	public int PanelRows { get; set; } = 1;

	public bool PanelEnabled { get; set; }

	public int PanelColumns { get; set; } = 1;

	public List<double> PanelRowSplits { get; set; } = new List<double>();

	public List<double> PanelColumnSplits { get; set; } = new List<double>();

	public List<string> PanelCellColors { get; set; } = new List<string>();

	public List<string> PanelCellRoles { get; set; } = new List<string>();

	public string PanelDividerColor { get; set; } = "#FF172033";

	public double PanelDividerThicknessPt { get; set; } = 1.0;

	public double PanelDividerOpacity { get; set; } = 1.0;

	public string PanelDividerStyle { get; set; } = "実線";

	public string TextureName { get; set; } = string.Empty;

	public string? TextureDataBase64 { get; set; }

	public double TextureOpacity { get; set; } = 0.55;

	public double TextureScale { get; set; } = 1.0;

	public List<ShapePointModel> ShapePoints { get; set; } = new List<ShapePointModel>();

	public bool ShapeClosed { get; set; }

	public string? ImageDataBase64 { get; set; }

	public string? PdfSourcePath { get; set; }

	public int? PdfPageIndex { get; set; }

	public string? ImageOriginalDataBase64 { get; set; }

	public string? ImagePreTrimDataBase64 { get; set; }

	public bool ImageTransparentTrimApplied { get; set; }

	public byte ImageTransparentTrimThreshold { get; set; } = 1;

	public int ImageTransparentTrimPaddingPixels { get; set; }

	public string? ImageCutoutSettingsJson { get; set; }

	public string? ImageSourcePath { get; set; }

	public bool ImageUsesLinkedOriginal { get; set; }

	public long ImageSourceBytes { get; set; }

	public int ImagePixelWidth { get; set; }

	public int ImagePixelHeight { get; set; }

	public double ImageExtrusionDepthPt { get; set; }

	public double ImageExtrusionAngle { get; set; } = 135.0;

	public string ImageExtrusionColor { get; set; } = "#FF172033";

	public double ImageExtrusionSmoothness { get; set; } = 1.0;

	public double Brightness { get; set; }

	public double Contrast { get; set; }

	public double Saturation { get; set; }

	public string QrContent { get; set; } = string.Empty;

	public string QrForeground { get; set; } = "#FF000000";

	public string QrBackground { get; set; } = "#FFFFFFFF";

	public string QrErrorCorrection { get; set; } = "M";

	public int QrQuietZone { get; set; } = 4;

	public string QrLabel { get; set; } = string.Empty;

	public string PlaceholderKey { get; set; } = string.Empty;

	[JsonIgnore]
	public double EffectiveDpi
	{
		get
		{
			if (Kind != ElementKind.Image || !(WidthMm > 0.0))
			{
				return 0.0;
			}
			return (double)ImagePixelWidth / (WidthMm / 25.4);
		}
	}
}
