# F4-R R2-P03 商品评价与公开等级能力门禁实现

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：能力门禁、静态验证与 Pencil 评分区已完成；等待整组设计确认
>
> 范围：只实现经确认的公开等级安全投影、Completed 购买商品评价、聚合、CAS 与既有内容治理接入；未进入正式商品详情 / 公开主页视觉实现，未启动服务或浏览器

## 1. 结论

- 公开资料只新增当前等级和等级名，不公开经验、进度、排名、冻结状态或内部 LongId；无经验记录时由服务端读取初始等级配置。
- 商品评价固定为当前租户内“每用户、每商品一条”；只有存在 `Completed` 订单才能创建、编辑或恢复，评分为 `1..5` 整数，评论可选且最多 `500` 字。
- 创建由唯一约束与 Main 事务抵抗重复提交；编辑、删除、本人恢复、治理限制与申诉恢复均使用单调 `Version` CAS，冲突保持结构化 `409`。
- 公开读取由数据库计算一位小数平均分、总数和五星分布；只聚合未删除评价，真实零评价不伪造默认五星或示例内容。
- `ProductReview` 作为新的受控举报目标复用既有 `console.moderation.*`、案件、审计与申诉链路，不新增权限种类、审批流或 Console 专用评价模块。

## 2. 数据与存储边界

- Main 新增 `ShopProductReview`，保存 LongId、租户、商品、用户、Completed 订单证据、公开名快照、星级、评论、版本、治理动作和软删除审计字段。
- `(TenantId, ProductId, UserId)` 唯一索引保持单用户单商品一条评价；商品分页与评分聚合分别使用稳定索引。
- 迁移 `20260809_020_product_review` 创建评价结构，并为 `ContentReport` 增加父商品快照；诊断与验证会检查唯一关系、五星范围、版本、Completed 订单证据和商品租户边界。
- 评价写入、治理限制与申诉恢复继续位于 Main 存储事务内；没有跨存储提交、动态 key 或实时刷新。

## 3. API 与错误契约

- `GET /api/v1/Shop/GetProductReviews/{productId}`：匿名读取综合评分、五星分布和稳定分页。
- `GET /api/v1/Shop/GetMyProductReview/{productId}`：读取本人评价、资格原因和当前 CAS 版本。
- `PUT /api/v1/Shop/UpsertProductReview/{productId}`：创建、编辑或本人恢复，提交 `Rating / Comment / ExpectedVersion`。
- `DELETE /api/v1/Shop/DeleteProductReview/{reviewId}?expectedVersion=`：按本人和版本软删除。
- 结构化错误固定为认证、商品不存在、评价不存在、未完成购买、版本冲突和已被治理六类；TypeScript LongId 继续保持字符串。
- `@radish/http` 新增统一 contract / client，正式 Client API 只做薄封装；没有自建 fetch、URL intent 或权限扩张。

## 4. 公开等级与治理

- `UserPublicProfileVo` 只投影 `VoCurrentLevel / VoCurrentLevelName`；公开主页仍以显示名和公开 identifier 组成身份，不暴露经验对象中的其他字段。
- 举报快照保存评价作者、商品、星级、正文和版本；案件队列、收据及导航通过父商品 LongId 回到正式商品详情。
- 限制动作按案件目标版本软删除评价并记录动作 ID；申诉只在来源动作、结果版本和父商品仍有效时恢复，版本继续单调增加。
- Console 中英文筛选和证据说明加入 `ProductReview`，仍由现有 View / Review / Action 权限裁决。

## 5. Pencil 落点

- 商品详情 PC 在价格区显示综合评分和已购评价数，详情区补五星分布、评价入口和两条评价预览。
- 商品详情 Mobile 保持“顶部大图—商品信息—购买动作—评价摘要 / 预览—商品详情—共享胶囊导航”顺序。
- 新增局部关键状态板，覆盖真实零评价、无 Completed 资格、dirty 离开保护、CAS `409` 草稿保留、评价局部 unavailable / stale，以及治理隐藏与申诉边界；不复制完整商品路由。
- PC、Mobile 与评价状态板均无 placeholder、裁切、塌陷或横向溢出，设计源已经显式保存。

## 6. 静态验证

- 后端完整测试：`1220 passed / 39 skipped / 0 failed`；PostgreSQL 专项按环境条件跳过。
- `@radish/http`：`36 / 36` 测试、type-check、Lint 通过。
- Client：`514 / 514` 测试、type-check、Lint 与 production build 通过。
- Console：`83 / 83` 测试、普通 / strict type-check、Lint 与 production build 通过。
- Pencil 三个受影响板的结构检查均为 `issues: [] / placeholders: []`。
- 本批未启动 API、Gateway、Vite 服务或浏览器；运行态 smoke 只在整组设计确认并进入正式 UI 后另行授权执行。

## 7. 停止线与下一步

- 不扩入评价图片 / 视频、回复、点赞 / 有用、追评、标签、推荐排序、独立评价中心或实时刷新。
- 不允许非 Completed 订单评价，不按订单重复评分，不新增 Console 权限或审批流，不开放公开经验详情或用户背景上传。
- 当前等待整组 R2-P03 设计确认；确认后才把已冻结契约接入正式商品详情与公开主页，不提前推进 `R2-W02 / R2-A02` 或 R3 页面。
