# F4-R R1-P02 帖子详情成组实现记录

> 日期：2026-08-08
> 状态：代码实现、静态验证与 Gateway PC / mobile 运行态验收完成，R1-P02 关闭。

## 本批结论

- 正式 `/forum/post/:postId` 详情已按确认的 PC / Mobile 代表设计落地，正文保持连续阅读主轴。
- PC 使用社区导航、正文、线程索引三栏；`1120px` 以下不再堆叠两侧栏，改为顶部紧凑入口与正文后的行内线程索引。
- 复用现有 API、权限、路由、PublicId、登录回流和幂等写入边界，没有新增后端能力或全局主题 token。
- 普通登录读者状态接入帖子与回帖点赞、表情回应、赞赏、回帖的回帖；父回帖“神评”和子回帖“沙发”继续由既有评论树语义承载。
- 轻回应与赞赏保持在正文后的次级参与层；作者删除、投票提交和抽奖操作未进入普通读者代表状态。
- 运行态复核将详情页用户可见的“讨论 / 评论”统一收口为“回帖”；代码、API 与既有 `Comment` 领域标识不改名。

## 结构治理

- `PublicForumDetail.tsx` 从 `2292` 行收敛到 `1500` 行，保留路由数据、写入协调和身份边界。
- 新增独立详情视图，承接正式 PC / Mobile 信息架构和响应式结构。
- 回答分页、评论实时同步、评论定位高亮和公开 Head 快照分别下沉到领域 Hook。
- 帖子点赞高亮沿用论坛既有 `forum_liked_posts` 本地账号状态；点赞总数始终以服务端写入回包为准。

## 交互边界

- 帖子点赞：复用 `likePost`。
- 回帖点赞：复用 `toggleCommentLike`。
- 帖子 / 回帖回应：复用 `useReactions`、批量汇总与既有 toggle 契约。
- 回帖的回帖：继续固定两级展示，提交既有 `parentId / replyToCommentId / replyToCommentSnapshot / replyToUserName` 字段。
- 赞赏、收藏、举报、编辑历史、问答回答和来源返回保持既有权限、intent 与 canonical 规则。
- 未新增点赞或回应登录 intent；匿名交互继续沿用受控评论回流边界。

## 验证结果

- `npm run test --workspace=radish.client`：`491` 项通过。
- `npm run lint --workspace=radish.client`：通过，`0` warning。
- `npm run build --workspace=radish.client`：production build 通过。
- `git diff --check`：通过。
- 新增 R1-P02 静态契约测试，覆盖能力接线、普通读者停止线、三栏 PC 结构和移动端侧栏折叠规则。

## Gateway 运行态验收

- 入口：Gateway `https://localhost:5000/forum/post/pst_019f375f0da37990a58682ae8f669acd`。
- 身份：普通登录读者 `TestUser`；目标为 `Admin` 发布的普通帖子，未出现作者删除、投票或抽奖动作。
- PC：浏览器 CSS 视口 `1440 × 1000 @ DPR 1`。社区导航、正文主轴和本帖索引三栏同时可见；正文列保持约 `820px` 轨道，文档与内部滚动容器均无横向溢出。
- Mobile：浏览器 CSS 视口 `390 × 844 @ DPR 1`。桌面两侧栏均隐藏，顶部紧凑入口与正文后行内索引进入内容流；正文、动作带、轻回应、回帖编辑器和回帖流保持单列，无页面级横向溢出，底部导航不遮挡当前操作目标。
- 交互：帖子与回帖点赞均完成一次写入并反向恢复；帖子与回帖 Unicode reaction 均完成一次写入并反向恢复；回帖目标提示、取消回复、轻回应 / 回帖快捷聚焦、复制链接、来源返回和 canonical 均通过。未创建新轻回应、回帖、收藏或赞赏。
- 赞赏：`GetTargetStates` 正常返回帖子和回帖目标状态；本地既有 `Forum.ContentReward.Enabled` 按专题验收后的默认值保持关闭，因此代表页按现有状态机隐藏零记录赞赏面板。本批未修改运行设置、未执行余额写入；能力接线继续由静态契约与既有 F4-N-D 专题验收覆盖。
- 语言 / 主题：`zh-CN / en-US`、`default / guofeng` 均通过；英文 `Replies / Write reply` 与中文“回帖 / 写回帖”口径一致，切换后无页面级横向溢出。
- 日志：最终重新加载窗口内浏览器 `error / warning` 为 `0`；登录前历史 refresh token 失效日志不计入已重新建立的 TestUser 会话。
- 截图：PC 顶部、PC 回帖区、Mobile 顶部、Mobile 回帖区已在本次验收交付中提供。浏览器页面、视口覆盖与本轮启动的服务均在验收后清理。

## 后续顺位

- R1-P02 已关闭；下一步回到 F4-R C-1B 的代表页顺位选择，不在本记录中提前进入下一页面实现。
- 全局品牌 token、赞赏运行开关、作者删除、投票和抽奖状态仍遵循各自既有专题与后续代表状态，不在普通读者代表页中扩张。
