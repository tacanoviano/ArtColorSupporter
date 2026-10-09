# ArtColorSupporter

リアルの絵を描くための補助アプリ（Unity / 横持ち専用 / Android・iOS）。

## 開き方

1. このフォルダ `ArtColorSupporter` をパソコンにコピーする。
2. Unity Hub で「Add（追加）→ Add project from disk」からこのフォルダを選ぶ。
   - 推奨バージョン: **Unity 6（6000.0 LTS 以降）**。Android/iOS の Build Support モジュールも入れておく。
3. 初回に開くと自動でセットアップが走り、次の設定がされる（Console に `Project setup completed.` と出る）。
   - アプリ名: ArtColorSupporter
   - 画面の向き: 横持ち専用（左右の横向きのみ回転）
   - 最初のシーン `Assets/Scenes/Main.unity` を作成し、Build Settings に登録
   - iOS のカメラ使用目的の説明文
   - うまくいかなかったときはメニュー **ArtColorSupporter > Setup Project** で再実行できる。
4. `Assets/Scenes/Main.unity` を開いて Play。

## NativeGallery（画像選択プラグイン）の導入

「画像」ボタンで端末の写真を選ぶ機能と、撮影画像をアルバムに保存する機能は
[NativeGallery](https://github.com/yasirkula/UnityNativeGallery)（無料・MIT）を使う。

1. Unity メニュー **Window > Package Manager** を開く。
2. 左上の **＋ → Add package from git URL...** を選ぶ。
3. `https://github.com/yasirkula/UnityNativeGallery.git` を入力して Add。
   - パソコンに Git がインストールされている必要がある。

導入すると自動的に `NATIVE_GALLERY` が有効になり、実機で画像選択・アルバム保存が使えるようになる。
未導入でもプロジェクトはエラーなく動き、エディタ上ではファイル選択ダイアログで画像を選べる。

> Asset Store や .unitypackage で入れた場合は、Player Settings > Other Settings >
> Scripting Define Symbols に `NATIVE_GALLERY` を手動で追加する。

## 画面と機能

| 場所 | 内容 |
|---|---|
| 左半分 | 撮影した画像／選んだ画像。下に色数ボタン（フルカラー／256色／16色／8色／モノクロ） |
| 右上のボタン | 「カメラ」（もう一度押すと停止）「画像」「English」（押すと英語表示、もう一度押すと日本語。選択は保存される） |
| 右の上半分 | カラーホイール（角度＝色相、中心からの距離＝彩度）と明るさバー、色見本、アドバイス |
| 右の下半分 | カメラの映像と中央の白い円、「撮影」ボタン |

- **左の画像**: 2本指ピンチで拡大（最大12倍）、ドラッグで移動。エディタではマウスホイールで拡大。
  - 1点をタップすると、その場所の色（周り5×5ピクセルの平均）がターゲットになり、
    画像とカラーホイールに ✕ 印が出る。印の色は下の色を反転させた色。
- **色数ボタン**: 256/16/8 色はメディアンカット法で画像に合った色を選んで減色する。モノクロはグレースケール。
  あわせて画素も粗くする（長辺 256色=320px、16色=160px、8色=96px）。粗い画素はぼかさず四角いまま拡大表示する。
  モノクロは画素数を変えない。
  ターゲットは同じ場所のまま、減色後の色で取り直す。
- **カメラ**: 右下に背面カメラの映像を表示。中央の白い円の中の平均色をカラーホイールに ○ 印で表示する。
  - 「撮影」で今の映像を左に表示して保存する。保存先: アプリ内 `persistentDataPath/Captures/` と、
    NativeGallery 導入時は端末のアルバム「ArtColorSupporter」。
- **アドバイス**: ターゲットとカメラの色を CMYK に変換して比べ、いちばん足りない色を
  青（シアン）・赤（マゼンタ）・黄・黒で表示する。差が4%未満なら「ほぼ同じ」、
  足りない色がなく濃すぎるときは「白で明るく」と表示する。下の小さな数字は各インクの不足量。
- **画像**: 端末の写真から選んで左に表示（長辺 2048px まで縮小して読み込む）。

## Input System の導入（入力の新しい仕組み）

古い Input Manager は将来なくなる予定なので、新しい Input System パッケージを使う。

1. **Window > Package Manager** を開き、左の **Unity Registry** から **Input System** を選んで Install。
2. 「新しい入力の仕組みを有効にするか（enable the new backends）」と聞かれたら **Yes**。Unity が再起動する。
3. 再起動後、Player Settings > Other Settings > **Active Input Handling** が
   「Input System Package (New)」になっていることを確認する。

入れると自動的に `INPUT_SYSTEM_PACKAGE` が有効になり、タッチ操作や戻るボタンが新しい仕組みで動く。
入れていなくても古い Input Manager でそのまま動く。

## 日本語/英語対応のルール（今後の追加分も含む）

UI の文字はすべて `Assets/Scripts/Localization.cs` の `Table` に `{ キー, { 日本語, English } }` で登録し、
`LocalizedText`（`UIFactory.CreateText` / `CreateButton` が自動で付ける）か `Localization.Get("キー")` で表示する。
言語を切り替えると `LocalizedText` は自動で表示を更新する。直接文字列を書かないこと。

## スクリプト構成

| ファイル | 役割 |
|---|---|
| `Scripts/AppController.cs` | 画面の UI 組み立てとボタン処理、ターゲット色・カメラ色の比較 |
| `Scripts/ImageViewer.cs` | 左の画像の拡大・移動・タップ |
| `Scripts/ColorReducer.cs` | 減色（メディアンカット／グレースケール） |
| `Scripts/ColorWheel.cs` | カラーホイールと明るさバー、✕／○ 印 |
| `Scripts/ColorAdvice.cs` | CMYK 変換と「いちばん足りない色」の判定 |
| `Scripts/CameraController.cs` | WebCamTexture によるカメラ起動・権限確認・撮影（向き補正付き）、中央の円の平均色 |
| `Scripts/GalleryPicker.cs` | 画像選択とアルバム保存（NativeGallery／エディタではファイルダイアログ） |
| `Scripts/Localization.cs` / `LocalizedText.cs` | 日本語/英語の切り替え |
| `Scripts/UIFactory.cs` / `SafeArea.cs` | UI 部品の生成、ノッチ回避 |
| `Editor/ProjectSetup.cs` | アプリ名・横持ち・シーンの自動セットアップ |

## うまく動かないとき

- 「This project uses Input Manager, which is marked for deprecation」と出る: 下の「Input System の導入」をする。
- ボタンが反応しない: Player Settings > Other Settings > **Active Input Handling** を確認する。
  Input System を入れたなら「Input System Package (New)」、入れていないなら「Input Manager (Old)」にする。
- 日本語が表示されない（エディタ）: OS に日本語フォントがあれば表示される。実機では端末のフォントが使われる。
