# Physics Queries for Entities

Unity Physicsのクエリを、従来の`Physics.Raycast`などに近い引数順序で呼び出す拡張メソッド集です。

- Package ID: `com.seikasan.physicsqueries.entities`
- Namespace: `PhysicsQueries.Entities`
- Unity 6000.3 / Entities 1.5.0 / Unity Physics 1.5.0
- Burst / Job System対応。独自の物理エンジン、結果型、更新System、Source Generatorは使用しません。
- 他のEntities.Extensionパッケージには依存しません。

Raycastに加えてCast、Check、OverlapのAPIを提供し、サンプル、自動テスト、性能検証を含みます。

## 導入

Unity Package Managerの **Install package from git URL...** に、次のURLを貼り付けてください。

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.physicsqueries.entities#main
```

## 公開API

| API | 代表的な呼び出し | 結果型 |
| --- | --- | --- |
| Raycast | `physics.Raycast(origin, direction, out hit, distance)` | bool / RaycastHit |
| RaycastAll | `physics.RaycastAll(origin, direction, ref hits, distance)` | bool / NativeList&lt;RaycastHit&gt; |
| SphereCast | `physics.SphereCast(origin, radius, direction, out hit, distance)` | bool / ColliderCastHit |
| BoxCast | `physics.BoxCast(center, halfExtents, direction, out hit, distance)` | bool / ColliderCastHit |
| CapsuleCast | `physics.CapsuleCast(point1, point2, radius, direction, out hit, distance)` | bool / ColliderCastHit |
| CheckSphere | `physics.CheckSphere(center, radius)` | bool |
| CheckBox | `physics.CheckBox(center, halfExtents)` | bool |
| CheckCapsule | `physics.CheckCapsule(point1, point2, radius)` | bool |
| OverlapSphere | `physics.OverlapSphere(center, radius, ref hits)` | bool / NativeList&lt;DistanceHit&gt; |
| OverlapBox | `physics.OverlapBox(center, halfExtents, ref hits)` | bool / NativeList&lt;DistanceHit&gt; |
| OverlapCapsule | `physics.OverlapCapsule(point1, point2, radius, ref hits)` | bool / NativeList&lt;DistanceHit&gt; |

合計24個の拡張オーバーロードを追加します。標準と同一シグネチャのメソッドは重複定義していません。Sphere / CapsuleのCheck・Overlapでフィルタを明示する形式は、Unity Physicsの既存インスタンスメソッドをそのまま使用します。

### Filter・Trigger・回転

Filterを省略した呼び出しは`CollisionFilter.Default`を使用します。`default(CollisionFilter)`は通常何も検出しない別のフィルタです。

```csharp
physics.Raycast(
    origin,
    direction,
    out var rayHit,
    100f,
    filter);
physics.SphereCast(
    origin,
    radius,
    direction,
    out var sphereHit,
    100f,
    filter,
    QueryInteraction.IgnoreTriggers);
physics.CapsuleCast(
    point1,
    point2,
    radius,
    direction,
    out var capsuleHit,
    100f,
    filter,
    QueryInteraction.IgnoreTriggers);

// Boxの引数はcenter, halfExtentsから始まり、回転は距離の後です。
physics.BoxCast(
    center,
    halfExtents,
    direction,
    out var boxHit,
    100f,
    orientation,
    filter,
    QueryInteraction.IgnoreTriggers);
physics.CheckBox(
    center,
    halfExtents,
    orientation,
    filter,
    QueryInteraction.IgnoreTriggers);
physics.OverlapBox(
    center,
    halfExtents,
    ref overlaps,
    orientation,
    filter,
    QueryInteraction.IgnoreTriggers);

// 回転省略はidentity。Filterだけの指定も可能です。
physics.BoxCast(
    center,
    halfExtents,
    direction,
    out boxHit,
    100f,
    filter);
physics.CheckBox(
    center,
    halfExtents,
    filter);
physics.OverlapBox(
    center,
    halfExtents,
    ref overlaps,
    filter);

// Filterを省略してTriggerだけ指定することも可能です。
physics.CheckSphere(
    center,
    radius,
    QueryInteraction.IgnoreTriggers);
physics.OverlapSphere(
    center,
    radius,
    ref overlaps,
    QueryInteraction.IgnoreTriggers);
```

Ray以外は、標準の`QueryInteraction.Default` / `IgnoreTriggers`を渡せます。Raycast / RaycastAllは標準CastRayと同じTriggerの扱いを維持し、Trigger除外の独自Collectorは追加していません。

球には向きがないためSphereCastに回転引数はありません。Capsuleの姿勢は両端球の中心である`point1`と`point2`で表します。Boxの`orientation`には正規化済みquaternionを使用してください。形状寸法や退化形状の制約はUnity Physics標準に従います。

UnityEngineのLayerMaskとCollisionFilterは異なる仕組みです。自動変換は行いません。

### 方向・距離・座標

Raycast / RaycastAllと本パッケージのCastオーバーロードは、方向の長さに依存しません。内部で正規化し、`maxDistance`だけ移動します。距離は必須の有限・正の値です。

ゼロ方向、長さが1e-6未満の方向、非有限の始点・方向・距離、0以下の距離、終点のオーバーフローはfalseを返します。`out hit`はdefault、既存の結果リストは変更しません。大きな有限方向でも正規化時にオーバーフローしないよう処理します。

すべて物理World上のワールド座標です。ParentやLocalTransformからの自動変換はありません。`Fraction * maxDistance`がRay / Castの移動距離であり、`hit.Position`は形状表面の接触位置です。

### NativeListの所有権

```csharp
var hits = new NativeList<DistanceHit>(32, Allocator.Temp);
try
{
    physics.OverlapSphere(
        position,
        5f,
        ref hits);
    foreach (var hit in hits)
    {
        Entity target = hit.Entity;
    }

    hits.Clear(); // 別の問い合わせとして使う場合は呼び出し側がClearします。
    physics.OverlapSphere(
        otherPosition,
        5f,
        ref hits);
}
finally
{
    hits.Dispose();
}
```

RaycastAll / OverlapはUnity標準と同じく**追記**します。Clear、ソート、Entity単位の重複排除は行いません。falseは今回新しいHitがなかったことを表し、既存リストの内容とは無関係です。複合Collider等の複数Hitや結果順序も標準の挙動です。

Listは呼び出し側が確保・破棄します。事前に十分なCapacityを確保し、繰り返し再利用できます。Managed配列は生成しません。並列Jobで同じNativeListへ同時追記する使い方はサポートしません。各Job / 反復に独立した出力領域を用意してください。

## Jobと物理更新順

```csharp
[ReadOnly]
public PhysicsWorldSingleton Physics;

// IJobEntity.Execute内
grounded.Value = Physics.Raycast(
    position,
    new float3(
        0,
        -1,
        0),
    out _,
    0.2f);

// ISystem.OnUpdate内
var physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
state.Dependency = new GroundCheckJob { Physics = physics }.ScheduleParallel(state.Dependency);
```

`PhysicsWorldSingleton`をSystemAPI経由で読み取り、取得した依存関係をJobへ引き継ぎます。メインスレッドで直接問い合わせる場合は、取得後に`state.Dependency.Complete()`してから読みます。Singleton取得だけで全物理Jobの完了を保証するとは考えないでください。

キャッシュしたSingletonを物理Worldの再構築・破棄後に使わず、必要な更新ごとに取得してください。ライブラリがJobのSchedule・Completeや物理Systemの更新を代行することはありません。

Ground Checkサンプルは`AfterPhysicsSystemGroup`で実行します。動的Bodyの**シミュレーション直後の位置**に対して正確に問い合わせたい場合は、標準の`PhysicsStep.SynchronizeCollisionWorld = 1`も検討してください。既定値0では、AfterPhysicsであってもCollisionWorldがそのステップ開始時の状態を保持する場合があります。同期には追加コストがあります。このパッケージは設定を自動変更しません。

ライブラリ自体は更新順を固定せず、渡されたWorldの状態を読み取ります。

## サンプル

Package ManagerのSamplesからImportします。

- **Raycast**: 空のGameObjectへ`RaycastSample`を追加してPlay。独立した物理Worldに対して11種類のクエリをJobから実行し、Consoleに`PASS (11 query kinds)`を表示します。Inspectorの参照設定は不要です。
- **Ground Check**: SubScene内の空のGameObjectへ`GroundCheckAuthoring`を追加し、位置を`(0, 0.6, 0)`にします。Baking時に原点の静的床とProbeを生成します。Play時、Entities Hierarchyで`Grounded.Value`がtrueであることを確認できます。高さを3へ変更するとfalseになります。データのみのサンプルでMeshは含みません。

## テスト・検証

manifestの`testables`へ`com.seikasan.physicsqueries.entities`を追加し、Test Runnerで`PhysicsQueries.Entities`を実行します。EditMode / PlayModeに対応しています。Burst CompilationとJobs Safety Checksを有効にしてください。

標準クエリとの一致、Filter、Trigger、無効Ray、List再利用、複数World、Burst System / 並列IJobEntity、実際の物理更新順、GC、全11種について1回 / 10,000回の性能測定を含みます。サンプルをImportすると各サンプルのテストも追加されます。テストの実行にはUnity Test Frameworkが必要です。

実行結果と測定条件は[検証記録](Documentation~/Validation.md)を参照してください。

## 対象外

Component条件による自動フィルタリング、独自レイヤー、非同期Scheduler、Collider自動生成、Trigger Enter / Exit追跡、Rayの独自Trigger除外、Managed結果配列。SphereCastAll等の追加クエリは、既存のUnity Physics APIをそのまま利用できます。

## ライセンス

[MIT](LICENSE.md)



