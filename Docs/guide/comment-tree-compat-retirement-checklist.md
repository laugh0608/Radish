# `GetCommentTree` 兼容入口退场清单

> 本页用于收束旧评论树兼容入口 `GET /api/v1/Comment/GetCommentTree` 的观察、确认与正式删除顺序，避免后续继续依赖口头约定判断“现在能不能删”。
>
> 当前主线背景见：[M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)

## 适用场景

以下场景优先参考本页：

- 想确认 `GetCommentTree` 当前是否还值得保留
- 评论详情、评论分页或评论 `HttpTest` 刚发生调整
- 想把旧评论树兼容入口从“观察池”推进到“正式删除窗口”
- 想确认当前是否还有仓库外调用方依赖旧接口

## 当前已确认事实

截至 `2026-04-06`，当前已确认以下事实成立：

- 前端主链已经切到“根评论分页 + 子评论懒加载”正式契约
- 仓库内高频评论详情访问当前不再依赖 `GetCommentTree`
- `GetCommentTree` 当前已从 API 展示面隐藏
- 兼容入口命中时，当前会补：
  - 响应头 `X-Radish-Deprecated`
  - 日志 `legacy GetCommentTree invoked`
- 仓库内未被任何调用使用的旧树方法 `ICommentService.GetCommentTreeAsync` 已移除
- 本轮论坛评论专项回归已通过：回复父评论、回复子评论、编辑父评论 / 子评论后的子评论展开态、点赞、删除与历史记录当前均正常
- 本轮日志复核已确认未见有效 `legacy GetCommentTree invoked` 命中
- 当前仓库内保留的旧评论树相关资产只剩：
  - `CommentController.GetCommentTree`
  - `ICommentService.GetCommentTreeWithLikeStatusAsync`
  - `CommentService.GetCommentTreeWithLikeStatusAsync`
  - 两个“仅用于观察旧调用”的 `HttpTest` 条目

## 正式删除前必须同时满足的条件

只有以下三项**同时成立**，才进入正式删除窗口：

1. **仓库内依赖已清零**
   - 前端主链、仓库内控制器 / 服务直接调用、专题文档默认主链都已不再依赖 `GetCommentTree`
   - 观察用途的 `HttpTest` 条目已确认可以一并移除

2. **真实命中观测持续为空**
   - 兼容入口命中日志在约定观察窗口内持续无有效命中
   - 若仍有命中，需要先确认调用来源，而不是直接删除接口

3. **仓库外依赖事实已关闭**
   - 当前部署环境、反代、自定义脚本、外部联调资产或第三方调用方都已完成事实确认
   - 不能只根据“仓库里没搜到”就默认视为仓库外也无依赖

## 推荐观察窗口

当前建议采用以下保守口径：

- **最短观察窗口**：连续 `7` 天无有效命中
- **更稳妥的窗口**：覆盖至少 `1` 次测试部署 / 生产环境例行使用周期

如果观察窗口内发生以下任一变化，应重新起算：

- 评论主链再次调整
- 部署形态、反代或联调方式发生变化
- 新增仓库外脚本、巡检或第三方消费者

## 默认检查顺序

### 1. 先确认仓库内主链事实

优先确认：

- 前端评论详情是否只走：
  - `GetRootComments`
  - `GetChildComments`
- 论坛专题文档是否已把分页链路当作默认主链
- `HttpTest` 是否仅把 `GetCommentTree` 标为“观察旧调用”，而不是主链步骤

当前若这一步仍不成立，不进入删除窗口。

### 2. 再查兼容入口命中日志

优先关注以下日志特征：

```text
[CommentController] legacy GetCommentTree invoked for PostId=..., SortBy=...
```

当前推荐至少记录：

- 观察起止日期
- 是否出现命中
- 命中次数
- 命中环境（本地 / test / prod）
- 是否能追到调用来源

如果日志有命中，先回答：

- 是人工观察触发，还是业务真实调用
- 是仓库内测试资产，还是仓库外调用方
- 是否只是短期排障痕迹

### 3. 再查仓库外事实

优先确认：

- 当前部署脚本是否仍显式调用旧接口
- 反向代理、巡检脚本或监控探针是否误打到旧接口
- 是否存在仓库外联调文档、第三方客户端或本地工具仍依赖旧评论树响应

如果这一层无法确认，当前结论只能维持“继续观察”，不能直接删。

## 达到删除窗口后的执行顺序

当三项前置条件同时成立后，建议按以下顺序收口：

1. 移除两个“观察旧调用”用途的 `HttpTest` 条目
2. 删除 `CommentController.GetCommentTree`
3. 删除 `ICommentService.GetCommentTreeWithLikeStatusAsync`
4. 删除 `CommentService.GetCommentTreeWithLikeStatusAsync`
5. 同步更新：
   - `planning/current.md`
   - `development-plan.md`
   - 论坛相关功能文档
   - 周志 / 回归记录

不建议先删服务实现、再长时间保留控制器空壳；一旦进入正式删除窗口，应尽量同批次完成。

## 最小记录模板

建议每次观察或删前评审至少保留一条记录：

```md
### `GetCommentTree` 兼容入口观察记录（YYYY-MM-DD）

- 记录日期：YYYY-MM-DD
- 记录人：<name>
- 观察窗口：<start> ~ <end>
- 仓库内主链依赖：已清零 / 未清零
- 命中日志：无 / 有
- 命中来源：无 / <summary>
- 仓库外依赖事实：已关闭 / 未关闭 / 待确认
- 当前结论：继续观察 / 可进入正式删除窗口 / 暂不删除
- 遗留项：无 / <remaining items>
```

## 当前结论

截至 `2026-04-06`，当前结论已更新为：

- **正式删除窗口已就绪**
- **本轮观察前置条件已完成当前批次确认**
- **下一批次应一并删除兼容控制器 / 服务实现与两个观察用 `HttpTest` 条目**

换句话说，当前最合理的下一步已不是“继续观察”，而是按本页既定顺序完成正式删除批次，并同步更新文档与回归记录。

## 本轮观察记录（2026-04-06）

- 记录日期：2026-04-06
- 记录人：Codex 协作记录
- 观察窗口：2026-04-06 本轮论坛评论专项回归
- 仓库内主链依赖：已清零
- 命中日志：无
- 命中来源：无
- 仓库外依赖事实：当前测试范围内未发现有效依赖，且用户确认日志无 `GetCommentTree`
- 当前结论：可进入正式删除窗口
- 遗留项：待下一批次删除 `CommentController.GetCommentTree`、`ICommentService.GetCommentTreeWithLikeStatusAsync`、`CommentService.GetCommentTreeWithLikeStatusAsync` 与两个观察用 `HttpTest`
