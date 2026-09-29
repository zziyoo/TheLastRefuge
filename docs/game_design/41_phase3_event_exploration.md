# 《最后避难所》Phase 3 设计：事件系统 + 最小可玩探索 + JSON 内容基础设施

## 0. 文档目的与范围决策

本文件是 Phase 3 的内容与实现设计，依据：

- `30_feature_dependency.md`：第三层主动玩法 ⑥ EventSystem ⑦ ExplorationSystem
- `29_mvp_scope.md`：MVP 范围冻结（本阶段只做少量垂直切片，不做大规模内容生产）
- `31_gameplay_state_machine.md`：Event / Resolution 状态与强制暂停规则
- `33_content_database_schema.md`：Definition / State 分离、ID 规则、内容验证
- `05/13/14/15`：事件与探索的权威内容设计

已确认的两项范围决策：

1. **内容用 JSON 存储**：代码注册表只承担规则与系统，不再作为 Phase 3 普通事件与地点的内容存储方式。
2. **系统全量、内容切片**：完整 EventSystem + 最小可玩 ExplorationSystem + 数据加载/校验基础设施；
   只做少量垂直切片内容（约 8 个事件、3 个地点），**不做** 100+ 事件、34 地点的大规模内容生产。

---

## 1. 当前基线（commit e3300e5）

已实现并全绿（EditMode 48 / PlayMode 5）：

| 层 | 已完成 |
|---|---|
| Core | GameState、TimeSystem、RandomSystem（含权重与 context 种子）、SaveSystem（原子写 + .bak 回退）、EventBus、EffectResolver（22/27 EffectType） |
| 经营 | ResourceSystem、BuildingSystem（含 scout_station 侦察站）、CharacterSystem（含 KillCharacter 剥离链路） |
| 状态 | GameplayState 枚举已有 Event / Resolution；GameplayStateChangeEvent 已发布 |
| UI | 主界面、人员/建筑/资源/日志/存档面板、底栏按钮模式（UISetup 程序化构建） |

本阶段依赖的现成缺口：

- `Assets/GameData/{Events,Locations}`：只有空目录占位，无加载器、无内容
- `EventStartedEvent` / `EventFinishedEvent` 结构体已定义但无人发布
- `GameState.eventHistory`、`gameFlags`、`worldState.discoveredLocations` 字段已存在但只有 saveSystem 初始化
- EffectResolver 未实现 5 个类型（见第 9 节）

---

## 2. 【阻断问题 M0】gameFlags 无法通过存档（已实证）

### 2.1 证据

用临时探针测试验证（已清理）：

```csharp
var flags = new GameFlags();
flags.intFlags["event_cd_x"] = 5;
string json = JsonUtility.ToJson(flags, true);   // 结果: "{}"
```

- `GameFlags` 的 4 个 Dictionary 全部被 JsonUtility 静默丢弃 → 存档/读档后全部丢失
- 同样验证：`CharacterState.relationships`（Dictionary）在存档后触发 `KeyNotFoundException`

### 2.2 影响

- **EventSystem 必须依赖 gameFlags**：事件冷却（`event_cd_<id>`）、事件链排程（`event_due_<id>`）、
  事件记忆（`met_stranger`、`spared_rats` 等世界记忆，`05_event_and_exploration.md` §12）
- 不修复则：所有事件记忆与冷却在读档后清零，事件链断裂 —— 读档回归测试必红
- `CharacterState.relationships` 目前**无任何生产代码读取**（真实关系走 `GameState.relationships`
  的 `RelationshipState[]`，可正常序列化）→ 标记为遗留字段，本阶段不得在其上构建功能

### 2.3 修复方案

`GameFlags` 实现 `ISerializationCallbackReceiver`：

- `OnBeforeSerialize`：把 4 个字典扁平化到可序列化的 `FlagEntry[] { key, type, value }` 数组字段
- `OnAfterDeserialize`：从数组重建字典
- 调用方（EffectResolver、EventSystem、条件系统）API 完全不变，仍是字典
- SaveSystem / SaveData 结构不变；新增的数组字段是 **additive**，旧存档读入时数组为空 = 现状，不需迁移

### 2.4 验收

- 回归测试 `SaveLoad_PreservesGameFlags`：写入 4 类 flag → SaveGame → LoadGame → 4 类 flag 全部还原
- 修复由探针测试转正：`GameFlags_JsonUtility_RoundTrip`、`CharacterRelationships` 遗留字段加注释说明不用

---

## 3. JSON 内容基础设施

### 3.1 目录与加载路径

现状 `Assets/GameData/` 不在 Resources 下，`Resources.Load` 读不到；StreamingAssets 在 Android
需要 UnityWebRequest，增加平台分支。

**方案：把内容目录迁移到 `Assets/Resources/GameData/`**（空目录移动，meta GUID 保留）：

```
Assets/Resources/GameData/
├── Events/    event_food_storage_01.json ...（一事件一文件）
├── Locations/ location_forest.json ...
```

- 加载：`Resources.LoadAll<TextAsset>("GameData/Events")`
- 编辑器、PC、Android APK 行为一致，无平台分支
- 一事件一文件：git diff 友好、策划可并行编辑
- 顶层必须是包装对象（JsonUtility 不支持顶层数组）：`{ "event": { ... } }`

### 3.2 JSON Schema（对齐 33 文档 §8/§9）

Effect 与 Condition 都用"单类 + type 枚举 + 通用字段"模式（与现有 `Effect` 一致，JsonUtility 无多态）。

```json
{
  "event": {
    "id": "event_food_storage_01",
    "name": "粮仓里的老鼠",
    "type": "Normal",
    "phase": "Evening",
    "weight": 10,
    "cooldownDays": 3,
    "minDay": 1,
    "maxDay": 15,
    "conditions": [
      { "type": "ResourceAtLeast", "resource": "Food", "amount": 20 }
    ],
    "options": [
      {
        "id": "trap",
        "text": "捕杀",
        "conditions": [],
        "effects": [
          { "type": "RemoveResource", "resourceType": "Food", "intValue": 3 }
        ]
      },
      {
        "id": "poison",
        "text": "下毒（消耗药品）",
        "conditions": [ { "type": "ResourceAtLeast", "resource": "Medicine", "amount": 1 } ],
        "effects": [
          { "type": "RemoveResource", "resourceType": "Medicine", "intValue": 1 },
          { "type": "SetFlag", "targetId": "spared_rats", "stringValue": "true" }
        ]
      },
      {
        "id": "ignore",
        "text": "不管",
        "conditions": [],
        "effects": [ { "type": "RemoveResource", "resourceType": "Food", "intValue": 10 } ]
      }
    ],
    "followUpEvents": [ { "id": "event_food_storage_02", "delayDays": 4 } ],
    "tags": [ "resource" ]
  }
}
```

```json
{
  "location": {
    "id": "location_forest",
    "name": "森林",
    "tier": 1,
    "unlockDay": 1,
    "distance": 2,
    "danger": 1,
    "baseYield": [ { "resource": "Wood", "min": 8, "max": 15 } ],
    "eventPool": [ "event_forest_01", "event_scrap_01", "event_forest_rain" ],
    "prerequisites": [],
    "tags": [ "scavenge" ]
  }
}
```

字段说明：

- `EventDefinition.type`：`Normal | Character | Exploration | Crisis`
- `phase`：普通事件的滚动窗口（MVP 只实现 `Evening`；字段保留 33 文档的完整枚举）
- `cooldownDays`：冷却存入 `gameFlags.intFlags["event_cd_" + id]`（依赖 M0 修复）
- `followUpEvents[].delayDays`：排程写 `gameFlags.intFlags["event_due_" + id] = day + delayDays`，
  每日 Morning 检查到期 → 直接启动（**不受 weight 影响的定向链**）
- `conditions` / `options[].conditions`：见 3.3
- ID 全部 `lower_snake_case`（33 文档 §13），文件名 = ID + `.json`
- 效果目标占位符（M4 实现）：character/building 定向效果的 `targetId` 支持
  `@random`（随机存活角色，经 RandomSystem 抽取）与 `@building:<definitionId>`
  （该 definitionId 的第一个实例）。解析发生在 `ResolveChoice` 应用效果之前，
  找不到目标时该效果返回失败（记入 effectLog，不影响其余效果）

### 3.3 Condition 类型（MVP 实现集）

| 类型 | 字段 | 用途 |
|---|---|---|
| ResourceAtLeast / ResourceBelow | resource, amount | 资源门槛（粮仓老鼠：食物 > 20） |
| FlagIs / FlagNot | targetId, stringValue | 事件记忆与互斥 |
| HasTrait / HasProfession | targetId | 人物事件条件（有医生 → 医疗事件） |
| DayBetween | intValue(起), floatValue(止) | 阶段门槛（危机第 3 天起） |
| LocationDiscovered | targetId | 探索事件与地点前置 |
| BuildingExists | targetId | 农场害虫要求农田存在 |

校验器只接受**已实现**的类型；`WeatherIs` 等未来类型出现在 JSON 中 → 校验报错（防止静默失效）。

### 3.4 加载器与校验器

新增 `Core/IContentDatabase.cs` + `Data/ContentLoader.cs`：

- `ContentLoader.LoadAll()`：Resources 加载全部 Events/Locations → 解析 → 注册进 `ContentDatabase`
  （`Dictionary<string, EventDefinition>` / `LocationDefinition`）→ 返回诊断报告
- **开发环境 fail-fast**：任何解析失败 `Debug.LogError`，且 EditMode 内容校验测试直接红
  （33 文档 §14：不存在的引用必须报错）
- **运行环境降级**：单条内容损坏 → 跳过该条 + LogError，绝不让进行中的存档崩溃
- 校验项（33 文档 §16）：
  1. ID 唯一（跨文件查重）
  2. 引用存在：`followUpEvents`、`eventPool`、`prerequisites`、effects 的 targetId
  3. 枚举合法（含"实现了没有"）
  4. 必填字段非空：id/name/options ≥ 1、每个 option ≥ 1 effect
  5. 数值合法：weight > 0、cooldownDays ≥ 0、amount ≥ 0、minDay ≤ maxDay
  6. 循环引用：followUpEvents 不得成环
- `contentVersion` 写入 SaveData（现有字段），由校验报告给出

### 3.5 已解决：枚举解析（M1 探针结论）

探针测试证实：**JsonUtility 不解析枚举字符串**（`"type": "Crisis"` 会落回默认值），
包装对象字段 `{"event": {...}}` 映射正常。

实现结论：ContentLoader 用私有 Raw DTO（枚举字段全部是 string）承载线格式，
严格 `Enum.TryParse` + `IsDefined` 转换后生成带枚举的 Definition 类；
**线格式规则：JSON 键名 = C# 字段名，枚举值 = 枚举名**（如 `resourceType`/`intValue`）。
未知枚举值在校验期报错，不会静默变成 0 号成员。

---

## 4. EventSystem 设计

### 4.1 接口（输入 / 处理 / 输出 / 通知，30 文档 §12）

新增 `Core/IEventSystem.cs` + `Systems/EventSystem.cs`：

```text
输入：GameState（resources/characters/eventHistory/gameFlags/gameplayState）、
     ContentDatabase（EventDefinition）、IRandomSystem、EffectResolver、ITimeSystem
处理：每日滚动、条件过滤、权重抽选、冷却、选项校验、Effect 结算、历史记录、链式排程
输出：GameState（gameplayState、eventHistory、gameFlags、资源/角色/建筑变化）
通知：EventBus → EventStartedEvent / EventFinishedEvent / GameplayStateChangeEvent
```

### 4.2 触发时机

| 类型 | 触发 |
|---|---|
| Normal | 每天 Action → Evening 推进时滚动一次，成功则当天不再滚（`gameFlags` 当日标记） |
| Character | 同一每日滚动，但来自人物池（条件多为 HasTrait/压力/关系） |
| Crisis | 每日滚动，`minDay` 门槛（MVP 切片：第 3 天起才可能） |
| Exploration | **不由每日滚动**：ExplorationSystem 在抵达地点时从 `location.eventPool` 抽取后直接 `StartEvent` |

滚动被以下情况跳过：`gameplayState` 已是 Event/Resolution、事件冷却未到、当天已出过事件。

### 4.3 结算流程（对齐 31 文档 §7）

```text
Action→Evening 推进
  ↓ 条件/权重命中
SetGameplayState(Event) + publish EventStartedEvent   ← UI 弹事件模态，底栏禁用
  ↓ 玩家点选项（选项校验：conditions 不满足 → 按钮禁用，不发请求）
SetGameplayState(Resolution)
  ↓ EffectResolver.ResolveAll(option.effects)
  ↓ eventHistory.Append { eventId, day, timeSlot, choiceId, effects[] }
  ↓ followUpEvents → gameFlags 排程；cooldown → gameFlags 写入
publish EventFinishedEvent → 恢复到所在时段的状态（UpdateGameplayState）
  ↓
继续 Evening → Night → ...
```

- Effect 单条失败：继续执行其余 effect（沿用 `ResolveAll` 现语义），记录日志，事件历史照常写入
- 非法状态处理（31 文档 §17）：Event 状态下 `AdvanceTimeSlot`、建造、分配、探索出发
  → **拒绝命令、零状态变更**（沿用 Phase 2 的拒绝语义）

### 4.4 存档规则

- **不新增 pendingEvent 字段**：选项结算发生在单次交互内；Event 状态下 UI 禁用存档按钮
- 兜底：`CommitAutosaveBeforeExit`（手机切后台/退出）写档前若状态是 Event/Resolution，
  先归一到所在时段状态再写 —— 强杀时未决事件丢弃，读档后可按权重重新触发（已知可接受行为）
- 依赖 M0：cooldown / 链排程 / 记忆全部走 gameFlags，必须随存档存活
- 结论：**SaveVersion 不变**（新字段全部 additive）；新增回归测试见第 10 节

### 4.5 依赖方向（30 文档 §13）

```text
GameManager → EventSystem → { IContentDatabase, IResourceSystem, ICharacterSystem,
                               IRandomSystem, ITimeSystem, EffectResolver }
EventSystem → EventBus（单向发布）
禁止：EffectResolver / ResourceSystem 等反向引用 EventSystem
ExplorationSystem → IEventSystem（单向），EventSystem 不知探索存在
```

---

## 5. ExplorationSystem 设计（最小可玩）

### 5.1 接口

新增 `Core/IExplorationSystem.cs` + `Systems/ExplorationSystem.cs`：

```text
输入：LocationDefinition、GameState（worldState/characters）、ContentDatabase（事件池）、
     IRandomSystem、IEventSystem、IResourceSystem
处理：出发校验、编队、时间成本推进、地点事件抽取、产出结算、伤害/死亡、地点发现
输出：worldState.discoveredLocations、characters（locationId / health / alive）、资源、eventHistory
通知：EventBus → 新增 ExplorationStartedEvent / ExplorationFinishedEvent
```

### 5.2 流程（31 文档 §12 子状态的 MVP 简化）

```text
Action 时段 · 探索面板
  ↓ 选地点（已发现或解锁日到达）+ 选 1~4 人（0 人 → 拒绝）
出发校验（不满足 → 拒绝、零状态变更）:
  - gameplayState == Action 且无未决事件
  - day ≥ location.unlockDay
  - 口粮：teamSize × distance ÷ 2 食物，不足 → 拒绝
  ↓ 扣口粮；队员 locationId = 地点；publish ExplorationStartedEvent
抽取地点事件（eventPool → 权重 → EventSystem.StartEvent，模态结算）
  ↓ 结算完成
baseYield 随机产出（min..max，RandomSystem）+ 伤害风险
  （危险度 × 队伍平均 探索/战斗 能力 → 伤害概率；致命 → CharacterSystem.KillCharacter，
   沿用现成的剥离工作/口粮链路）
  ↓ 首次抵达 → worldState.discoveredLocations += id
  ↓ 清空 locationId；publish ExplorationFinishedEvent（含收益与伤亡）
GameManager.AdvanceTimeSlot() × distance   ← 时间成本 = 地点距离（2/4/6，15 文档 §5）
```

- **时间成本走正常推进路径**：每次推进都经过 `AdvanceTimeSlot` 的全部钩子（每日事件滚动、
  DayEnd、自动存档），"一次点击 = 一个时间槽"的 Phase 2 不变式不被破坏 —— 变化由玩家命令驱动
- 结果类型 MVP 范围：资源、伤害、发现新地点、死亡；不做失联/负重/装备（ItemSystem 属 Phase 4）
- 地图：MVP 固定拓扑 3 地点（不做 seed 生成；15 文档 §2 的生成式地图属后续阶段）

### 5.3 地点持久化

- 发现：`worldState.discoveredLocations`（已有，可序列化）
- 地点永久状态（15 文档 §11，如"矿区已开采"）：`gameFlags.intFlags["loc_<id>_<key>"]`（依赖 M0）
- MVP 切片只做发现；开采衰减等留到内容扩展阶段

---

## 6. UI 设计

沿用 UISetup 程序化构建模式，不引入预制件依赖：

1. **EventPanel**（全屏模态，GamePanel 子面板）
   - 标题 + 描述 + 选项按钮列表；选项 conditions 不满足 → 按钮禁用 + 灰显原因
   - 选择后显示结算摘要（资源/伤害变化），点"继续"关闭
2. **ExplorationPanel**（底栏新增"探索"按钮）
   - 地点列表：名称、危险（★）、距离（时间成本）、发现状态、锁定原因（未达解锁日）
   - 队伍选择：存活角色 1~4 多选，预览口粮消耗与大致风险
   - 出发按钮 → 校验失败显示原因（拒绝语义不进代码层 LogError）
3. **状态门控**（31 文档 §16/§17，沿用 Phase 2 RefreshButtons 模式）
   - `gameplayState ∈ {Event, Resolution}`：禁用 下一阶段/人员/建筑/资源/探索/存档，
     EventPanel 强制置顶
   - UI 只读状态、只发命令，不直接改 GameState（30 文档 §8）

---

## 7. 垂直切片内容（约 8 事件 + 3 地点）

目标：覆盖全部触发类型、主要 Condition 类型、已支持的 Effect 类型与一条链式后续，
为后续内容生产验证完整管线；**不是** MVP 38 事件的完成。

| 类型 | 数量 | 条目（源：13/14 文档） |
|---|---|---|
| Normal | 3 | event_food_storage_01 粮仓里的老鼠、event_building_01 农场害虫、event_building_02 发电机异响（有工程师则免损） |
| Character | 1 | event_character_02 生日（食物充足 → 压力 -10） |
| Crisis | 1 | event_crisis_01 野兽袭击（minDay=3，伤人/损粮，多选项） |
| Exploration | 3 | event_forest_01 意外丰收、event_mine_01 浅层铁矿、event_scrap_01 废弃零件（工程角色加成） |
| 后续链 | 1 | event_food_storage_01 → event_food_storage_02 发霉的食物（delayDays=4） |

地点 3 个：

| id | 解锁 | 距离 | 危险 | 事件池 |
|---|---|---|---|---|
| location_forest 森林 | Day 1 | 2 | ★ | event_forest_01, event_scrap_01 |
| location_cabin 废弃小屋 | Day 1 | 2 | ★ | event_scrap_01, event_forest_01（复用验证池机制） |
| location_mine 矿区 | Day 6 | 4 | ★★ | event_mine_01, event_scrap_01 |

---

## 8. Effect 缺口

| EffectType | 状态 | 本阶段 |
|---|---|---|
| StartEvent | 未实现（default → LogWarning） | **实现**：事件链立即触发（探索事件进入由 StartEvent 承接） |
| UnlockLocation | 未实现 | **实现**：写 `worldState.discoveredLocations` |
| AddModifier / RemoveModifier | 未实现 | 不实现（Modifier 体系属第 4 层，届时连同数据结构一起设计） |
| UnlockTechnology | 未实现 | 不实现（TechnologySystem 属 Phase 4+） |

- 校验器只放行已实现类型 → 切片内容不会引用空 Effect
- 未实现类型被引用时：Resolve 返回 false + LogWarning（现行为保留，测试覆盖）

---

## 9. 测试计划

### M0
- `GameFlags_SaveRoundTrip`：4 类 flag 存读还原
- 现有 48 + 5 全绿不回退

### M1（内容基础设施）
- `Content_AllFilesParseAndValidate`：全部 JSON 解析、ID 唯一、引用可解析、枚举合法、数值合法
- `Content_InvalidContentFailsValidation`：构造坏引用/重复 ID/非法枚举 → 校验器报错
- `Content_EnumStringParse`：枚举字符串解析行为探针（3.5）
- `Content_MissingFileDegradesGracefully`：单条损坏 → 跳过 + 诊断，不抛异常

### M2（EventSystem）
- 每日只滚一次；Event 状态下 `AdvanceTimeSlot` 被拒且零状态变更
- 条件不满足 → 不触发；冷却期内不触发
- **冷却与记忆跨存档存活**（M0 回归的实战验证）
- 权重确定性：同 seed 同 context 抽选一致（RandomSystem 规范）
- 选项条件不满足 → 拒绝结算；满足 → Effect 落账 + eventHistory 写入 + 状态恢复
- followUpEvents 到期触发，不受权重影响
- 强制退出归一：Event 状态写档 → 读档回到合法时段状态

### M3（ExplorationSystem）
- 0 人 / 未解锁 / 口粮不足 / 非 Action 时段 → 拒绝且零状态变更
- 出发扣口粮、locationId 设置与清理
- 时间成本精确：distance=2 从 Action 推进 2 槽
- 收益落账、发现地点写入 worldState 并跨存档存活
- 致死伤害 → KillCharacter 剥离链路 + 幸存者口粮正确（复用既有断言模式）

### M4（切片内容 + UI 验收）
- PlayMode 真场景：触发事件 → 模态 → 选项 → 资源变化 → "下一阶段"恢复可用
  （`EventModal_ShowsChoicesSettlesSummaryAndUnblocks`，due 强制 `event_food_storage_01`）
- PlayMode 真场景：探索面板 → 出发 → 结算 → 日志与资源/时间推进正确
  （`ExplorationFlow_DepartsSettlesAndAdvances`，自建空池地点保证确定性；
  断言含 Action 槽每日消耗：口粮 -1 + 消耗 -demand）
- 切片内容：`Content_SliceAllFilesParseAndValidate`（9 事件 / 3 地点 / 0 诊断 / 引用可达）
- 全量回归：EditMode 89 + PlayMode 7 双平台全绿（M4b 交付时实测）

---

## 10. 存档与兼容汇总

| 项 | 结论 |
|---|---|
| saveVersion | **不升**：所有新增字段 additive（GameFlags 扁平数组、worldState.discoveredLocations 已有） |
| contentVersion | 内容校验报告维护；切片内容上线时递增 |
| 旧档读入 | flag 数组缺失 → 空字典 = 现状，无迁移代码 |
| 新增回归 | GameFlags 还原、eventHistory 还原、discoveredLocations 还原、Event 状态写档归一 |

---

## 11. 里程碑（每个独立提交，提交前双平台测试全绿）

| 里程碑 | 内容 | 交付判定 |
|---|---|---|
| M0 | GameFlags 序列化修复 + 回归测试 | 存读 4 类 flag 还原；48+5 全绿 |
| M1 | 目录迁移到 Resources、ContentLoader、校验器、枚举探针 | 内容校验测试绿；坏内容能报错 |
| M2 | EventSystem + 状态机集成 + 门控 + StartEvent/UnlockLocation | M2 测试组绿；事件可从测试触发并结算 |
| M3 | ExplorationSystem + 时间成本 + 发现持久化 | M3 测试组绿 |
| M4 | 8 事件 + 3 地点 JSON、EventPanel、ExplorationPanel、PlayMode 验收 | M4 验收绿；人工冒烟一轮 |

---

## 12. 本阶段明确不做（按 29 文档冻结 + 本次范围决策）

- 100+/38 个事件与 34 地点的大规模内容生产（切片 8+3，MVP 38 事件属后续内容阶段）
- ItemSystem / 装备 / 负重、TechnologySystem、RoguelikeSystem、WeatherSystem、FactionSystem、EndingSystem
- 地图 seed 生成（固定拓扑）、失联/多日远征、探索跨多天的补给管理
- Modifier 体系（AddModifier/RemoveModifier）、JSON 之外的新内容形式

---

## 13. 已发现问题报告（15 条规范要求，不得隐藏）

1. **gameFlags 存档丢失（已实证，M0 必修）**：JsonUtility 丢弃全部 Dictionary —— 已列为 M0
2. **`CharacterState.relationships` 为死字段**：无生产代码读取，序列化同样丢失 —— 标注不用，
   是否删除留到内容扩展阶段统一处理
3. **EffectResolver 5 个类型未实现**：本阶段实现 2 个（StartEvent、UnlockLocation），
   其余 3 个明确延后（第 8 节）
4. **JsonUtility 枚举字符串解析行为未验证**：M1 首个探针（3.5）
5. **强制退出期间的未决事件会丢失**：归一写档处理，行为已知且可接受（4.4）
6. **`Effect.AddResource/RemoveResource` 测试工厂漏设 `resourceType`**（M4a 发现）：
   工厂方法只填 `intValue`，不补类型则效果落账为未知资源 —— 生产路径
   （JSON → ContentLoader）正常，仅测试代码受影响；未修，留作测试工具债务
7. **PlayMode `AcceptanceFlow_RunsInsidePlayMode` 食物断言 flaky**（M3 起偶发）：
   `NewGame()` seed=null → 随机内容导致产出波动，复现过一次失败；未修，
   需固定 seed 或改写断言，独立任务处理
8. **探索拒绝原因为英文**：`ExplorationSystem.Validate` 的 reason 全英文
   （"team must contain..."），直接显示在中文 `explorationInfoText`，语言不一致；
   功能正确，留到 UI 文案整理
9. **UI 队伍选择曾可多选超 4 人**（M4c 已修）：系统层 `Validate` 本就拒绝
   >4 人，UI 现已在 `OnToggleTeamMember` 截断上限

---

## 14. 待用户确认的开放项

1. 内容目录迁到 `Assets/Resources/GameData/`（3.1）—— 若坚持保留 `Assets/GameData`，
   需改为 StreamingAssets + Android UnityWebRequest 分支，成本更高
2. 探索时间成本模型（5.2）：distance 直接等于推进的时间槽数（2/4/6）——
   这是把 01 文档"时间成本表"落地到现有 6 槽时间架构的最直接方式，需确认
