# Basic Input

1. Package Managerから **Basic Input** サンプルをImportします。
2. `PlayerControls.inputactions`を開き、Input Actions EditorでMoveとJumpのBindingを確認します。
3. `PlayerInputSampleSystem`を実行します。WASDまたは左スティックの値と、SpaceまたはGamepad south buttonの押下状態を`PlayerInputSampleState`へ書き込みます。

Input Actionの定義から型と収集Systemは自動生成されます。手動でSystemを登録したり、Project-wide Actionsへ設定したりする必要はありません。生成結果が見つからない場合は、**Tools > Input System for Entities > Regenerate**を実行してください。
