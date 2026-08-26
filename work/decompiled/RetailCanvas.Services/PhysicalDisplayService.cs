using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace RetailCanvas.Services;

public static class PhysicalDisplayService
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MonitorInfoEx
	{
		public int Size;

		public RectNative Monitor;

		public RectNative Work;

		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string DeviceName;
	}

	private struct RectNative
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const int MonitorDefaultToNearest = 2;

	private const int HorzSize = 4;

	private const int VertSize = 6;

	private const int DesktopHorzRes = 118;

	private const int DesktopVertRes = 117;

	public static double GetActualSizeZoom(Window window, double calibrationPercent, out string description)
	{
		double pixelsPerInchX = VisualTreeHelper.GetDpi(window).PixelsPerInchX;
		double? num = TryPhysicalDpi(window);
		double num2 = Math.Clamp(calibrationPercent, 50.0, 200.0) / 100.0;
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			if (valueOrDefault > 45.0 && valueOrDefault < 600.0)
			{
				description = $"モニター検知 {num.Value:0.#}dpi／補正 {calibrationPercent:0.#}%";
				return Math.Clamp(num.Value / Math.Max(1.0, pixelsPerInchX) * num2, 0.25, 4.0);
			}
		}
		description = $"Windows標準値で推定／補正 {calibrationPercent:0.#}%";
		return Math.Clamp(96.0 / Math.Max(1.0, pixelsPerInchX) * num2, 0.25, 4.0);
	}

	private static double? TryPhysicalDpi(Window window)
	{
		nint num = MonitorFromWindow(new WindowInteropHelper(window).Handle, 2);
		MonitorInfoEx info = new MonitorInfoEx
		{
			Size = Marshal.SizeOf<MonitorInfoEx>()
		};
		if (num == IntPtr.Zero || !GetMonitorInfo(num, ref info))
		{
			return null;
		}
		nint num2 = CreateDC("DISPLAY", info.DeviceName, null, IntPtr.Zero);
		if (num2 == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			int deviceCaps = GetDeviceCaps(num2, 4);
			int deviceCaps2 = GetDeviceCaps(num2, 6);
			int deviceCaps3 = GetDeviceCaps(num2, 118);
			int deviceCaps4 = GetDeviceCaps(num2, 117);
			if (deviceCaps < 100 || deviceCaps2 < 80 || deviceCaps3 < 640 || deviceCaps4 < 480)
			{
				return null;
			}
			double num3 = (double)deviceCaps3 / ((double)deviceCaps / 25.4);
			double num4 = (double)deviceCaps4 / ((double)deviceCaps2 / 25.4);
			bool flag = ((num3 < 45.0 || num3 > 600.0) ? true : false);
			bool flag2 = flag;
			if (!flag2)
			{
				bool flag3 = ((num4 < 45.0 || num4 > 600.0) ? true : false);
				flag2 = flag3;
			}
			if (flag2)
			{
				return null;
			}
			if (Math.Max(num3, num4) / Math.Min(num3, num4) > 1.25)
			{
				return null;
			}
			return Math.Sqrt(num3 * num4);
		}
		finally
		{
			DeleteDC(num2);
		}
	}

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint hwnd, int flags);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfoEx info);

	[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
	private static extern nint CreateDC(string driver, string device, string? output, nint initData);

	[DllImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool DeleteDC(nint dc);

	[DllImport("gdi32.dll")]
	private static extern int GetDeviceCaps(nint dc, int index);
}
