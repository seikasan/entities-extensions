# 検証記録

## 環境

| 項目 | バージョン |
| --- | --- |
| Unity Editor | 6000.3.22f1 |
| Entities | 1.5.0 |
| Burst | 1.8.30 |
| Collections | 2.7.1 |
| Mathematics | 1.3.3 |
| Cinemachine | 3.1.7 |
| VFX Graph / URP | 17.3.0 |
| FMOD for Unity / native SDK | 2.03.20 (`00020314`) |

## 最新のテスト結果

Unity Editorでコンパイルエラー・警告がないことを確認しました。

| 実行 | 成功 | 失敗 | Skip |
| --- | ---: | ---: | ---: |
| EditMode | 51 | 0 | 12 |
| PlayMode（Runtime） | 36 | 0 | 0 |
| PlayMode（連携・サンプル・性能） | 14 | 0 | 0 |

EditModeのSkipはローカルFMOD bankを必要とするテストです。`GAMEOBJECT_BRIDGE_FMOD_BANK_DIRECTORY`が未設定のため実行していません。PlayModeはUnity Test Frameworkの実行不具合を避けるため、Runtime本体とその他の対象を分けて実行し、計50件が成功しました。

## 確認範囲

テストではEntityとGameObjectの自動関連付け、Transform同期、表示用GameObjectの生成・破棄、Prefab差し替え、Disabled状態、World間の独立性、Awake / OnEnableより前の初期化を確認しています。Cinemachine、Visual Effect Graph、FMODの連携、複数のVFX・FMOD対象、Burst producer、サンプル動作、大量Entityの性能測定も対象に含みます。

FMODの実行テストは、登録済みEventInstanceを操作する経路です。通常のRuntimeManagerによるbankロードと可聴出力は確認していません。性能測定の条件・結果は[性能測定](Performance.md)を参照してください。

## 再実行

1. Unity 6000.3とEntities 1.5.0のプロジェクトへPackageを導入します。
2. manifestの`testables`へ`com.seikasan.gameobjectbridge.entities`を追加します。
3. 任意連携を検証する場合は、対応するCinemachine、VFX Graph / URPまたはHDRP、FMODを導入します。
4. FMODではScripting Define `GAMEOBJECT_BRIDGE_FMOD`を設定し、bankを必要とするテストには`GAMEOBJECT_BRIDGE_FMOD_BANK_DIRECTORY`を設定します。
5. Test RunnerでEditMode / PlayModeの`GameObjectBridge.Entities`を実行します。

## 未検証の範囲

スタンドアロンPlayer、IL2CPP、Windows以外の環境は検証していません。FMODの可聴出力、通常RuntimeManagerでのbankロード設定とListenerを含む聴感、VFX描画画像の見た目とGPU性能、Cinemachine SmartUpdate / FixedUpdateの追従品質は対象外です。
