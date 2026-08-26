using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using RetailCanvas.Models;

namespace RetailCanvas.Services;

public static class WindowSizing
{
	private struct MonitorInfo
	{
		public int Size;

		public NativeRect Monitor;

		public NativeRect Work;

		public uint Flags;
	}

	private struct NativeRect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	private const uint MonitorDefaultToNearest = 2u;

	public static void Attach(Window window, double preferredWidth, double preferredHeight, double minimumWidth = 320.0, double minimumHeight = 240.0)
	{
		window.MinWidth = minimumWidth;
		window.MinHeight = minimumHeight;
		window.SourceInitialized += delegate
		{
			Apply(window, preferredWidth, preferredHeight, minimumWidth, minimumHeight);
		};
		window.Loaded += delegate
		{
			Apply(window, preferredWidth, preferredHeight, minimumWidth, minimumHeight);
		};
	}

	public static void AttachMainWindow(Window window, AppSettings settings)
	{
		window.MinWidth = 560.0;
		window.MinHeight = 320.0;
		bool applied = false;
		window.SourceInitialized += delegate
		{
			if (!applied)
			{
				applied = true;
				ApplyMainWindow(window, settings);
			}
		};
	}

	public static void ApplyMainWindow(Window window, AppSettings settings)
	{
		window.MaxWidth = double.PositiveInfinity;
		window.MaxHeight = double.PositiveInfinity;
		window.MinWidth = 560.0;
		window.MinHeight = 320.0;
		Rect workArea = GetWorkArea(window);
		if (settings.StartupWindowMode == "最大化")
		{
			window.WindowState = WindowState.Maximized;
		}
		else if (settings.StartupWindowMode == "前回の状態" && settings.RememberWindowPlacement)
		{
			window.WindowState = WindowState.Normal;
			window.Width = Math.Clamp(settings.LastWindowWidth, 560.0, Math.Max(560.0, workArea.Width));
			window.Height = Math.Clamp(settings.LastWindowHeight, 320.0, Math.Max(320.0, workArea.Height));
			if (settings.LastWindowLeft.HasValue && settings.LastWindowTop.HasValue)
			{
				window.WindowStartupLocation = WindowStartupLocation.Manual;
				window.Left = Math.Clamp(settings.LastWindowLeft.Value, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
				window.Top = Math.Clamp(settings.LastWindowTop.Value, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
			}
			if (settings.LastWindowMaximized)
			{
				window.WindowState = WindowState.Maximized;
			}
		}
		else
		{
			window.WindowState = WindowState.Normal;
			double value = ((settings.StartupWindowMode == "カスタム") ? settings.CustomWindowWidth : 1280.0);
			double value2 = ((settings.StartupWindowMode == "カスタム") ? settings.CustomWindowHeight : 800.0);
			window.Width = Math.Clamp(value, 560.0, Math.Max(560.0, workArea.Width - 12.0));
			window.Height = Math.Clamp(value2, 320.0, Math.Max(320.0, workArea.Height - 12.0));
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Left = workArea.Left + Math.Max(0.0, (workArea.Width - window.Width) / 2.0);
			window.Top = workArea.Top + Math.Max(0.0, (workArea.Height - window.Height) / 2.0);
		}
	}

	public static void SaveMainWindowPlacement(Window window, AppSettings settings)
	{
		if (settings.RememberWindowPlacement)
		{
			settings.LastWindowMaximized = window.WindowState == WindowState.Maximized;
			Rect restoreBounds = window.RestoreBounds;
			if (restoreBounds.Width >= 560.0 && restoreBounds.Height >= 320.0)
			{
				settings.LastWindowLeft = restoreBounds.Left;
				settings.LastWindowTop = restoreBounds.Top;
				settings.LastWindowWidth = restoreBounds.Width;
				settings.LastWindowHeight = restoreBounds.Height;
			}
		}
	}

	private static void Apply(Window window, double preferredWidth, double preferredHeight, double minimumWidth, double minimumHeight)
	{
		Rect workArea = GetWorkArea(window);
		double val = Math.Max(320.0, workArea.Width - 12.0);
		double val2 = Math.Max(260.0, workArea.Height - 12.0);
		window.MinWidth = Math.Min(minimumWidth, val);
		window.MinHeight = Math.Min(minimumHeight, val2);
		window.MaxWidth = double.PositiveInfinity;
		window.MaxHeight = double.PositiveInfinity;
		if (window.WindowState == WindowState.Normal)
		{
			window.Width = Math.Min(preferredWidth, val);
			window.Height = Math.Min(preferredHeight, val2);
			window.Left = Math.Clamp(window.Left, workArea.Left + 6.0, Math.Max(workArea.Left + 6.0, workArea.Right - window.Width - 6.0));
			window.Top = Math.Clamp(window.Top, workArea.Top + 6.0, Math.Max(workArea.Top + 6.0, workArea.Bottom - window.Height - 6.0));
		}
	}

	private static Rect GetWorkArea(Window window)
	{
		nint handle = new WindowInteropHelper(window).Handle;
		if (handle == IntPtr.Zero)
		{
			return SystemParameters.WorkArea;
		}
		nint num = MonitorFromWindow(handle, 2u);
		MonitorInfo monitorInfo = new MonitorInfo
		{
			Size = Marshal.SizeOf<MonitorInfo>()
		};
		if (num == IntPtr.Zero || !GetMonitorInfo(num, ref monitorInfo))
		{
			return SystemParameters.WorkArea;
		}
		Point point = new Point(monitorInfo.Work.Left, monitorInfo.Work.Top);
		Point point2 = new Point(monitorInfo.Work.Right, monitorInfo.Work.Bottom);
		CompositionTarget compositionTarget = PresentationSource.FromVisual(window)?.CompositionTarget;
		if (compositionTarget != null)
		{
			Matrix transformFromDevice = compositionTarget.TransformFromDevice;
			point = transformFromDevice.Transform(point);
			point2 = transformFromDevice.Transform(point2);
		}
		return new Rect(point, point2);
	}

	[DllImport("user32.dll")]
	private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(nint monitorHandle, ref MonitorInfo monitorInfo);
}
