# COM3D25.PostEffects.Plugin

COM3D2 / COM3D2.5 のメインカメラに多彩なポストエフェクトを適用し、GUI から制御するプラグイン。
**COM3D2 (2.0) と COM3D2.5 (Unity 2022) の両方に対応**（バージョンごとに dll が分かれています）。

https://github.com/user-attachments/assets/becec22f-b537-4a2d-b0e2-8fc74a3debcb

ゲーム内蔵のイメージエフェクトに加え、同梱のシェーダーバンドルによる
撮影向けエフェクトを合計 40 種類以上収録している。

## インストール方法

[Releases](https://github.com/kidonaru/COM3D25.PostEffects.Plugin/releases)
から最新の `COM3D25.PostEffects.Plugin-vX.X.X.X.zip` をダウンロードします。

zip を解凍すると次の構成になっています。

```
COM3D25.PostEffects.Plugin\
├── README.txt
├── UnityInjector\                    ← COM3D2.5 用
│   ├── COM3D25.PostEffects.Plugin.dll
│   └── Config\
│       └── PostEffects\
└── UnityInjector (COM3D2)\           ← COM3D2 (2.0) 用
    ├── COM3D2.PostEffects.Plugin.dll
    └── Config\
        └── PostEffects\
```

**お使いのバージョンに対応したフォルダ**の中身を、ゲームフォルダの `Sybaris\UnityInjector\` へ
そのままコピーしてください。配置後は以下のようになります（COM3D2.5 の例）。

```
（ゲームフォルダ）\Sybaris\UnityInjector\
├── COM3D25.PostEffects.Plugin.dll
└── Config\
    └── PostEffects\
```

各ファイルの説明:
- `COM3D25.PostEffects.Plugin.dll` / `COM3D2.PostEffects.Plugin.dll`
  - プラグインの本体。バージョンに合った方だけを入れてください。
- `Config\PostEffects\`
  - シェーダーバンドル・テクスチャ・サンプルプリセット等。**必須**。
  - 入れ忘れると、ゲーム内蔵エフェクト以外が動作しません。
  - シェーダーバンドルはバージョンごとに中身が異なるため、両方のフォルダを混ぜないでください。

COM3D2.5 Ver.3.49.0 で動作確認済みです。

## 使い方

- ギアメニューのアイコン、または `Alt+P` でウィンドウを開閉
- ウィンドウ上部の「エフェクト」「プリセット」でモードを切り替える
- 「エフェクト」モードではカテゴリとドロップダウンで対象を選び、「有効」トグルで個別に ON/OFF
  （有効中のエフェクトは名前の前に `*` が付く）
- 設定は `Sybaris/UnityInjector/Config/PostEffects.xml` に自動保存され、次回起動時も維持される
- ウィンドウを閉じてもエフェクトは適用されたまま。無効化はトグルを OFF にする
- エフェクトを OFF にすると、有効化前のコンポーネントの状態に復元される
- 「リセット」はエフェクトの設定値をプラグイン既定値へ戻す
- ウィンドウのサイズは 4 辺・4 隅をドラッグして変更できる
- EditorWindow プラグインを導入している場合、ヘッダーを他のウィンドウへ重ねるとタブとして統合できる

### ショートカットキーの変更

ウィンドウ開閉のショートカットキーは `Sybaris/UnityInjector/Config/PostEffects.xml` の
`keyBind` の `value` で変更できる（ゲームを終了した状態で編集すること）。

```xml
  <keyBind>
    <key>PluginToggle</key>
    <value>Alt+P</value>
  </keyBind>
```

`Ctrl` / `Shift` / `Alt` を `+` でつなげて指定する（例: `F5` / `Ctrl+Shift+P`）。
キー名は Unity の `KeyCode` に準拠し、数字キーは `Alpha1`、テンキーは `Keypad1` のように書く。

### プリセット

全エフェクトの設定をまとめて名前付きで保存・呼び出しできる。

- 「プリセット」モードで名前を入力して「保存」
- 一覧の名前ボタンをクリックすると設定が復元され、名前欄にも反映される（そのまま「保存」で上書き）
- 「既定」で起動時に読み込むプリセットを指定、「削除」でプリセットを削除
- 「更新」で一覧を再読み込み（手動でファイルを追加・削除したとき用）
- 保存先は `Sybaris/UnityInjector/Config/PostEffects/Presets/<名前>.xml`
- 保存対象はエフェクト設定のみ。ウィンドウ位置やキーバインド等の動作設定は含まれない

EditorWindow プラグインを導入している場合、EditorWindow のシーンプリセットにも
ポストエフェクト設定を一緒に保存できる（保存ポップアップの「ポストエフェクト」を
チェックする）。ペイロードは `<プリセット名>.COM3D25.PostEffects.xml` として本体 XML の隣に
書き出され、形式は上記のプリセットファイルと同じなので相互に流用できる。

### カスタムテクスチャ

`Sybaris/UnityInjector/Config/PostEffects/Images/` 以下に画像を置くと、
対応するエフェクトから選択できる。

| フォルダ | 用途 |
|---|---|
| `LUTs/` | LUT 色補正のルックアップテクスチャ |
| `Ramp/` | グラデーションのランプテクスチャ |
| `Overlay/` | オーバーレイの画像 |
| `Bokeh/` | ボケ形状テクスチャ |
| `LensDirt/` | レンズ汚れテクスチャ |

## 収録エフェクト

| カテゴリ | エフェクト |
|---|---|
| 色調 | セピア / グレースケール / コントラスト / 色補正 / グラデーション / LUT 色補正 / トーンマッピング / ホワイトバランス / パラフィン / GTトーンマップ |
| ブルーム・光 | ブルーム / シネマティックブルーム / フィルミックブルーム / 光条 / 光芒 / 光の筋 / ディフュージョン |
| ボケ・被写界深度 | 被写界深度 / ボケ (物理) / フィルミックボケ / シネマティック被写界深度 / ブラー / チルトシフト / ラジアルブラー / メディアンフィルタ |
| 輪郭・線画 | 輪郭検出 / 折り目 / 等高線 / ハーフトーン / 油絵風 |
| ノイズ・グリッチ | ノイズ/グレイン / アナログノイズ / デジタルノイズ |
| レンズ・歪み | 魚眼レンズ / レンズ収差 / ビネット |
| フォグ・遮蔽 | フォグ / スタイリッシュフォグ / 環境遮蔽 / 距離フォグ |
| その他 | モーションブラー / アンチエイリアス / レターボックス / オーバーレイ / シャープネス / メイド非表示 / リムライト |

## 規約

### MOD規約

※MODはKISSサポート対象外です。
※MODを利用するに当たり、問題が発生してもKISSは一切の責任を負いかねます。
※「カスタムメイド3D2」か「カスタムオーダーメイド3D2」か「CR EditSystem」を購入されている方のみが利用できます。
※「カスタムメイド3D2」か「カスタムオーダーメイド3D2」か「CR EditSystem」上で表示する目的以外の利用は禁止します。
※これらの事項は http://kisskiss.tv/kiss/diary.php?no=558 を優先します。


他の機能追加などをしたい場合は、リポジトリを公開しているのでこちらにPRをお願いします。
https://github.com/kidonaru/COM3D25.PostEffects.Plugin

質問、要望などは@kidonaruまで (可能な範囲で対応します)
https://twitter.com/kidonaru

### ライセンス

このプラグイン本体は MIT ライセンスです。詳細は [LICENSE](LICENSE) を参照。

同梱シェーダーには Keijiro Takahashi 氏の Kino シリーズ等、
MIT ライセンスのオープンソースを含む。詳細は `UnityInjector\Config\PostEffects\License` を参照。

## 開発者向け

ビルド手順は [docs/build.md](docs/build.md) を参照。

## 変更履歴

### 2026/08/16 v2.0.0.0
- ウィンドウをドッキング対応の基盤へ移行。SceneEditor プラグイン導入時は、
  ヘッダーを他のウィンドウへ重ねるとタブとして統合できるようになった
- SceneEditor のシーンプリセットにポストエフェクト設定を同梱できるようになった
  （保存ポップアップの「ポストエフェクト」をチェック）
- SceneCapture プリセットの取り込みに対応。SceneEditor 経由で読み込んだ
  SceneCapture のシーンプリセットから、ポストエフェクト設定を復元できる
- SceneEditor の有効/無効にエフェクトの適用が追従するようになった
- UI のアクセントカラーをシアンに変更
- 修正: リムライトの「髪には適用」の初期値を false に変更

### v1.0.0.0
- 初回リリース
