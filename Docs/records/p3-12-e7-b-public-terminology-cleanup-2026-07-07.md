# P3-12-E7-B Public 术语清理记录

> 日期：2026-07-07（Asia/Shanghai）
>
> 状态：代码实现与静态验证已完成
>
> 上游记录：[P3-12-E7 Console / Public UI 与文案成熟度首批差距审计](/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07)

## 目标

`E7-B` 只处理用户侧可见术语，不改变功能边界、路由契约、API、权限、数据模型或运行时行为。

本批重点清理以下正式产品阻断词：

- `公开壳层`
- `正式 Web 私域路由`
- `私域路由`
- `桌面工作台`
- `公开 docs`
- 用户可见的 `reaction`

## 实施范围

已覆盖：

- `radish.client` 公开发现、公开论坛、公开 Docs、公开商城、公开榜单、公开主页的 i18n 文案。
- Workbench、通知、消息、圈子、宠物、资产和隐私安全页中与公开页跳转相邻的可见说明。
- 公开 head 默认描述。
- Web shell 的导航 aria label。
- Docs 作者入口中面向用户的边界说明。

替换口径：

- `公开壳层` 改为 `公开阅读页`、`公开文档页`、`公开商城页` 等页面化表达。
- `正式 Web / 私域路由` 改为 `网页端`、`登录后的个人页面`、`个人账号路径`。
- `桌面工作台` 类表达改为 `个人页面`、`兼容入口` 或 `登录后操作`。
- `公开 docs / 公开 forum` 改为 `公开文档 / 公开论坛`。
- 用户可见 `reaction` 改为 `表情回应` 或 `responses`；代码 key、API 类型、CSS class 和接口命名保持不变。

## 停止线

本批未做：

- 未修改路由路径、登录回流、来源返回、权限判断或 API 调用。
- 未修改后端、数据库、OpenIddict、审计或错误模型。
- 未处理 Auth 授权页信息层级；该项继续进入 `E7-D`。
- 未处理 Public / Docs / Forum / Shop 信息密度和布局问题；该项继续进入 `E7-C`。
- 未执行 Gateway 真实页面 smoke；本轮未在执行 smoke 前重新获得前后端已启动确认。

## 本地验证

已通过：

- `npm run build --workspace=radish.client`
- `git diff --check`
- `npm run check:repo-hygiene:changed`
- 目标术语扫描：`公开壳层 / 正式 Web / 私域路由 / 账号私域 / 公开 docs / 公开 Docs / 桌面工作台 / 桌面壳层 / 桌面交互 / 公开 forum / 表情回应Sticker / reaction ` 等目标词在本批覆盖文件中无命中。

未执行：

- Gateway 真实页面 smoke：本轮未在执行 smoke 前重新获得前后端已启动确认。
