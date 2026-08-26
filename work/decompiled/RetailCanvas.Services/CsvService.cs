using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RetailCanvas.Services;

public static class CsvService
{
	public static List<List<string>> Read(string path)
	{
		return ReadDetected(path).Rows;
	}

	public static CsvReadResult ReadDetected(string path)
	{
		byte[] array = File.ReadAllBytes(path);
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		string text;
		string encodingName;
		if (array.Length >= 3 && array[0] == 239 && array[1] == 187 && array[2] == 191)
		{
			text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array, 3, array.Length - 3);
			encodingName = "UTF-8 (BOM付き)";
		}
		else
		{
			try
			{
				text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(array);
				encodingName = "UTF-8";
			}
			catch (DecoderFallbackException)
			{
				text = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(array);
				encodingName = "CP932 / Shift_JIS";
			}
		}
		return new CsvReadResult(Parse(text), encodingName);
	}

	public static List<List<string>> Parse(string text)
	{
		List<List<string>> list = new List<List<string>>();
		List<string> list2 = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (flag)
			{
				if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
				{
					stringBuilder.Append('"');
					i++;
				}
				else if (c == '"')
				{
					flag = false;
				}
				else
				{
					stringBuilder.Append(c);
				}
				continue;
			}
			if (c == '"' && stringBuilder.Length == 0)
			{
				flag = true;
				continue;
			}
			switch (c)
			{
			case ',':
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				break;
			case '\n':
			case '\r':
				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					i++;
				}
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				list.Add(list2);
				list2 = new List<string>();
				break;
			default:
				stringBuilder.Append(c);
				break;
			}
		}
		if (flag)
		{
			throw new FormatException("CSVの引用符が閉じられていません。");
		}
		if (stringBuilder.Length > 0 || list2.Count > 0)
		{
			list2.Add(stringBuilder.ToString());
			list.Add(list2);
		}
		return list;
	}

	public static void Write(string path, IEnumerable<IReadOnlyList<string>> rows)
	{
		string text = string.Join(Environment.NewLine, rows.Select((IReadOnlyList<string> row) => string.Join(",", row.Select(Escape))));
		File.WriteAllText(path, "\ufeff" + text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	private static string Escape(string value)
	{
		if (value == null)
		{
			value = string.Empty;
		}
		if (value.IndexOfAny(new char[4] { ',', '"', '\r', '\n' }) >= 0)
		{
			return "\"" + value.Replace("\"", "\"\"") + "\"";
		}
		return value;
	}
}
