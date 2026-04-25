# Phase 2-3 Flutter 客户端 MVP

> 状态：当前主线
>
> 最后更新：2026-04-25（Asia/Shanghai）
>
> 关联文档：
>
> - [开发路线图](/development-plan)
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)

## 1. 目标

`Phase 2-3` 不回答“移动 Web 是否还能再补一点”，而是回答三件事：

1. Radish 是否开始拥有独立于 WebOS 的原生客户端入口
2. Android 高频路径是否能用原生导航、原生手势和原生生命周期承载
3. 仓库是否为后续 Android / Windows / Linux 扩展建立清晰、可维护的 Flutter 基础

## 2. 第一批边界

第一批固定只做 **范围定义 + 工程骨架**，不直接铺完整业务页。

### 2.1 首批纳入

- Android 起步的客户端壳层范围定义
- Flutter 工程最小目录结构与基础入口
- 导航壳层占位：`discover / forum / docs / profile`
- 环境配置与 API 基础地址语义
- 认证存储接口占位与后续接线边界
- 与 Web 侧共享主题语义的最小 Theme 基线

### 2.2 首批不纳入

- Windows / Linux 同批次落地
- 聊天、完整通知中心、完整商城工作台、创作器
- “移动版 WebOS”
- 复刻桌面窗口系统
- 为 Flutter 额外设计一套独立 BFF

## 3. 仓库落点

Flutter 客户端当前固定落在：

```text
Clients/radish.flutter/
```

该目录独立于 `Frontend/` 的 npm workspaces，避免把原生客户端混入 Web 构建链路。

## 4. 首批信息架构

第一批只建立最小客户端壳层，而不是同时交付完整产品页。

```text
Radish Flutter Shell
├── Discover
├── Forum
├── Docs
└── Profile
```

说明：

- `Discover` 作为高价值内容分发入口
- `Forum` / `Docs` / `Profile` 先提供客户端导航占位与信息架构
- 登录态、通知回流与更深业务链路在后续批次继续接入

## 5. 复用约束

Flutter 客户端第一批固定遵循以下约束：

1. 优先复用现有 API、认证契约与公开内容路由语义
2. 不复刻 WebOS 窗口交互，不把桌面工作台直接搬进原生端
3. 主题语义复用 Radish 品牌口径，但不强求与 Web 结构完全同形
4. Android 跑通后，再评估 Windows / Linux 的平台扩展节奏

## 6. 本批交付物

第一批默认交付：

- `Docs/` 真相源文档切换到 `Phase 2-3`
- `Clients/radish.flutter/` 最小工程骨架
- 壳层导航、环境配置、认证存储、Theme 与页面占位
- Flutter MVP 规划文档本页

截至 `2026-04-22` 的已落地事实补充：

- 第一批骨架当前已完成，`Clients/radish.flutter/` 已建立独立入口、壳层导航、环境配置与最小 Theme 基线
- 第二批当前已开始进入真实业务接线：应用启动会话恢复 gate、匿名态 / 已登录态三态，以及 `forum` 首条真实公开只读 feed 读取链路均已落地
- 当前 forum 原生读取链路明确复用现有 `/api/v1/Post/GetList` 契约，只承载匿名列表阅读、`latest / hottest` 排序、基础分页、加载态与错误态
- Android 平台目录当前已生成并纳入仓库，Flutter Android Debug APK 构建已通过
- Android 模拟器最小联调已确认可通过 Gateway `https://localhost:5000` 访问公开只读接口；联调前需要先执行 `adb reverse tcp:5000 tcp:5000`
- Android 真机最小联调当前也已完成一轮人工收口：`discover / forum / docs / profile` 四个主 tab 已可进入，`discover -> docs` 与 `discover -> profile` 的公开跳转当前可正常完成；期间暴露的 `Profile` 统计卡与壳层状态区窄屏溢出已修复并通过复测
- Flutter 当前已补齐真实会话恢复基础能力：Android 本地会话持久化、启动恢复、Access Token 过期判断，以及 refresh token 刷新失败后的匿名回落
- Flutter forum 当前已继续从“公开只读 feed”推进到“公开只读帖子详情”最小链路：列表卡片可原生 handoff 到详情页，详情页当前承载正文、作者/分类/时间、基础统计与原生返回，不同时接入评论、互动提交与编辑治理
- Flutter `discover / docs / profile` 当前已分别接通公开摘要分发、公开文档阅读与公开个人资料读取，四个主 tab 都已完成首批真实只读页面替换，不再停留在统一占位说明
- Flutter forum 当前已继续从“公开只读帖子详情”推进到“公开只读评论阅读”：根评论分页、子评论分页、作者 / 评论作者原生公开 profile 跳转当前都已落地
- Flutter forum 评论精确定位最小闭环当前也已落地：带 `commentId` 打开帖子详情时，原生端会先解析公开评论定位信息，再自动补载目标根评论页与子评论页并滚动到目标评论
- Flutter forum 外部详情 handoff 当前已完成最小收口：原生壳层已支持从 shell 层直接透传 `postId + commentId` 打开 forum 详情，并复用现有评论定位链路落到目标评论
- Flutter forum 外部 handoff 当前已继续先收口到 `public profile` 的最近帖子 / 最近评论：profile 不再发散出独立详情跳转，而是统一复用共享 `ForumDetailHandoffTarget`
- Flutter forum 真实来源接线当前已进一步从 discover 的最小演示入口推进到更接近真实来源的原生壳层 / 宿主层：Android 宿主当前可在启动时透传 forum 通知来源，壳层当前也已支持“最近阅读继续”这一条 browse history 续接能力
- Flutter forum detail 打开路径当前也已统一收口：forum feed、public profile、notification 宿主 handoff 与 browse history 壳层续接当前都已接到同一套原生 handoff 目标，继续保持 `postId / commentId` 的字符串口径
- Flutter 当前已补齐最小登录 UI、显式登出与 Android 浏览器 OIDC 回调闭环：原生壳层当前会通过系统浏览器发起 `/connect/authorize`，以 `radish://oidc/callback` 回跳并完成授权码换 Token；登出当前也已统一走 `/connect/endsession` + `radish://oidc/logout-complete`
- Flutter Android 最小登录连续性当前也已补齐：浏览器取消登录后，壳层会显式展示原生认证提示；`AppBar` 状态区当前已收口到可换行的壳层状态条；从 `profile` 或 forum 详情来源发起登录后，成功返回时也会自动续接回原始 tab / handoff 目标
- Flutter forum detail 当前也已补齐最小原地登录入口：匿名用户可直接在详情页发起登录，登录目标会以可持久化 follow-up 状态保留 `postId / commentId`，浏览器往返或壳层重建后仍能回到当前帖子 / 评论上下文
- Android 真机当前也已完成一轮围绕 forum detail 登录入口的人工联调：详情页登录、取消登录后的显式提示、重试登录与成功回到当前 detail 上下文这几条主路径均已通过
- Android MVP 当前人工验证范围已收口到真实可测链路：登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed、forum detail、评论阅读、评论分页与 detail 原地登录续接优先作为当前可验收面；现阶段没有真实通知入口时，`notification / commentId` 宿主深链只保留为入口条件满足后补验项，不再阻断当前 Android MVP 稳定性复核
- Android MVP 当前可测链路已完成一轮人工验收：登录和退出逻辑确认正常，forum 评论区显示问题已修复并在真机确认；当前无真实 notification 入口，因此 notification 来源 handoff 与 `commentId` 深链继续作为后续补验项
- Flutter 当前已补齐一个最小可测 forum notification 来源：已登录壳层会读取当前用户最新可跳 forum 的通知，解析 `voExtData` 中字符串化的 `postId / commentId`，并通过既有 `ForumDetailHandoffTarget(source: notification)` 打开原生 forum detail
- 最小 forum notification 回流当前已完成真机人工联调：`Forum notification` 入口回到 forum detail / 评论上下文的逻辑确认正常，系统通知栏推送与完整通知中心继续不纳入当前批次

## 7. 当前第二批范围

第二批当前固定收口到两类事情：

1. **最小登录 / 会话恢复边界**
   - 当前已完成启动恢复、匿名态 / 已登录态建模、Android 本地会话持久化、refresh token 恢复回落，以及最小登录 UI / 显式登出 / 浏览器 OIDC 回调闭环
   - 当前也已补齐登录取消后的显式提示、窄屏壳层状态区收口，以及 `profile / forum detail` 两类最小登录后续接；forum detail 内也已具备可手工触发的最小原地登录入口
   - 当前仍未进入更完整的账户治理、多平台原生登录深化与通知中心登录回流联调
2. **首批真实页面接线**
   - forum 当前已从公开列表继续收口到公开详情、评论分页、子评论分页、作者跳转、评论精确定位、public profile 详情回跳，以及 `notification / browseHistory` 的首批壳层 / 宿主 handoff
   - `discover / docs / profile` 首批最小真实页面接线与 Android 真机联调当前都已完成；forum 的首批真实来源接线当前也已完成一轮收口，后续优先转向 Android MVP 当前可测链路的稳定性复核，而不是回头扩壳层占位
   - `notification / commentId` 这类依赖真实通知来源的宿主深链联调，当前已补最小可测来源并完成真机人工联调：已登录壳层读取最新 forum 通知并打开原生 detail；系统通知栏推送、完整通知中心与通知管理继续不纳入本批

## 8. 下一顺位

当前第二批继续推进时，优先顺序建议为：

1. 第二批当前可测链路已完成一轮人工验收，后续不再把重复执行当前 checklist 作为主线扩项
2. 最小 forum notification 来源已完成真机人工联调，后续不再以补可测 notification 入口作为阻断项
3. 下一步在扩展下一个高价值只读原生页面与 Android MVP 打包 / 发布候选收口之间择一
4. 第三批方向明确并完成 Android MVP 稳定后，再评估 Windows / Linux 平台目录与更深原生能力
