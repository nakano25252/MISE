using System.Collections.Generic;
using System.Linq;

namespace RetailCanvas.Services;

public static class SalesPointCatalog
{
	private static readonly IReadOnlyList<SalesPointCard> Cards = new SalesPointCard[36]
	{
		new SalesPointCard("TWS", "ライト", "ノイズキャンセリング", "周囲の音を抑えて、音楽に集中", "ANC方式・外音取り込み・自動補正の有無を確認"),
		new SalesPointCard("TWS", "ライト", "装着感", "軽く、長時間でも快適", "本体重量・イヤーチップ・オーバルチューブを確認"),
		new SalesPointCard("TWS", "ライト", "バッテリー", "充電を気にせず長く楽しめる", "本体／ケース／ANC時／急速充電を分けて記載"),
		new SalesPointCard("TWS", "標準", "通話品質", "騒がしい場所でも声をクリアに", "マイク数・ビームフォーミング・風切り音対策"),
		new SalesPointCard("TWS", "標準", "防水・防塵", "汗や雨を気にせず使える", "IP等級は本体とケースを分けて確認"),
		new SalesPointCard("TWS", "標準", "マルチポイント", "PCとスマホをスムーズに切り替え", "同時接続台数・対応条件を確認"),
		new SalesPointCard("TWS", "詳しい", "コーデック", "対応機器で音質と遅延を最適化", "SBC／AAC／LC3／LDAC等。対応プロファイルも確認"),
		new SalesPointCard("TWS", "詳しい", "ドライバー・再生帯域", "音の輪郭とレンジを数値で比較", "口径mm・方式・周波数特性・感度・インピーダンス"),
		new SalesPointCard("TWS", "詳しい", "Bluetooth仕様", "新しい接続規格と省電力性", "Bluetoothバージョン・LE Audio・Auracast対応"),
		new SalesPointCard("ヘッドホン", "ライト", "没入感", "包み込まれるようなサウンド", "密閉／開放・オーバーイヤー／オンイヤー"),
		new SalesPointCard("ヘッドホン", "ライト", "快適な装着", "やわらかな装着感で長時間快適", "重量・側圧・イヤーパッド素材・折りたたみ"),
		new SalesPointCard("ヘッドホン", "ライト", "長時間バッテリー", "移動中も仕事中も充電を気にしない", "ANCオン／オフ・急速充電時間を分記"),
		new SalesPointCard("ヘッドホン", "標準", "ハイブリッドANC", "環境に合わせて騒音を細かく制御", "リアルタイム補正・外音取り込み・会話モード"),
		new SalesPointCard("ヘッドホン", "標準", "空間サウンド", "映画やライブを立体的に楽しめる", "ヘッドトラッキング・対応アプリ・音源条件"),
		new SalesPointCard("ヘッドホン", "標準", "有線・無線両対応", "充電がなくてもケーブルで使える", "USB Audio・3.5mm・パッシブ再生を確認"),
		new SalesPointCard("ヘッドホン", "詳しい", "高音質コーデック", "対応端末で情報量の多いワイヤレス再生", "SBC／AAC／LDAC／LC3、最大ビット深度・サンプリング周波数"),
		new SalesPointCard("ヘッドホン", "詳しい", "音響スペック", "ドライバー性能を数値で比較", "口径・周波数特性・感度・インピーダンス・最大入力"),
		new SalesPointCard("ヘッドホン", "詳しい", "USBオーディオ", "デジタル接続時の精細な再生", "最大bit/kHz、対応OS、同時充電可否"),
		new SalesPointCard("スピーカー", "ライト", "迫力のサウンド", "コンパクトでも力強い音", "サイズに対する出力・低音ユニット構成"),
		new SalesPointCard("スピーカー", "ライト", "持ち運び", "好きな場所へ気軽に持ち出せる", "重量・ハンドル・ストラップ・充電方式"),
		new SalesPointCard("スピーカー", "ライト", "防水・防塵", "アウトドアや水辺でも安心", "IP等級・水没条件・端子カバーを確認"),
		new SalesPointCard("スピーカー", "標準", "再生時間", "一日中使えるロングバッテリー", "音量条件・充電時間・モバイルバッテリー機能"),
		new SalesPointCard("スピーカー", "標準", "複数台接続", "スピーカーをつないで音を広げる", "Auracast／PartyBoost等の世代互換を確認"),
		new SalesPointCard("スピーカー", "標準", "低音強化", "見た目以上の深いベース", "ウーファー・パッシブラジエーター・EQ"),
		new SalesPointCard("スピーカー", "詳しい", "アンプ出力", "音量とユニット構成を数値で確認", "AC／バッテリー時W数、ウーファー／ツイーター別出力"),
		new SalesPointCard("スピーカー", "詳しい", "接続仕様", "用途に合う入力と無線規格", "Bluetoothバージョン・コーデック・AUX・USB・Wi-Fi"),
		new SalesPointCard("スピーカー", "詳しい", "周波数特性・S/N", "再生レンジとノイズ性能を比較", "Hz～kHz、S/N比、最大音圧、ビット深度"),
		new SalesPointCard("サウンドバー", "ライト", "映画館のような音", "テレビの音を迫力あるサウンドへ", "チャンネル数・サブウーファー・リアスピーカー"),
		new SalesPointCard("サウンドバー", "ライト", "声が聞き取りやすい", "ニュースや会話をクリアに", "センターチャンネル・ボイス強調モード"),
		new SalesPointCard("サウンドバー", "ライト", "かんたん接続", "テレビとケーブル1本で接続", "HDMI eARC/ARC・CEC・同梱ケーブル"),
		new SalesPointCard("サウンドバー", "標準", "立体音響", "頭上まで広がる3Dサウンド", "Dolby Atmos／DTS:X・実スピーカー／バーチャル方式"),
		new SalesPointCard("サウンドバー", "標準", "自動音場補正", "部屋に合わせて聴こえ方を最適化", "測定方式・マイク・補正範囲"),
		new SalesPointCard("サウンドバー", "標準", "音楽ストリーミング", "スマホの音楽も高音質で楽しめる", "AirPlay／Chromecast／Spotify Connect／Bluetooth"),
		new SalesPointCard("サウンドバー", "詳しい", "HDMI映像仕様", "ゲーム・映像機器の信号を高品質に通過", "4K/8K・HDR10+・Dolby Vision・VRR・ALLM・帯域Gbps"),
		new SalesPointCard("サウンドバー", "詳しい", "チャンネル・出力", "スピーカー構成と出力を数値で比較", "x.x.x ch、総合W、ユニット径、ワイヤレスch"),
		new SalesPointCard("サウンドバー", "詳しい", "音声フォーマット", "再生できる立体音響・ロスレス音声", "Dolby TrueHD／Atmos、DTS-HD MA／DTS:X、PCM ch/bit/kHz")
	};

	public static IReadOnlyList<SalesPointCard> For(string category, string level)
	{
		string[] array = ((level == "ライト") ? new string[1] { "ライト" } : ((!(level == "標準")) ? new string[3] { "ライト", "標準", "詳しい" } : new string[2] { "ライト", "標準" }));
		string[] levels = array;
		return Cards.Where((SalesPointCard x) => x.Category == category && levels.Contains(x.Level)).ToList();
	}
}
