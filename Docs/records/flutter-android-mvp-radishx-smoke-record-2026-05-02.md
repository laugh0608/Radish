# Flutter Android MVP 正式域名临时 smoke 记录

- 记录日期：2026-05-02
- 记录人：laugh0608
- 验证性质：正式域名临时兼容性 smoke
- Gateway：`https://radishx.com`
- 服务端版本：`v26.3.2-release`
- 客户端范围：当前 `Phase 2-3 Flutter 客户端 MVP`

## 验证摘要

本轮使用 `https://radishx.com` 作为临时 Gateway，对当前 Flutter Android MVP 做一轮正式域名兼容性 smoke。

该域名背后的服务端版本为 `v26.3.2-release`，早于当前 `Phase 2-3 Flutter 客户端 MVP` 的第二十批开发状态。因此本轮只判断当前客户端是否能连接正式 HTTPS 域名并读取既有公开内容，不判断当前 Flutter MVP 的完整 RC / release 外部分发验收已完成。

## 执行环境

- Android Studio 虚拟机：已验证
- Android 真机：已验证
- Gateway 基址：`https://radishx.com`
- 验证方式：用户手动复核

## 已验证项

1. 帖子公开读取可用，未见异常
2. 文档公开读取可用，未见异常
3. 用户公开信息读取可用，未见异常
4. Android Studio 虚拟机与 Android 真机上的基础只读读取结论一致

## 未覆盖项

本轮未作为完整 RC / release 验收，不覆盖以下事项：

1. testing Gateway 环境的 release APK 构建与安装
2. `flutter analyze`、`flutter test`、Android JVM 单测与签名检查的本轮重新执行
3. 登录、退出、会话恢复与 OIDC 浏览器回调完整链路
4. forum detail 轻回应发布、最小 forum notification 回流与评论定位完整复核
5. 第八批至第二十批新增复访、刷新、局部降级与长文本窄屏显示的完整批次级真机验收
6. 外部分发对象、反馈闭环与正式 release 前回归留痕

## 失败归类口径

若后续使用 `https://radishx.com` 临时 smoke 时发现某些当前 Flutter MVP 新链路失败，优先按以下顺序分诊：

1. 服务端版本是否仍为 `v26.3.2-release`，是否缺少当前 Flutter MVP 后续批次依赖的接口、字段或数据
2. 当前链路是否属于公开只读读取，还是登录、写入、通知回流或个人复访等更高依赖链路
3. Android 客户端是否通过 `RADISH_GATEWAY_BASE_URL=https://radishx.com` 显式指定 Gateway，而不是误连本机开发地址
4. 若同一链路在 matching testing Gateway 上失败，再按 Flutter 客户端或服务端契约回归继续定位

## 结论

`https://radishx.com` 可作为当前 Flutter Android MVP 的临时正式域名 smoke 目标。Android Studio 虚拟机与 Android 真机均已确认帖子、文档和用户公开信息基础只读读取未见异常。

该结论可以补强“正式域名 HTTPS Gateway 兼容性”的信心，但不替代正式 release 包发布前的 testing Gateway、release APK 构建、真机安装、登录 / 通知 / 写入链路与批次级外部分发回归。
