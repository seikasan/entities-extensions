# Unity 6.3 検証記録

## Buffer自動AuthoringのUnity Editor検証（2026-10-23）

`[AutoAuthoring]`で`IBufferElementData`を指定すると、シリアライズ可能な要素リストを持つAuthoringと、`AddBuffer<T>()`を使用するBakerを生成します。

- Source Generator単体テスト: **63件成功**。複数フィールド、Entity参照、FixedString／FixedList、Enableable、null要素、未対応型診断、メタデータ生成とスキーマ同期待ちを確認。
- Unity EditModeテスト: **25件成功 / 失敗0 / Skip 0**。InspectorのSerializedObjectでBuffer要素を編集してBakingし、要素順・値・Entity参照・初期Enabledを確認。容量超過時にBuffer全体を追加しないことを確認。
- UnityがPackageとテストアセンブリをコンパイルし、全テストが成功することを確認。
- Generatorのビルドは警告・エラー0件。生成DLLを更新。
- Unity 6.3 EditorでBasic AuthoringサンプルをImportし、AuthoringとBuffer要素リストのInspector表示を目視確認。

## 検証環境

| 項目 | バージョン |
| --- | --- |
| Unity | 6000.3.22f1 |
| Entities | 1.5.0 |
| Collections | 2.7.1 |
| Mathematics | 1.3.3 |
| Test Framework | 1.6.0 |
| Unity同梱Roslyn DLL | 4.300.122.52613 |
| GeneratorのNuGet参照 | Microsoft.CodeAnalysis.CSharp 4.3.0 |
| Generatorターゲット | .NET Standard 2.0 / C# 9 |
| Generatorテスト実行 | .NET SDK 10.0.301 |
| OS | Windows 11 / x64 |
