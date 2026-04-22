# Phase 2-3 Flutter 客户端 MVP

> 状态：当前主线
>
> 最后更新：2026-04-22（Asia/Shanghai）
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

## 7. 当前第二批范围

第二批当前固定收口到两类事情：

1. **最小登录 / 会话恢复边界**
   - 当前已完成启动恢复、匿名态 / 已登录态建模、Android 本地会话持久化与 refresh token 恢复回落
   - 暂不进入完整登录 UI、显式登出治理与完整浏览器 OIDC 回调接线
2. **首批真实页面接线**
   - forum 当前已从公开列表继续收口到公开详情、评论分页、子评论分页、作者跳转、评论精确定位、public profile 详情回跳，以及 `notification / browseHistory` 的首批壳层 / 宿主 handoff
   - `discover / docs / profile` 首批最小真实页面接线与 Android 真机联调当前都已完成；forum 的首批真实来源接线当前也已完成一轮收口，后续优先转向登录边界与 Android 稳定性复核，而不是回头扩壳层占位

## 8. 下一顺位

当前第二批继续推进时，优先顺序建议为：

1. 最小登录 UI、显式登出治理与后续浏览器 OIDC 回调接线评估
2. 基于现有宿主 / 壳层 handoff 的 Android MVP 稳定性回归与更多真机联调
3. 在现有统一 handoff 目标基础上继续补剩余高价值真实来源，而不是回头保留 discover 演示入口
4. 在 Android MVP 稳定后再评估平台目录与更深原生能力
