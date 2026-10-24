# Entities Extension

Unity 6.3向けのEntities用UPMパッケージ集です。配布リポジトリの`entities-extensions/<Package ID>`にPackageを置き、Package単位で導入できます。

## Package一覧

| Package | Package ID | 主な機能 |
| --- | --- | --- |
| Entities Authoring | `com.seikasan.entities.authoring` | Component定義からAuthoringとBakerを生成 |
| GameObject Bridge for Entities | `com.seikasan.gameobjectbridge.entities` | EntityとGameObjectの関連付け、Transform同期、Cinemachine・VFX Graph・FMOD連携 |
| Input System for Entities | `com.seikasan.inputsystem.entities` | Input ActionsからECS向け入力APIを生成 |
| Instantiation for Entities | `com.seikasan.instantiation.entities` | Entity生成時のTransform・Component初期化 |
| Physics Events for Entities | `com.seikasan.physicsevents.entities` | Unity PhysicsのTrigger・CollisionイベントBuffer |
| Physics Queries for Entities | `com.seikasan.physicsqueries.entities` | Unity PhysicsのRaycast、Cast、Check、Overlap |

各Packageの依存関係、使い方、制約、サンプル、検証範囲は、それぞれの`README.md`と`Documentation~`を参照してください。GameObject Bridgeの任意SDK連携には対象SDKの別途導入が必要です。

## Git URL

Unity Package Managerの **Install package from git URL...** に、導入するPackageのURLを貼り付けてください。

### Entities Authoring

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.entities.authoring
```

### GameObject Bridge for Entities

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.gameobjectbridge.entities
```

### Input System for Entities

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.inputsystem.entities
```

### Instantiation for Entities

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.instantiation.entities
```

### Physics Events for Entities

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.physicsevents.entities
```

### Physics Queries for Entities

```text
https://github.com/seikasan/entities-extensions.git?path=/entities-extensions/com.seikasan.physicsqueries.entities
```

## 6 Packageを一括導入する

`install-all-git-dependencies.json`の`dependencies`を、導入先プロジェクトの`Packages/manifest.json`にある`dependencies`へ追加します。既存の依存関係は残してください。単一Packageだけを使う場合は、上記のURLを個別に使用できます。

## ライセンス

各PackageはMITライセンスです。Packageフォルダー内の`LICENSE.md`を参照してください。
