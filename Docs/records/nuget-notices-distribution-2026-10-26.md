# 重点 NuGet notices 分发接线实施记录（2026-10-26）

> 项目所有者批准[具体方案](../features/nuget-notices-distribution-plan.md)后，在 `dev` 完成材料归档、四宿主构建 / publish 接线及定向验证。源代码基线为 `5fe407ce`，原文与来源缺口见[补证记录](./nodrive-source-review-2026-10-26.md)；本批不改变依赖版本、业务行为、Dockerfile 或根 LICENSE。

## 1. 已完成范围

- [材料目录](../../ThirdParty/nuget/NOTICE-INDEX.txt)覆盖 13 个重点 NuGet 组件；11 个 runtime 组件按实际宿主交付，Mapperly / VS Container Tools 两个 build-only 组件保留仓库材料。
- [manifest](../../ThirdParty/nuget/manifest.json)保存包作者、copyright、原始许可声明、来源 URL / commit、用途与宿主、原始 SHA256、规范化副本 SHA256及剩余证据项。31 份正文 / 来源材料逐一核验；只规范化文本格式，不改正文措辞与署名。
- `Scripts/check-nuget-notices.mjs` 生成逐组件署名文件、仓库 / 宿主索引、宿主 manifest 和 MSBuild props，默认只检查。更新材料需先审查 manifest / 原文，再显式使用 `--write`；`--source-root` 可复核原始字节与规范化关系，不自动联网下载。
- `Directory.Build.targets` 导入独立 notices targets；SDK `ResolvePackageDependencies` 输出到私有 item，`GetFileHash` 校验全部声明材料。缺失、哈希不符、包消失 / 版本漂移、宿主映射偏移、索引缺失、重复 / 越界目标均直接失败。
- 四宿主分别复制到 `third-party-notices/nuget/`；不注册可传递的 `Content` 项，防止宿主作为项目引用时互相覆盖。增量复制先清除自有子树，其他路径保留；输出文件进入 `FileWrites`，正常 `dotnet clean` 可删除。
- NoDrive / SystemCommon 的 Apache 包声明与候选源码 MIT 文本分开标明，精确来源与声明差异继续为 `unresolved`。Hangfire 核心商业备选不视为购买授权；PostgreSql 插件单独记录 LGPL 发行边界，SQLite 插件保留其自身 MIT 正文。

四宿主 Dockerfile 已完整复制 publish，不需新增 Node、改变镜像层或运行入口。本批没有新 CI job；日常校验入口已同步[验证基线](../guide/validation-baseline.md)。

## 2. 实际产物

在全新临时目录执行四宿主 Release `dotnet publish --no-build --no-restore --warnaserror`，随后按最终署名文本刷新并重新核对。Debug / Release 输出目录与 publish 均逐文件验证路径、数量和 SHA256，无多余 build-only 或其他宿主组件。

| 宿主 | runtime 组件 | notices 文件 | notices 总字节 |
| --- | ---: | ---: | ---: |
| API | 11 | 41 | 330,341 |
| Auth | 6 | 16 | 52,103 |
| Gateway | 6 | 16 | 52,109 |
| DbMigrate | 6 | 16 | 52,113 |

字节数只统计 `third-party-notices/nuget/`，不是镜像层体积。结构化输入哈希、宿主清单、逐文件哈希与验证摘要见[证据 JSON](./nuget-notices-distribution-evidence-2026-10-26.json)。

四份 SqlSugar DLL SHA256 仍为 `38a361e486a809ee2242861ce5ecb94016dca99bcaa9541d8f54cda8b06e9d0b`。与正式 NoDrive 批次的 publish 对比，所有非 Radish 自有程序集 / PDB 的原有文件哈希均相同，包含第三方 DLL、`.deps.json`、配置、native 产物与字体许可。

Radish 自有 DLL / PDB 因重编译存在哈希差异；核对到旧候选 `AssemblyInformationalVersion` 为 `26.8.2+571054e8…`，本批为 `26.8.2+5fe407ce…`。不把不同 Git 版本的自有程序集宣称为逐字节相同；本批没有业务 C# 源码变更。

## 3. 验证结果

| 检查 | 结果 |
| --- | --- |
| `npm run check:nuget-notices` + 四宿主 assets | 13 组件、23 个派生文件一致，四宿主映射通过 |
| 原始材料复核 | 31 份原文 SHA256 与规范化副本关系全部通过 |
| notices 自测 | 17 项通过，0 失败 / 跳过；包含真实 SDK target 的 9 类反向用例 |
| 四宿主 Debug / Release 严格构建 | 8 次均 0 warning / error |
| 四宿主 Release publish | 全部通过，无 restore；最终路径和哈希一致 |
| Gateway 增量 build / publish | 自有旧文件消失，相邻测试文件保留；测试文件随后精确删除 |
| Gateway clean / rebuild | clean 删除全部已登记声明文件，空目录可保留；无 restore 重建通过 |
| `Radish.Api.Tests` Debug 严格构建 | 通过；四个宿主的 notices 未传入测试项目输出 |
| 文档与仓库卫生 | 71 个变更文件、894 个文档 / 治理文件、844 个 Markdown 与 1,044 个本地链接检查通过，`git diff --check` 通过 |

验证基线与记录索引仍有原有的建议篇幅提醒，本批没有新增文本卫生错误，也没有扩入无关文档拆分。

开发中首次生成的 XML 注释包含 `--write` 导致 XML 解析失败，修正为不含双连字符的注释。首轮反向测试还发现 MSBuild property function 在 item 展开前小写化，导致宿主名称列表未正确解析；改为先展开 item 再转换大小写，最终 17 项全部通过。清理检查曾将带点的版本目录误算为文件；按实际文件复核确认 `FileWrites` 清理有效，没有降低产品检查。

最终补充了 SQLite / PostgreSql 与核心 Hangfire 不同条款的精确说明，仅改变声明文本；使用真实 MSBuild 复制 target 刷新八个输出，再重新 publish 和核对四宿主。没有重复运行数据库全量测试，因为本批没有数据库行为或依赖变更。

## 4. 证据、维护与未关闭边界

本机证据目录为 `/private/tmp/radish-nuget-notices-ovkspl2p`：`evidence/` 保存构建、publish、定向测试与来源校验日志；`original-materials/` 保存本轮核对的原始字节；`publish/` 保存四宿主候选。仓库保存哈希与摘要，临时目录不代表永久远程归档。反向测试临时副本已清理；本批未启动任何项目服务或容器。

常规使用：

```bash
npm run check:nuget-notices
npm run check:nuget-notices:self-test
# 审查清单与正文变更后，重建派生文件：
node Scripts/check-nuget-notices.mjs --write --assets
# 原始材料目录采用 package-id/version/filename 布局：
node Scripts/check-nuget-notices.mjs --assets --source-root=/path/to/original-materials
```

本批关闭的是重点组件的材料交付接线。NoDrive / SystemCommon 精确二进制来源与 Apache / MIT 声明差异、Hangfire 发行条件及其余 NuGet / 全生态 / 镜像材料仍未关闭。没有进行依赖安装、生产访问、Valkey 切换、镜像构建、浏览器 Smoke、PR、tag 或发布；后续动作继续按具体范围推进。
