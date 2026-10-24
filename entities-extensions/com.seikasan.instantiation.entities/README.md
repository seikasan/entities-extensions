# Instantiation for Entities

Unity標準の`Instantiate`と既存Componentの初期化を、1回の呼び出しにまとめるUPMパッケージです。

- Package ID: `com.seikasan.instantiation.entities`
- API Namespace: `Unity.Entities`
- Assembly（asmdef参照名）: `Instantiation.Entities`
- Unity: 6000.3 / Entities: 1.5.0 / Mathematics: 1.3.3
- EntityManager、EntityCommandBuffer、EntityCommandBuffer.ParallelWriterに各10種類の初期化APIと、ECB向けの一括生成APIを追加します（合計31メソッド）。
- Burst対応。独自Builder、Handle、Source Generatorは使用しません。

## 導入

Unity Package Managerの **Install package from git URL...** に、次のURLを貼り付けてください。

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.instantiation.entities#main
```
拡張メソッドは `using Unity.Entities;` で使用できます。通常利用のためのInspector設定、Authoring、System登録は不要です。
利用側にasmdefがある場合は、アセンブリ参照に `Instantiation.Entities` を追加してください。

```csharp
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct BulletData : IComponentData
{
    public float Damage;
    public float Speed;
}

// prefabにはLocalTransformとBulletDataが必要です。
var bullet = ecb.Instantiate(
    prefab,
    LocalTransform.FromPositionRotationScale(
        position,
        rotation,
        1f),
    new BulletData
    {
        Damage = 10f,
        Speed = 20f
    });

// 追加の初期化も標準APIで記録できます。
ecb.SetComponent(bullet, new BulletData
{
    Damage = 15f,
    Speed = 20f
});
```

## 名前空間とアセンブリ参照

拡張メソッドを含む公開クラスは`Unity.Entities`名前空間にあります。利用側では`using Unity.Entities;`を使用してください。asmdefから参照するアセンブリ名は`Instantiation.Entities`です。

内部ヘルパーの`ComponentInitialization`は`Instantiation.Entities`にありますが、利用側で参照する必要はありません。
## 10種類のオーバーロード

| ECBでの呼び出し | 動作 |
| --- | --- |
| `ecb.Instantiate(prefab, position)` | Positionを指定、Rotationはidentity、Scaleは1 |
| `ecb.Instantiate(prefab, position, rotation)` | Position・Rotationを指定、Scaleは1 |
| `ecb.Instantiate(prefab, position, rotation, scale)` | 3要素を指定 |
| `ecb.Instantiate(prefab, transform)` | 既存LocalTransform全体を上書き |
| `ecb.Instantiate(prefab, component)` | 既存Componentの値だけを上書き。他はPrefabから継承 |
| `ecb.Instantiate(prefab, transform, component)` | LocalTransform、Componentの順に上書き |
| `ecb.Instantiate(prefab, component1, component2)` | Component 2個を初期化 |
| `ecb.Instantiate(prefab, transform, component1, component2)` | LocalTransformとComponent 2個を初期化 |
| `ecb.Instantiate(prefab, component1, component2, component3)` | Component 3個を初期化 |
| `ecb.Instantiate(prefab, transform, component1, component2, component3)` | LocalTransformとComponent 3個を初期化 |

`position`は`float3`、`rotation`は`quaternion`、`scale`は`float`、`transform`は`LocalTransform`です。
位置だけを指定した場合、PrefabのRotation・Scaleは引き継ぎません。`LocalTransform.FromPosition()`と同じ初期値です。

EntityManagerにも同じ6種類があります。

```csharp
// 即時生成。戻り値は生成済みEntityです。
var entity = entityManager.Instantiate(
    prefab,
    position,
    rotation);
```

ParallelWriterでは、すべてのオーバーロードで`sortKey`を先頭に指定します。

```csharp
var writer = ecb.AsParallelWriter();
var bullet = writer.Instantiate(
    sortKey,
    prefab,
    position);
var initialized = writer.Instantiate(
    sortKey,
    prefab,
    transform,
    new BulletData
    {
        Damage = 10f,
        Speed = 20f
    });
```

## 複数Componentの同時初期化

2個または3個の異なるComponent型を、追加のBuilderや独自のHandleなしで一度に指定できます。
引数は`unmanaged, IComponentData`の値に限られ、指定順に`SetComponent`で上書きします。

```csharp
public struct Damage : IComponentData { public int Value; }
public struct Lifetime : IComponentData { public float Seconds; }
public struct Team : IComponentData { public int Id; }

// Prefabには、下記の3つのComponentとLocalTransformを事前に追加してください。
var entity = ecb.Instantiate(
    prefab,
    LocalTransform.FromPosition(position),
    new Damage { Value = 10 },
    new Lifetime { Seconds = 5 },
    new Team { Id = 1 });

// EntityManager版も同じ指定順序です（生成は即時）。
var immediate = entityManager.Instantiate(
    prefab,
    new Damage { Value = 20 },
    new Lifetime { Seconds = 3 });

// JobのParallelWriter版はsortKeyを先頭に置きます。
var deferred = writer.Instantiate(
    sortKey,
    prefab,
    new Damage { Value = 30 },
    new Lifetime { Seconds = 2 });
```

どのオーバーロードも戻り値は従来どおり`Entity`です。EntityManager版は実Entity、ECBとParallelWriter版は同一ECB内の仮Entityです。複数Componentを用いた際にもこの規則は変わりません。

## ECBでの一括生成

配列の確保を自分で書かず、生成数とAllocatorを指定できます。戻り値はUnity標準の `NativeArray<Entity>` です。独自のBuilderやHandleは使用しません。

```csharp
using var entities = ecb.Instantiate(
    prefab,
    count,
    Allocator.Temp);

for (int i = 0; i < entities.Length; i++)
{
    ecb.SetComponent(entities[i], new BulletData
    {
        Damage = 10f,
        Speed = 20f
    });
}
```

- `count` は0以上です。0なら空の配列を返し、Entityを生成しません。負数は記録前に `ArgumentOutOfRangeException` になります。
- 内部で標準の `ecb.Instantiate(prefab, entities)` を呼びます。生成自体はPlaybackまで遅延し、Component値やLinkedEntityGroupの扱いもUnity標準に従います。
- このオーバーロードは生成だけを記録し、TransformやComponentを上書きしません。必要な初期化は、上の例のように同じECBへ続けて記録できます。
- 配列の確保は内部で行いますが、解放は呼び出し側の責任です。`using var` を使用してください。記録時に例外が発生して配列を返せない場合は、内部で解放します。
- 配列内は**仮Entity**です。Playbackしても配列内の値は実Entityに置き換わりません。後続コマンドを記録した後は、Playback前に配列をDisposeして構いません。
- `allocator` の型は `AllocatorManager.AllocatorHandle` です。通常の `Allocator.Temp` / `TempJob` / `Persistent` も渡せます。配列の使用期間・スレッドに合うAllocatorを指定してください。これはECB本体のAllocatorとは別です。
- EntityManagerにはUnity標準で同じ `Instantiate(prefab, count, allocator)` があります。ECB.ParallelWriterには今回の一括生成オーバーロードを追加していません。

## 実行・初期化の契約

- 標準の`Instantiate`の後に`SetComponent`を記録します。EntityManagerでは`SetComponentData`を即時実行します。`AddComponent`への切り替えはしません。
- 指定したComponentとLocalTransformは、Prefabにあらかじめ存在する必要があります。Componentだけを指定するオーバーロードにはLocalTransformは不要です。複数Component版でも**新しいComponentの追加は行いません**。
- EntityManager版では生成元Entityの存在と初期化対象の全Componentを**Instantiate前に検証**します。不足している場合は生成せず例外を送出するため、このケースでは生成済みEntityが残りません。Unity標準のInstantiateやSetComponentData自体が別の理由で失敗した場合まで含めたトランザクション保証ではありません。
- ECB / ParallelWriter版はPrefabのComponent構成を記録時に参照できないため、Component不足は**Playback時にエラー**になります。Playbackの部分的な実行は自動ロールバックしません。
- `T`は値を持つ`unmanaged, IComponentData`です。ゼロサイズTagは、複数Component版でもすべての引数を生成・記録前に検証し、`ArgumentException`で拒否します。TagをPrefabに保持することや、生成後に標準APIで追加することは可能です。
- Enableable Componentの値を設定しても、有効・無効の状態は変更しません。
- `T`として`LocalTransform`を明示的に渡すことも可能です。TransformとComponentの両方にLocalTransformを渡した場合は、最後の`component`の値が採用されます。複数Component版で同じ型を複数指定した場合も、**最後の値が採用**されます。
- Parentを持つ場合、指定座標はローカル座標です。Parent追加やワールド座標への変換は行いません。
- EntityManagerはUnity標準と同じ構造変更と同期ポイントを伴います。ECBの記録だけではEntityは生成されません。

### ECBから返るEntityの寿命

ECBとParallelWriterの戻り値は、そのECB内だけで使える**仮Entity**です。Playbackでその変数自体が実Entityに変わることはありません。Playback後にEntityManagerへ渡したり、将来のフレームの実Entity参照として保持したりしないでください。

同一ECBの`SetComponent`などに仮Entityを渡すことは可能です。Component内のEntity参照もUnity標準のPlaybackによって実Entityに変換されます。後から実Entityを取得する必要がある場合は、識別用Componentなどを設定し、Playback後のQueryから取得してください。

### 並列JobとBurst

各呼び出し内の生成・初期化には、同じ`sortKey`を使います。`IJobEntity`なら`[ChunkIndexInQuery]`、`IJobParallelFor`なら反復indexなど、Unity標準の安定したキーを使用してください。異なるJobは別々のECBを使い、同じキーを持つ無関係なコマンド列の実行順序に依存しないでください。

Playback前に記録Jobを完了させるか、標準のECB Systemによる依存関係管理を使用してください。このパッケージはJobをスケジュールしたり、ECBの生成・Playback・Disposeを代行したりしません。

31メソッドはいずれもBurstから呼び出すことを想定して実装しています。新規12メソッドのUnity実行テストは別途実施が必要です。通常のUnity / Burstと同様、無効なPrefab、Tag、Component不足などの例外をPlayer内の回復手段として使用せず、正しいPrefab構造と型を使用してください。例外経路の検証はEditorで行います。

## サンプル

Package Managerで **Bullet Spawning** をImportし、空のGameObjectに`BulletSpawningSample`を追加してPlayしてください。Prefab等のInspector参照設定は不要です。

サンプルは必要なEntity Prefabと生成要求を作り、Burst対応ISystemから3発の弾丸を生成します。各弾丸はDamage 10、Speed 20、寿命5秒で初期化し、+Z方向へ移動して消滅します。描画用Meshは含みません。Consoleの開始メッセージとEntities Hierarchyの`BulletData` / `LocalTransform`で動作を確認できます。

## テスト

Unityプロジェクトの`Packages/manifest.json`の`testables`に`com.seikasan.instantiation.entities`を追加し、Test RunnerのEditMode / PlayModeで`Instantiation.Entities`を実行してください。
このフィルターはテスト名・アセンブリ名に対応しており、公開APIの名前空間 `Unity.Entities` とは異なります。

テストは31種類の公開API、一括生成、初期値、Prefab非変更、Parent、Tag拒否、Component不足、Enableable Component、ECBの遅延生成と参照変換、後続SetComponent、配列の解放、Burst System / Jobを確認します。Burst Compilerを有効にして実行してください。Managed fallbackが起きた場合はテストが失敗します。

Unity 6000.3.22f1 / Entities 1.5.0で、EditModeとPlayModeの各89テストがすべて成功しています。実行結果は[検証記録](Documentation~/Validation.md)を参照してください。

検証環境と結果は[検証記録](Documentation~/Validation.md)を参照してください。

## 対象外

非同期Prefabロード、Parentの自動設定、座標変換、可変長の複数Component初期化、DynamicBuffer / Managed Component初期化、Pooling、生成済みEntityの管理。

## ライセンス

MIT。詳細は[LICENSE.md](LICENSE.md)を参照してください。




