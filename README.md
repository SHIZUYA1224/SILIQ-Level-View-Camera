# SILIQ Level View Camera

SILIQ Level View Camera は、Unity標準機能だけでプレイヤー目線のサイズ感を確認するためのレベルデザイン補助ツールです。

VRChat SDK、VCC、Udon、VRCSceneDescriptor、VRCPlayerApi などには依存しません。VRChat本体やSDKの挙動を再現するものではなく、ワールド制作中に「この高さ・幅・距離がプレイヤー目線でどう見えるか」を素早く確認するためのツールです。

アバターは不要です。基本はCameraだけを一人称視点として動かします。シーン内にアバターモデルを置く使い方は、見た目の比較や展示確認のための任意オプションです。

## 対象環境

- Unity 2022.3 LTS
- Unity 6 LTS / Unity 6000.x 系
- macOS上のUnity Editor
- Built-in Render Pipeline / URP / HDRP
- VRChat SDKなし
- VCCなし
- Udonなし
- New Input Systemは不要
- Player SettingsがInput System Packageのみのプロジェクトでも動作
- 外部Packageなし

## 導入方法

Unity Package Manager の Git URL 追加で導入します。

1. Unity Editorで導入先プロジェクトを開きます。
2. `Window > Package Manager` を開きます。
3. 左上の `+` ボタンを押します。
4. `Add package from git URL...` を選びます。
5. 以下のURLを入力します。

```text
https://github.com/SHIZUYA1224/SILIQ-Level-View-Camera.git
```

6. Package ManagerのインストールとCompileが完了したら、任意のCameraを選択します。
7. `LevelViewCameraController` をCameraにアタッチします。

PrefabやAssembly Definitionは不要です。
アバターPrefabも不要です。
追加のCameraや一時Rig GameObjectは生成しません。

## Cameraへのアタッチ方法

1. Hierarchyで確認用に使うCameraを選択します。
2. Inspectorで `Add Component` を押します。
3. `Level View Camera Controller` を追加します。
4. 必要に応じて `Height Preset`、身長、移動速度、Collision設定、Gizmo表示を調整します。
5. Play Modeに入ると操作できます。

Edit Mode中は操作せず、Scene View上のGizmo表示だけを行います。

## Editor Navigation

Level View Cameraをシーン内に置きっぱなしにして、視点確認用のブックマークとして使えます。

Cameraを選択するとInspectorに `Editor Navigation` が表示されます。

- `Move Scene View To This Camera`: Scene ViewをこのCamera位置へ移動します。
- `Move This Camera To Scene View`: このCameraを現在のScene View位置へ移動します。

同じ操作は `Tools > SILIQ > Level View Camera` メニューからも実行できます。複数の確認ポイントにCameraを残しておく場合は、Hierarchyで対象Cameraを選んで `Move Scene View To Selected Camera` を使うとすぐその場所へ移動できます。

## Play Game View

Play Mode中にGame Viewを選択中のLevel View Cameraへ切り替えられます。

Cameraを選択するとInspectorに `Play Game View` が表示されます。

- `Activate On Play`: Play開始時にこのCameraをGame View用として有効化します。
- `Disable Other Cameras`: 切り替え時に他のCameraを一時的に無効化します。
- `Active Camera Depth`: Game View用に有効化した時のCamera depthです。
- `Switch Game View To This Camera`: Play中のGame ViewをこのCameraへ切り替えます。
- `Restore Previous Game Cameras`: 切り替え前のCamera状態に戻します。

同じ操作は `Tools > SILIQ > Level View Camera > Switch Game View To Selected Camera` からも実行できます。

## 操作方法

| 操作 | 内容 |
| --- | --- |
| W / A / S / D | 水平移動 |
| Mouse | 視点回転 |
| Space | ジャンプ、Collision OFF時は上昇 |
| Left Shift | 高速移動 |
| Left Ctrl | しゃがみ、Collision OFF時は下降 |
| R | 初期位置と初期回転に戻る |
| Esc | マウスロック解除 |
| Left Click | マウスロック再開 |

視点感度はInspectorの `Look` セクションで調整できます。

- `Mouse Sensitivity`: 視点回転の基本感度
- `Input System Mouse Scale`: Player SettingsがInput System Packageのみの場合のマウスdelta補正
- `Invert Mouse Y`: 上下視点操作の反転

Input System環境で視点が速すぎる場合は、まず `Input System Mouse Scale` を下げてください。標準値は `0.03` です。

入力は、旧Input Managerが有効な場合は `UnityEngine.Input` を使用します。

Player SettingsでInput handlingを `Input System Package (New)` のみにしているプロジェクトでは、`UnityEngine.Input` を呼ばず、利用可能なInput SystemのKeyboard/Mouseを自動で読みます。Input Systemへのコンパイル時依存は持たないため、Input System packageがないプロジェクトでも導入できます。

## 身長と目線高さ

Inspectorでは `Body Height` に身長を入力します。

普通の人間基準として、目線高さは以下で自動計算します。

```text
Eye Height = Body Height - 0.10m
```

例えば `Body Height = 1.70m` の場合、Cameraの目線高さは `1.60m` になります。

`Crouch Body Height` はしゃがみ時の身体の高さです。しゃがみ時の目線も同じ基準で `Crouch Body Height - 0.10m` として計算します。

## 身長プリセット

| Preset | Body Height | Calculated Eye Height |
| --- | ---: | ---: |
| Small Avatar | 1.10m | 1.00m |
| Seated | 1.30m | 1.20m |
| Short Avatar | 1.50m | 1.40m |
| Average VRChat Avatar | 1.70m | 1.60m |
| Tall Avatar | 1.90m | 1.80m |
| Custom | 任意 | Body Height - 0.10m |

`Body Height` は床から頭頂までの高さです。`Eye Height` は床からCameraまでの高さです。Unityでは `1 unit = 1 meter` として扱います。

Presetを選ぶと `Body Height` が自動で更新されます。`Custom` の場合のみ、`Body Height` を自由に入力できます。

## Collision ON / OFF の違い

### Collision ON

`useCollision = true` の場合、Camera位置を基準にした垂直カプセルで物理判定しながら移動します。

- `LevelViewCameraController` をCameraに追加すると、`CharacterController` も同じGameObjectに付きます。
- 追加のCamera、親Rig、一時GameObjectは生成しません。
- Cameraを上下に向けても、衝突判定カプセルはワールドの上下方向に固定されます。
- `Collision Mask` で衝突対象にするLayerを選べます。
- 壁や段差に衝突します。
- 重力と接地判定を使用します。
- Spaceでジャンプします。
- Left Ctrlでしゃがみます。
- カプセルの半径は `0.3m` です。
- カプセルの高さは現在の `Body Height` です。
- カプセルはCameraの目線位置から足元側へ自動オフセットされます。
- Cameraの高さは `Body Height - 0.10m` で計算した `Eye Height` です。

衝突処理は、カメラをピッチ回転してもCollider付きオブジェクトをすり抜けにくくするため、内部ではUnity Physicsのカプセルキャストで行います。Trigger Colliderは無視します。

### Collision OFF

`useCollision = false` の場合、Transform移動で動作します。

- 壁や床を無視して移動できます。
- 重力は使いません。
- Spaceで上昇します。
- Left Ctrlで下降します。
- Shiftで高速移動します。
- シーン全体を素早く空間確認したい場合に向いています。

## VRChat SDKに依存しないこと

このツールは以下を使用しません。

- VRChat SDK
- VCC
- Udon
- VRCSDK
- VRCSceneDescriptor
- VRCPlayerApi
- VRCObjectSync
- VRChat用Prefab
- VRChat用Assembly Definition
- 外部Package

主に使用するAPIは `UnityEngine`、`UnityEditor`、`MonoBehaviour`、`Transform`、`Camera`、`CharacterController`、`Input`、`Gizmos`、`Handles`、`CustomEditor`、`EditorGUILayout` です。

## このツールで確認できること

- 部屋に入った時の目線
- 天井の高さ
- 通路の圧迫感
- 展示物の見え方
- ドアや入口の幅
- 階段や段差の感覚
- 座り視点と立ち視点の違い
- 小さいアバターと高身長アバターの見え方

## このツールで確認できないこと

- VRChat本体と完全に同じ移動挙動
- 実際のVRアバターの手や頭の動き
- VRChat SDKのアップロード可否
- Udonギミック
- ネットワーク同期
- Quest/iOS実機の正確な負荷
- 他ユーザーが入った時の見え方
