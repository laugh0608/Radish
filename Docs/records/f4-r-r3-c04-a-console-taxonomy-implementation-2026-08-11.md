# F4-R R3-C04-A Console Categories / Tags 与共享响应式资源表面实现

> 日期：2026-08-11（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Console Categories、Tags、无业务状态的共享响应式资源列表表面

## 本批结论

R3-C04-A 已关闭 Categories / Tags 的 LongId、分页回跳、过期响应、写入权限、列表权威状态和脏表单风险，并让两类资源在同一查询快照上分别渲染 PC 连续表格与 Mobile 资源卡。共享层只承载布局与响应式结构，不接管资源查询、权限、表单或 API 状态机。

## 实现事实

- `CategoryVo.voId / voParentId / parentId` 与 `TagVo.voId` 及所有动作 ID 均改为字符串；创建接口返回值同步使用字符串。
- 筛选草稿与已应用查询分离，分页只由已应用查询驱动；请求代际忽略过期响应，查询快照键防止把上一组筛选结果误标为当前结果。
- 首次读取失败显示 `unavailable`，同查询刷新失败保留上次快照并显示 `stale`；两种状态均冻结新增、编辑、启停、排序、删除和恢复写入。
- 写 handler 复核动作级权限；Form 再复核 create / edit 权限。提交或图片上传期间拒绝关闭，未保存变更关闭与页面离开均有确认停止线。
- Category 父级选项按服务端最大 `100` 条逐页完整读取，不再请求一个会被服务端截断的 `200` 条伪全量页面。
- 新增 `ConsoleResourceList` 作为无业务状态壳层：PC 保留筛选—连续表格—上下文 rail，Mobile 使用结果摘要、按需 Bottom Sheet、连续资源卡与轻量翻页。
- Categories / Tags 继续各自拥有查询、权限、文案、表单和写入流程；没有新增万能 CRUD hook、统一 DTO 或控制器。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| `npm run test --workspace=radish.console` | `87 / 87` 通过，含 4 组 R3-C04-A 契约测试 |
| `npm run type-check --workspace=radish.console` | 普通与 strict TypeScript 均通过 |
| `npm run lint --workspace=radish.console` | 通过，`0 warning` |
| `npm run build --workspace=radish.console` | production build 通过 |
| `npm run check:console-permissions` | 四层权限对象基础对齐通过；仅保留既有 `console.hangfire.replay` 未引用警告 |
| `npm run check:long-id-safety` | `681` 个前端 / Flutter 文件扫描通过 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过 |

## 停止线与未执行项

- 未修改后端、数据库、migration、权限键、公开 Forum 分类 / 标签语义或 Pencil。
- 未启动 API、Gateway、Console dev server 或浏览器；PC `1440 × 900`、Mobile `390 × 844`、最小权限与双语运行态验收留到获得当轮启动授权后的成组验收。
- 既有三处 `DateTime.Now` 漂移与全仓历史卫生债务仍留在独立维护线。
- 下一批只进入 `R3-C04-B Users`，不提前迁移 Applications、Products、Stickers 或 Coins。
