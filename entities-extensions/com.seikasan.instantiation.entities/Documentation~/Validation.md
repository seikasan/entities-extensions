# 検証記録

## 環境

| 項目 | バージョン |
| --- | --- |
| OS | Windows |
| Unity Editor | 6000.3.22f1 |
| Entities | 1.5.0 |
| Mathematics | 1.3.3 |
| Collections | 2.7.1 |
| Burst | 1.8.30 |
| Test Framework | 1.6.0 |

## 結果

| 検証 | 結果 |
| --- | --- |
| EditMode | 89 / 89成功 |
| PlayMode | 89 / 89成功 |
| Unity Console compile errors | 0件 |

EntityManager、EntityCommandBuffer、ParallelWriterの生成APIを検証しました。複数Componentの初期化、生成前の入力検証、Tagの拒否、Prefab値の継承、Enableable Componentの状態、Component不足、型の指定順、ECBの遅延実行を含みます。Burst ISystemとBurst IJobParallelForからの利用も確認しています。

一括生成では0体・1体・複数体、Temp / TempJob / Persistent、Playback前の遅延生成、仮Entityの重複、後続SetComponent、Prefab値の継承を検証しました。返却された仮Entityの扱い、配列のDispose、負数および無効Prefabの入力も確認しています。

Burst検証ではBurst同期コンパイルとBurstDiscardによるManaged fallback検出を使用しました。

## テストの実行

Packageを`testables`へ追加し、Unity Test Runnerで`Instantiation.Entities`のEditMode / PlayModeテストを実行します。サンプル統合テストはBullet SpawningサンプルをImportすると追加されます。

## 未検証の範囲

Playerビルド、IL2CPP、Windows以外の実行環境、描画付きサンプルの目視確認は行っていません。GC Allocationと性能ベンチマークも測定していません。
