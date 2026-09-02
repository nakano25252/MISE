using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RetailCanvas.Services;

namespace RetailCanvas.Dialogs;

public sealed class HelpDialog : Window
{
	public HelpDialog()
	{
		base.Title = "操作ガイド";
		base.Width = 720.0;
		base.Height = 650.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		WindowSizing.Attach(this, 720.0, 650.0, 380.0, 300.0);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(20.0)
		};
		Button button = new Button
		{
			Content = "閉じる",
			MinWidth = 90.0,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 12.0, 0.0, 0.0)
		};
		button.Click += delegate
		{
			Close();
		};
		DockPanel.SetDock(button, Dock.Bottom);
		dockPanel.Children.Add(button);
		TextBlock textBlock = new TextBlock
		{
			TextWrapping = TextWrapping.Wrap,
			FontSize = 13.0,
			LineHeight = 22.0
		};
		textBlock.Inlines.Add(new Run("MISE 1.1.19 操作ガイド\n")
		{
			FontSize = 24.0,
			FontWeight = FontWeights.Bold,
			Foreground = (FindResource("NavyBrush") as Brush)
		});
		textBlock.Inlines.Add("\n1. ホームから用紙サイズを選びます。\n2. 上部ツールバーから文字・画像・図形・QRコードを追加します。\n3. 要素をドラッグして移動し、周囲のハンドルで拡大縮小します。上部のハンドルで回転できます。\n4. 右パネルで位置・サイズ・色・文字内容を数値調整します。\n5. 「チェック」で文字サイズ、画像DPI、安全領域、QRコードを確認します。\n6. 「書き出し」からPDF・PNG・JPEGを作成します。\n\n便利な操作\n・Ctrl＋ホイール：ズーム\n・Space＋ドラッグ、または中ボタンドラッグ：キャンバス移動\n・Shift／Ctrl＋クリック：複数選択\n・ドラッグで囲む：複数選択\n・Shift＋矢印：10mm移動\n・Shift／Altを押しながらドラッグ：一時的に吸着を無効化\n・通常回転：45度単位／Shift＋回転：自由回転\n・Esc：作成中の操作を中止、または選択解除\n・同じ場所を連続クリック：重なった要素を順番に選択\n\n印刷の注意\nPDFを印刷するときは「実際のサイズ」「100%」を選択してください。「用紙に合わせる」では縮小される場合があります。\n\nデータ保存\nプロジェクト画像は.rcanvas内へ埋め込まれます。30MB以上の大容量画像やPDFは軽量プレビューと元ファイル参照を併用します。自動保存は初期設定で3分ごとです。");
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = textBlock,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		base.Content = dockPanel;
	}
}
