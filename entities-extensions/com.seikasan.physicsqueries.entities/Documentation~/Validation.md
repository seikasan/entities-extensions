# 検証記録

検証日: 2026-10-18

## 環境と範囲

- Windows / Unity 6000.3.22f1 (1c726e1fb402)
- Entities 1.5.0 / Unity Physics 1.5.0
- Burst 1.8.30 / Collections 2.7.1 / Mathematics 1.3.3
- Unity Test Framework 1.6.0
- batchmode / nographics、EditorのMono環境、Burst Compilation / Jobs Safety Checks有効

Raycast、Cast、Check、Overlapの各APIをUnity 6.3で検証しました。IL2CPP、Windows以外のPlayer、描画およびInspector操作は検証対象外です。

Unity Physics 1.5.0の実際のPhysicsWorldSingleton / QueryWrappers / CollisionFilterのソースを確認して実装しています。標準APIの同一シグネチャを重複定義せず、全11種に対して24個の拡張オーバーロードを提供します。

## 結果

| 項目 | 結果 |
| --- | --- |
| Runtime、テスト、Importした両サンプルのコンパイル | 成功 |
| EditMode | 74 / 74成功（パッケージ73件 + Ground Check Baking 1件） |
| 更新順Jobの追加確認 | 最後に追加した依存関係のAssertを含む2件をEditModeでも再実行し、2 / 2成功 |
| PlayMode | 75 / 75成功（パッケージ73件 + サンプル2件） |
| Burst ISystem | 全11種の成功をビットマスクで検証 |
| Burst IJobEntity.ScheduleParallel | 128 Entityで全11種を検証 |
| 全24拡張オーバーロード | Burstコードからの呼び出し経路をコンパイル・実行 |
| 実際のPhysicsSystemGroupでの更新順 | 同期あり / なし、連続する固定ステップを検証 |
| Physics Job後のクエリJob依存関係 | AfterPhysicsでstate.Dependencyを引き継いでScheduleParallel、完了後の値を確認 |
| GC | ウォームアップ後、11種類×10,000反復で0 bytes |
| サンプル | Raycast全11種、Ground Checkの接地 / 非接地、実際のBakingからの接地判定が成功 |

BurstCompile属性だけで判定せず、BurstDiscardによるManaged fallback検出を各ProbeとベンチマークでAssertしています。テストで使う反射は、Entities 1.5.0の内部Bakingテスト入口に接続するEditorサンプルテストだけです。ライブラリRuntimeに反射はありません。

Raycastの結果は標準CastRay、Cast / Check / Overlapの結果は各標準APIと比較しています。Entity、Fraction、位置・法線、DistanceHit / ColliderKeyを確認し、Filter、Trigger、方向の大きさ、巨大な有限方向、無効Ray、短い距離、未ヒット、List追記・再利用、複数Worldの独立性も含みます。

## 物理更新順で確認した動作

中心Z=5の動的Boxを、速度4、dt=0.25でZ=6へ移動する実シミュレーションを使用しました。

- シミュレーション前: レイは表面Z=4へHit。
- SynchronizeCollisionWorld=0: AfterPhysicsのクエリでも表面Z=4へHit。
- SynchronizeCollisionWorld=1: AfterPhysicsのクエリは更新後の表面Z=5へHit。
- 次の固定ステップ開始時: 同期なしでも再構築によって表面Z=5へHit。

メインスレッドのクエリ前にはstate.Dependency.Complete()が必要です。初期テストで同期ありの未完了Jobとの競合を検出し、読み取り側を修正しました。クエリをJobで行う場合はstate.DependencyをScheduleへ渡します。ライブラリが同期や設定変更を暗黙に行うことはありません。

## GCと性能

GCは十分なCapacityを持つNativeListを再利用し、Singletonを一度取得してから、全11種のクエリを32反復ウォームアップした後、10,000反復を計測しました。結果は0 bytesです。Nativeメモリの確保はGCとは別であり、利用側のList確保・拡張・破棄を不要にするという意味ではありません。

以下はPlayModeの実測値です。3個の静的Box（うち1個はTrigger）のWorldを使用し、Burst IJob.Runで1回 / 10,000回を各32バッチ計測した1バッチ平均です。ループ内でRay / 形状の位置を変え、結果を集計して標準版と一致することを確認しています。Listは計測外で確保し、各反復でClearします。

値にはJob.Runと安全性検査、反復処理等の費用も含みます。特に1回の測定は呼び出し側の費用とノイズの影響が大きく、クエリ関数だけの時間ではありません。標準版→拡張版の順で測定した1環境の結果であり、負の差分は高速化の保証ではありません。Ray / Castの拡張には方向正規化と入力検査の追加費用があります。性能の合否閾値は設定していません。

| Query | 標準 1回 (us) | 拡張 1回 (us) | 標準 10,000回 (us) | 拡張 10,000回 (us) | 差分 10,000回 (us) |
| --- | ---: | ---: | ---: | ---: | ---: |
| Raycast | 2.897 | 3.181 | 5589.028 | 6977.363 | 1388.334 |
| RaycastAll | 5.066 | 4.759 | 6547.863 | 8866.644 | 2318.781 |
| SphereCast | 5.513 | 5.531 | 9091.038 | 11692.269 | 2601.231 |
| BoxCast | 6.797 | 7.588 | 27563.034 | 22583.869 | -4979.166 |
| CapsuleCast | 7.434 | 6.403 | 21259.863 | 24522.909 | 3263.047 |
| CheckSphere | 10.200 | 10.369 | 47420.013 | 43668.409 | -3751.603 |
| CheckBox | 10.681 | 10.797 | 113880.513 | 126725.341 | 12844.828 |
| CheckCapsule | 8.366 | 8.691 | 38449.372 | 35397.438 | -3051.934 |
| OverlapSphere | 5.794 | 5.906 | 43863.659 | 47817.097 | 3953.438 |
| OverlapBox | 18.759 | 18.803 | 128370.109 | 135074.838 | 6704.728 |
| OverlapCapsule | 9.941 | 12.513 | 45594.316 | 56426.294 | 10831.978 |

Singleton取得は上の計測から分離しています。キャッシュ済みEntityQuery.GetSingleton<PhysicsWorldSingleton>()の10,000回は3826.900 usでした。これはSystemの更新・依存関係完了・World構築の費用を含みません。

生データは[Performance.csv](Performance.csv)を参照してください。

## テストの実行

Packageを`testables`へ追加し、Unity Test RunnerでEditModeまたはPlayModeの`PhysicsQueries.Entities`を実行します。パッケージ単体では各モード73件です。Ground CheckサンプルをImportするとEditModeテストが1件、サンプル2種をImportするとPlayModeテストが2件追加されます。

サンプルの手動実行方法はREADMEを参照してください。
