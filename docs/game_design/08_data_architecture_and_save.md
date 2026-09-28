# 《最后避难所》数据结构与存档设计规范

# 1. 总原则

游戏逻辑和 UI 分离。

禁止：

> 把所有游戏状态直接写死在 UI 脚本里。

推荐：

```text
Data
↓
Game Systems
↓
Game State
↓
UI
```

---

# 2. 核心状态

主要数据对象：

```text
GameState
DayState
WorldState
BaseState
ResourceState
CharacterState
RelationshipState
FactionState
RoguelikeState
EventState
EndingState
```

---

# 3. GameState

负责记录整局游戏。

字段概念：

```text
gameSeed
currentDay
currentTimeSlot
worldRuleIds
baseState
characters
relationships
factions
technologies
relics
events
flags
endingState
```

---

# 4. 世界状态

保存：

```text
天气
灾难
地图
资源点
派系
世界事件
```

---

# 5. 基地状态

每座建筑：

```text
buildingId
level
durability
enabled
workers
storage
```

---

# 6. 角色状态

每名角色至少保存：

```text
characterId
name
profession
stats
traits
health
hunger
stress
fatigue
location
relationships
personalQuest
alive
```

---

# 7. 肉鸽状态

保存：

```text
developmentPath
ordinaryUpgradeIds
relicIds
constructionTags
synergyFlags
```

---

# 8. 事件状态

所有事件应该由 ID 管理。

例如：

```text
event_mine_collapse_01
event_stranger_02
event_trade_03
```

不要把大量剧情硬编码到单个总控制器。

---

# 9. 事件结果

事件结果应该采用：

```text
条件
→ 选项
→ 条件检查
→ 效果
→ 后续事件
```

例如：

```text
选择：帮助陌生人

条件：
拥有药品 ≥ 1

效果：
药品 -1
陌生人加入

后续：
event_stranger_join_02
```

---

# 10. 随机种子

每局创建：

```text
Random Seed
```

所有随机内容尽量由该 Seed 派生。

这样可以：

```text
复现 Bug
重放事件
调试某一局
测试某个结局
```

---

# 11. 存档

建议：

> 每天结束自动存档。

同时允许：

> 玩家手动保存一个独立存档。

---

# 12. 存档内容

保存：

```text
游戏日期
时间段
随机种子
资源
建筑
角色
关系
科技
遗物
事件状态
探索状态
派系状态
终局状态
```

---

# 13. 数据驱动

尽量让以下内容数据化：

```text
建筑
资源
角色
特性
科技
遗物
事件
探索地点
派系
天气
灾难
结局
```

后期增加内容时：

> 优先增加数据，而不是修改核心代码。

---

# 14. 版本兼容

SaveData 必须拥有：

```text
saveVersion
```

游戏更新后需要支持：

```text
旧存档
→ 数据迁移
→ 新版本 GameState
```

禁止因为增加一个字段就直接让所有旧存档失效。

---

# 15. 测试要求

至少覆盖：

```text
新游戏
自动存档
手动存档
读档
角色死亡
建筑损坏
事件选择
探索结果
肉鸽选择
结局判断
60天正常结束
异常结束
旧版本存档迁移
```

---

# 16. UI 与数据分离示例

错误：

```text
UI按钮
→ 直接修改 food
```

正确：

```text
UI按钮
→ 调用 GameSystem
→ 修改 GameState
→ 触发事件
→ UI 刷新
```

这样以后才能：

```text
PC 调试
手机运行
自动测试
AI Agent 编写内容
```

共享同一套核心逻辑。

---

# 17. 扩展目标

最终希望可以做到：

```text
新增一个遗物
=
新增一个数据文件

新增一个事件
=
新增一个事件定义

新增一个角色
=
新增一个角色定义
```

核心代码尽量不需要改变。
