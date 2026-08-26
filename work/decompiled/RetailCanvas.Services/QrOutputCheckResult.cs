using System;

namespace RetailCanvas.Services;

public sealed record QrOutputCheckResult(Guid ElementId, string ElementName, string Content, bool Passed, double PatternMatchPercent, double Contrast, double PixelsPerModule, string Detail);
