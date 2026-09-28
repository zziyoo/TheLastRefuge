《最后避难所》功能依赖关系
1. 总体原则

系统必须按照依赖关系开发。

不能在核心状态系统未稳定前，让 Agent 大量开发内容层。

2. 第一层：Core

基础依赖：

GameState
TimeSystem
RandomSystem
SaveSystem
EventBus

这些不依赖具体内容。

3. 第二层：基础经营

依赖 Core：

ResourceSystem
BuildingSystem
CharacterSystem

形成：

时间
↓
角色
↓
工作
↓
建筑
↓
资源
4. 第三层：主动玩法

依赖：

CharacterSystem
ResourceSystem
TimeSystem

开发：

ExplorationSystem
EventSystem
ItemSystem
5. 第四层：发展系统

依赖：

Resource
Building
Character
Event

开发：

TechnologySystem
RoguelikeSystem
WeatherSystem
6. 第五层：社会系统

依赖：

Character
Event
Resource
Exploration

开发：

RelationshipSystem
FactionSystem
7. 第六层：终局

依赖全部主要系统：

EndingSystem
MetaProgressionSystem
8. UI 依赖

UI 不应该成为其他系统的底层依赖。

正确：

System
↓
GameState
↓
Event / Notification
↓
UI

错误：

System
↓
寻找某个 UI Button
↓
修改状态
9. 推荐开发顺序
① Core

② ResourceSystem
③ CharacterSystem
④ BuildingSystem

⑤ Time / Work

⑥ EventSystem
⑦ ExplorationSystem

⑧ TechnologySystem
⑨ RoguelikeSystem

⑩ WeatherSystem
⑪ ItemSystem

⑫ RelationshipSystem
⑬ FactionSystem

⑭ EndingSystem

⑮ UI 完善
10. 可以并行的内容

Core 完成后，以下可以部分并行：

建筑数据
角色数据
事件数据
科技数据
遗物数据
物品数据

因为它们主要属于：

Data 层。

11. 不允许提前实现

没有稳定：

GameState
EffectSystem
RandomSystem

之前，不应开始大量制作：

100 个事件
100 个遗物
复杂派系任务
复杂终局

否则返工成本很高。

12. 每个系统的输入输出

每个 System 必须明确：

输入：
什么状态

处理：
什么规则

输出：
修改什么状态

事件：
通知谁

例如：

BuildingSystem

输入：
建筑、资源、工作者

处理：
生产/维护

输出：
资源、耐久

通知：
ResourceChanged
BuildingDamaged
13. 系统依赖禁止形成循环依赖

例如：

ResourceSystem
→ BuildingSystem
→ ResourceSystem

应通过：

Command / Event / Effect

进行间接通信。

禁止直接互相持有业务级依赖。

14. 完成判定

一个 System 只有在：

核心逻辑完成
数据接口稳定
存档支持
基础测试通过

之后，才视为：

可供其他 Agent 依赖的稳定模块。