《最后避难所》内容数据库 Schema
1. 总原则

所有可扩展游戏内容：

必须数据驱动。

代码负责：

规则。

数据负责：

内容。

2. Definition 与 State

必须分离：

Definition

表示：

“这个东西是什么。”

State

表示：

“这个东西当前怎么样。”

3. CharacterDefinition
id
name
profession
baseStats
startingTraits
startingEquipment
tags
description
4. BuildingDefinition
id
name
category
constructionCost
upkeep
capacity
production
energyCost
level
prerequisites
effects
tags
description
5. TechnologyDefinition
id
name
category
tier
researchCost
researchTime
prerequisites
unlockEffects
tags
description
6. RelicDefinition
id
name
rarity
tags
effects
negativeEffects
synergies
appearanceRules
description
7. ItemDefinition
id
name
type
rarity
stackable
maxStack
weight
effects
craftRecipe
sellValue
tags
8. EventDefinition
id
name
type
phase
weight
cooldown
conditions
options
effects
followUpEvents
characterTags
factionTags
rogueTags
worldTags
9. LocationDefinition
id
name
tier
distance
danger
resourceTags
eventPool
prerequisites
specialRules
10. FactionDefinition
id
name
ideology
preferredTags
tradePool
questPool
rewardPool
characterPool
relations
endingTags
11. EndingDefinition
id
name
priority
conditions
presentation
rewards
unlocks
12. ModifierDefinition
id
name
source
duration
statModifiers
eventModifiers
productionModifiers
explorationModifiers
tags
13. ID 规则

统一使用：

lower_snake_case

例如：

technology_basic_farming
relic_automation_core
event_mine_collapse_01
building_small_farm

禁止：

中文 ID
空格
重复 ID
自动生成不可读 ID
14. 引用规则

内容之间通过：

ID 引用。

例如：

event_stranger_01
→ character_trait_brave

不存在的引用：

开发环境必须报错。

15. 数值规则

数据层禁止保存：

NaN
Infinity
负资源成本
负时间

除非明确属于合法特殊效果。

16. 内容验证

游戏启动或开发工具执行：

检查重复 ID
检查引用
检查枚举
检查必填字段
检查非法数值
检查循环引用
17. 数据版本

所有数据库应该拥有：

contentVersion

用于：

调试
迁移
兼容
内容追踪
18. Agent 新增内容

Agent 添加一个内容时：

优先新增 Definition。

不得复制核心逻辑。