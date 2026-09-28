《最后避难所》Unity 技术架构规范
1. 总体结构

推荐：

Assets/
├── Scripts/
│   ├── Core/
│   ├── Systems/
│   ├── Data/
│   ├── UI/
│   ├── Save/
│   └── Debug/
│
├── Resources/
│   └── GameData/
│
└── Scenes/
    └── Main.unity
2. Core
GameManager
GameState
TimeSystem
RandomSystem
CommandSystem
EventBus

只负责：

核心状态与生命周期。

3. Systems
ResourceSystem
CharacterSystem
BuildingSystem
TechnologySystem
ExplorationSystem
FactionSystem
RoguelikeSystem
WeatherSystem
EventSystem
EndingSystem
ItemSystem

一个系统只负责自己的领域。

4. Data

推荐使用：

ScriptableObject 或 JSON 数据。

所有内容定义：

CharacterDefinition
BuildingDefinition
TechnologyDefinition
EventDefinition
RelicDefinition
ItemDefinition
FactionDefinition
LocationDefinition
EndingDefinition
5. Runtime State 与 Definition 分离

例如：

CharacterDefinition

保存：

基础职业
基础特性
基础能力

而：

CharacterState

保存：

当前生命
当前压力
当前经验
当前装备
当前关系
当前状态

禁止修改 Definition 来存储运行状态。

6. 游戏逻辑与 UI 分离

正确：

UI
↓
Command
↓
System
↓
GameState
↓
Event
↓
UI Refresh

错误：

UI Button
↓
直接修改 GameState
7. 时间系统

统一：

Day
TimeSlot

时间推进必须经过：

TimeSystem。

禁止各个系统自行：

day++;
8. 随机系统

所有随机行为通过：

RandomSystem。

禁止各处直接随意创建：

new Random()
9. Seed

每局：

gameSeed

所有重要随机结果由 Seed 派生。

用于：

复现
测试
存档
Bug 调试
10. 事件系统

事件流程：

EventSelector
↓
EventDefinition
↓
ConditionEvaluator
↓
EventUI
↓
Choice
↓
EffectResolver
↓
GameState

事件内容不应该直接写入 UI。

11. Modifier 系统

天气、肉鸽、科技、人物特性都应该能够产生：

Modifier

统一修改：

资源产量
资源消耗
工作效率
探索风险
事件权重
建筑效率

避免：

每个系统各写一套加成逻辑。

12. 存档

推荐：

SaveData

包含：

saveVersion
gameSeed
gameState
history
13. 场景数量

V1 尽量：

一个主场景。

通过 UI 面板切换：

基地
人员
探索
科技
日志
事件
结局

减少场景管理复杂度。

14. 性能

因为游戏主要是：

UI + 数据计算。

性能重点不是 GPU，而是：

UI 重建
事件对象创建
日志数量
频繁刷新
存档序列化

不要每帧刷新所有 UI。

采用：

状态变化后刷新。

15. 代码扩展要求

新增内容优先：

添加 Data。

新增系统才：

添加 System。

禁止因为一个新事件修改：

多个核心系统。