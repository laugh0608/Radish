# F4-R R2-P03 Public 只读详情变体正式实现与 Gateway 验收

> 日期：2026-08-10（Asia/Shanghai）
> 状态：设计—实现—Gateway PC / mobile 运行态闭环已完成，专题关闭

## 1. 本批结论

`R2-P03` 已把确认稿和既有权威契约接入正式商品详情与公开主页，并完成匿名、种子管理员、临时 Completed 买家、真实写入 / 冲突和 PC / mobile 响应式运行态复核。实现没有新增 API、数据库、权限、审批流、LongId 转换或新的移动壳层，也没有修改 Pencil；临时订单与评价已经精确清理，专题关闭。

## 2. 商品详情

- PC 主区采用 `24px` 外距、`12px` 核心分栏和 `14–20px` 主要卡片内距；Mobile DOM 顺序固定为顶部大图、商品信息、购买动作、评价、商品详情。
- 价格使用 `carrot` 萝卜币图标，返回、分享与举报收口到商品图上方的轻量操作层；正式购买仍复用既有订单入口。
- 评价区消费现有公开聚合、五星分布和稳定分页；没有权威评价数据时显示真实零态，初次请求失败时显示 unavailable，不伪装为 `5.0` 或零评价。
- 登录用户按 Completed 订单资格加载本人评价，可在同一入口创建、编辑和删除；评分限制为 `1..5`，评论限制为可选 `500` 字。
- 编辑器保留 baseline、dirty 状态和离开确认；CAS `409` 时保留草稿并刷新最新权威版本，不自动覆盖服务端内容。
- 同页已有权威快照而刷新失败时显示 stale；首次请求失败与跨页失败不借用其他页数据。`ProductReview` 继续复用共享举报弹窗和既有治理 / 申诉链路。
- 所有评价、商品和分页 LongId 继续以字符串传递，未引入 `Number` 转换。

## 3. 公开主页

- PC 头部使用主题装饰封面和身份、等级、统计 / 操作三个职责区；Mobile 保持封面—身份—等级—统计—关系动作的纵向节奏。
- 身份区展示昵称与 `用户名#公开ID`。当前公开资料契约没有签名字段，因此显示明确的签名空态文案，不伪造内容，也不新增背景上传或资料编辑能力。
- 等级区只消费 `VoCurrentLevel / VoCurrentLevelName`，展示当前等级、等级名和加入时间；等级名只作为视觉徽章，不建立独立徽章实体，也不暴露经验值、进度或排名。
- 统计继续独立加载权威帖子、评论和关系结果；未取得权威结果时显示 loading / unavailable，不以省略号或零值冒充读取成功。关系动作沿用既有 follow / message 权限与版本边界。

## 4. 关键实现边界

- `PublicEntry` 只增加通用 dirty 导航确认：覆盖应用内路由、浏览器返回和整页卸载，不改变现有 Forum 上传导航锁语义。
- 评价登录入口使用受控 `/shop/product/:id?intent=review` 回跳；认证白名单只接受单个 `purchase / review` intent，拒绝额外 query、重复参数和 hash。回跳后先恢复权威资格，再滚动评价区并仅在符合资格时打开编辑器。
- 商品主体、评价局部状态和公开主页统计保持独立裁决；局部失败不会覆盖商品或用户主资料的权威结果。
- 本批未扩入评价媒体、回复、有用、追评、标签、推荐排序、实时刷新、独立评价中心、公开经验详情、用户背景上传或 R3 派生页面。

## 5. 验证与运行态验收

### 5.1 代码侧验证

- `npm run test --workspace=radish.client`：`518 / 518` 通过。
- `npm run type-check --workspace=radish.client`：通过。
- `npm run lint --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过；仅保留既有大 chunk 提示。
- 新增商品评价正式展示静态契约测试 `4 / 4`；同步修正既有 SEO / 举报静态断言。
- `npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

补充执行了 `npm run validate:baseline:quick`；前端 type-check、HTTP `36 / 36`、UI `31 / 31`、Client `518 / 518`、Console `83 / 83`、权限与敏感字面量扫描均通过，但统一入口最终被本批未修改的既有 `Radish.Repository/SystemConfigStorageCoordinator.cs` 时间语义 baseline 阻断：该文件在当前 `HEAD` 已存在 `3` 处 `DateTime.Now`，baseline 仍登记为 `0`。本记录不把统一入口写成通过，也不在页面实现批中顺手修改该独立治理问题。

### 5.2 数据库与宿主

- 经用户明确授权执行 `Radish.DbMigrate apply`，补齐本地尚未应用的 `main.20260809_019_console_role_governance` 与 `main.20260809_020_product_review`，并完成种子数据同步；严格 doctor 确认评价业务表与索引存在。
- Gateway、API、Auth 与 Client 在当前任务中启动；`npm run check:host-runtime -- --details` 确认 Gateway、API、Auth 均返回 `200`。
- 浏览器会话结束后已恢复默认视口并关闭验收页签；Gateway、API、Auth 与 Client 随后停止，`5000 / 5100 / 5200 / 3000` 均无监听进程。

### 5.3 Gateway 浏览器验收

- 匿名商品详情覆盖 PC `1440 × 900` 与 Mobile `390 × 844`：商品、真实零评价、登录守卫、举报守卫、Mobile 大图—信息—购买—评价—详情顺序和无横向溢出均通过。
- 使用种子账号 `admin@radishx.com` 登录，公开主页真实展示 `Admin#2`、签名空态、`Lv0 / 凡人`、加入时间与独立统计；同时覆盖 `en-US` 和 `guofeng`。
- 以精确临时 Completed 订单建立评价资格，真实创建五星评价并验证 `5.0 / 1` 聚合、五星分布、已购标识和作者；随后覆盖编辑、删除，删除后恢复真实零评价。
- 两个浏览器页签制造真实 CAS 冲突：旧页保留草稿并显示本地化冲突状态；“加载最新版本”后草稿仍保留，重试保存成功。
- dirty 草稿离开确认、`ProductReview` 举报弹窗和受控 `intent=review` 登录回跳通过；无资格时显示本地化资格说明，有资格时自动滚动评价区并打开编辑器。
- 初始 unavailable 在迁移前按局部失败态显示，不泄露原始服务端消息；迁移后权威零态正常。稳定新页签的浏览器日志为 `0 warning / 0 error`。
- 临时评价与订单按精确标识物理清理并复核为 `0`；没有提交举报、保存关系动作或留下其他临时业务数据。

## 6. 下一步

1. `R2-P03` 保持关闭，不重开 Pencil 或扩张冻结范围。
2. 按代表页既定顺位进入 `R2-W02 Private 仪表 / 任务侧栏` 设计前代码事实与能力覆盖审计。
3. 先裁决 `R1-F01 / R1-W01` 继承、正式 Web / WebOS 能力差异和窄代码门禁；审计完成前不修改 Pencil 或推进 R3。
