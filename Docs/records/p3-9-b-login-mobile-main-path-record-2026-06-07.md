# P3-9-B 登录移动用户主路径整备记录（2026-06-07）

> 本记录承接 `P3-9-B 登录移动用户主路径整备`。
>
> 本批围绕 Flutter 登录态用户从发现 / 论坛 / 文档 / 商城 / 我的进入常用任务，复核返回态、刷新态、失败态、登录恢复和 LongId / PublicId 字符串契约。

## 批次结论

本批没有扩展完整移动商城、完整通知中心、完整资产中心、完整创作器、WebOS / Tauri 新功能、完整 PublicId 迁移或完整 E2E 平台。

自动化复核发现一个真实主路径缺口：Flutter 发现页商城商品夹具仍使用 `product-4001`，但商品详情页已经按规范 LongId 字符串在请求前拒绝非规范 ID，导致“发现 -> 商品详情”的壳层主路径无法进入详情内容。处理方式是把 Flutter 壳层测试夹具改为真实契约下的 `4001` 字符串，并补登录态从发现商品详情完成单商品购买、进入订单详情、返回商品详情再回到发现来源的定向守护。

当前 Flutter P3-9-B 自动化结论为通过；晚间人工复核随后暴露登录态商城 / 钱包路径缺口，已在同日回拉修复。下一步需要由用户复测商品详情余额、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水入口。

## 处理范围

| 项目 | 处理结果 |
| --- | --- |
| 发现 -> 商城详情 | 商品 ID 测试夹具从 `product-4001` 对齐为规范 LongId 字符串 `4001` |
| 发现 -> 商城列表 -> 详情 | 公开路径断言同步为 `/shop/product/4001` |
| 登录态购买路径 | 新增从发现页商品详情购买 1 件商品、进入订单详情、返回商品详情、再返回发现页的壳层测试 |
| 登录态商城 / 钱包人工缺口 | API 客户端补 401 / 403 / 404、空响应和非 JSON 失败态识别；商品详情购买面板补当前余额展示和购买后余额刷新 |
| 论坛 / 文档 / 我的 | 复用现有 Flutter 页面测试，未发现本批需要修改的真实缺口 |

## 验收覆盖

| 路径 | 覆盖点 | 自动化守护 |
| --- | --- | --- |
| 登录恢复 | 会话恢复、过期刷新、刷新失败回到游客态、OIDC 回调进入壳层 | `test/smoke_test.dart` |
| 发现入口 | 发现到论坛、文档、榜单、公开主页、商城详情和商城列表，Android Back 返回来源 | `test/smoke_test.dart` |
| 论坛参与 | 发帖、评论、回复、轻回应登录回流、失败保留上下文、PublicId 展示与复制 | `test/forum_page_test.dart`、`test/forum_detail_page_test.dart` |
| 文档阅读 | 文档列表刷新态 / 失败态、搜索详情返回、内链打开和来源返回 | `test/docs_page_test.dart` |
| 商城购买 | 登录态单商品购买、订单详情、背包发放、扣款流水、非规范 LongId 拒绝 | `test/shop_product_detail_page_test.dart`、`test/smoke_test.dart` |
| 商城 / 钱包失败态 | 后端 `MessageModel` envelope、401 空响应、成功非 envelope 响应异常、商品详情余额展示 | `test/radish_api_client_test.dart`、`test/shop_product_detail_page_test.dart` |
| 我的入口 | 订单、背包、胡萝卜资产、经验、最近访问、资料编辑成功与失败保留输入 | `test/smoke_test.dart`、`test/profile_page_test.dart`、`test/browse_history_page_test.dart` |
| 通知回流 | 通知列表、已读标记失败不阻断详情打开、评论目标保留 | `test/smoke_test.dart`、`test/notification_repository_test.dart` |

## 验证记录

执行目录：`Clients/radish.flutter`

```bash
flutter test test/smoke_test.dart
flutter test test/radish_api_client_test.dart test/shop_product_detail_page_test.dart
flutter test
flutter analyze
```

结果：

- `flutter test test/smoke_test.dart`：通过，`51` 个测试通过。
- `flutter test test/radish_api_client_test.dart test/shop_product_detail_page_test.dart`：通过，覆盖 API envelope、登录态失败态、商品详情余额展示和商城购买路径。
- `flutter test`：通过，`186` 个测试通过。
- `flutter analyze`：通过，`No issues found`。

## 后续建议

- Flutter 登录移动用户路径可继续进入人工账号复核：优先复测商品详情余额展示、购买资格检查、单商品购买、订单详情、背包发放、胡萝卜流水、通知回流和资料编辑。
- 若人工复核没有暴露新阻断，本批不继续深挖完整移动商城、通知中心或资产中心，建议转向 `P3-9-C 访客公开访问与分享整备` 或 `P3-9-D Console 排障与治理入口整备`。
