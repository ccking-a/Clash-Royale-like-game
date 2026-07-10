# Mirror 网络化配置说明

本项目已完成 Mirror 网络化改造，以下为 Unity 编辑器中的配置步骤：

## 1. 单位预制体添加 NetworkIdentity

为以下预制体添加 **NetworkIdentity** 组件（若已有 SoldierController，其继承 NetworkBehaviour 会自动要求 NetworkIdentity）：
- `Assets/Prefebs/PlayerSoilder/` 下的所有兵种预制体（amiya, chen, yinhui 等）
- `Assets/Prefebs/AnemySoilder/Anemyamiya` 敌方单位预制体
- `Assets/Prefebs/Scene/` 下的 Myjianzhu、Anemyjianzhu（防御塔）如需要网络同步

## 2. 1v1 玩家圣水（PlayerElixir）

1v1 时双方各自圣水条，放置时只扣自己的圣水：
1. 在 **Player 预制体**（NetworkManager 的 Player Prefab）上添加 **PlayerElixir** 脚本
2. 确保该预制体有 **NetworkIdentity**（Player 预制体通常已有）
3. 圣水恢复由服务器在 PlayerElixir 内自动执行；未使用 Player 预制体时仍可用 NetworkGameState 的全局圣水

## 3. 创建 NetworkGameState 预制体

1. 在场景中创建空 GameObject，命名为 `NetworkGameState`
2. 添加 **NetworkIdentity** 组件
3. 添加 **NetworkGameState** 脚本
4. 将其拖为 Prefab 保存到 `Assets/Prefebs/Scene/` 或 Resources 目录
5. 在 **NetworkGameManager** 的 `Network Game State Prefab` 字段中引用该预制体

## 4. NetworkGameManager 配置

在 NetworkGameManager（或你的 NetworkManager）上：
- **Spawnable Unit Prefabs**：可选，填入需动态生成的单位预制体（已在 NetworkUnitPlacer 中自动从 allUnits 注册）
- **Network Game State Prefab**：填入上述 NetworkGameState 预制体

## 5. NetworkUnitPlacer 配置

确保 NetworkUnitPlacer 的 `allUnits` 已正确配置，脚本会在服务器/客户端启动时自动将带 NetworkIdentity 的预制体注册到 NetworkManager。

## 6. 可选：使用场景中的 NetworkGameState

若希望 NetworkGameState 作为场景对象而非动态生成：
- 在游戏场景中放置带 NetworkIdentity + NetworkGameState 的 GameObject
- 将 NetworkGameManager 的 `networkGameStatePrefab` 留空
- 确保该场景为 NetworkManager 的在线场景

## 7. 启动方式

- **Host（主机）**：`NetworkManager.singleton.StartHost()` —— 同时作为服务器和客户端
- **Server（仅服务器）**：`NetworkManager.singleton.StartServer()`
- **Client（仅客户端）**：`NetworkManager.singleton.StartClient()` 并设置 networkAddress

## 已网络化的功能

- **单位放置**：点击/拖拽仅本地预览，不请求服务器；**松开确认放置**时才发 Command，服务器校验并扣除**该玩家**圣水后 `NetworkServer.Spawn`
- **1v1 圣水**：双方各自圣水条（PlayerElixir 挂 Player 预制体），放置时只扣自己的圣水；无 Player 预制体时仍用 NetworkGameState 全局圣水
- 技能释放：通过 SoldierController.CmdActivateSkill 请求服务器执行
- 敌兵生成：仅在服务器生成，使用 NetworkServer.Spawn 同步
- 游戏结束：服务器调用后通过 ClientRpc 通知所有客户端
