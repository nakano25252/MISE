using System.Windows;
using System.Windows.Controls;
using RetailCanvas.Models;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class ImageExtrusionDialog : Window
{
	private readonly Slider _depth = new Slider
	{
		Minimum = 0.0,
		Maximum = 30.0,
		TickFrequency = 1.0,
		IsSnapToTickEnabled = true
	};

	private readonly Slider _angle = new Slider
	{
		Minimum = 0.0,
		Maximum = 359.0,
		TickFrequency = 1.0
	};

	private readonly Slider _smoothness = new Slider
	{
		Minimum = 0.25,
		Maximum = 4.0,
		TickFrequency = 0.25,
		IsSnapToTickEnabled = true
	};

	private readonly TextBlock _summary = new TextBlock();

	private string _color;

	public double DepthPt => _depth.Value;

	public double Angle => _angle.Value;

	public string ColorValue => _color;

	public double Smoothness => _smoothness.Value;

	public ImageExtrusionDialog(CanvasElementModel model)
	{
		base.Title = "画像の立体効果 － MISE";
		base.Width = 480.0;
		base.Height = 420.0;
		base.MinHeight = 360.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 480.0, 420.0, 380.0, 360.0);
		_depth.Value = model.ImageExtrusionDepthPt;
		_angle.Value = model.ImageExtrusionAngle;
		_smoothness.Value = model.ImageExtrusionSmoothness;
		_color = model.ImageExtrusionColor;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(18.0)
		};
		stackPanel.Children.Add(new TextBlock
		{
			Text = "PNGの透明輪郭を使って奥行きを作ります",
			FontSize = 18.0,
			FontWeight = FontWeights.SemiBold
		});
		stackPanel.Children.Add(new TextBlock
		{
			Text = "飛び出し量 (pt)",
			Margin = new Thickness(0.0, 16.0, 0.0, 3.0)
		});
		stackPanel.Children.Add(_depth);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "飛び出す角度 (°)",
			Margin = new Thickness(0.0, 12.0, 0.0, 3.0)
		});
		stackPanel.Children.Add(_angle);
		stackPanel.Children.Add(new TextBlock
		{
			Text = "輪郭の滑らかさ",
			Margin = new Thickness(0.0, 12.0, 0.0, 3.0)
		});
		stackPanel.Children.Add(_smoothness);
		Button button = new Button
		{
			Content = "立体部分の色…",
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			string text = ColorPickerDialog.Show(this, _color);
			if (text != null)
			{
				_color = text;
				Refresh();
			}
		};
		stackPanel.Children.Add(button);
		_summary.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		stackPanel.Children.Add(_summary);
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		Button button2 = new Button
		{
			Content = "キャンセル"
		};
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button3 = new Button
		{
			Content = "適用",
			MinWidth = 90.0,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		button3.Click += delegate
		{
			base.DialogResult = true;
		};
		stackPanel2.Children.Add(button2);
		stackPanel2.Children.Add(button3);
		stackPanel.Children.Add(stackPanel2);
		_depth.ValueChanged += delegate
		{
			Refresh();
		};
		_angle.ValueChanged += delegate
		{
			Refresh();
		};
		_smoothness.ValueChanged += delegate
		{
			Refresh();
		};
		Refresh();
		base.Content = stackPanel;
	}

	private void Refresh()
	{
		_summary.Text = $"奥行き {_depth.Value:0}pt ／ 角度 {_angle.Value:0}° ／ 滑らかさ {_smoothness.Value:0.##} ／ {_color}";
	}
}
