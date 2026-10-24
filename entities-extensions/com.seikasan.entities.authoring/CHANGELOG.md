# Changelog

## 1.0.0 — 2026-10-24

- `[AutoAuthoring]`で`IBufferElementData`をサポート。複数フィールドのInspector要素リストと`AddBuffer<T>()`のBakingコードを生成します。
- Entity参照・FixedString・FixedListの既存変換ルール、Enableable Bufferに対応。全要素を検証してから追加することで不正なリストの部分Bakingを防ぎます。
- Generator単体テスト63件と、Unity 6000.3.22f1 / Entities 1.5.0でのUnity EditModeテスト25件が成功しました。Bufferサンプルと検証手順を追加しました。


