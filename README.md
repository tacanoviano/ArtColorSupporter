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
| 左半分 | 撮影した画像／選んだ画像を表示 |
| 右上 | 「English」ボタン（押すと英語表示、もう一度押すと「日本語」に戻る。選択は保存される） |
| 右側 | 「カメラ」「画像」ボタンと説明メッセージ |

- **カメラ**: 背面カメラの映像を全画面で表示。「撮影」で保存して左半分に表示。「閉じる」で戻る。
  - 保存先: アプリ内 `persistentDataPath/Captures/` と、NativeGallery 導入時は端末のアルバム「ArtColorSupporter」。
- **画像**: 端末の写真から選んで左半分に表示。

## 日本語/英語対応のルール（今後の追加分も含む）

UI の文字はすべて `Assets/Scripts/Localization.cs` の `Table` に `{ キー, { 日本語, English } }` で登録し、
`LocalizedText`（`UIFactory.CreateText` / `CreateButton` が自動で付ける）か `Localization.Get("キー")` で表示する。
言語を切り替えると `LocalizedText` は自動で表示を更新する。直接文字列を書かないこと。

## スクリプト構成

| ファイル | 役割 |
|---|---|
| `Scripts/AppController.cs` | 最初の画面の UI 組み立てとボタン処理 |
| `Scripts/CameraController.cs` | WebCamTexture によるカメラ起動・権限確認・撮影（向き補正付き） |
| `Scripts/GalleryPicker.cs` | 画像選択とアルバム保存（NativeGallery／エディタではファイルダイアログ） |
| `Scripts/Localization.cs` / `LocalizedText.cs` | 日本語/英語の切り替え |
| `Scripts/UIFactory.cs` / `SafeArea.cs` | UI 部品の生成、ノッチ回避 |
| `Editor/ProjectSetup.cs` | アプリ名・横持ち・シーンの自動セットアップ |

## うまく動かないとき

- ボタンが反応しない: Player Settings > Other Settings > **Active Input Handling** を「Input Manager (Old)」か「Both」にする。
- 日本語が表示されない（エディタ）: OS に日本語フォントがあれば表示される。実機では端末のフォントが使われる。
