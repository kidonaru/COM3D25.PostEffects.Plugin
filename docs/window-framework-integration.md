# ウィンドウ共通基盤への移行（EditorWindow 連携）

`MainWindow` を MTEUtils の `DockableWindowBase` へ載せ替え、EditorWindow プラグインの
タブドッキングへ参加できるようにした記録（2026-08-12）。
設計方針の原典は `COM3D2.EditorWindow.Plugin/docs/window-framework-design.md`。

## 何が変わったか

| 項目 | 移行前 | 移行後 |
|---|---|---|
| ヘッダー・閉じるボタン | `MainWindow` が手書き | `DockableWindowBase` が描画 |
| リサイズ | 右下のグリップ「□」をドラッグ | 4 辺 + 4 隅。カーソル形状も変わる |
| 配置の永続化 | `OnGUI` 内で config へ書き戻し | `LoadPlacement` / `StorePlacement` の override |
| コンボボックスのドロップダウン | ウィンドウ内に描画（`GUIView.DrawComboBox`） | 別窓の `ComboBoxPopupWindow`（MTEUtils） |
| ウィンドウ群の管理 | `WindowManager` が自前でループ | `WindowManagerBase`（MTEUtils）を継承 |
| ドッキング | 非対応 | EditorWindow があればタブ統合・分離が可能 |
| スナップ / コネクト | 非対応 | 辺への吸着と連結移動に参加する（`EnableConnect` 宣言済み） |
| タブバー | 非対応 | グループ加入中は自前ヘッダーへタブ列を描画（`EnableTabBar` 宣言済み） |

EditorWindow が入っていない環境では `DockingClient.isAvailable` が false になり、
従来どおり独立ウィンドウとして動作する。
スナップ/コネクト・タブバーは後発 API のため、旧バージョンのホストでは
`DockingClient` が機能ごと自動で無効化する（タブドッキング自体は従来どおり動く）。

ゲスト側の実装義務（`Register`/`Unregister`、ヘッダー押下通知、吸着中の `GUI.DragWindow` 抑止、
連結中の個別クランプ抑止、タブ列の描画と押下通知）はすべて `DockableWindowBase` が持っており、
`MainWindow` 側の追加実装は不要。義務の一覧は
`COM3D2.EditorWindow.Plugin/docs/docking-guest-guide.md` を参照。

## 実装上の注意

- **閉じるボタンはプラグインごと無効化する**。`MainWindow.Close()` を override して
  `plugin.isEnable = false` にしている（従来のヘッダー「x」の挙動を維持）。
  `isEnable` の setter は同値で早期 return するため、無効化経路からの再入は起きない
- **`config` は `WindowManager.Init()` より前に読めている必要がある**。
  `MainWindow.Init()` が `LoadPlacement` で config を読むため。
  現状は `Initialize()` 冒頭で `configManager.Init()` を呼んでいるので順序は満たされている
- **ウィンドウ内座標は `view.viewRect.width` 基準で書く**。
  コンテンツ領域が `contentRect`（左右に `FRAME`）へ変わったため、
  旧コードの `_windowWidth - 80` のようなウィンドウ幅基準の指定は使えない

## 未検証（実機）

- ドッキング参加の一連の操作（ヘッダー重ねでのタブ統合、タブドラッグでの分離、タブ切替）
- タブバーの見た目・位置が内部窓と揃っているか（自前描画へ変わったため）
- ドラッグスナップとコネクト（連結移動・連結中のクランプ）
- EditorWindow と同時起動したときの `GUI.Window` ID 衝突の有無

## 既知の未対応

EditorWindow の GameView モード中は `Input.mousePosition` が RT 座標へ書き換わるため、
PostEffects の入力ブロック判定（`MTEUtils.IsMouseOverWindowRect`）が誤爆しうる。
MTEUtils には差し替えフック `MTEUtils.mousePositionGetter` があるが、
PostEffects からホストの生座標を取る経路（`DockingHost` の API 追加等）が未整備のため据え置き。
これは今回の共通化以前から存在する問題。
