# Changelog

## 1.0.0 — 2026-10-24

- Cinemachine 3のFollow/LookAtが同じ追跡先を共有する場合に備え、カメラの元の追跡先を変更前に退避するよう修正。
- 重複した表示対象がある場合に、どの生成Entityにも追従しないことをテストで検証。
- 自動生成した表示を非アクティブな階層で初期化し、Awake/OnEnableより前にBind・初回同期・SDK設定を完了。
- 一括同期のComponentLookup更新とJob依存関係の完了を、EntityごとからSystem更新ごとに集約。
- 同じEntity上の複数VFX・FMODに対応。対象ID、個別コマンド、差し替え、破棄とFMODの個別3D位置を追加。
- Awakeの順序、複数の実SDK対象と所有権のテスト、100・1,000・10,000体のGameObject比較ベンチマークを追加。
- GC測定に既知の割り当てによる確認を追加し、当環境で値を取得できなかったスレッド別カウンターへの依存を除去。
- 表示用PrefabをAuthoringで指定し、Entityの生成・破棄に合わせてGameObjectを自動管理する機能を追加。
- Prefab差し替え、設定の削除、Disabledの切り替え、World破棄に対応。
- 表示用Prefabに保存できるCinemachine・VFX・FMODの初期設定と、Automatic Viewsサンプルを追加。
- Authoring・連携設定・サンプルのInspector設定をSerializeField付きprivateフィールドに変更。
- Cinemachine/FMODのEditor用テストにも同じコンパイル条件を適用。

