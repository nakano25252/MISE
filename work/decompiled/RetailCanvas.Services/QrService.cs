using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QRCoder;

namespace RetailCanvas.Services;

public static class QrService
{
	public static byte[] CreatePng(string content, string level, string foreground, string background, int pixelsPerModule = 14)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			content = "https://example.com";
		}
		using QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
		using QRCodeData data = qRCodeGenerator.CreateQrCode(content, ParseLevel(level), forceUtf8: true);
		using PngByteQRCode pngByteQRCode = new PngByteQRCode(data);
		return pngByteQRCode.GetGraphic(pixelsPerModule, ParseRgba(foreground), ParseRgba(background));
	}

	public static IReadOnlyList<BitArray> CreateMatrix(string content, string level)
	{
		if (string.IsNullOrWhiteSpace(content))
		{
			content = "https://example.com";
		}
		using QRCodeGenerator qRCodeGenerator = new QRCodeGenerator();
		using QRCodeData qRCodeData = qRCodeGenerator.CreateQrCode(content, ParseLevel(level), forceUtf8: true);
		return qRCodeData.ModuleMatrix.Select((BitArray row) => new BitArray(row)).ToList();
	}

	private static QRCodeGenerator.ECCLevel ParseLevel(string level)
	{
		return level.ToUpperInvariant() switch
		{
			"L" => QRCodeGenerator.ECCLevel.L, 
			"Q" => QRCodeGenerator.ECCLevel.Q, 
			"H" => QRCodeGenerator.ECCLevel.H, 
			_ => QRCodeGenerator.ECCLevel.M, 
		};
	}

	private static byte[] ParseRgba(string value)
	{
		string text = value.Trim().TrimStart('#');
		if (text.Length == 8)
		{
			string text2 = text;
			text = text2.Substring(2, text2.Length - 2) + text.Substring(0, 2);
		}
		if (text.Length != 8)
		{
			text = "000000FF";
		}
		return new byte[4]
		{
			Convert.ToByte(text.Substring(0, 2), 16),
			Convert.ToByte(text.Substring(2, 2), 16),
			Convert.ToByte(text.Substring(4, 2), 16),
			Convert.ToByte(text.Substring(6, 2), 16)
		};
	}
}
