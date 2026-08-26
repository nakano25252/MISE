using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ImageTrimDialog : Window
{
	private readonly Slider _threshold = new Slider
	{
		Minimum = 0.0,
		Maximum = 254.0,
		TickFrequency = 1.0,
		IsSnapToTickEnabled = true
	};

	private readonly Slider _padding = new Slider
	{
		Minimum = 0.0,
		Maximum = 100.0,
		TickFrequency = 1.0,
		IsSnapToTickEnabled = true
	};

	private readonly TextBlock _summary = new TextBlock();

	public byte AlphaThreshold => (byte)_threshold.Value;

	public int PaddingPixels => (int)_padding.Value;

	public bool RestoreOriginal { get; private set; }

	public ImageTrimDialog(CanvasElementModel model)
	{
		base.Title = "透明余白のトリミング － MISE";
		base.Width = 500.0;
		base.Height = 390.0;
		base.MinWidth = 420.0;
		base.MinHeight = 330.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 500.0, 390.0, 420.0, 330.0);
		_threshold.Value = (int)model.ImageTransparentTrimThreshold;
		_padding.Value = model.ImageTransparentTrimPaddingPixels;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(20.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "透明部分を自動トリミング",
			FontSize = 21.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "透明PNGの輪郭を検出し、不要な余白だけを非破壊で除きます。",
			Foreground = Brushes.SlateGray,
			Margin = new Thickness(0.0, 3.0, 0.0, 14.0)
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "透明とみなすしきい値 (0～254)"
		});
		stackPanel.Children.Add(_threshold);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "輪郭の外側に残す余白 (px)",
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		});
		stackPanel.Children.Add(_padding);
		_summary.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		stackPanel.Children.Add(_summary);
		if (model.ImageTransparentTrimApplied || !string.IsNullOrWhiteSpace(model.ImagePreTrimDataBase64))
		{
			Button button = new Button
			{
				Content = "元の画像範囲へ戻す",
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
			};
			button.Click += delegate
			{
				RestoreOriginal = true;
				base.DialogResult = true;
			};
			stackPanel.Children.Add(button);
		}
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		Button button2 = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button3 = new Button
		{
			Content = "トリミングを適用",
			MinWidth = 130.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsDefault = true
		};
		button3.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel2.Children.Add(button2);
		stackPanel2.Children.Add(button3);
		stackPanel.Children.Add(stackPanel2);
		base.Content = stackPanel;
		_threshold.ValueChanged += delegate
		{
			Refresh();
		};
		_padding.ValueChanged += delegate
		{
			Refresh();
		};
		Refresh();
	}

	private void Refresh()
	{
		_summary.Text = $"透明度 {_threshold.Value:0} 以下を透明扱い ／ 余白 {_padding.Value:0}px";
	}
}
