# Flutter Android MVP docs 详情只读上下文补强记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 下的 docs 公开文档详情页、详情错误态与窄屏长 slug 显示

### 变更摘要

- docs 详情页新增轻量只读上下文，明确来源、公开地址与只读边界
- docs 详情顶部 slug 与来源类型芯片改为受控宽度显示，避免长 slug 在窄屏详情页撑破布局
- docs 详情错误态新增目标 `/docs/:slug` 提示，并引导返回来源后重试
- 列表内联详情与 discover / profile / 文档内链等 route 详情继续共用同一套详情内容展示

### 影响专题

- Flutter Android MVP
- Flutter docs 公开只读阅读
- Flutter docs 最近文档 / discover 文档直达 / 文档内链 route 详情

### 自动化执行

- `flutter test test/docs_page_test.dart`：通过
- `flutter test test/smoke_test.dart`：通过
- `flutter analyze`：通过
- `git diff --check`：通过
- `npm run check:repo-hygiene:staged`：通过，已检查 6 个 staged 变更文件
- `npm run check:identity-impact:staged`：通过，命中 `Docs/guide/regression-index.md` 与 `Docs/planning/current.md` 的默认执行面文档 / 门禁资产类别

说明：

- 本轮只触达 Flutter docs 展示层与 widget / smoke 测试
- 本轮未触达后端接口契约、数据库迁移、Android 原生平台代码、身份协议输出或 Gateway 配置

### 人工验收

- 执行情况：未执行
- 摘要：
  - 用户已明确个人开发阶段暂缓真机 APK 安装与 testing Gateway 验收
  - 本轮以 Flutter widget / smoke 测试覆盖 docs 详情来源、只读上下文、长 slug 窄屏与错误态提示

### 部署复核

- 执行情况：未执行
- 摘要：
  - 本轮未进入 testing Gateway release APK 构建、真机安装、外部分发或生产部署

### 结论

- docs 详情只读上下文补强已完成代码与自动化验证，可作为第十一批首个窄范围小闭环收口
- 本轮继续保持公开只读边界，不扩展目录树、编辑、发布、回收站、版本历史、完整 Markdown 引擎、完整浏览历史中心或新的后端 API
- Android MVP RC 分发线在个人开发阶段继续暂缓：testing Gateway、测试对象、反馈闭环与真机安装复核统一留到正式 release 包发布前

### 风险 / 后置项

- 真机安装与真实 Gateway 联调本轮按用户要求跳过，正式 release 包发布前再补齐
- 文档目录树、编辑 / 发布 / 版本治理、完整 Markdown 引擎、外部浏览器打开、完整浏览历史中心、跨端同步治理、iOS 移动端评估、Tauri + WebOS 桌面安装包评估与商店发布继续后置
