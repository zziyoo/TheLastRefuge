《最后避难所》Command / Effect 系统
1. 目的

解决：

大量事件、科技、遗物、角色特性都需要修改游戏状态。

所有状态变化必须尽可能统一。

2. Command

Command 表示：

玩家/系统“想做什么”。

例如：

AssignWorkerCommand
BuildCommand
ExploreCommand
ChooseEventOptionCommand
ResearchCommand
EquipCommand
3. Effect

Effect 表示：

“做完以后改变什么”。

4. 基础 Effect 类型

至少准备：

AddResource
RemoveResource

HealCharacter
DamageCharacter

AddStress
RemoveStress
AddFatigue

AddRelationship
RemoveRelationship

ChangeFactionReputation

AddTrait
RemoveTrait

AddExperience

DamageBuilding
RepairBuilding

AddModifier
RemoveModifier

UnlockTechnology
UnlockLocation

AddItem
RemoveItem

StartEvent
SetFlag
ClearFlag
5. Effect 执行原则

事件不直接操作 GameState。

例如：

事件：
选择帮助伤员

Effects：

RemoveResource(food, 5)
RemoveResource(medicine, 1)
HealCharacter(character_001, 20)
ChangeFactionReputation(faction_x, +5)

统一交给：

EffectResolver。

6. Effect 顺序

Effects 必须按照定义顺序执行。

例如：

RemoveResource
↓
HealCharacter
↓
ChangeFactionReputation

如果某 Effect 失败：

必须具有明确失败策略。

7. Atomicity

对于要求“全部成功，否则全部取消”的操作：

使用事务式 Effect Group。

例如：

购买：

食物 -20
水 -10
获得物品 X

不能出现：

扣了钱但没拿到物品。

8. 条件与 Effect 分离

条件：

“能不能做。”

Effect：

“做了之后发生什么。”

例如：

Condition:
food >= 20

Effect:
food -= 20

禁止把复杂条件混在 UI。

9. Modifier

Modifier 通过 Effect 添加。

例如：

AddModifier(
    "relic_automation_bonus",
    duration = permanent
)
10. 临时 Modifier

必须保存：

duration
remaining
source

例如：

寒潮：

food_consumption +20%
remaining = 3 days
11. 事件链

事件可以：

StartEvent(event_lab_02)

但不能直接调用另一个事件的 UI。

12. Effect 日志

每次重大 Effect 应能够在 Debug 日志中记录：

来源
类型
目标
变化前
变化后

例如：

event_mine_collapse
DamageCharacter
王
84 → 62
13. Undo

普通玩家操作可以在提交前撤销。

一旦 Effect 已正式结算：

默认不支持任意 Undo。

14. 随机性

Effect 本身尽量不直接创建随机数。

由：

RandomSystem

产生结果。

这样 Seed 才能复现。

15. Agent 新增效果

如果新内容需要一个现有 Effect 不支持的操作：

优先：

扩展通用 Effect。

不要在单个事件脚本中写临时逻辑。