# Entities Authoring

`IComponentData`または`IBufferElementData`に`[AutoAuthoring]`を付けると、Inspector用のMonoBehaviourとBakerを自動生成するUPMパッケージです。
Componentを`partial`にしたり、Authoring・Baker・生成メニュー呼び出しを手書きしたりする必要はありません。

- Package ID: `com.seikasan.entities.authoring`
- Namespace: `Entities.Authoring`
- 開発・検証環境: Unity 6000.3.22f1 / Entities 1.5.0 / Collections 2.7.1 / Mathematics 1.3.3
- Generator: .NET Standard 2.0 / Microsoft.CodeAnalysis.CSharp 4.3.0 / `IIncrementalGenerator`
- License: MIT

## 使い方

```csharp
using Entities.Authoring;
using Unity.Entities;

[AutoAuthoring(TransformUsageFlags.Dynamic)]
public struct MoveSpeed : IComponentData
{
    public float Value;
}
```

保存してUnityのコンパイルが完了すると、同じフォルダーに`MoveSpeedAuthoring.cs`が生成されます。
初回はAuthoringの生成後にもう一度コンパイルが走り、privateなBakerが結合されます。
InspectorのAdd Componentから`Move Speed Authoring`を追加し、`Value`を設定してください。
通常のEntitiesと同様にSubSceneへ配置すると、指定した値のComponentがBakingされます。
`IBufferElementData`の場合はInspectorに要素のリストを生成し、Baking時に`AddBuffer<T>()`で要素を追加します。

`[AutoAuthoring]`の引数を省略した場合は`TransformUsageFlags.None`です。
既存のComponentには、書き換え可能なpublicフィールドを使用してください。名前は生成先でも維持します。
private・readonlyフィールドや、状態を持つ自動プロパティを黙って無視することはありません。

自分のasmdefを使う場合は、`Entities.Authoring`、`Unity.Entities`、`Unity.Entities.Hybrid`、`Unity.Collections`を参照してください。
数学型を使うアセンブリには`Unity.Mathematics`も必要です。
Assembly-CSharpでは通常、これらの自動参照を利用できます。

## 型変換

| Componentのフィールド型 | Inspector用フィールド | Baking |
| --- | --- | --- |
| byte/sbyte/short/ushort/int/uint/long/ulong/float/double/bool | 同じ型 | コピー |
| 32ビット以下のenum | 同じenum | コピー |
| float2/float3/float4/quaternion | 同じ型 | コピー |
| Entity | GameObject | `GetEntity`、nullなら`Entity.Null` |
| FixedString32/64/128/512/4096Bytes | string | UTF-8容量確認後に変換 |
| FixedList32/64/128/512/4096Bytes&lt;T&gt; | List&lt;T&gt; | Capacity確認後に全要素をコピー |
| フィールドのないTag | フィールドなし | Componentを追加 |
| IEnableableComponent | 上記に初期Enabledチェックを追加 | 追加後に`SetComponentEnabled` |
| IBufferElementData | シリアライズ可能な要素の`List` | `AddBuffer<T>()`へ全要素をコピー |

FixedListの要素は、上表の数値・bool・32ビット以下のenumに限定します。
Entity、FixedString、数学型、独自型などを要素とするFixedListは初期バージョンでは対応していません。

### Entity参照

```csharp
[AutoAuthoring]
public struct Spawner : IComponentData
{
    public Entity Prefab;

    [EntityReferenceFlags(TransformUsageFlags.None)]
    public Entity Target;
}
```

InspectorではPrefab/TargetともGameObject参照です。
参照先のTransformUsageFlagsの既定値はDynamicで、フィールド単位に`EntityReferenceFlags`で変更できます。
Component本体のTransformUsageFlagsとは別の設定です。

### IBufferElementDataの自動Authoring

Buffer要素型にも、そのまま`[AutoAuthoring]`を付けられます。Componentに`partial`は不要です。

```csharp
using Entities.Authoring;
using Unity.Entities;
using Unity.Collections;

[AutoAuthoring]
public struct WaveEnemy : IBufferElementData
{
    public Entity Prefab;
    public float SpawnDelay;
    public FixedString64Bytes Label;
}
```

生成された`WaveEnemyAuthoring`をGameObjectへ追加すると、Inspectorには`Buffer Elements`（内部フィールド名`__EntitiesAuthoringBufferElements`）のリストが表示され、各要素の`Prefab`（GameObject参照）、`SpawnDelay`、`Label`を編集できます。Baking後は通常の`DynamicBuffer<WaveEnemy>`です。

```csharp
var buffer = SystemAPI.GetBuffer<WaveEnemy>(entity);
foreach (var enemy in buffer)
{
    // enemy.Prefab / enemy.SpawnDelay / enemy.Label を使用
}
```

- 複数フィールドのBuffer要素をサポートします。要素の各フィールドには、上記「型変換」と同じ規則を適用します。
- Entity参照は要素単位でGameObjectから変換します。`EntityReferenceFlags`も利用可能です。
- FixedString・FixedListは要素単位で容量を検証します。途中で容量超過やnull要素が見つかった場合は、そのBuffer全体を追加しません。
- リストが空またはnullなら、要素数0のBufferを追加します。`IEnableableComponent`付きBufferはEnabledの初期状態を設定できます。
- データフィールドが1つもないBuffer要素、Managed型、未対応フィールドは診断して生成しません。Bufferを同一Entityへ別のBakerから重複追加するとUnity標準のエラーになります。
- 既存の`IComponentData`のAuthoringは変更しません。変更前に生成されたAuthoringの同名ファイルを独自に編集せず、通常の自動同期で更新してください。

### FixedString・FixedList

FixedStringは文字数ではなくUTF-8のバイト数と実際のCapacityで検証します。
日本語のような複数バイト文字も対象です。容量超過時はBakingエラーを表示し、Component全体を追加しません。
文字列やListを切り詰めて正常扱いにすることはありません。nullの文字列・Listは空として扱います。

### Enableable

初期状態は有効です。生成Authoringに表示される`Enabled`を変更できます。
この追加フィールドには専用名を使い、Componentのフィールドと衝突する場合は別名を選びます。

## 自動同期とファイルの扱い

実ファイルにするのはMonoBehaviourのpartial宣言とprivateなSerializeFieldフィールドです。
BakerはRoslynの生成ソースとしてコンパイルに追加され、ディスクへ重複保存しません。
元Componentの宣言ファイルはGeneratorのメタデータで特定し、構造体の`MonoScript.GetClass()`には依存しません。
Editorがフィールド型を別ルールで推測せず、Generatorが確定した同じ変換結果を使用します。

- フィールド変更後はコンパイル後に自動同期し、差分があるときだけ書き込みます。
- 再コンパイル前の古いAuthoringに新しいフィールドがない場合、完全なスキーマになるまでBakerを生成しません。
  一時的な不一致でC#エラーになったり、不完全なComponentをBakingしたりすることを防ぎます。
- 生成ファイルには所有情報と内容ハッシュを記録します。手書きの同名ファイルや手動編集を上書きしません。
- `.meta`を作り直したり削除したりしません。生成された`.cs`と`.meta`は一緒にバージョン管理してください。
- Componentが削除されたり属性が外されたりしても、既存Authoringを自動削除しません。Prefabなどの参照を確認してから整理してください。
- namespaceや同一アセンブリ内でのソース移動では、元ソースのGUIDで所有ファイルを追跡します。
- AuthoringとComponentは同じアセンブリに属する必要があります。asmdefを跨いだpartial宣言を生成しません。
- Registry/Git/Built-inのPackage Cacheへは書き込みません。生成・更新が必要な場合はEmbeddedまたは書き込み可能なLocal Packageにしてください。
  配布済み生成ファイルが一致している場合は書き込みなしで利用できます。

生成ファイルを手動編集すると、その後の自動更新は停止して診断を出します。
変更を別ファイルへ退避し、生成時のファイルをバージョン管理から復元してから同期してください。
フィールド削除・型変更に伴う既存のInspector値の移行は、Unityの通常のシリアライズ規則に従います。

## 未対応・制約

- Managed IComponentData、ICleanupComponentData、データフィールドを持たないIBufferElementData、ジェネリックComponent、アクセス不能な入れ子型。
- BlobAssetReference、unsafe fixed配列、独自構造体、配列、decimal、char、64ビットenumなど、対応表にないフィールド型。
- 特殊なカスタムBaking。必要な場合は属性を外し、通常のAuthoring/Bakerを実装してください。
- Authoringクラス名、必須の入れ子型名`Baker`と同じフィールド名はC#上衝突するため診断します。
- 手書きの同名Authoringクラスとの併用、アセンブリを移動するリファクタリングの自動解決。

未対応フィールドが1つでもあるComponentは、全体をエラーとして扱います。
元Componentのフィールドを無視して成功扱いにはしません。

## 診断

| ID | 内容 |
| --- | --- |
| AEA001 | 自動生成できないComponent定義 |
| AEA002 | 未対応・書き込めないフィールド |
| AEA003 | クラス名・アセンブリ・生成先の衝突 |
| AEA004 | 同期待ち、所有情報の不一致、手動編集 |
| AEA005 | 検証済みシリアライズ型ではないフィールド |
| AEA006 | 読み取り専用または特定不能な生成先 |
| AEA007 | 不正な属性・フラグ指定・自動プロパティ |
| AEA008 | Baking時のFixedString/FixedList容量超過 |

AEA004の一時的なスキーマ同期待ちはRoslynの情報診断です。
手動編集・ファイル所有の衝突・書き込み失敗はEditorのConsoleへ原因と対処を表示します。

## 導入とサンプル

Git URLで導入する場合は、次のURLをUnity Package Managerへ貼り付けてください。

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.entities.authoring#main
```

**Samples > Basic Authoring > Import**で、MoveSpeed・Spawner・PlayerTag・WaveEnemy（Buffer）の例を導入できます。
インポート後の自動コンパイルを待ち、生成された各AuthoringをGameObjectへ追加してSubSceneに配置してください。
ドットネットSDKやGeneratorの手動ビルドは、利用者には不要です。

## 開発とテスト

Generatorのソースは`Tools~/SourceGenerator`にあります。Unity 6.3の同梱Roslyn 4.3系に合わせています。
変更した場合は`Build.ps1`を実行し、`Analyzers/Entities.Authoring.Generator.dll`を更新してください。
DLLはRoslynAnalyzerラベル付きで、通常のプラグインとしては読み込まない設定です。

```text
dotnet run --project Tools~/SourceGenerator/Tests/Entities.Authoring.Generator.Tests.csproj --configuration Release
```

UnityテストはTest RunnerのEditModeで`Entities.Authoring.Tests.Editor`を実行します。
Local/Git Packageのテストを有効にする場合は、manifestの`testables`へPackage IDを追加してください。
テスト用Componentとその生成Authoringも同梱しており、通常利用のComponentには追加されません。

Buffer対応後のGenerator単体テストは**63件成功**し、Unity 6.3.22f1 / Entities 1.5.0のUnity Editor EditModeテストも**25件すべて成功**しました。InspectorのSerializedObject編集からBufferをBakingする経路、容量超過時にBuffer全体を追加しない動作、Entity参照、Enableable状態を確認しています。Unity EditorでBasic AuthoringサンプルをImportし、生成AuthoringとBuffer要素リストのInspector表示も目視確認済みです。
実際に確認した範囲と未検証項目は[検証記録](Documentation~/Validation.md)を参照してください。



