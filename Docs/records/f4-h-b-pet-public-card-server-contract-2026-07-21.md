# F4-H-B 电子宠物公开名片服务端权威契约完成记录

> 日期：2026-07-21（Asia/Shanghai）
>
> 结论：服务端权威聚合与隐私字段白名单已完成；下一顺位进入 F4-H-C Pencil 与正式 Web。

## 本批范围

- 新增公开宠物卡片 VO，不复用包含私域状态的 `PetProfileVo`。
- 由 Pet Service 按服务端已解析用户与租户读取公开宠物。
- 在既有匿名 `User/GetPublicProfile` 响应中聚合可空 `VoPet`。
- 同步 client 公共资料 TypeScript 类型。
- 新增全量 Docs 编码 / 乱码检查入口，并修复扫描发现的历史确定性文本问题。

本批按停止线没有修改 Pencil、正式 `/u/:id` 页面、public head、JSON-LD、sitemap、WebOS、Flutter 或 Tauri。

## 权威隐私契约

- 查询同时校验 `UserId / TenantId / IsPublic / IsDeleted`，客户端不能提交宠物 LongId 或租户。
- 未领取、未公开、软删除与跨租户统一返回 `VoPet = null`，不形成宠物存在性探测接口。
- `PetPublicCardVo` 只包含 PublicId、名称、物种 / 形态键、成长阶段、心情和可空安全装扮摘要。
- `pet_` PublicId、物种、形态、心情和成长阶段进入服务端公开白名单校验；非法投影安全隐藏。
- 三项状态、成长值、最后照顾时间、流水、动作资格、内部 LongId 和租户均未进入公开 VO。
- 当前不存在正式宠物装扮注册来源，未透传 `EquippedBackgroundKey / EquippedToyKey`，`VoAdornment` 固定为 `null`。

## 文档编码检查

- 新增 `./scripts/check-docs.sh` 与 `npm run check:docs`。
- 脚本扫描 `Docs/` 全部文件并复用 `Scripts/check-repo-hygiene.mjs`，覆盖非法 UTF-8、BOM、`U+FFFD`、常见 mojibake、换行和末尾换行。
- 首次扫描修复 `Docs/changelog/2026-01/week1.md` 缺失末尾换行，以及 `week4.md` 中可从上下文确定的“创建/编辑”“可访问性”乱码和末尾换行。
- 该专用入口不输出文档篇幅治理提醒，避免编码检查被历史篇幅信息淹没；篇幅治理仍由通用仓库卫生入口负责。

## 验证结果

- `./scripts/check-docs.sh`：通过，最终扫描 `583` 个文件。
- Pet / User Profile 定向测试：`21/21` 通过。
- `dotnet test Radish.Api.Tests --no-restore`：`966` 通过、`25` 个环境用例按配置跳过。
- `dotnet build Radish.slnx -c Debug --no-restore`：`0 warning / 0 error`。
- `npm run type-check --workspace=radish.client`、`npm run lint:changed`：通过。
- `npm run validate:baseline:quick`：通过；四个前端 workspace type-check、`18 + 24 + 449 + 57` 项测试及固定扫描正常。
- `npm run validate:identity`：通过；身份扫描、LongId 字符串安全与 `31` 项身份定向测试正常。
- `npm run check:repo-hygiene:changed`、`git diff --check`：通过。

本批没有启动服务或执行 Gateway 浏览器 smoke，也没有新增 migration、tag、镜像或部署。
