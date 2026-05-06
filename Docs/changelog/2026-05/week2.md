# 2026 年 5 月第 2 周开发日志

> 范围：2026-05-05 至 2026-05-10（Asia/Shanghai）
>
> 本周从多端路线验证回到产品功能补全，优先处理 WebOS / PC 工作台、后端与 Console 治理中用户能点到但不能真正完成的功能缺口。

## 2026-05-06

### 主线判断

- 当前阶段继续保持 `第二开发阶段：社区深化与多端化`，但默认执行重心已从端侧验证、分发材料和低收益微体验，切回产品功能补全。
- WebOS / PC 工作台继续承载已登录用户完整工作流，Console 与后端治理优先补模拟 API、统计、权限、安全、经验治理和商城管理缺口。
- Flutter 移动端保持高价值移动路径，不复刻桌面工作台；公开 Web 壳层只做必要稳定维护。

### 已完成提交

- `8076302d docs: 更新代码行数约束`
  - 同步通用协作规则中的单文件行数建议与硬上限口径。
- `31153430 docs: 精简规划入口文档`
  - 收束 `current.md` 与路线图入口，减少历史流水和长背景堆叠。
- `6dcd2356 feat(client): 优化工作台复访续接`
  - 补强 WebOS / PC 工作台复访续接体验。
- `76ab7501 feat(client): 增加桌面应用恢复入口`
  - 增加桌面应用恢复入口，让复访能回到更具体的应用上下文。
- `c2c64a2f docs(planning): 调整第二阶段产品补全规划`
  - 明确第二阶段回到产品功能补全后的端侧分工、近期批次和当前不做范围。
- `d71d6887 feat(radish-pit): 补齐萝卜坑工作流`
  - 接通支付密码设置 / 修改真实能力，移除模拟等待和 `alert`。
  - 快捷操作可切换到转移、记录、安全、统计；最近交易“查看全部”切到交易记录；复制流水号改为统一 toast。
- `ace78ccf feat(console): 接入用户详情治理数据`
  - 用户详情、萝卜币流水、订单列表从模拟 API 切到真实治理数据。
- `46b03a5a feat(console): 接入个人资料与设置真实能力`
  - Console 个人资料与设置保存动作从占位推进到真实后端能力。
- `e3187c37 feat(console): 接入仪表盘真实统计`
  - 新增统计仓储与服务，`StatisticsController` 从固定 / 随机占位数据切到真实聚合。
  - 补充 Console 统计接口权限种子和统计服务定向测试。
- `3eb72ba4 chore(build): 降低既有构建警告噪音`
  - 移除 `Radish.Extension` 旧 `Microsoft.AspNetCore.Mvc 2.3.0` 包引用，改用 `Microsoft.AspNetCore.App` framework reference。
  - 测试项目启用 nullable，`ImageProcessorTest` 传递 xUnit 测试取消令牌。
  - `AttachmentService` 改用 `File.ReadAllBytesAsync`，避免手写流读取 analyzer 警告。

### 验证记录

- `dotnet build Radish.slnx -c Debug`
  - 通过，`0` 错误。
  - 构建警告从约 `186` 条降至 `120` 条。
  - `NU1903 System.Security.Cryptography.Xml`、`CA2022`、`xUnit1051` 和测试项目大量 `CS8632` 噪音已消失。
- `dotnet test Radish.Api.Tests\Radish.Api.Tests.csproj -c Debug --filter FullyQualifiedName~StatisticsServiceTest`
  - 通过，`4/4`。
- `npm run build --workspace=radish.console`
  - 通过。
  - 仍保留既有 chunk size warning。
- `dotnet test Radish.Api.Tests\Radish.Api.Tests.csproj -c Debug --filter FullyQualifiedName~ImageProcessorTest`
  - 通过，`6/6`。

### 剩余风险与下一顺位

- 构建输出仍有历史 nullable、obsolete、XML 注释和 ASP.NET analyzer 警告，后续宜按模块继续治理，避免用全局抑制掩盖真实问题。
- P2-C1 与 P2-C2 已完成首轮；下一顺位建议从 P2-C3 经验 / 等级治理、P2-C4 商城权益效果收口、P2-C5 移动端高价值已登录链路中选择。
- Tauri 正式签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单和公开分发方式继续后置到真实对外分发前评估。
