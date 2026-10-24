# Physics Events for Entities v1.0.0

Unity 6.3 / Entities 1.5.0 / Unity Physics 1.5.0向けの物理イベントBufferです。独自のSource Generator、属性によるSystem登録、コールバック、Dispatch呼び出しはありません。

```csharp
using PhysicsEvents.Entities;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics.Systems;

[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
[BurstCompile]
public partial struct PickupSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (events, self) in SystemAPI
                     .Query<DynamicBuffer<TriggerEnter>>()
                     .WithAll<PickupZone>()
                     .WithEntityAccess())
        {
            foreach (var contact in events)
            {
                Entity other = contact.Other;
                // selfはBufferを持つEntityです。otherのComponentで対象を絞り込みます。
            }
        }
    }
}
```

## 公開Buffer

| Buffer要素型 | 内容 |
| --- | --- |
| `TriggerEnter` | Trigger接触の開始 |
| `TriggerStay` | Trigger接触の継続。開始した固定更新は含めません |
| `TriggerExit` | Trigger接触の終了 |
| `CollisionEnter` | Collision接触の開始 |
| `CollisionStay` | Collision接触の継続。開始した固定更新は含めません |
| `CollisionExit` | Collision接触の終了 |

すべて`IBufferElementData, IEnableableComponent`で、`Entity Other`を持ちます。Bufferを持つEntityがselfです。購読条件は、そのBufferが存在して有効であることです。読み取りSystemの数・存在は購読条件に含みません。

```csharp
entityManager.AddBuffer<TriggerEnter>(receiver);
entityManager.SetComponentEnabled<TriggerEnter>(receiver, false);
entityManager.SetComponentEnabled<TriggerEnter>(receiver, true);
```

SystemAPI.Queryの通常のEnableableフィルターが働きます。複数種類のDynamicBufferを同時にQueryすると、通常のECSと同様に、指定したすべてのBufferが有効なEntityだけを列挙します。独立したイベントの読み取りには種類ごとにQueryしてください。

## InspectorとBaking

1. 受信するPhysics BodyのGameObjectに`PhysicsEventReceiverAuthoring`を追加します。
2. 必要なTrigger／CollisionのEnter／Stay／Exitにチェックを付け、Capacityを指定します。
3. Unity標準Colliderでは、Trigger用に`Is Trigger`、Collision通知用に`Provides Contacts`を設定します。Collision Responseを指定するPhysics Authoringでは、それぞれ`Raise Trigger Events`／`Collide Raise Collision Events`を選択します。
4. 読み取りSystemを`AfterPhysicsSystemGroup`に配置します。

Bakerはチェックされた種類のBufferと`PhysicsEventBufferCapacity`だけを追加します。未選択の種類は、容量が正数でもBufferを追加せず、その容量設定も0にします。すべて未選択なら、このBakerはComponentを追加しません。受信設定だけでTransformを要求しないため、`GetEntity(TransformUsageFlags.None)`を使用します。TransformのBakingはPhysics Bodyなど本来のAuthoringに任せます。PrefabではUnity側の処理でTransformが付く場合があります。`DisallowMultipleComponent`は指定していません。同じEntityに同じBufferを重複して追加するBaker構成はUnityの通常の重複追加エラーになります。

初期選択されたBufferは有効です。実行中に無効化したBufferは、`SetComponentEnabled`で再有効化できます。Bakerで未選択だった種類を後から使う場合は、標準の`AddBuffer<T>()`やECBで追加してください。必要なら容量設定Componentも追加・更新します。

Capacityは要素数です。専用の`PhysicsEventCapacitySystem`が`BeforePhysicsSystemGroup`の最後に`EnsureCapacity()`します。無効なBufferも予約対象です。Prefab自体は処理せず、Instantiateした実体を処理します。SubSceneから読み込まれたEntityやECBで生成したEntityも、次にこのSystemを通る時点で対象になります。容量確保Systemより後にPlaybackしたEntityは、次の固定更新で初期予約されます。予約前にイベントが発生しても通常のDynamicBufferとして必要量まで成長します。

```csharp
entityManager.AddComponentData(receiver, new PhysicsEventBufferCapacity
{
    TriggerEnter = 8,
    TriggerStay = 8,
    TriggerExit = 8
});
```

容量設定は既存Bufferだけに作用し、Bufferを自動追加しません。負数は予約なしとして扱います。容量設定を下げても縮小しません。新規Entityや設定が変更されたチャンクを変更フィルターで検出するため、Bufferを手動で縮小した後に再予約したい場合は容量設定Componentも更新してください。`ReservationCount`は、その実体で初期容量設定によって確保した回数です。Prefabから継承した値も通常のComponentとしてコピーされます。

## 固定更新と購読の意味

- イベントの寿命は1固定更新です。次の収集前に全BufferをClearし、無効なBufferにも古い通知を残しません。複数の読み取りSystemが同じ内容を読めます。読み取り側でClearしないでください。
- 1描画フレーム中に複数FixedStepが進んでも、AfterPhysicsSystemGroupの利用Systemは各固定更新で読み取れます。通常のSimulationSystemGroupや描画更新だけで読むと、中間の固定更新を取り逃がします。
- 同じEntity同士のCompound Collider接触は1件にまとめます。双方が購読していれば双方へ1件ずつ書きます。EntityのIndexとVersionで識別します。Collider単位の情報は公開しません。
- 履歴はTriggerとCollisionで独立し、それぞれWorld全体のDirectedContactを1回だけ比較します。SystemごとのSubscriptionや履歴はありません。
- 同じ種類のEnter／Stay／Exitのいずれかが有効である間は接触履歴を維持します。途中でEnterだけを有効にしてもEnterは再通知しません。すべてを無効にした固定更新では履歴から外れ、その後の再購読は新しい接触として扱います。固定更新間に無効→有効へ戻した変更は観測しません。
- `Disabled`のEntityは受信しません。再有効化時は新しい購読になります。
- 破棄したselfには書き込みません。ExitのOtherには破棄済みEntityが入ることがあります。`Exists`や`HasComponent`で確認してからアクセスしてください。
- 列挙中の構造変更は通常のECSの制約に従います。ECBに記録し、列挙後にPlaybackしてください。Bufferを保持したまま構造変更を挟まないでください。

## Jobと未使用時の動作

有効なBufferがない種類の`ITriggerEventsJob`／`ICollisionEventsJob`はスケジュールしません。初めから未購読なら接触保存用Nativeコンテナも確保しません。無効化されたBufferのClearや以前の履歴の解除は実行することがあります。Unity Physics本体のシミュレーション負荷は別です。

購読QueryのEnableableマスクを使い、有効な受信EntityとEnter／Stay／Exitの購読マスクをWorld共有のHashMapへ直接列挙します。一時Entity配列は作りません。Bufferの存在と有効状態という購読条件は変わりません。

収集→履歴比較・通知作成→Buffer書き込みを依存Jobで接続します。前回と今回の履歴を一度比較して、購読されている種類だけを連続した通知リストへ格納します。各Entityの通知範囲も同じHashMapへ記録します。書き込みJobはこの範囲を1回参照してコピーするため、Entityごとの履歴マージは不要です。作業領域のClearも1つのJobへまとめています。

前回の収集直後の列も保存し、今回と要素数・順序・self／other双方のIndexとVersionが完全に一致する場合は、確定済みの重複除去済み履歴を直接参照してStayを生成します。この場合はソート・重複除去・履歴コピー・履歴交換を省略します。購読マスクは毎固定更新で取り直すため、Stayの有効状態変更は反映します。順序だけの変更、接触の増減、Entityの再生成などで一致しなければ、通常のソート・比較を行い、通知生成後に新しい履歴を確定済み履歴へコピーします。ハッシュ値や近似判定でEnter／Exitを省略することはありません。

この履歴方式は安定接触時のコピーを削減する一方、接触列が変わる更新には履歴確定のコピーが加わります。3変更を併用した状態の動作・性能比較は[接触入れ替わりベンチマーク](Documentation~/ContactChurnValidation.md)を参照してください。性能検証記録の該当値は比較前構成で測定したものです。

Trigger／Collisionそれぞれで有効な受信Entityが1体の場合は、単一受信者経路を使用します。毎固定更新のEnableable対応Queryで人数を判定し、対象列挙JobでEntityのIndex・Versionと最新のEnter／Stay／Exit購読マスクを取得します。このJobに収集列・通知リストのClearもまとめ、単独の準備Jobを省略します。収集・通知生成・Buffer書き込みはEntityの直接比較で行い、HashMapのClear・登録・検索・通知範囲更新は行いません。通知リスト全体がその受信者の範囲になります。

2体以上では従来のHashMap経路へ戻り、人数・受信Entityが変わっても共通の接触履歴から通知を生成します。Disabled EntityとPrefabの除外条件、Bufferの有効状態、履歴のEntity Version判定は変更していません。UnityのSingleton取得APIはEnableableを含むQueryでは利用できないため使用しません。追加のメインスレッド同期で受信Entityを取得する処理もありません。

単一受信者情報はWorld内の種類別NativeReferenceに保持します。共通JobのNativeコンテナ安全性を維持するため、単一経路でも有効なHashMap領域は確保します（初回の単一購読ではCapacity 1）。複数受信から単一受信へ移った場合は既存Capacityを再利用し、最後の購読終了時に受信者情報とともに解放します。安全性チェックを無効化する属性は追加していません。人数切り替え・Disabled・破棄／再生成・再購読の回帰テストと、単一受信者を含む実接触の負荷試験を実行しました。各変更の単独の性能寄与は未分離です。

書き込みはBurst IJobChunkで行い、各チャンクが自分のEntityのBufferだけを更新します。複数チャンクは並列実行し、1チャンクは直列にします。任意EntityへのBufferLookup書き込みや、安全性チェックを無効化する属性は使いません。Gatherから書き込みまでの依存鎖でBufferの有効状態を保護し、利用側の通常のECS依存関係へ引き継ぎます。利用側も通常のIJobEntity／IJobChunkでBufferを処理できます。

TriggerとCollisionは更新開始時の同じ依存関係から別々にスケジュールし、最後に`JobHandle.CombineDependencies`で結合して`state.Dependency`へ渡します。各経路内の準備・収集・履歴更新・Buffer書き込みの順序は維持し、同じ固定更新のTrigger書き込みをCollision収集の前提にはしません。例外が発生した場合も、スケジュール済みのJobは結合した依存関係へ残します。後続System・診断・System破棄は両経路の完了を待てます。

購読終了時の解放と容量変更は、対象経路の依存関係を完了してから行います。この依存関係には更新開始時の共通依存も含まれるため、前回のJobが利用中の領域を変更しません。他方の経路で今回スケジュールしたJobを完了させる処理は加えていません。World破棄時は結合済みの依存関係を完了してから全Native領域を解放します。

`PHYSICS_EVENTS_PROFILING`のNativeReferenceとチャンク別統計領域もTrigger／Collisionで分離しています。接触数リセット・計測値更新・統計集計は各経路の領域だけを使い、診断メソッドで両経路を完了してから値を合算します。累積カウンターを維持するため、計測用NativeReferenceは最初の有効購読時に確保し、購読終了後もWorld破棄まで保持します。チャンク別統計配列は対象種類の購読終了時に解放します。両方の種類を使う計測構成ではNativeReferenceが従来の1個から2個になります。通常構成にはこれらの計測領域・集計処理はありません。依存関係分離後のコンパイル・回帰テスト・性能測定を実行しました。Job間の実際の重なりをProfiler Timelineから独立に確認したわけではありません。

接触履歴、収集列のスナップショット、通知リスト、対象HashMapは容量を再利用します。最後の有効な購読がなくなった種類では、既存Jobを完了してこれらを解放します。再購読時には再確保します。公開DynamicBufferの予約容量は維持します。容量の成長・購読終了時を除き、毎回メインスレッドで収集Jobを完了させる処理はありません。

1.0.0は速度を優先し、スナップショットと通知リストの分だけNative保持量を増やしています。収集列の順序が頻繁に変わる場合は再利用率が下がり、比較とコピーの追加費用が発生します。速度・メモリ・再利用率は[Unity 6.3の性能検証記録](Documentation~/PerformanceValidation.md)を参照してください。

最新の測定では、10,000組の実Collision、Mixed条件では追加の10,000組の実Triggerを発生させ、接触ペアが毎固定更新0／10／50／100%入れ替わる48条件を各3試行測定しました。通常構成の再測定では受信あり40条件中19条件が短縮、21条件が増加しました。一律の高速化は確認できません。Editor上のばらつきがあるため、初回値も保存しています。GCはPackage区間のメインスレッドで0 B、定常時のNative再確保カウンターは0、未購読時の収集回数は0でした。Native値は保持データ量の下限であり、World全体のNative総使用量ではありません。[測定条件・全結果・生データ](Documentation~/ContactChurnValidation.md)を確認してください。

容量確保Systemのハンドル権限は、通常構成でReadOnly、計測構成でReadWriteを明示します。測定中に見つかった権限不一致の安全性エラーを修正し、両比較対象へ共通適用しています。修正後は通常構成61件・計測構成62件のPlayModeテストが成功しました。

通常構成では、計測用NativeReference、チャンク別の計測配列、計測値のリセット・集計Job、容量設定Componentの予約回数フィールドをコンパイルしません。詳細な累積カウンターやBurstDiscardによる検証が必要な場合だけ、プロジェクトのScripting Define Symbolsに`PHYSICS_EVENTS_PROFILING`を追加してください。計測構成は処理時間・メモリが変わるため、通常構成の性能とは区別します。

`PhysicsEventCollectSystem`は`ISystem`です。`OnUpdate(ref SystemState)`のBurst化は見送り、更新本体とTrigger／Collisionの準備・JobスケジュールはManagedで実行します。収集・履歴比較・Buffer書き込みの既存Jobは引き続きBurstで実行します。`OnCreate`と`OnDestroy`もManagedで実行し、破棄時は依存Jobを完了してからNative領域を解放します。イベントのアルゴリズム・公開Buffer・Inspector設定は変更していません。

診断には、そのWorldのSystemHandleから解決したSystemStateを渡します。以下のメソッドは呼び出した時だけ依存Jobを完了します。Systemの実体を直接参照するのは診断・テスト時に限り、通常のイベント読み取りにはこれらのAPIは不要です。

```csharp
var handle = world.GetExistingSystem<PhysicsEventCollectSystem>();
ref var collector = ref world.Unmanaged.GetUnsafeSystemRef<PhysicsEventCollectSystem>(handle);
ref var state = ref world.Unmanaged.ResolveSystemStateRef(handle);
var metrics = collector.CompleteAndGetMetrics(ref state);
long bytes = collector.GetContactStorageBytes(ref state);
```

通常構成のMetricsは接触数だけ有効で、累積カウンターの0は未計測を表します。`ProfilingEnabled`で区別できます。保持容量は履歴・スナップショット・通知・対象HashMapのキーと通知範囲等のデータ量の下限であり、HashMapバケットやチャンク等を含むNativeメモリ総量ではありません。[性能・メモリ検証](Documentation~/PerformanceValidation.md)を参照してください。

`PHYSICS_EVENTS_PROFILING`では更新本体と両分岐のManaged実行検出、および`PhysicsEvents.Collector.Schedule`の計測区間も有効になります。通常構成ではこれらをコンパイルしません。

Unity Physicsを正式対象とし、Havokは検証対象外です。IL2CPP検証は行いません。

## 導入とテスト

Package Managerの **Install package from git URL...** で、配布リポジトリURLに`?path=/entities-extensions/com.seikasan.physicsevents.entities`を付けて導入します。バージョンを固定する場合は、URL末尾に`#コミットID`を指定してください。Namespaceは`PhysicsEvents.Entities`です。Test Runnerでパッケージのテストを表示するには、プロジェクトのmanifest.jsonの`testables`へPackage IDを追加してください。PlayModeでは実際のUnity Physics、EditModeではBakerとSubSceneストリーミングを検証します。性能テストは1万体の物理演算を実行します。

サンプル`Trigger Pickup`／`Collision Phases`はシーン参照を必要とせず、セットアップSystemがデモ用Entityを生成します。

MIT License。





