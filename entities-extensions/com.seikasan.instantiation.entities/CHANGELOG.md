# Changelog

## 1.0.0 — 2026-10-24

- `EntityManager` / `EntityCommandBuffer` / `ParallelWriter` に、2〜3個のComponentを同時初期化するオーバーロードを各4種類追加しました（計12 API、合計31 API）。
- `EntityManager`版では生成前にPrefabの存在と必要なComponentをすべて検証し、Component不足で生成済みEntityを残さないようにしました。
- 複数ComponentのTag検証、実行順序、Enableable状態、既定値、失敗時の安全性、Burst ISystem / IJobParallelForを対象に回帰テストを追加しました。Unity 6000.3.22f1 / Entities 1.5.0でEditMode・PlayMode各89件の成功を確認しました。

- 公開拡張クラス3つの名前空間を `Unity.Entities` に変更しました。利用側では `using Unity.Entities;` を使用します。アセンブリ参照名は引き続き `Instantiation.Entities` です。
- ECBに `Instantiate(prefab, count, allocator)` を追加し、仮Entity配列の確保と一括生成の記録をまとめました。
- 一括生成の遅延実行、参照変換、配列の早期解放、入力検証、Burst Jobの自動テストを追加しました。

