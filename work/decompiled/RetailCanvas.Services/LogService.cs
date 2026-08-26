using System;
using System.IO;

namespace RetailCanvas.Services;

public static class LogService
{
	private static readonly object Gate = new object();

	public static void Info(string message)
	{
		Write("INFO", message, null);
	}

	public static void Error(string message, Exception? ex = null)
	{
		Write("ERROR", message, ex);
	}

	private static void Write(string level, string message, Exception? ex)
	{
		try
		{
			AppPaths.EnsureCreated();
			string path = Path.Combine(AppPaths.Logs, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
			string contents = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}" + ((ex == null) ? string.Empty : (Environment.NewLine + ex)) + Environment.NewLine;
			lock (Gate)
			{
				File.AppendAllText(path, contents);
			}
		}
		catch
		{
		}
	}

	public static void CleanupOldLogs(int days)
	{
		try
		{
			DateTime dateTime = DateTime.Now.AddDays(-days);
			foreach (string item in Directory.EnumerateFiles(AppPaths.Logs, "*.log"))
			{
				if (File.GetLastWriteTime(item) < dateTime)
				{
					File.Delete(item);
				}
			}
		}
		catch
		{
		}
	}
}
