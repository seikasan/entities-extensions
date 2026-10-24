# GameObject Bridge for Entities

Unity 6.3 / Entities 1.5.0向けの、Entityと表示用GameObjectを関連付けるUPMパッケージです。
Transform同期と、Cinemachine・VFX Graph・FMODの任意連携を提供します。

Package ID: `com.seikasan.gameobjectbridge.entities`  
Namespace: `GameObjectBridge.Entities`  
License: MIT

## 導入

Unity Package Managerの **Install package from git URL...** に、次のURLを貼り付けてください。

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.gameobjectbridge.entities#main
```
本体にCinemachine・VFX Graph・FMODへの依存はありません。追加機能はSDKを導入した場合だけコンパイルされます。

| 連携 | 必要なSDK | 有効化 |
| --- | --- | --- |
| Cinemachine | 3.1.x（実検証3.1.7） | インストール時に自動 |
| VFX Graph | 17.3.x、対応するURP/HDRP | インストール時に自動 |
| FMOD | FMOD for Unity 2.03（実検証2.03.20） | SDK導入後、Player SettingsのScripting Define Symbolsへ `GAMEOBJECT_BRIDGE_FMOD` を追加 |

自作asmdefから利用する場合は `GameObjectBridge.Entities` と、必要な `GameObjectBridge.Entities.Cinemachine` / `.VfxGraph` / `.Fmod` を参照します。
SDK本体、FMODのbank、音源は同梱していません。

## Prefabからの自動生成

1. EntityにするGameObjectまたはEntity Prefabの元に **GameObject Bridge Authoring** を追加します。
2. `Prefab` に、表示用の通常のGameObject Prefabアセットを指定します。表示用PrefabはEntityへ変換せず、そのまま生成します。
3. SubSceneのBaking後、通常の `EntityManager.Instantiate()` またはECBの `Instantiate()` でEntityを生成します。

利用側でGameObjectの `Instantiate()` や `Bind()` を呼ぶ必要はありません。シーンに直接配置してBakeしたEntityにも対応します。
`LocalTransform` と `LocalToWorld` はBakerが用意し、表示はEntity→GameObject方向に同期します。
自作asmdefからAuthoringを使用する場合は `GameObjectBridge.Entities` を参照します。

コードでEntity Prefabを構築する場合も、表示の設定は1つのComponentだけです。

```csharp
entityManager.AddComponentData(entityPrefab, new GameObjectBridgePrefab
{
    Prefab = viewPrefab
});

var entity = entityManager.Instantiate(entityPrefab);
```

このコード例のEntity Prefabには、標準の `Prefab`、`LocalTransform`、`LocalToWorld` が必要です。

- Simulation末尾の `GameObjectBridgeSystem` が、未登録の有効なEntityごとに1つ生成します。Entity Prefab自体やDisabledのEntityには生成しません。
- Entity破棄、設定Componentの削除、World破棄で、所有する表示を破棄します。Play ModeのGameObject破棄はフレーム末尾です。
- `GameObjectBridgePrefab.Prefab` や `SynchronizeScale` の変更では、所有する表示を差し替えます。表示用Prefabアセットは破棄しません。
- 既にある表示は、EntityのDisabled中は非アクティブにし、再有効化時に元のActive状態へ戻します。
- 表示を直接破棄した場合や `Unbind()` した場合、設定が残っていれば次回更新で再生成します。停止する場合は `GameObjectBridgePrefab` を削除します。
- 明示的に `Bind()` した既存GameObjectには、引き続き手動の所有権設定を適用します。自動設定の削除で借用GameObjectを破棄しません。
- Prefab未指定の実行時Componentは指定されるまで待機します。生成・Bindに失敗した要求はエラーを記録してComponentを外します。修正後は再追加してください。
- 同期は既存の `LocalToWorld` を読みます。Transform伝播後のECBで位置を変更した場合、表示への反映は次回のTransform更新後です。

## 表示用Prefabの連携設定

自動生成は、非アクティブな階層への複製 → `Bind()` → 初回Transform同期 → 連携設定 → 元のActive状態の復元、の順で行います。
アクティブなPrefabの `Awake()` / `OnEnable()` は、この初期化が終わってから呼ばれます。子GameObjectからも次のようにアクセスできます。

```csharp
private void Awake()
{
    var bridge = GetComponentInParent<EntityGameObjectBridge>();
    var entity = bridge.Entity;
    var position = transform.position;
}
```

元から非アクティブなPrefabや子は非アクティブのままで、後から有効化した際にも関連付け済みです。
初回座標は、その時点の `LocalToWorld` です。コンストラクターやデシリアライズ中のコールバックは対象外です。
初回同期に失敗した表示は有効化せずに破棄し、要求Componentを外してエラーを記録します。

必要なSDKの導入後、表示用Prefabに次のComponentを追加すると、生成時に既存の連携APIを1回だけ適用します。
子GameObjectにも配置でき、Prefab内の参照は生成したオブジェクトへ通常どおり複製されます。無効な設定Componentは呼び出しません。
Authoringと連携設定のInspector項目は、SerializeField付きprivateフィールドとして保存し、読み取り専用プロパティで公開しています。

| Component | Prefabに保存する設定 |
| --- | --- |
| `VfxBridgeSetup` | `TargetId`、`Effect`（未指定なら同じGameObjectのVisualEffect）。VFX用Bufferを自動構成 |
| `FmodBridgeSetup` | `TargetId`、`Event`、`PlayOnSpawn`。所有するEventInstanceを生成し、既定では再生要求も記録 |
| `CinemachineBridgeSetup` | `Camera`（未指定なら同じGameObjectのCinemachineCamera）、`Follow`、`LookAt`。生成した表示を追従先に指定 |

通常のCinemachine Brain、VFX Graphの公開プロパティ、FMODのbankロード・Listener設定は必要です。
表示用Prefabにカメラを含める場合は従来どおり `CinemachineBridgeSetup` の `Camera` を使います。
シーン内に既にあるカメラを自動で追跡させる場合は、下記「シーン内CinemachineCameraの自動追跡」を参照してください。
SDKごとのSystemが、その後のVFX/FMODコマンドと破棄処理を担当します。
設定処理に失敗しても他の設定は継続し、失敗は1回記録します。再適用には表示の再生成が必要です。

独自のPrefab内Componentも必要なら `IGameObjectBridgeSetup.Configure()` を実装できます。呼び出しはBindと初回Transform同期の後で、毎フレームのイベント配信は行いません。

## 既存GameObjectの明示的な関連付けと同期

```csharp
using GameObjectBridge.Entities;
using Unity.Entities;

// entityはWorld内に存在し、LocalTransformとLocalToWorldを持つこと。
// viewはPrefabアセットではなく、シーンに生成済みのGameObject。
var bridge = EntityGameObjectBridge.Bind(
    world,
    entity,
    view);

// GameObjectからEntityへ同期する場合（1組につき方向は1つ）。
var inputBridge = EntityGameObjectBridge.Bind(
    world,
    inputEntity,
    inputView,
    TransformSyncDirection.GameObjectToEntity);

if (EntityGameObjectBridge.TryGet(
    world,
    entity,
    out var current))
{
    current.Synchronize(); // 必要な依存Jobを完了して、この1組を即時同期
}

bridge.Unbind();
```

関連付けはメインスレッドで行います。同じEntity、同じGameObjectの重複登録は禁止です。
EntityとWorldの組で管理し、別WorldのEntity番号と混同しません。
既存のシーンオブジェクトやGameObject→Entity方向の入力には、この明示的なBindを使用します。

- 既定は位置・回転のみ。GameObjectの表示用Scaleを維持します。
- Entity→GameObjectは `LocalToWorld` のワールド位置・回転を読みます。
- GameObject→Entityは親Entityの変換を逆算し、`LocalTransform` へ書きます。
- `synchronizeScale: true` は両方が親を持たないルートで、正の一様Scaleの場合だけ対応します。
- 非一様Scale、反転、シアーを含む祖先変換、無効な数値は同期をスキップし警告します。
- GameObject→EntityのEntity自身に `PostTransformMatrix` がある場合もスキップします。
- `LastSyncSucceeded` で直近の同期結果を取得できます。無効な状態が続く間の警告は1回です。
- DisabledなEntityは同期を停止し、再有効化時に再開します。

## 実行順序と寿命

標準WorldではSystemが自動登録されます。

1. `GameObjectToEntitySystem`: Simulationの先頭、FixedStepとTransformSystemGroupの前。
2. UnityのTransformSystemGroupが `LocalToWorld` を更新。
3. `GameObjectBridgeSystem`: Simulationの末尾、EndSimulation ECBの後。
4. VFX/FMODの連携Systemがコマンドを実行。
5. 通常のMonoBehaviour LateUpdate（Cinemachineを含む）。

Entityの移動はTransformSystemGroupより前に行ってください。EndSimulation ECBでTransformを書いた場合、`LocalToWorld` の再計算は次回まで待ちます。
`Synchronize()` 自体は `LocalToWorld` を再計算しません。LateUpdateやPresentationで動かす独自構成では、利用側で同期順序を指定してください。
固定更新の補間やスムージングは追加していません。
手動構築WorldではBindが作成したSystemを適切なGroupへ追加するか、明示的にUpdateしてください。

既存GameObjectは通常借用し、UnbindやEntity破棄で消しません。生成した表示オブジェクトをBridgeに所有させる場合は `destroyGameObjectOnUnbind: true` を指定します。
所有GameObjectはUnbind、Entity破棄、World破棄で削除されます（Play Mode中のDestroyはフレーム末尾）。
GameObjectを先に破棄してもEntityは削除せず、次回System更新で関連付けだけを解除します。
Entity破棄後のCleanup Componentは次回Bridge/連携System更新で除去されます。
再Bindは明示的なUnbind後に行います。内部世代番号で古い連携コマンドの再送を防ぎます。
内部の `GameObjectBridgeLink` と連携用Componentを手動で複製・追加しないでください。

## Cinemachine

### シーン内CinemachineCameraの自動追跡（コード不要）

1. シーンに配置した **CinemachineCamera** に **Cinemachine Bridge Target** を追加し、`Target Id` に `Player` などの固有IDを指定します。
2. Entityに設定する**表示用GameObject Prefab** に **Cinemachine Bridge Setup** を追加し、`Scene Camera Id` に同じ `Player` を入力します。Prefab内にCinemachineCameraを置く必要はありません。
3. 従来どおりEntityを生成します。Bridgeが表示用GameObjectを作った時点で、シーンのCinemachineCameraの Follow / LookAt が自動的にそのTransformへ設定されます。 `Follow` / `Look At` チェックでそれぞれ制御できます。

同じIDを持つカメラと表示は**各1つ**にしてください。同一IDのどちらかが複数存在する場合は警告し、そのIDの自動追跡を解除します。複数のカメラやプレイヤーを使う場合は、組ごとに異なるIDを付けます。

表示がカメラより先に生成された場合も待機して自動接続します。EntityのUnbind・破棄、表示の非アクティブ化、カメラ側のComponentの無効化・破棄では自動接続を解除します。元のカメラFollow / LookAtが変更されていなければ接続前の値へ復元します。追跡中に他のスクリプトがカメラのターゲットを書き換えた場合は、その変更を優先します。

**注意:** シーン内カメラの`Cinemachine Bridge Target`は有効状態である必要があります。Prefab側の`Scene Camera Id`を空欄にすると従来のPrefab内カメラ方式になります。Brainは従来どおりLate Update推奨です。


```csharp
using GameObjectBridge.Entities.Cinemachine;

camera.FollowEntity(bridge); // CinemachineCameraの追従先を設定
camera.LookAtEntity(bridge);
```

Entity→GameObjectのBridgeをCinemachineの通常のTransformターゲットに設定します。
BrainのUpdate Methodは **Late Update** を使用してください。
独自ループではBrainを **Manual Update** に設定し、`brain.ManualUpdateAfterBridge(world)` を1描画フレームに1回呼びます。
カメラ本体、レンズ、追従方式は通常のCinemachineで設定します。借用GameObjectをUnbindした際のFollow/LookAt解除は利用側で行います。

## VFX Graph

```csharp
using GameObjectBridge.Entities.VfxGraph;

bridge.ConfigureVisualEffect(); // 同じGameObjectのVisualEffect。引数で別のEffectも指定可能
int intensity = Shader.PropertyToID("Intensity"); // managed側で一度計算
world.EntityManager.GetBuffer<VfxCommand>(entity).Add(
    VfxCommand.SetFloat(intensity, 10f));
```

Float / Vector3 / Boolの設定、名前IDによるSendEvent、Play、Stopに対応します。
Graph側には一致する型・名前の公開プロパティを用意します。存在しないプロパティへのコマンドは警告して破棄します。
未設定プロパティの自動生成、イベント属性ペイロード、VFX出力イベントの逆方向通知は提供しません。
EffectのTransformも追従させる場合は、Bridgeと同じGameObjectまたはその子に配置してください。

1つのEntityに複数のEffectを設定できます。Prefabの同じGameObjectまたは子へ `VfxBridgeSetup` を複数追加し、それぞれ異なる非負の `TargetId` と `Effect` を指定してください。
既定のIDは0です。IDはEntity内・SDKごとに区別され、VFXのID 1とFMODのID 1を共存させられます。

```csharp
bridge.ConfigureVisualEffect(0, bodyEffect);
bridge.ConfigureVisualEffect(7, weaponEffect);
world.EntityManager.GetBuffer<VfxCommand>(entity).Add(
    VfxCommand.SetFloat(intensity, 10f, targetId: 7));
```

同じIDの明示的な再設定は、その対象だけを置き換えてその対象の未実行コマンドを取り消します。他のIDには影響しません。
Prefab設定のID重複は設定ミスとして記録し、先に登録した対象を維持します。
破棄されたEffectは個別に除外します。内部の `VfxBridgeTarget` は複数対象を保存するBufferへ変更しました。

## FMOD

```csharp
using GameObjectBridge.Entities.Fmod;

bridge.ConfigureFmod(eventReference); // RuntimeManagerで作成し、Bridgeが所有
// または bridge.ConfigureFmod(existingInstance, ownsInstance: false);

var commands = world.EntityManager.GetBuffer<FmodCommand>(entity);
commands.Add(FmodCommand.Play());
commands.Add(FmodCommand.SetVolume(0.5f));
commands.Add(FmodCommand.SetParameter(parameterId, 1f)); // PARAMETER_IDをmanaged側で取得
commands.Add(FmodCommand.Stop(allowFadeout: true));
```

通常のFMOD設定とbankロード、3Dイベント、Studio Listenerが必要です。
位置・方向・速度を同期後のGameObjectから `set3DAttributes` に渡します。速度はWorldのDeltaTimeから計算し、最初の更新とDisabled解除直後は0です。
同じEventInstanceへ `RuntimeManager.AttachInstanceToGameObject` 等による自動位置更新を重ねないでください。
Bridge所有のInstanceはUnbind/Entity破棄後の連携System更新、またはWorld破棄で即時Stop・Releaseします。借用Instanceは停止・解放しません。
FMOD側の失敗は警告し、そのコマンドを消費します。

複数の音には `FmodBridgeSetup` を複数追加し、異なる `TargetId` を指定してください。同じGameObjectへの複数追加にも対応します。
各設定のTransformを3D位置の参照元にするため、武器や足元の子GameObjectへ分けることもできます。

```csharp
bridge.ConfigureFmod(0, engineEvent, bridge.transform);
bridge.ConfigureFmod(7, weaponEvent, weaponTransform);
world.EntityManager.GetBuffer<FmodCommand>(entity).Add(
    FmodCommand.SetVolume(0.5f, targetId: 7));
```

既存Instanceも `ConfigureFmod(7, instance, ownsInstance: false, source: weaponTransform)` で登録できます。
1つのInstanceを複数の対象へ登録することは禁止です。対象ごとに異なるInstanceを用意してください。
各対象は位置・速度・所有権を個別に保持し、同じIDの再設定ではその対象だけを解放・置換します。
Prefab設定のID重複はエラーです。全体の解除・Entity破棄・World破棄では、すべての所有Instanceを後始末します。
`FmodBridgeInstance.TryGetInstance(targetId, out instance)` で対象を取得でき、従来の `Instance` はID 0を返します。

## Burstとコマンド

UnityEngine.ObjectやSDKの操作はメインスレッドで行います。Bridgeの登録やConfigureをBurstから呼ぶことはできません。
`LocalTransform` の更新と、`DynamicBuffer<VfxCommand>` / `DynamicBuffer<FmodCommand>` への要求追加はBurst対応です。

```csharp
// Burst対応SystemのOnUpdate内。構成済みEntityだけが列挙される。
foreach (var commands in SystemAPI.Query<DynamicBuffer<VfxCommand>>())
{
    commands.Add(VfxCommand.SetBool(activePropertyId, true));
}
```

コマンドは記録順に1回実行し、その更新でBufferを空にします。DisabledなEntityでは保留します。
VFX/FMODとも、未登録の対象IDへのコマンドは警告して消費します。
Producerを各連携Systemより前に配置してください。標準のECS依存関係を完了してからSDKを呼びます。
同期時にJob完了を待つ場合があります。登録・構造変更・SDK内部までGC Alloc 0を保証するものではありません。

## サンプルとUnity Editorでの作業

Package ManagerのSamplesから必要なものをImportしてください。

| サンプル | 操作 |
| --- | --- |
| Automatic Views | 表示用Prefabを用意し、空のGameObjectへ `AutomaticBridgeSample` を追加して `ViewPrefab` に指定。Playすると3つのEntityと表示が生成され、4秒後に1つのEntityと表示が消えます。 |
| Transform Bridge | 空のGameObjectへ `TransformBridgeSample` を追加してPlay。CubeとSphereが生成され、逆方向の同期を確認できます。表示にはシーンのCamera/Lightを用意してください。 |
| Cinemachine | Cinemachine 3.1を導入し、空シーンのGameObjectへ `CinemachineBridgeSample` を追加してPlay。サンプル自身がCamera/Brain/追従対象を作ります。 |
| VFX Graph | 対応URP/HDRPとVFX Graph 17.3を設定。**Assets > Create > GameObject Bridge > VFX Sample Graph** でGraphを作成し、`VfxBridgeSample.Graph` に割り当ててPlay。移動するVFXと公開プロパティ値を確認します。生成GraphはSDK標準ループに3つの公開入力を追加したものです。`BridgeFloat` を発生量へ反映するにはGraphでSpawn Rateへ接続してください。 |
| FMOD | FMOD SDK導入・通常の設定・bankロード・Scripting Define追加後、`FmodBridgeSample.Event` へ自身の3Dイベントを指定し、Studio ListenerのあるシーンでPlay。 |

SDK設定やVFX Graph、FMODイベントのInspector割り当ては利用プロジェクト固有のため必要です。

## テスト

導入先のmanifest.jsonの `testables` に `com.seikasan.gameobjectbridge.entities` を追加し、Test Runnerで `GameObjectBridge.Entities` を実行します。
本体・Cinemachine・FMODにEditMode/PlayModeのテストがあります。VFXは実GraphをEditorで生成するEditModeテストです。
FMODテストでは環境変数 `GAMEOBJECT_BRIDGE_FMOD_BANK_DIRECTORY` にローカルの互換bankディレクトリを指定します。未指定では理由付きSkipとなります。
FMODテストは独立したNOSOUND出力のStudio Systemを使い、実ネイティブAPIの状態を検証します。

検証バージョン、測定範囲と未検証事項は [検証記録](Documentation~/Validation.md) を参照してください。
100・1,000・10,000体と通常のGameObjectの比較、および再測定手順は [性能測定](Documentation~/Performance.md) を参照してください。



