# Input System for Entities

Unity Input Actions Editorの定義から、入力の読み取りに使うECS Componentと収集Systemを自動生成します。
入力の処理は公式Input Systemが担当し、ECS側はその結果のスナップショットを読み取ります。

- Package ID: `com.seikasan.inputsystem.entities`
- Namespace: `InputSystem.Entities`
- 最低対応対象: Unity 6.3 LTS
- Unity 6.6での互換性検証は別途必要です。

## 導入

Package Managerの **Install package from git URL...** で、配布リポジトリURLに`?path=/entities-extensions/com.seikasan.inputsystem.entities`を付けて導入します。バージョンを固定する場合は、URL末尾に`#コミットID`を指定してください。

## サンプル

Package Managerの **Samples > Basic Input** から、KeyboardとGamepadに対応した`.inputactions`とBurst対応SystemをImportできます。手順は[Basic Input](Samples~/BasicInput/README.md)を参照してください。

## 利用方法

`Assets`内で通常どおり`.inputactions`を作成します。定義が変更されると、自動で
`Assets/InputSystem.Entities.Generated`に生成用メタデータ・アセンブリ定義・Asset参照が用意されます。
Roslyn Source GeneratorのDLLは同梱済みです。利用者がGeneratorをビルドする必要はありません。
入力用Component、収集System、シーン上のブリッジ用GameObjectの手動実装・配置は不要です。

Project Settings > Input System PackageでProject-wide Actionsとして指定したAssetは、次の型になります。

```csharp
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Actions = InputSystem.Entities.InputActionAsset;

[BurstCompile]
public partial struct CharacterInputSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Actions>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var actions = SystemAPI.GetSingleton<Actions>();
        float2 direction = actions.Gameplay.Move.ReadValue<float2>();
        if (actions.Gameplay.Jump.WasPressedThisFrame())
        {
            // ゲーム側の処理
        }
    }
}
```

上の`Gameplay`、`Move`、`Jump`は例です。プロパティは実際のMap名・Action名から生成されます。
公式Input Systemと両方の名前空間を使う場合は、例のように型の別名で区別してください。

それ以外のAssetは、`InputSystem.Entities.Generated.<Asset名>.InputActionAsset`として生成されます。
たとえば`VehicleControls.inputactions`なら`InputSystem.Entities.Generated.VehicleControls.InputActionAsset`です。
Project-wide Actionsが未指定でも、各Assetの名前空間から利用できます。

入力Assetは`Assets`とLocal・Embeddedパッケージ内のインポート済み定義から収集されます。
Registryパッケージの内部用Assetは自動収集せず、Project-wide Actionsとして明示された場合だけ含めます。
Asset名が同じ場合や、名前をC#識別子に変換した結果が衝突する場合は、コンパイル診断`ISE002`で通知します。
空白・記号は`_`に変換し、先頭が数字なら`_`を付けます。日本語・C#予約語にも対応します。
生成型の`id`、`FindAction`、内部の`ActionMapN`などと衝突する名前も診断対象です。

生成結果が更新されない場合の再生成メニュー:

**Tools > Input System for Entities > Regenerate**

独自asmdefから使う場合は、`InputSystem.Entities`と`InputSystem.Entities.Generated`への参照を追加してください。

## 対応する読み取りAPI

- `ReadValue<T>()`
- `IsPressed()`
- `WasPressedThisFrame()` / `WasReleasedThisFrame()`
- `WasPerformedThisFrame()` / `WasCompletedThisFrame()`
- `id` / `phase` / `enabled` / `inProgress` / `triggered`

| Expected Control Typeの値型 | ECSで使用できる型 |
|---|---|
| float（Button、Axisなど） | `float` |
| int（Integerなど） | `int` |
| Vector2 | `float2`、`UnityEngine.Vector2` |
| Vector3 | `float3`、`UnityEngine.Vector3` |
| Vector4 | `float4`、`UnityEngine.Vector4` |
| Quaternion | `quaternion`、`UnityEngine.Quaternion` |

Float値を持つActionでは、`ReadValue<bool>()`も使用できます。この場合は数値をboolへ変換せず、収集時点の公式`IsPressed()`と同じ押下判定を返します。押下状態やフレーム内の変化だけを調べる場合は、`IsPressed()`、`WasPressedThisFrame()`なども利用できます。

ButtonでExpected Control Typeが空の場合は`float`です。それ以外の型未指定や、Poseなどの未対応型は診断します。
同じサイズでも`float`を`int`として読み取ることはできません。
型不一致はCollectionsの安全性チェック有効時に例外、無効時は`default(T)`を返します。
ReadValueは公式側のProcessor適用後の値をコピーします。Interactionの判定も公式側の結果を保持します。

## InputActionReferenceとBaking

SubScene内など、Baking対象のGameObjectに**Input Action Reference** Componentを追加し、
`Action`フィールドへ公式`UnityEngine.InputSystem.InputActionReference`を設定します。
追加のBakerは不要です。Asset GUIDとAction GUIDが`InputSystem.Entities.InputActionReference`へ変換されます。

```csharp
var actions = SystemAPI.GetSingleton<Actions>();
var reference = SystemAPI.GetComponent<InputSystem.Entities.InputActionReference>(entity);
var action = actions.FindAction(reference);
float value = action.ReadValue<float>();
```

参照先と同じAssetのスナップショットを使用してください。AssetやActionが一致しない参照は既定値を返し、
返されたActionの`id.IsValid`は`false`になります。
名前変更でGUIDは変わりません。削除して作り直したActionは別IDなので、参照の再設定が必要です。
未設定の参照は無効なComponentとしてBakingし、壊れた参照はBaking時にエラーを通知します。
名前変更後は生成されたメンバー名も変わるため、そのメンバーを直接使うC#コードの変更は必要です。

## 更新と所有権

- 収集は`SimulationSystemGroup`の冒頭、`FixedStepSimulationSystemGroup`より前に実行します。
- `ReadValue`などの読み取りはBurst対応です。公式Assetへのアクセスと収集はmanaged Systemで行います。
- 各Simulation更新の時点で公式Input Systemの最新状態をコピーします。独自に`InputSystem.Update()`は呼びません。
- `ThisFrame`は収集時点の公式Input Systemの判定です。同一スナップショットを読む複数のFixed Stepでは同じ結果になります。
  ECSのFixed Stepごとのイベント履歴や、各ステップへの一度だけの消費を表すものではありません。
- Project-wide Actionsの有効化・Map切替・Rebindingは公式側が管理します。ECSはその状態を読み取ります。
- その他のAssetはWorldごとに複製して全Mapを有効化し、System停止・World破棄時に解放します。
  実行中の元AssetへのRebindingは、それらの複製へ自動反映しません。
- LocalSimulationとClientSimulation Worldが対象です。Server、Baking、Editor Worldでは自動収集しません。
- 生成されるSingletonはAsset全体を含むため、EntitiesのComponentサイズ上限が適用されます。

ECSのスナップショットに対する`Enable()`、`Disable()`、コールバック登録、Rebinding操作は提供しません。
PlayerInputのプレイヤー別Asset複製への自動接続も対象外です。

## 検証

Package ManagerのTestableとしてこのPackageを指定し、Test RunnerのPlayModeから
`InputSystem.Entities.Tests`を実行できます。
入力値とフレームフラグ、Processor・Hold Interaction、Burst Job、定常状態のGC割り当て、
生成Singletonの生成・破棄、参照IDの一致を確認します。

Source Generator単体のテスト:

```powershell
dotnet run --project 'Packages/com.seikasan.inputsystem.entities/SourceGenerator~/Tests/InputSystem.Entities.SourceGenerator.Tests.csproj'
```

Generatorを変更した開発者は`SourceGenerator~/Build.ps1`を実行し、同梱DLLを更新してください。
依存するRoslynはUnity 6.3向けの4.3.0、Generator本体のターゲットは.NET Standard 2.0です。



