# P3-9-D Console 排障与治理入口整备记录（2026-06-07）

> 本记录承接 `P3-9-D Console 排障与治理入口整备`。
>
> 本批围绕 Console 管理员从用户、订单、商品、胡萝卜流水和角色授权入口定位同一笔业务，复核返回态、刷新态、失败态、无权限 / 保存态阻断和 LongId 字符串契约。

## 批次结论

本批没有扩展完整移动商城、完整通知中心、完整资产中心、完整创作器、WebOS / Tauri 新功能、完整 PublicId 迁移或完整 E2E 平台。

自动化复核确认角色授权 helper 已覆盖 `roleId`、`resourceId`、`apiModuleId` 字符串保留、保存 payload 稳定排序，以及加载中、保存中、无权限时禁用保存和勾选。订单与商品 URL helper 已覆盖 `returnTo` 合法同源相对路径、`orderId / productId / userId / businessId` 字符串口径和来源返回。

进一步代码复核发现一个发布候选排障缺口：订单列表支持通过 URL 恢复详情弹窗，但管理员在列表内点击“详情”时只更新组件状态，不同步 `orderId + openDetail` 到 URL，刷新后会丢失当前订单排障上下文。用户详情、胡萝卜流水和商品页进入订单路径也存在手写查询串，后续容易和订单 URL helper 口径漂移。

处理方式是在订单 URL helper 中增加订单详情查询与路径构建器，订单列表点击详情时同步 URL；用户详情、胡萝卜流水和商品相关订单入口统一复用订单 helper。新增 console 静态契约测试，确保跨模块排障入口继续使用统一 helper。

## 处理范围

| 项目 | 处理结果 |
| --- | --- |
| 订单详情刷新态 | 列表点击“详情”后同步 `orderId`、`openDetail=1`、当前筛选和合法 `returnTo` |
| 用户详情到订单 | 最近订单和订单型流水进入订单详情时复用 `buildOrderDetailPath` |
| 胡萝卜流水到订单 | `BusinessType=Order / BusinessId=OrderId` 进入订单详情时保留当前流水页来源 |
| 商品到订单 | 商品相关订单入口复用订单 URL helper，并继续保留商品详情返回来源 |
| 角色授权 | 本批未改角色授权逻辑；继续由既有 helper 守护保存态、无权限态和字符串 ID |
| 自动化守护 | 增加订单详情 URL helper 测试和 Console 排障路径静态契约测试 |

## 验收覆盖

| 路径 | 覆盖点 | 自动化守护 |
| --- | --- | --- |
| 用户详情 -> 订单详情 | 从最近订单或订单型流水进入订单详情，返回用户详情来源 | `orderListUrlState.test.ts`、`consoleTroubleshootingPathContracts.test.ts` |
| 订单列表 -> 订单详情 | 列表点击详情后刷新仍能按 URL 恢复同一订单详情 | `orderListUrlState.test.ts`、`consoleTroubleshootingPathContracts.test.ts` |
| 订单详情 -> 商品 / 用户 / 流水 | 跨页面跳转保留当前订单列表或详情来源 | `orderListUrlState.test.ts`、`productListUrlState.test.ts` |
| 商品详情 -> 相关订单 | 商品详情进入相关订单后仍能返回商品详情或原订单来源 | `productListUrlState.test.ts`、`consoleTroubleshootingPathContracts.test.ts` |
| 胡萝卜流水 -> 订单详情 | `businessType=Order` 与 `businessId=OrderId` 保持字符串并保留来源 | `orderListUrlState.test.ts`、`consoleTroubleshootingPathContracts.test.ts` |
| 角色授权 | 保存 payload、资源树勾选、接口预览和按钮禁用态稳定 | `rolePermissionHelpers.test.ts` |

## 验证记录

执行目录：仓库根目录。

```bash
npm run test --workspace=radish.console
npm run type-check --workspace=radish.console
npm run lint:changed
npm run build --workspace=radish.console
npm run check:repo-hygiene:changed
node Scripts/check-repo-hygiene.mjs Frontend/radish.console/package.json Frontend/radish.console/src/pages/Coins/CoinAdminPage.tsx Frontend/radish.console/src/pages/Orders/OrderList.tsx Frontend/radish.console/src/pages/Orders/orderListUrlState.ts Frontend/radish.console/src/pages/Products/ProductList.tsx Frontend/radish.console/src/pages/Users/UserDetail.tsx Frontend/radish.console/tests/orderListUrlState.test.ts Frontend/radish.console/tests/consoleTroubleshootingPathContracts.test.ts Docs/records/index.md Docs/records/p3-9-d-console-troubleshooting-governance-record-2026-06-07.md
git diff --check
npm run validate:identity
```

结果：

- `npm run test --workspace=radish.console`：通过，`20` 项测试通过。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run lint:changed`：通过；仍报告既有 `OrderList.tsx` 与 `ProductList.tsx` hook 依赖 warning，未产生 lint error。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过，检查 `7` 个已跟踪变更文件。
- `node Scripts/check-repo-hygiene.mjs ...`：通过，显式检查 `10` 个变更 / 新增文件。
- `git diff --check`：通过。
- `npm run validate:identity`：通过，LongId 字符串安全扫描未发现回归；后端身份语义定向测试 `14` 项通过，仍有项目既有 XML 注释 warning。

未启动本地服务，未执行 Console 管理员账号人工复核。需要人工复核时，由用户启动 Gateway 后访问 `https://localhost:5000/console/`，按用户详情、订单详情、商品详情、胡萝卜流水和角色授权路径逐项确认。

## 后续建议

- 若本批后续未暴露新的 Console 管理员路径阻断，可继续进入 `P3-9-E 发布候选路径总回归与收口结论`，把 P3-9-A/B/C/D 的自动化证据、人工复核清单和已知风险汇总到一份发布候选结论。
- 若人工复核命中新的 Console 缺口，只回拉影响排障路径的状态恢复、失败态或身份契约问题，不扩大为完整管理后台重做。
