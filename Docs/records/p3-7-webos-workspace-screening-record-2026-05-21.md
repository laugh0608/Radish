# P3-7 WebOS / PC 工作台高信号候选筛查记录（2026-05-21）

## 结论

`P3-7-B` 高信号候选筛查未发现新的 `P0/P1` 或需要立即拉起的小闭环，当前转入真实使用观察。

本轮只筛查真实使用断点，不扩大到低收益按钮、样式、微交互、完整 E2E、运营平台或全量 `PublicId`。

## 筛查范围

- 桌面启动续接：`/desktop` 外部入口参数、已处理 query 清理。
- 最近应用：可恢复窗口参数、临时导航参数过滤、最多 5 条、排除开发 / 外部入口。
- 通知分流：chat / forum / user / order 高信号业务跳转。
- 工作台回流：浏览历史 forum / docs / shop 解析，PublicId / long fallback 边界。
- 窗口参数：forum / chat 大整数 ID 字符串精度，PublicId 优先。
- 窗口几何：业务参数记忆位、`__navigationKey` 忽略、越界恢复。

## 验证

- `node --test --test-isolation=none ./tests/desktopEntryNavigation.test.ts ./tests/desktopRecentApps.test.ts ./tests/workspaceNavigation.test.ts ./tests/forumNavigation.test.ts ./tests/chatNavigation.test.ts ./tests/windowGeometry.test.ts ./tests/desktopExternalUrl.test.ts`
  - 结果：`67/67` 通过。
- `npm run type-check --workspace=radish.client`
  - 结果：通过。
- `http://localhost:3000/desktop`
  - 结果：`200`。
- `http://localhost:5100/health`
  - 结果：`200`。

## 后续观察

- 只在真实使用、运行日志或发布前回归暴露高信号断点时回拉小闭环。
- 若后续改动商城、通知、聊天、forum / docs / shop 回流、桌面窗口参数或登录态，再做对应定向复核。
- 未出现新证据前，不继续主动扩 WebOS 复访入口、不扫低收益 UI 细节、不启动完整 E2E 或运营平台。
