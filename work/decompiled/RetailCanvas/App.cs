using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using RetailCanvas.Services;

namespace RetailCanvas;

public class App : Application
{
	private bool _contentLoaded;

	public static string? StartupProjectPath { get; private set; }

	protected override void OnStartup(StartupEventArgs e)
	{
		base.DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += delegate(object _, UnhandledExceptionEventArgs args)
		{
			LogService.Error("Unhandled exception", args.ExceptionObject as Exception);
		};
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
		CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ja-JP");
		ApplyMiseTheme();
		AppPaths.EnsureCreated();
		try
		{
			TextureCatalogService.EnsureInstalled();
		}
		catch (Exception ex)
		{
			LogService.Error("Texture initialization failed; continuing without bundled textures", ex);
		}
		StartupProjectPath = e.Args.FirstOrDefault(File.Exists);
		LogService.Info("Application started");
		base.OnStartup(e);
	}

	protected override void OnExit(ExitEventArgs e)
	{
		LogService.Info("Application exited");
		LogService.CleanupOldLogs(30);
		base.OnExit(e);
	}

	private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		LogService.Error("UI exception", e.Exception);
		MessageBox.Show("予期しないエラーが発生しました。自動保存データは保持されています。\n\n" + e.Exception.Message, "MISE", MessageBoxButton.OK, MessageBoxImage.Hand);
		e.Handled = true;
	}

	private void ApplyMiseTheme()
	{
		Color color = Color.FromRgb(16, 24, 39);
		Color color2 = Color.FromRgb(35, 48, 70);
		Color color3 = Color.FromRgb(byte.MaxValue, 107, 74);
		Color color4 = Color.FromRgb(50, 199, 201);
		Color color5 = Color.FromRgb(246, 243, 238);
		base.Resources["NavyColor"] = color;
		base.Resources["NavyLightColor"] = color2;
		base.Resources["OrangeColor"] = color3;
		base.Resources["CyanColor"] = color4;
		base.Resources["SurfaceColor"] = color5;
		base.Resources["NavyBrush"] = new SolidColorBrush(color);
		base.Resources["NavyLightBrush"] = new SolidColorBrush(color2);
		base.Resources["OrangeBrush"] = new SolidColorBrush(color3);
		base.Resources["CyanBrush"] = new SolidColorBrush(color4);
		base.Resources["SurfaceBrush"] = new SolidColorBrush(color5);
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.25.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
			Uri resourceLocator = new Uri("/RetailCanvas;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "8.0.25.0")]
	public static void Main()
	{
		try
		{
			App app = new App();
			app.InitializeComponent();
			app.Run();
		}
		catch (Exception ex)
		{
			WriteEmergencyStartupLog(ex);
			try
			{
				MessageBox.Show("起動中にエラーが発生しました。\n\n" + ex.Message + "\n\n詳細は startup-error.log を確認してください。", "MISE", MessageBoxButton.OK, MessageBoxImage.Hand);
			}
			catch
			{
			}
		}
	}

	private static void WriteEmergencyStartupLog(Exception ex)
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetailCanvas", "Logs");
			Directory.CreateDirectory(text);
			File.AppendAllText(Path.Combine(text, "startup-error.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\n{ex}\n\n");
		}
		catch
		{
		}
	}
}
