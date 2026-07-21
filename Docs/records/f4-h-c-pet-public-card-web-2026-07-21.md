# F4-H-C 电子宠物公开名片 Pencil 与正式 Web 完成记录

> 日期：2026-07-21（Asia/Shanghai）
>
> 结论：公开个人主页的 PC / mobile 设计源与正式只读宠物名片已完成；下一顺位进入 F4-H-D 成组运行态验收与专题关闭。

## 本批范围

- 更新 Public Web Pencil 的 `P09 - Public Profile` 与 `P14 - Mobile Public Profile`。
- 在既有 `/u/:id` 身份摘要与公开内容之间消费 `UserPublicProfileVo.VoPet`。
- 补齐物种、形态、成长阶段、心情与公开名片说明的中英文词元。
- 覆盖长名称、移动布局、主题 token、语义结构与公开字段白名单。
- 保持 public head、JSON-LD、sitemap、WebOS、Flutter、Tauri 和宠物经济边界不变。

本批按停止线没有启动服务或执行 Gateway 浏览器 smoke；运行态显隐与身份矩阵保留到 F4-H-D。

## 设计与页面实现

- Pencil `P09 / P14` 均在身份摘要与内容列表之间加入独立只读名片，并使用长宠物名称验证 PC / mobile 换行。
- 桌面名片以宠物形象、名称、公开状态和四项基础元数据形成次级身份区，不抢占公开帖子 / 评论主体。
- 移动名片保持单列阅读顺序与紧凑两列元数据，不引入横向滚动；画板保留代表性内容列表与底部安全区域。
- 正式页面新增独立 `PublicPetCard` 组件，主页面只在 `profile.voPet` 非空时挂载，不发起第二次 Pet API 请求。
- 物种、形态、成长阶段和心情从稳定 key 解析本地词元；宠物名称作为用户内容保留原文。
- 卡片无按钮、链接或 `/pet` 管理入口；主人查看自己的公开主页时也使用相同公开投影。

## 隐私与公开边界

- 组件只读取 `VoName / VoSpeciesKey / VoShapeKey / VoGrowthStage / VoMood`，不读取内部 LongId、租户、三项状态、成长值、照顾时间、动作资格或流水。
- `VoPet = null` 时不渲染空态、私密提示或未领取提示，避免形成页面侧存在性探测。
- 名片不写入 localStorage / sessionStorage，不建立独立公开宠物路由或分享 URL。
- `buildProfilePageStructuredData` 的回归用例显式带入 `VoPet`，并验证宠物名称和 PublicId 均不会进入 JSON-LD。
- 当前服务端没有正式宠物装扮注册项，`VoAdornment` 继续为空；页面没有透传任意装扮 key。

## 验证结果

- Pencil `P09 / P14`：`snapshot_layout` 均为 `No layout problems`，并完成截图复核。
- 宠物展示、公开名片和结构化数据定向测试：`19/19` 通过。
- `npm run test --workspace=radish.client`：`453/453` 通过。
- `npm run build --workspace=radish.client`：生产构建通过；保留仓库既有大 chunk 提醒，无新增构建失败。
- `npm run type-check --workspace=radish.client`、`npm run lint:changed`：通过。
- `npm run validate:baseline:quick`：通过；四个前端 workspace type-check、`18 + 24 + 453 + 57` 项测试及固定扫描正常。
- `npm run check:docs`：通过，扫描 `584` 个 Docs 文件。
- `npm run check:repo-hygiene:changed`：通过，扫描 `12` 个文本文件。
- `git diff --check`：通过。

本批没有新增 migration、依赖、tag、镜像或部署。
