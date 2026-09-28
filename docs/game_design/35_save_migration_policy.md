《最后避难所》存档与版本迁移策略
1. 存档目标

游戏属于：

长周期、持续开发的单机游戏。

必须保证：

更新版本后尽可能继续使用旧存档。

2. SaveData

至少包含：

saveVersion
contentVersion
gameVersion
timestamp
gameSeed
gameState
eventHistory
3. SaveVersion

示例：

1
2
3
...

每当：

存档结构发生不兼容变化

才提升 SaveVersion。

4. ContentVersion

内容变化和结构变化分开。

例如：

增加一个新事件。

通常只改变：

contentVersion

不需要改变：

saveVersion
5. 添加新字段

例如：

旧：

CharacterState
{
    health
}

新：

CharacterState
{
    health
    stress
}

迁移默认：

stress = 0
6. 删除字段

如果旧字段不再使用：

读取时忽略。

不要求旧存档立即重写。

7. 字段重命名

禁止直接重命名导致旧存档失效。

必须提供：

Migration。

8. 数据类型变化

例如：

int
→
float

必须显式迁移。

不得依赖：

自动反序列化碰巧成功。

9. Definition 变化

内容 Definition 发生变化时：

已经存在的 Runtime State 原则上继续使用当前局状态。

不能因为升级内容文件：

把当前玩家已有的资源/建筑/角色偷偷重置。

10. 已删除内容

如果一个事件在新版本删除：

旧存档仍引用该事件时：

必须具有兼容处理。

方案：

保留 Legacy Definition
或
迁移至替代内容
11. 保存时机

自动保存：

每天结束。

重大操作后也可以自动保存：

终局
角色死亡
重大事件
重大肉鸽选择
12. 写入失败保护

保存过程不得：

直接覆盖唯一有效存档后再发现写入失败。

应该使用：

临时文件
↓
写入成功
↓
验证
↓
替换正式文件
13. 存档损坏

读取失败时：

不应该直接删除存档。

至少保留：

原文件
备份
错误日志
14. 存档测试

必须覆盖：

每个版本
重大事件中
探索中
角色死亡后
Day 59
Day 60

以及：

旧版本
→ 新版本
→ 再保存
→ 再读取
15. Debug 存档

开发环境允许：

导出 Seed
导出完整 SaveData
导入 SaveData

方便复现复杂 Bug。

16. 原则

存档格式优先考虑：

可迁移、可调试、可恢复。

不要为了追求最小文件体积而把结构做得过于复杂。