# 性能測定

Unity 6000.3.22f1 / Entities 1.5.0、Windows 11 x64のEditor PlayModeで実測しました。IL2CPP検証は対象外です。
Intel Core i7-1185G7 @ 3.00 GHz、8論理プロセッサ、メモリ約32 GiB、Intel Iris Xe Graphicsを使用しました。
実行日時は各JSONの `RecordedUtc` に保存しています。

## 条件

- 空のアクティブなGameObject（Transformのみ）を複製します。Renderer、Collider、VFX、FMOD、Cinemachineの処理は含みません。
- GameObject方式は通常の `Object.Instantiate` と、キャッシュしたTransformへの `SetPositionAndRotation` です。
- Bridge方式はEntity Prefabを一括Instantiateし、初期Transformを設定して `GameObjectBridgeSystem.Update()` で表示を生成します。生成時間にはBind・初回同期・連携設定の走査・有効化を含みます。World/System/Entity Prefabの準備は生成時間と生成GCに含みません。
- 両方式とも入力座標の準備は同期の測定窓から外します。Bridge側の `LocalToWorld` 更新、ゲームロジック、Transform伝播、Job待ちの負荷は含みません。同期時間にはSystem全体の走査・後始末判定を含みます。
- 16体で事前実行後、各方式5回、実行順を交互に変えて時間とNativeメモリを測定しました。各回8回のウォームアップ後、異なる座標で32回同期します。同期の列は各回の中央値をさらに5回分の中央値にしたものです。
- GC測定は別の各方式3回です。CPU Profilerの記録をバイナリ保存し、`RawFrameDataView` で生成・同期マーカー内の `GC.Alloc` メタデータを合計しました。既知の1 MiB割り当てを検出できることを毎規模で検証しています。同期GCはウォームアップ後の1回です。
- 時間・Native測定中はProfilerを停止し、GC計測の負荷を除きます。Editorの安全チェックは有効です。これらはPlayerの性能値や実ゲーム全体のフレーム時間ではありません。

## 時間とGC

いずれも中央値です。生成は全体の1回、同期は全体の1更新で、1体あたりの値ではありません。

| 体数 | 方式 | 生成 ms | 同期 ms | 同期 p95 ms | 生成 GC B | 同期 GC B |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 100 | GameObject | 0.5739 | 0.0047 | 0.0048 | 4,000 | 0 |
| 100 | Bridge | 3.5679 | 0.1395 | 0.1443 | 87,888 | 0 |
| 1,000 | GameObject | 4.5379 | 0.0494 | 0.0536 | 40,000 | 0 |
| 1,000 | Bridge | 29.5908 | 1.4493 | 1.5644 | 860,088 | 0 |
| 10,000 | GameObject | 40.9041 | 0.8317 | 1.2003 | 400,000 | 0 |
| 10,000 | Bridge | 286.3104 | 16.6626 | 17.5959 | 8,582,088 | 0 |

生成時には両方式でManaged割り当てがあります。BridgeはEntityと表示の関連付け・管理も行うため、直接Transformを操作するGameObject方式より時間・メモリを使います。
定常同期では、どの規模・方式も3回すべて割り当てイベント0件でした。警告、再生成、SDK処理などへ一般化しません。
10,000体を毎回すべて同期する処理は、このEditor環境では約1フレーム分の時間を使います。60 fpsのゲームへ無条件に適用できる結果ではありません。可視範囲や必要数で表示を絞る判断が必要です。非同期生成・プーリング・JobでのTransform反映は今回追加していません。

## Nativeメモリ

MiBは1,048,576 Bです。以下は異なる測定範囲の指標であり、各列を足したり、ManagedのGC割り当て量と合算したりしません。

| 体数 | 方式 | Unity内部Allocator増分 MiB | 表示オブジェクトのNativeサイズ合計 MiB | 使用ECSデータChunk MiB |
| ---: | --- | ---: | ---: | ---: |
| 100 | GameObject | 0.0987 | 0.0565 | 0.0000 |
| 100 | Bridge | 4.6542 | 0.1102 | 0.0312 |
| 1,000 | GameObject | 0.9986 | 0.5646 | 0.0000 |
| 1,000 | Bridge | 6.1118 | 1.1024 | 0.1562 |
| 10,000 | GameObject | 10.5949 | 5.6458 | 0.0000 |
| 10,000 | Bridge | 22.7179 | 11.0245 | 1.3281 |

- **Unity内部Allocator増分**: `Profiler.GetTotalAllocatedMemoryLong()` の生成前後差です。Bridgeは独立Worldを作る前から測り、World/System/Entity Prefabの固定費も含みます。GameObject方式にはWorldがありません。テストの配列やEntity参照用NativeArrayは測定前に確保します。Editorの他の内部Allocator変動も混ざり得ます。プロセス全体のNativeヒープ・外部SDK・GPUメモリの網羅値ではありません。
- **表示オブジェクトのNativeサイズ合計**: 生成した各GameObjectとそのComponentに対する `Profiler.GetRuntimeMemorySizeLong()` の合計です。Bridgeでは追加されたMonoBehaviourを含みます。内部の共有資源やWorldを網羅する値ではありません。
- **使用ECSデータChunk**: WorldのUniversalQueryが返す使用Chunk数×16 KiBです。空きChunk、Entity番号管理、System、その他Nativeコンテナを含まないデータ領域の指標です。World全体のNative量として扱いません。

## 再測定

1. READMEのテスト設定を行い、EditorのProfiler記録を停止します。
2. Test RunnerのPlayModeで `GameObjectBridge.Entities.Benchmarks.BridgeScaleBenchmarks` を実行します。測定中のC#編集と別の負荷の高い操作は避けてください。
3. 結果は `Application.persistentDataPath/GameObjectBridgeBenchmarks` へJSON出力します。環境変数 `GAMEOBJECT_BRIDGE_BENCHMARK_DIRECTORY` で保存先を変更できます。

Profilerの取得が無効、既知の割り当てを取得できない、生成後の座標が誤っている場合は、0として成功させずテストを失敗させます。Profilerの記録履歴は計測のためにクリアします。
`GC.GetAllocatedBytesForCurrentThread()` は当環境で既知の割り当てにも0を返したため使用していません。
`ProfilerRecorder` の `GC.Alloc` サンプル値は割り当てバイト数として使用しません。

## 生データと参考API

- [100体](Benchmarks/bridge-100.json)
- [1,000体](Benchmarks/bridge-1000.json)
- [10,000体](Benchmarks/bridge-10000.json)
- [Unity: GC.Allocのサイズメタデータ](https://docs.unity3d.com/ja/6000.0/ScriptReference/Profiling.RawFrameDataView.GetSampleMetadataAsLong.html)
- [Unity: 内部Allocatorの使用量](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.GetTotalAllocatedMemoryLong.html)
- [Unity: オブジェクトのNativeメモリ](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.GetRuntimeMemorySizeLong.html)
