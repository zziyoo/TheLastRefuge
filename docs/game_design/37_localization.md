《最后避难所》本地化规范

第一版可以只实现中文，但数据结构应预留多语言能力。

最终需要定义
LocalizationKey
中文文本
英文文本
其他语言
原则

代码不直接依赖显示文本判断逻辑。

错误：

if text == "寒潮"

正确：

if eventId == "event_cold_wave"
内容文本要求

所有：

事件
科技
遗物
建筑
角色
特性
结局

应尽量通过：

Localization Key。

正式开发时再确定具体 Unity 本地化方案。