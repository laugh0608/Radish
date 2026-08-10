# F4-R R2-A02 Author 列表、修订与 Forum 发布差异局部代表设计

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：Pencil 局部代表设计与静态复核已完成，等待人工确认
>
> 活动设计源：`Docs/frontend/design-sources/radish-web-family-ui-v1.pen`
>
> 范围：Docs Mine、Docs Revisions、Forum Compose 的 PC `1440px`、Mobile `390px` 和必要关键状态；不复制 R1 Author 编辑页或 Forum 详情页

## 1. 结论

- `R2-A02` 继续保持 R2，Docs Mine / Revisions 继承 `R1-A01` 的内容主轴、从属上下文与普通 Author 停止线，Forum Compose 继承 `R1-P02` 的正式论坛壳层与写作语法；没有出现需要升级为新 R1 类型的结构冲突。
- Docs Mine PC 固定为权威筛选—连续列表主轴—选中文档窄上下文，Mobile 删除并排上下文，只保留紧凑筛选入口、连续列表和稳定分页。
- Docs Revisions PC 固定为修订时间线—独立详情，Mobile 将历史收敛为顶部版本选择器，正文详情保持唯一主任务；普通 Author 不出现审核、发布、回滚、删除或恢复动作。
- Forum Compose PC / Mobile 均由正式页面直接承载共享 Composer；WebOS 继续只提供薄 Bottom Sheet 外壳，不复制发布状态机。账号草稿、单一失败反馈、附件、标签、问答、投票、抽奖和提交幂等继续服从已完成的能力门禁。
- 必要关键状态覆盖首次读取、unavailable、stale、Revision detail 独立失败、上传 / 发布锁定和账号草稿安全失败，不为主题、locale、权限或等价状态复制完整页面。

## 2. 顶层代表画板

| 代表画板 | Pencil 节点 | 尺寸 | 固定结构 |
| --- | --- | --- | --- |
| Docs Mine PC | `t6KLfy` | `1440 × 900` | 权威范围 / 草稿阶段筛选、连续列表、服务端稳定分页与选中文档从属上下文 |
| Docs Revisions PC | `pVevC` | `1440 × 900` | 修订时间线、独立详情、只读修订证据与公开阅读入口 |
| Forum Compose PC | `b62p7` | `1440 × 900` | 页面态共享 Composer、发布类型、正文主轴、账号草稿与发布检查 |
| Docs Mine Mobile | `J8x9N` | `390 × 844` | 紧凑筛选入口、连续文档列表、稳定分页与“更多”导航 |
| Docs Revisions Mobile | `qfTR9` | `390 × 844` | 顶部版本选择器、单一修订详情与普通 Author 只读边界 |
| Forum Compose Mobile | `GY8pi` | `390 × 844` | 发布类型—标题—Markdown—扩展—发布的单列写作流与“论坛”导航 |
| Author 与 Forum 发布必要关键状态 | `j3gEk` | `1440 × 860` | 六个局部状态；只替换受影响表面，不复制路由 |

七张顶层画板均使用 `guofeng` 主题语义 token、已有 Web Header / Mobile Tab Bar 和 Lucide 图标；所有新增节点均具备可读名称，最终均为 `placeholder: false`。

## 3. Docs Mine 与 Revisions

- Mine 的 dominant region 是连续文档列表。范围、草稿阶段、角色、总数、页码和最近修改顺序均表现为权威结果，不以翻译文本或前端全量结果代替筛选合同。
- PC 选中文档上下文只承载摘要、我的角色、正式版本、当前草稿、最近修改和已有入口；不建立第二套权限判断或大仪表摘要。
- Mobile 不把 PC 右侧 rail 压成首屏卡片堆；筛选进入按需表面，文档行直接进入编辑或修订任务。
- Revisions 的 history 与 detail 在视觉上保持两个独立读取面。详情失败时历史仍在，已有详情刷新失败时保留快照并标记 stale。
- Mobile 版本选择器替代并排时间线；普通 Author 只读停止线、受保护附件和正式公开阅读身份保持可辨识。

## 4. Forum Compose

- 正式 PC / Mobile 路由都直接呈现共享发布器，不使用 Modal 或 Sheet 遮蔽页面上下文；WebOS Sheet 不进入正式页面画板。
- 发布类型只暴露已有普通帖子、问答、投票与抽奖；分类、标签、标题、Markdown、图片 / 附件和扩展设置保持渐进披露。
- 草稿状态明确归属当前账号。切换账号时先清空内存，再读取新账号记录；发布成功只清理当前账号草稿。
- 上传或发布期间，关闭、取消和重复提交统一锁定；失败只由 Composer 本地化反馈一次，父级不追加第二个失败 toast，也不展示原始异常。
- Mobile 保持单列写作与直接发布动作，不复制 PC 发布检查 rail；账号草稿边界以内联提示保留。

## 5. 必要关键状态

- **Mine 首次读取**：无权威快照时显示读取中，不伪造 `0` 篇空态。
- **Mine unavailable**：首次请求失败时保留当前筛选并允许重试当前查询。
- **Mine stale**：同一查询刷新失败时保留上次成功页面、标注陈旧并提供再次刷新。
- **Revision detail unavailable**：历史列表保持可用，只替换失败的详情表面；重试绑定当前 Revision。
- **Composer busy lock**：上传进度与发布生命周期可见，关闭、取消和重复提交均被同一任务锁定。
- **Composer safe failure**：保留当前账号草稿，只展示一次本地化失败；账号变化不继承、覆盖或删除其他账号内容。

## 6. 契约与停止线

- 不新增 API、数据库、migration、权限、LongId 转换、服务端 Forum 草稿、移动壳层或第二套发布状态机。
- 不把审核、发布、回滚、导入导出、治理或删除恢复动作迁入普通 Author。
- 不把 WebOS 窗口、Bottom Sheet、Dock 或桌面能力复制到正式 Web；Tauri 与 Flutter 继续不进入本批。
- Pencil 只固定结构、层级、关键状态和 PC → Mobile 转换；功能、按钮、文案、权限、幂等与存储合同继续服从专题文档和当前代码。

## 7. Pencil 静态复核与下一步

- 七张顶层画板均已逐板截图复核；创建过程中发现的两个无效 Lucide 名称已直接替换，没有遗留警告。
- Pencil 原生 visitor 审计确认 `7` 个 R2-A02 根节点尺寸正确，`placeholder = 0`，累计根边界越界 `outOfRoot = 0`；截图未见布局塌陷、裁切、横向溢出或不可辨识操作。
- 设计文件已通过 Pen 原生保存落盘。本批没有启动 Gateway、API、Auth、Vite 或浏览器，也没有修改正式页面代码。
- 下一步等待人工确认七张代表板；确认后按本记录完成正式视觉实现、Client 定向静态验证和 production build，再在重新说明启动命令、端口与影响并获得授权后执行 Gateway PC / Mobile 成组验收。
