# 2026 年 6 月第 1 周开发日志

## 2026-06-01

- `P3-8-D` 继续沿纯 Web + Flutter 主线推进，完成 Flutter 登录态“我的浏览记录 / 最近访问”只读入口：复用 `User/GetMyBrowseHistory` 私有接口，覆盖公开帖子 / 文档 / 商品详情承接、加载 / 空态 / 错误 / 刷新 / 加载更多 / 返回状态。
- Flutter 通知已读状态受控写入已完成：Flutter API 客户端补齐 `PUT` 契约，通知仓储复用 `Notification/MarkAsRead`，通知列表支持单条显式标记已读并局部更新状态；本轮不扩展批量已读、删除、通知设置、系统推送或完整通知中心。
- Flutter 纯文本发帖受控写入已完成：论坛页读取顶级分类，登录态用户可发布 `contentType=text` 的纯文本帖子，成功后刷新列表并打开新帖子详情；本轮不扩展富文本、附件、投票、抽奖、草稿箱、编辑或点赞。
- Flutter 单商品购买路径受控写入已完成：登录态用户可从原生商品详情检查购买资格、输入支付口令购买 `1` 件商品，成功后进入订单详情确认订单号、状态和扣款结果；匿名态购买入口会发起登录并回到当前商品详情。本轮不扩展购物车、退款、权益使用、通知中心、完整移动商城或 WebOS 新功能。
- 胡萝卜资产发放与调账口径已回拉治理：注册默认奖励统一收敛到 `CoinService.GrantRegistrationRewardAsync`，普通注册、登录补偿和首个管理员初始化复用同一契约；余额查询、发放、扣除和管理员调账前校验真实用户，避免 Console 对错误 UserId 建立余额后用户侧购买仍显示 `0`。
- Console 用户 ID 字符串安全已按 `ID Phase A` 回拉：当前登录用户、个人资料和用户列表中的 `VoUserId / uuid` 不再进入 JavaScript `number` 精度域，避免后台展示、用户详情跳转或调账动作指向错误用户。
- 今日提交回顾后已确认规划入口、`P3-8-D` 专题、`ID 与联邦路线图`、6 月开发日志和收工交接记录需要同步；本轮没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量。
- 今日验证覆盖 Flutter 定向测试、`flutter analyze`、必要时全量 `flutter test`，后端定向测试与 `dotnet build Radish.slnx -c Debug`，Console `type-check / test / build`，以及仓库级 `git diff --check`。
- 收工前补 [2026-06-01 收工回顾与明日事项](/records/daily-handoff-2026-06-01)：明日先做 Flutter 单商品购买真实数据复核；若购买 / 资产链路仍有阻断，优先修该链路，若通过则推进 `ID Phase A` 外部 LongId 字符串安全自动化守护。
