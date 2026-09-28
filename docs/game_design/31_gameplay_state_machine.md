《最后避难所》Gameplay 状态机
1. 总体流程
MainMenu
↓
NewGame / LoadGame
↓
GameStart
↓
Morning
↓
Planning
↓
Action
↓
Event
↓
Resolution
↓
Evening
↓
Night
↓
DayEnd
↓
NextDay
2. MainMenu

允许：

新游戏
继续游戏
读取存档
设置
3. GameStart

执行：

生成 Seed
生成 WorldState
生成角色
生成初始资源
生成基地
生成地图
初始化事件系统

完成后：

进入 Day 1 Morning。

4. Morning

执行：

天气更新
灾难更新
人员恢复
建筑状态检查
每日 Modifier

然后：

Planning。

5. Planning

玩家可执行：

人员工作安排
生产计划
建筑
科技
人员调整
装备

此阶段：

不消耗大量时间。

6. Action

允许主动行动：

生产
维修
研究
治疗
探索
交易
互动

每个行动具有时间成本。

行动完成：

返回 Planning 或进入 Event。

7. Event

事件暂停正常时间推进。

流程：

展示事件
↓
玩家选择
↓
条件检查
↓
Effects
↓
记录 EventHistory
↓
Resolution
8. Resolution

统一处理：

资源变化
角色变化
建筑变化
关系变化
派系变化
Modifier
后续事件

完成后：

返回当前时间段。

9. Evening

执行：

资源预测
基地状态检查
危险提示
人员状态检查
日志总结
10. Night

执行：

食物消耗
水消耗
能源消耗
疲劳恢复
压力变化
关系变化
疾病检查
随机夜间事件
11. DayEnd

执行：

记录统计
自动保存
生成日报
推进 Day

然后：

Day < 60
→ Morning

Day >= 60
→ EndingCheck
12. 探索子状态

探索过程中：

ExploreStart
↓
Travel
↓
ExploreEvent
↓
Choice
↓
Resolution
↓
Continue / Return
13. 角色死亡状态

任何阶段角色死亡：

CharacterDeath
↓
记录死亡
↓
处理关系
↓
处理工作
↓
处理后续事件
↓
返回原状态

禁止死亡直接破坏主状态机。

14. 灾难状态

灾难不是独立主流程。

而是：

World Modifier。

因此：

Normal Day
+
Disaster Modifier

一起运行。

而不是：

Normal State
↓
Disaster State

这样可以避免大量状态分支。

15. 终局状态

Day 48：

FinalCountdown

Day 56：

FinalPreparation

Day 60：

EndingCheck
↓
EndingPresentation
↓
MetaProgression
↓
SaveResult
↓
MainMenu / NewGame
16. 强制暂停

事件、重大选择、终局必须：

暂停普通时间推进。

但“暂停 UI”不等于修改核心时间系统。

17. 非法状态处理

例如：

在 Event 状态尝试开始普通生产

应：

拒绝 Command。

而不是让游戏进入未知状态。

18. 状态切换要求

所有状态切换：

由统一 StateMachine 管理。

禁止不同系统直接修改当前状态。

19. 状态机测试

至少测试：

NewGame → Day1
DayEnd → NextDay
Event → Resolution
探索 → 返回
角色死亡 → 恢复
灾难期间 → 正常推进
Day59 → Day60
Day60 → Ending
Ending → NewGame
20. 核心原则

状态机只负责“现在游戏处于什么阶段”。

具体规则交给对应 System。