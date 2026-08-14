# F4-R R2-A02 Author 列表、修订与 Forum 发布差异局部代表设计

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：按人工反馈完成第二轮 Pencil 修订、静态复核与人工确认
>
> 活动设计源：`Docs/frontend/design-sources/radish-web-family-ui-v1.pen`
>
> 范围：Docs Mine、Docs Revisions、Forum Compose 的 PC `1440px`、Mobile `390px` 和必要关键状态；不复制 R1 Author 编辑页或 Forum 详情页

## 1. 结论

- `R2-A02` 继续保持 R2，Docs Mine / Revisions 继承 `R1-A01` 的内容主轴、从属上下文与普通 Author 停止线，Forum Compose 继承 `R1-P02` 的正式论坛壳层与写作语法；没有出现需要升级为新 R1 类型的结构冲突。
- Docs Mine PC 固定为权威筛选—连续列表主轴—选中文档窄上下文，Mobile 删除并排上下文，只保留紧凑筛选入口、连续列表和稳定分页。
- Docs Revisions PC 固定为修订时间线—`vN-1 → vN` 并排差异—窄证据区，Mobile 使用顶部版本对与单列 unified diff；默认表达一次修改前后，并可切换为“选中版本 → 当前版本”。普通 Author 不出现审核、发布、回滚、删除或恢复动作。
- 帖子与文档共享同一差异表达：文档比较标题与 Markdown，帖子额外比较分类、标签、封面和附件；PC / Mobile 不为两类内容复制等价画板。Forum 现有 Revision 弹层与评论入口后续继承该模式，不扩大公开全文读取权限。
- Forum Compose PC / Mobile 改为 Discourse 式底部半屏停靠与全屏写作两种形态；两种形态必须是同一个 Composer 实例，展开 / 收起不得重置标题、正文、附件、上传状态、账号草稿或提交标识。WebOS 继续只提供薄 Bottom Sheet 外壳，不复制发布状态机。
- 必要关键状态覆盖首次读取、unavailable、stale、Revision 修改前版本局部失败、上传 / 发布锁定和账号草稿安全失败，不为主题、locale、权限或等价状态复制完整页面。

## 2. 顶层代表画板

| 代表画板 | Pencil 节点 | 尺寸 | 固定结构 |
| --- | --- | --- | --- |
| Docs Mine PC | `t6KLfy` | `1440 × 900` | 权威范围 / 草稿阶段筛选、连续列表、服务端稳定分页与选中文档从属上下文 |
| Docs Revisions PC | `mV3Or` | `1440 × 900` | 修订时间线、相邻版本并排差异、对比当前切换、共享内容差异边界与只读证据 |
| Forum Compose PC 半屏 | `E7htfN` | `1440 × 900` | 论坛浏览上下文上的底部停靠 Composer、紧凑设置、账号草稿与原位全屏入口 |
| Forum Compose PC 全屏 | `nvUM8` | `1440 × 900` | 同一 Composer 的全屏写作面、完整编辑主轴、设置 rail 与回到半屏入口 |
| Docs Mine Mobile | `J8x9N` | `390 × 844` | 紧凑筛选入口、连续文档列表、稳定分页与“更多”导航 |
| Docs Revisions Mobile | `YjURt` | `390 × 844` | 顶部修改前 / 后版本对、单列 unified diff、差异摘要与折叠证据 |
| Forum Compose Mobile 半屏 | `zsvSf` | `390 × 844` | 论坛列表上的约 `72dvh` 底部 Composer、折叠设置与全屏入口 |
| Forum Compose Mobile 全屏 | `x6qYz` | `390 × 844` | 同一草稿的全屏单任务写作、折叠设置与回到半屏入口 |
| Author 与 Forum 发布必要关键状态 | `j3gEk` | `1440 × 860` | 六个局部状态；只替换受影响表面，不复制路由 |

九张顶层画板均使用 `guofeng` 主题语义 token、已有 Web Header / Mobile Tab Bar 和 Lucide 图标；所有新增节点均具备可读名称，最终均为 `placeholder: false`。

## 3. Docs Mine 与 Revisions

- Mine 的 dominant region 是连续文档列表。范围、草稿阶段、角色、总数、页码和最近修改顺序均表现为权威结果，不以翻译文本或前端全量结果代替筛选合同。
- PC 选中文档上下文只承载摘要、我的角色、正式版本、当前草稿、最近修改和已有入口；不建立第二套权限判断或大仪表摘要。
- Mobile 不把 PC 右侧 rail 压成首屏卡片堆；筛选进入按需表面，文档行直接进入编辑或修订任务。
- Revisions 的 history、选中版本与比较基准分别裁决。默认从有序版本轨取得前一版本作为修改前基准；切换“对比当前”时加载当前版本，但任何比较基准失败都不得清空已选版本。
- PC 差异区按修改前 / 修改后并排展示标题、结构化元数据和正文增删；Mobile 转为单列 unified diff，避免两栏压缩。首个版本没有前一版本时诚实显示“无更早版本”，不伪造空白修改前内容。
- 帖子沿用已有受权 Revision snapshot，并补分类、标签、封面与附件差异；文档沿用完整 Markdown Revision。普通 Author 只读停止线、受保护附件和正式公开阅读身份保持可辨识。

## 4. Forum Compose

- 正式 `/forum/compose` 路由保留，发帖入口默认在论坛浏览上下文上打开底部半屏 Composer；浏览器返回 / 关闭回到来源页面。刷新直达 Compose 路由时仍能重建合法的论坛背景与半屏写作面，不新增第二条路由。
- PC 半屏约占 `50–56dvh`，Mobile 半屏约占 `72dvh`；两端均可原位展开全屏，退出全屏回到半屏。展开 / 收起只改变承载面，不卸载共享 Composer 或重建草稿、上传和提交状态。
- 发布类型只暴露已有普通帖子、问答、投票与抽奖；分类、标签、标题、Markdown、图片 / 附件和扩展设置保持渐进披露。
- 草稿状态明确归属当前账号。切换账号时先清空内存，再读取新账号记录；发布成功只清理当前账号草稿。
- 上传或发布期间，关闭、取消和重复提交统一锁定；失败只由 Composer 本地化反馈一次，父级不追加第二个失败 toast，也不展示原始异常。
- Mobile 半屏保持标题、正文和发布动作可见，完整设置按需展开；全屏保持单任务写作，不复制 PC 设置 rail。账号草稿边界以内联提示保留。

## 5. 必要关键状态

- **Mine 首次读取**：无权威快照时显示读取中，不伪造 `0` 篇空态。
- **Mine unavailable**：首次请求失败时保留当前筛选并允许重试当前查询。
- **Mine stale**：同一查询刷新失败时保留上次成功页面、标注陈旧并提供再次刷新。
- **Revision 修改前版本不可用**：已选修改后版本保持可见，只替换失败的比较基准表面；重试绑定当前修改前 Revision，不清空时间线或当前快照。
- **Composer busy lock**：上传进度与发布生命周期可见，关闭、取消和重复提交均被同一任务锁定。
- **Composer safe failure**：保留当前账号草稿，只展示一次本地化失败；账号变化不继承、覆盖或删除其他账号内容。

## 6. 契约与停止线

- 不新增 API、数据库、migration、权限、LongId 转换、服务端 Forum 草稿、移动壳层或第二套发布状态机。
- 不把审核、发布、回滚、导入导出、治理或删除恢复动作迁入普通 Author。
- 不把 WebOS 窗口、Dock 或桌面能力复制到正式 Web；正式 Web 的半屏 / 全屏只复用共享 Composer 承载，不迁移 WebOS 壳层。Tauri 与 Flutter 继续不进入本批。
- Pencil 只固定结构、层级、关键状态和 PC → Mobile 转换；功能、按钮、文案、权限、幂等与存储合同继续服从专题文档和当前代码。

## 7. Pencil 静态复核与下一步

- 九张顶层画板均已逐板截图复核；第二轮修订没有引入无效图标或 Pencil 警告。
- Pencil 原生 visitor 审计确认 `9` 个 R2-A02 根节点尺寸正确，`placeholder = 0`，累计根边界越界 `outOfRoot = 0`；截图未见布局塌陷、裁切、横向溢出或不可辨识操作。
- 设计文件已通过 Pen 原生保存落盘。本批没有启动 Gateway、API、Auth、Vite 或浏览器，也没有修改正式页面代码。
- 九张代表板已于 `2026-08-10` 获得人工确认。下一步先补共享差异计算 / 双快照读取与 Composer 承载状态门禁，再按本记录完成正式视觉实现、Client 定向静态验证和 production build；运行态验收前仍需重新说明启动命令、端口与影响并获得授权。
