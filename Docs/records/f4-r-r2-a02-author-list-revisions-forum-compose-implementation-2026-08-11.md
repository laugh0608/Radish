# F4-R R2-A02 Author 列表、修订与 Forum Compose 正式实现记录

> 日期：2026-08-11
> 状态：正式代码、静态验证与 Gateway PC / Mobile 运行态验收均已完成
> 范围：Docs Mine、Docs Revisions、Forum 帖子 / 评论 Revision、正式 `/forum/compose`

## 结论

R2-A02 已按确认代表设计落地正式前端代码。Docs 与 Forum 共用双快照差异组件，默认比较相邻版本 `vN-1 → vN`，可切换“选中版本 → 当前版本”；PC 使用并排差异，`720px` 及以下使用 unified diff。比较基准失败只影响对应一侧并保留已选快照，未把 unavailable 伪装为空内容。

正式 `/forum/compose` 继续只有一个 `ForumPostComposer` 实例：PC 底部半屏为 `54dvh`，Mobile 为 `72dvh`，全屏通过原位状态切换实现。标题、正文、附件、上传状态、账号草稿和提交标识不会因半屏 / 全屏切换而重建；上传或提交期间继续冻结关闭和路由离开。

本批没有新增 API、数据库、migration、权限、服务端草稿、移动壳层或第二套发布状态机，也没有修改 Pencil。Gateway 已使用种子管理员完成 PC `1440 × 900` 与 Mobile `390 × 844` 成组验收；运行态发现的 Mobile 选中文档行响应式断点和 Composer 图标按钮可访问名称均已在 R2-A02 边界内修正并复验，专题正式关闭。

## 实现范围

### 1. 共享双快照差异

- 新增共享 `ContentSnapshotDiff`，统一文档、帖子和评论的字段 / 正文差异表达。
- 行级差异使用前后缀收敛后的 LCS；对超大比较矩阵设置明确上限，避免长内容把浏览器主线程和内存拖入无界增长。
- PC 并排列出两侧行号与删除 / 新增语义，Mobile 按 unified diff 顺序展示；颜色只消费现有语义 token。
- 任一侧 loading / unavailable / retry 独立展示；另一侧已有快照继续保留。

### 2. Docs Mine 与 Revisions

- Mine 删除重复的大型摘要区，保持“权威筛选—连续列表—从属上下文”主轴；列表显式选择决定 PC 右侧上下文，Mobile 不再保留挤压主任务的 rail。
- Revisions 从超长容器中拆为正式页面组件，默认选中当前版本并加载前一版本作为比较基准。
- 保留 history、selected detail 与 comparison baseline 三条独立请求状态和请求代际；切换版本或比较模式不会让迟到响应覆盖当前选择。
- 支持“与上一版比较”和“与当前版比较”；不存在前一版时给出明确边界，不制造虚假 v0。

### 3. Forum Revision

- 帖子与评论 Revision 弹层接入同一差异组件和相同相邻 / 当前比较模式。
- 帖子快照同时比较标题、分区、类型、标签、封面与附件标识；评论保持正文差异。
- 跨分页寻找相邻基准时仍使用现有 Revision list / detail API；读取失败保留已选版本并提供局部重试。
- 恢复权限、当前版本冲突、治理和全文读取边界保持不变。

### 4. Forum Compose 双形态

- 正式页面只挂载一次 `ForumPostComposer`，页面上下文退到背景辅助层。
- PC 半屏固定在底部 `54dvh`，Mobile 半屏固定为 `72dvh`；全屏使用同一组件实例切换样式，不依赖路由重建。
- Mobile 收紧头部元信息和动作文案，保留模式、设置、全屏、关闭、编辑与发布主路径。
- Composer 将上传 / 提交 busy 状态上报给正式 Public Forum 壳层，继续复用全局导航锁。

## 静态验证

- Client 定向契约与差异算法测试：`54 / 54` 通过。
- Client 全量测试：`536 / 536` 通过。
- Client type-check：通过。
- Client Lint：通过。
- Client production build：通过；仅保留既有大 chunk 提醒，没有新增构建错误。
- `git diff --check`：通过。
- 变更文件仓库卫生：通过。

`npm run validate:baseline:quick` 已执行，但停在既有时间语义预算：`Radish.Repository/SystemConfigStorageCoordinator.cs` 仍有 `3` 处 `DateTime.Now`，与当前允许增量 `0` 不一致。该问题已在 `Docs/planning/current.md` 作为独立维护线登记，本批不混入 R2-A02；在 `PR -> master` 前仍必须关闭或形成经过审计的新基线结论。

全量 `npm run check:repo-hygiene` 仍报告仓库既有 `95` 个编码 / 末尾换行问题和 `21` 个文档篇幅提醒；本批变更文件不在其问题清单中，不在功能实现中扩大为全仓清理。

## Gateway 运行态验收

本轮按 [浏览器 smoke 指南](/guide/browser-smoke) 启动 Gateway、API、Auth、Client 与 Console，并通过 Gateway 使用种子管理员复核；没有发布帖子、保存文档、恢复版本或制造临时业务数据。验收用本地草稿已清空，语言与主题已恢复为账号原有的 `en-US / guofeng`。

1. Docs Mine 在 PC 完成 `Collaborating → All` 权威筛选和显式选择，右侧上下文随选择出现；Mobile 隐藏从属 rail，主任务连续且无横向溢出。运行态发现选中行遗漏 `860px` 单列断点，导致操作列挤压正文；已将 `.documentRowSelected` 纳入与普通行相同的响应式规则，并增加静态契约守卫。
2. Docs Revisions 在种子文档仅有 `v1` 的真实边界下显示 `v— → v1` 和最早版本说明；PC 使用并排差异，Mobile 使用 unified diff，已有一侧快照正常保留且无横向溢出。种子数据没有第二个版本，因此多版本切换与迟到响应继续由请求代际和静态测试守卫，本轮没有为验收制造文档版本。
3. Forum 帖子 Revision 在 PC / Mobile 展示标题、分区、内容类型、标签、封面、附件和正文差异；评论 Revision 在 Mobile 展示 unified diff，首版边界与恢复不可用状态准确。种子帖子同样只有 `v1`，没有触发恢复写操作或伪造多版本数据。
4. `/forum/compose` 的同一实例在 PC `54dvh`、Mobile `72dvh` 半屏与全屏之间往返，标题、正文、分区和标签状态均保留；`default / guofeng` 与中英文代表状态、发布禁用、草稿清空及无横向溢出通过。运行态发现 Mobile 隐藏按钮文字后设置 / 全屏图标失去可访问名称；已补动态 `aria-label` 并增加契约测试。
5. 最终使用全新标签依次复核 Compose、Docs Mine、Docs Revisions 和 Forum Post Revision；页面均无横向溢出，浏览器控制台为 `0 warning / 0 error`。

## 下一步

R2-A02 已关闭。F4-R 下一顺位转为 R3 路由继承实施分批审计：先按既有继承表核对正式路由、当前代码、局部差异、关键状态与 Mobile 转换，再确定首个成组实现批次；继承不成立时必须停止扩张并重新裁决 R1 / R2，不为 R3 复制 Pencil 画板。
