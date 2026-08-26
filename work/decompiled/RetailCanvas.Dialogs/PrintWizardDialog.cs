using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class PrintWizardDialog : Window
{
	private readonly ComboBox _output = Combo("プリンター", "PDFで保管");

	private readonly ComboBox _paper = Combo("普通紙", "光沢紙", "マットフォトペーパー", "写真用紙", "厚紙・カード", "メーカー指定用紙");

	private readonly ComboBox _quality = Combo("標準（300dpi）", "高品質（600dpi）", "速い（200dpi）");

	private readonly ComboBox _scale = Combo("実寸で印刷", "印刷可能範囲に合わせる");

	private readonly CheckBox _borderless = new CheckBox
	{
		Content = "フチなし印刷をプリンタードライバーへ要求"
	};

	private readonly ComboBox _color = Combo("カラー", "販促物向けモノクロ", "写真向けグレースケール", "高コントラスト", "純黒（文字・枠線・QR）", "白黒2値化", "黒インク優先", "印刷会社向けK100%");

	private readonly Slider _density = Slider(50.0, 180.0, 115.0, 1.0);

	private readonly Slider _contrast = Slider(50.0, 220.0, 125.0, 1.0);

	private readonly Slider _gamma = Slider(50.0, 200.0, 100.0, 1.0);

	private readonly Slider _threshold = Slider(5.0, 95.0, 68.0, 1.0);

	private readonly TextBlock _densityValue = ValueText();

	private readonly TextBlock _contrastValue = ValueText();

	private readonly TextBlock _gammaValue = ValueText();

	private readonly TextBlock _thresholdValue = ValueText();

	private readonly CheckBox _dithering = new CheckBox
	{
		Content = "ディザリングで写真の階調感を残す"
	};

	private readonly ComboBox _photoTreatment = Combo("写真部分をグレーで残す", "写真を含めてすべて白黒にする");

	private readonly CheckBox _blackInk = new CheckBox
	{
		Content = "プリンターが対応する場合は黒インクのみを自動指定",
		IsChecked = true
	};

	private readonly ComboBox _duplex = Combo("片面", "両面・長辺綴じ", "両面・短辺綴じ");

	private readonly StackPanel _deviceOptions = new StackPanel();

	private readonly StackPanel _monoOptions = new StackPanel();

	private readonly TextBlock _flowHelp = new TextBlock
	{
		Foreground = Brushes.SlateGray,
		TextWrapping = TextWrapping.Wrap
	};

	private readonly TextBlock _modeHelp = new TextBlock
	{
		Foreground = new SolidColorBrush(Color.FromRgb(52, 74, 83)),
		TextWrapping = TextWrapping.Wrap
	};

	private bool _applyingPreset;

	public PrintWizardResult? Result { get; private set; }

	public PrintWizardDialog(string defaultMode)
	{
		base.Title = "印刷・PDF出力 － MISE";
		base.Width = 720.0;
		base.Height = 800.0;
		base.MinWidth = 570.0;
		base.MinHeight = 520.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 800.0, 570.0, 520.0);
		base.Content = Build();
		_output.SelectionChanged += delegate
		{
			UpdateFlow();
		};
		_color.SelectionChanged += delegate
		{
			ApplyModePreset();
		};
		Slider[] array = new Slider[4] { _density, _contrast, _gamma, _threshold };
		for (int num = 0; num < array.Length; num++)
		{
			array[num].ValueChanged += delegate
			{
				UpdateValueLabels();
			};
		}
		if (defaultMode == "PDF閲覧用")
		{
			_output.SelectedItem = "PDFで保管";
		}
		ApplyModePreset();
		UpdateFlow();
	}

	private UIElement Build()
	{
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(20.0)
		};
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
		};
		Button button = new Button
		{
			Content = "キャンセル",
			MinWidth = 90.0
		};
		button.Click += delegate
		{
			base.DialogResult = false;
		};
		Button button2 = new Button
		{
			Content = "次へ",
			MinWidth = 110.0,
			Style = (TryFindResource("PrimaryButton") as Style)
		};
		button2.Click += Accept;
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj.Children.Add(stackPanel);
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 12.0)
		};
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "印刷方法を選択",
			FontSize = 23.0,
			FontWeight = FontWeights.Bold
		});
		stackPanel2.Children.Add(new TextBlock
		{
			Text = "出力先と用途に合わせて、必要な項目だけを調整します。",
			Foreground = Brushes.SlateGray
		});
		DockPanel.SetDock(stackPanel2, Dock.Top);
		obj.Children.Add(stackPanel2);
		ScrollViewer scrollViewer = new ScrollViewer
		{
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		StackPanel stackPanel3 = (StackPanel)(scrollViewer.Content = new StackPanel());
		obj.Children.Add(scrollViewer);
		stackPanel3.Children.Add(Field("1. 出力先", _output));
		_flowHelp.Margin = new Thickness(0.0, 0.0, 0.0, 10.0);
		stackPanel3.Children.Add(_flowHelp);
		_deviceOptions.Children.Add(Field("2. 用紙の種類", _paper));
		_deviceOptions.Children.Add(Field("3. 印刷品質", _quality));
		_deviceOptions.Children.Add(Field("4. サイズ", _scale));
		_borderless.Margin = new Thickness(2.0, 0.0, 0.0, 10.0);
		_deviceOptions.Children.Add(_borderless);
		stackPanel3.Children.Add(_deviceOptions);
		stackPanel3.Children.Add(Field("5. カラー／モノクロ用途", _color));
		_monoOptions.Children.Add(Adjustment("黒の濃さ", _density, _densityValue));
		_monoOptions.Children.Add(Adjustment("コントラスト", _contrast, _contrastValue));
		_monoOptions.Children.Add(Adjustment("ガンマ", _gamma, _gammaValue));
		_monoOptions.Children.Add(Adjustment("2値化のしきい値", _threshold, _thresholdValue));
		_dithering.Margin = new Thickness(2.0, 2.0, 0.0, 7.0);
		_monoOptions.Children.Add(_dithering);
		_monoOptions.Children.Add(Field("写真部分の扱い", _photoTreatment));
		_blackInk.Margin = new Thickness(2.0, 0.0, 0.0, 9.0);
		_monoOptions.Children.Add(_blackInk);
		_monoOptions.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(242, 247, 248)),
			CornerRadius = new CornerRadius(7.0),
			Padding = new Thickness(11.0),
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Child = _modeHelp
		});
		stackPanel3.Children.Add(_monoOptions);
		stackPanel3.Children.Add(Field("6. 片面／両面", _duplex));
		return obj;
	}

	private static ComboBox Combo(params string[] values)
	{
		return new ComboBox
		{
			ItemsSource = values,
			SelectedIndex = 0
		};
	}

	private static Slider Slider(double minimum, double maximum, double value, double tick)
	{
		return new Slider
		{
			Minimum = minimum,
			Maximum = maximum,
			Value = value,
			TickFrequency = tick,
			Width = 260.0,
			IsSnapToTickEnabled = true
		};
	}

	private static TextBlock ValueText()
	{
		return new TextBlock
		{
			Width = 62.0,
			VerticalAlignment = VerticalAlignment.Center
		};
	}

	private static UIElement Field(string label, Control control)
	{
		return new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0),
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = label,
					FontWeight = FontWeights.SemiBold,
					Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
				},
				(UIElement)control
			}
		};
	}

	private static UIElement Adjustment(string label, Slider slider, TextBlock value)
	{
		return new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(18.0, 0.0, 0.0, 7.0),
			Children = 
			{
				(UIElement)new TextBlock
				{
					Text = label,
					Width = 155.0,
					VerticalAlignment = VerticalAlignment.Center
				},
				(UIElement)slider,
				(UIElement)value
			}
		};
	}

	private void UpdateFlow()
	{
		bool flag = _output.SelectedItem?.ToString() == "プリンター";
		_deviceOptions.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
		_duplex.IsEnabled = flag;
		_blackInk.IsEnabled = flag && _color.SelectedIndex > 0;
		_flowHelp.Text = (flag ? "この後にWindowsのプリンター選択画面が開きます。用紙種類は実際にセットする用紙と合わせてください。" : "印刷用PDFを保存します。モノクロ設定はPDFの描画結果にも適用します。");
	}

	private void ApplyModePreset()
	{
		if (!_applyingPreset)
		{
			_applyingPreset = true;
			string text = _color.SelectedItem?.ToString() ?? "カラー";
			_monoOptions.Visibility = ((text == "カラー") ? Visibility.Collapsed : Visibility.Visible);
			_dithering.IsChecked = false;
			_photoTreatment.SelectedIndex = 0;
			_density.Value = 115.0;
			_contrast.Value = 125.0;
			_gamma.Value = 100.0;
			_threshold.Value = 68.0;
			switch (text)
			{
			case "販促物向けモノクロ":
				_density.Value = 130.0;
				_contrast.Value = 145.0;
				_gamma.Value = 92.0;
				_modeHelp.Text = "文字・枠線・QR・ロゴを純黒へ寄せ、写真だけはグレー階調を残す推奨プリセットです。";
				break;
			case "写真向けグレースケール":
				_density.Value = 105.0;
				_contrast.Value = 105.0;
				_gamma.Value = 100.0;
				_dithering.IsChecked = true;
				_modeHelp.Text = "写真の明暗と中間階調を優先します。文字の完全な純黒化より自然な写真表現向けです。";
				break;
			case "高コントラスト":
				_density.Value = 135.0;
				_contrast.Value = 175.0;
				_gamma.Value = 90.0;
				_modeHelp.Text = "文字・値札・図形を濃くしつつ、主要な階調は残します。";
				break;
			case "純黒（文字・枠線・QR）":
				_density.Value = 150.0;
				_contrast.Value = 180.0;
				_threshold.Value = 72.0;
				_modeHelp.Text = "文字・枠線・QR・ロゴを#000000へ変換し、写真は選択した扱いに従います。";
				break;
			case "白黒2値化":
				_photoTreatment.SelectedIndex = 1;
				_contrast.Value = 200.0;
				_threshold.Value = 62.0;
				_modeHelp.Text = "全体を純黒と白の2色へ変換します。ディザリングを有効にすると写真を点の密度で表現します。";
				break;
			case "黒インク優先":
				_density.Value = 140.0;
				_contrast.Value = 150.0;
				_modeHelp.Text = "モノクロ画像を作成し、対応プリンターには黒インクのみの使用を要求します。未対応時はドライバー設定を案内します。";
				break;
			case "印刷会社向けK100%":
				_density.Value = 160.0;
				_contrast.Value = 200.0;
				_threshold.Value = 75.0;
				_modeHelp.Text = "黒要素を純黒へ固定する入稿向けモードです。印刷会社の指定プロファイルがある場合は、最終入稿条件も確認してください。";
				break;
			default:
				_modeHelp.Text = string.Empty;
				break;
			}
			Slider threshold = _threshold;
			bool isEnabled;
			switch (text)
			{
			case "純黒（文字・枠線・QR）":
			case "白黒2値化":
			case "印刷会社向けK100%":
				isEnabled = true;
				break;
			default:
				isEnabled = false;
				break;
			}
			threshold.IsEnabled = isEnabled;
			CheckBox dithering = _dithering;
			isEnabled = ((text == "写真向けグレースケール" || text == "白黒2値化") ? true : false);
			dithering.IsEnabled = isEnabled;
			ComboBox photoTreatment = _photoTreatment;
			switch (text)
			{
			case "販促物向けモノクロ":
			case "純黒（文字・枠線・QR）":
			case "高コントラスト":
				isEnabled = true;
				break;
			default:
				isEnabled = false;
				break;
			}
			photoTreatment.IsEnabled = isEnabled;
			_applyingPreset = false;
			UpdateValueLabels();
			UpdateFlow();
		}
	}

	private void UpdateValueLabels()
	{
		_densityValue.Text = $"{_density.Value:0}%";
		_contrastValue.Text = $"{_contrast.Value:0}%";
		_gammaValue.Text = $"{_gamma.Value / 100.0:0.00}";
		_thresholdValue.Text = $"{_threshold.Value:0}%";
	}

	private void Accept(object? sender, RoutedEventArgs e)
	{
		string text = _color.SelectedItem?.ToString() ?? "カラー";
		Result = new PrintWizardResult(_output.SelectedItem?.ToString() ?? "プリンター", _paper.SelectedItem?.ToString() ?? "普通紙", _quality.SelectedItem?.ToString() ?? "標準（300dpi）", _scale.SelectedItem?.ToString() ?? "実寸で印刷", _borderless.IsChecked == true, text, _density.Value, _contrast.Value, _gamma.Value / 100.0, _threshold.Value, _dithering.IsChecked == true, _photoTreatment.SelectedIndex == 0, _blackInk.IsChecked == true, text == "印刷会社向けK100%", _duplex.SelectedItem?.ToString() ?? "片面");
		base.DialogResult = true;
	}
}
