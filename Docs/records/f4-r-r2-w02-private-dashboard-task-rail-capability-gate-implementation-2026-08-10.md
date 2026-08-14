# F4-R R2-W02 Private 仪表 / 任务侧栏前端能力门禁实现

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：五组设计前能力门禁已关闭；R2 局部代表设计已确认
>
> 范围：正式 Web Notifications、Circle、Pet、Private Shop 与 Me；不修改 API、数据库、权限、LongId、业务范围或移动壳层

## 1. 结论

`R2-W02` readiness 识别的五组窄前端门禁已经成组关闭。正式 Private 页面现在能区分权威未读取、不可用和旧快照，已有表单具备未保存离开保护，Pet 模糊结果重试复用原幂等键，Private Shop 保留结构化错误诊断，`MeApp.tsx` 也已回到项目 `1500` 行硬上限内。

本批没有新增聚合端点、第二套状态源或视觉占位实现，也没有修改 Pencil、启动服务或执行浏览器 smoke。后续代表设计已在获得当前任务授权后于唯一活动 `.pen` 中完成并确认，详见[局部代表设计记录](/records/f4-r-r2-w02-private-dashboard-task-rail-representative-design-2026-08-10)。

## 2. 已关闭门禁

### 2.1 权威读取状态

- Notifications 直接消费通知 Store 的 `loadState`；权威摘要取得前，未读数、事件数、revision 和分类计数显示未知值，读取失败区分 unavailable 与 stale。
- Circle 将关注摘要由伪造 `{ 0, 0 }` 改为 nullable 权威快照，并显式维护 `idle / loading / ready / unavailable / stale`。
- Pet 初次读取失败进入独立不可用状态，不再渲染“未领取”表单；旧快照刷新失败时保留内容并标注 stale。
- Me 仪表盘在公开资料读取失败时显式显示局部错误，资产与最近访问错误也不再被真实空态掩盖。

### 2.2 未保存离开保护

- Notifications 偏好草稿按服务端稳定分类计算 dirty；dirty 时锁定共享页头 SPA 导航、启用 `beforeunload`，通知目标跳转要求明确确认。
- Pet 名称与公开状态按当前权威 Profile 计算 dirty；dirty 时锁定页头导航、启用 `beforeunload`，手动刷新前要求确认丢弃。
- 保存成功或重新取得权威快照后 dirty 自动解除；领取名称仍保持一次性提交输入，不扩成持久草稿。

### 2.3 Pet 模糊结果重试幂等

- 每种照料动作在前端维护待裁决 `idempotencyKey`，明确成功或明确客户端失败后才清除。
- 网络失败、无状态响应、`408 / 425 / 429` 与 `5xx` 作为模糊结果；失败后立即读取 Pet 与日志权威快照。
- 只有新增同类照料日志或服务端次数 / 冷却状态证明动作已经推进时，才结束该键；否则下一次点击继续复用原键。

### 2.4 Private Shop 错误契约

- `useShopData` 的分类、商品、购买资格、订单、订单详情和背包读取统一提升为 `ApiResponseError`，固定 fallback 全部经过 i18n。
- 页面错误保留 `code / messageKey / statusCode / httpStatus / traceId / target`，诊断复制继续只展示本地化用户消息。
- 为避免 `community.ts` 越过 900 行资源门禁，完整 `notification.*` 词元按真实业务域拆入独立中英文 `notification.ts`，资源聚合与键对齐门禁同步更新。

### 2.5 Me 容器边界

- 仪表数据模型拆入 `meDashboardModel.ts`，仪表渲染与公开链接 / 跳转动作拆入 `MeDashboardView.tsx`。
- `MeApp.tsx` 保留认证、路由、数据编排与既有子页协调，从 `1816` 行降至 `1465` 行。
- 原有 Me 路由、来源返回、登出、公开链接、经验 / 资产 / 历史 / 附件等子任务保持不变。

## 3. 定向守卫

新增或更新的 Client 测试覆盖：

- Notifications / Circle 权威摘要未知值与 unavailable 状态；
- Notifications / Pet dirty 导航锁和 `beforeunload`；
- Pet 模糊错误分类、权威动作推进判定和待裁决键；
- Private Shop 本地化结构化错误与购买资格诊断状态；
- Me 文件硬上限、仪表拆分、资料 / 资产 / 浏览错误表面；
- locale 独立 Notification 业务域、既有公开链接与 Me 登出契约。

## 4. 验证

- `npm run test --workspace=radish.client`：`525 / 525` 通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint --workspace=radish.client`：通过，`0 warning`。
- `npm run build --workspace=radish.client`：通过；保留既有大 chunk 提示，不构成本批阻断。
- `git diff --check`：通过。

本批属于设计前前端能力收口，按项目分层验证规则未启动 Gateway、API、Auth、Vite 服务，也未执行 PC / mobile 浏览器 smoke。

## 5. 后续顺位

1. `R2-W02` 局部代表设计已经确认，只覆盖六页需要改变的摘要、主任务与辅助轨关系，没有复制完整路由或既有 R1 壳层。
2. Mobile 固定主任务先于紧凑摘要和折叠辅助信息；Pet 只允许最优先照料动作随当前状态提前。
3. 下一步进入正式视觉实现，先完成 Client 静态验证，再在重新获得服务启动授权后执行 Gateway PC / mobile 成组验收。
4. 正式实现不得扩张 API、权限、状态机或移动壳层，也不提前推进 R3。

## 6. 主要实现证据

- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/circle/CircleApp.tsx`
- `Frontend/radish.client/src/pet/PetApp.tsx`
- `Frontend/radish.client/src/pet/petCareRetry.ts`
- `Frontend/radish.client/src/apps/shop/hooks/useShopData.ts`
- `Frontend/radish.client/src/apps/shop/shopDataError.ts`
- `Frontend/radish.client/src/shop/ShopWebApp.tsx`
- `Frontend/radish.client/src/me/MeApp.tsx`
- `Frontend/radish.client/src/me/MeDashboardView.tsx`
- `Frontend/radish.client/src/me/meDashboardModel.ts`
- `Frontend/radish.client/src/locales/resources.ts`
- `Frontend/radish.client/tests/privateDashboardAuthorityStatic.test.ts`
- `Frontend/radish.client/tests/petCareRetry.test.ts`
- `Frontend/radish.client/tests/shopDataError.test.ts`
