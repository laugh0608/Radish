# F3 i18n 完成度治理实施说明

> 本文是 `F3 i18n 完成度治理` 的审计与实施入口，承载当前范围、状态模型、模块职责、迁移批次、停止线和验证矩阵。
>
> 长期通用规范仍以[国际化与多语言规范](/architecture/i18n)为准；F2 主题运行时以[前端主题与 i18n 实施说明](/frontend/theme-i18n-implementation)为准。本专题不重新实施主题系统。

## 1. 当前状态

- **阶段**：发布后长期维护与功能完成。
- **顺位**：F1 商城与 F2 主题均已完成，F3 是当前工程第一顺位。
- **审计基线**：`dev` 提交 `0d8e63be`；实施基线已推进至 `91b41949`。
- **当前进度**：三端与共享边界审计、`F3-A` 核心契约及 `F3-B1 / F3-B2` 首批正式 Web 链路均已完成本地实现与静态回归；下一顺位为 `F3-C`，专题仍处于开发中。
- **正式范围**：`radish.client`、`radish.console`、`Radish.Auth`，以及它们依赖的 `@radish/http`、`@radish/ui` 共享契约。
- **兼容范围**：`/desktop` 只保证语言切换与高频反馈不发生阻断级回退。

F3 的目标不是把仓库中的中文逐个替换成翻译键，而是建立一套可以持续扩展、能自动防止双语漂移、并且让服务端错误与前端展示职责清晰的正式 Web 多语言契约。

## 2. 审计结论

### 2.1 语言选择、初始化与持久化

| 模块 | 已有能力 | 真实缺口 |
| --- | --- | --- |
| `radish.client` | i18next 支持 `zh / en`；检测顺序为 `?lang`、`localStorage.radish_lang`、浏览器语言；OIDC 发起时携带 `culture / ui-culture` | 正式 Web Header 没有语言入口，当前主要入口仍在 `/desktop` Dock；`document.lang`、Ant Design locale、API `Accept-Language` 没有由同一语言状态统一驱动 |
| `radish.console` | 设置页存在只读的“简体中文”占位 | 没有 i18n 依赖、初始化、语言入口或持久化；Ant Design 与日期均固定中文；API 请求没有统一语言头 |
| `Radish.Auth` | `RequestLocalizationOptions` 支持 Query String、Cookie、`Accept-Language`，默认 `zh`；三页均有中英文切换按钮 | 页面切换只改当前 URL query，没有写入文化 cookie；Login / Register 互跳和 POST 回跳不保留显式选择；三页各自复制切换脚本 |

当前不存在用户 Profile 级语言字段。F3 先把语言定义为**同一浏览器设备上的正式 Web 偏好**，不在首批引入数据库字段、Claim 或跨设备同步承诺。

### 2.2 文案资源与硬编码

- `radish.client/src/i18n.ts` 已达到约 `4,794` 行，主资源约 `2,360` 个 key / 语言；中英文主资源 key 当前成对且未发现重复 key，但没有自动化守卫。
- client 已按业务域形成 `forum / shop / wiki / profile / me / discover / workbench` 等 key 前缀，说明现有命名基础可复用；问题主要是资源物理组织仍集中在单文件。
- 正式 Web 仍存在用户可见硬编码，已确认的代表面包括：`WebShellHeader` 导航、`PublicShellHeader` 账号动作、Docs 作者态、公开承诺页、页面 head、部分论坛发布与错误状态、萝卜坑反馈和共享上传组件。
- `radish.console` 没有资源层。当前 138 个 TS / TSX 文件中 92 个含中文文本；这些命中包含日志、注释和技术文本，不能直接等同于迁移量，但路由元数据、管理壳、Dashboard、用户、治理、订单、文档和反馈文案均已确认是用户可见硬编码。
- `Radish.Auth` 的 Login / Register / Consent 分别约 `573 / 678 / 812` 行。Register 与 Consent 大量使用 `isZh ? ... : ...`，Controller 的注册校验和成功 / 失败反馈仍直接写中文。
- Auth `zh` / 默认资源各有 25 个 key，`en` 只有 18 个；英文缺少 7 个登录客户端信息 key，当前会回退到默认中文。
- `@radish/ui` 仍有上传、Markdown、通知、裁切、确认框等共享组件默认中文。共享组件不应读取宿主语言状态，应由宿主传入 locale 或 labels。

### 2.3 服务端错误与前端翻译职责

当前已有正确的契约骨架：

```text
HTTP status + Code + MessageKey + MessageInfo + TraceId
```

但实现尚未闭环：

- `MessageModel`、全局异常处理和 `ApiErrorContractAttribute` 已支持稳定 `Code / MessageKey / TraceId`；API 的 `zh / en` 资源目前各只有 6 个 key，多数 Controller 仍返回直接写死的中文 `MessageInfo`。
- `@radish/http` 有 `parseApiResponseWithI18n`，但统一 `apiGet / apiPost / apiPut / apiDelete` 只调用不带 i18n 的解析器；多数业务 API 因此继续直接展示服务端字符串。
- client 商城 API 等接口虽然接收 `t`，但立即 `void t`；这说明调用签名已经表达翻译意图，统一客户端却没有提供实际能力。
- client 主资源当前没有 `error.*` 键；少数使用 i18n 解析器的调用仍只能回退到服务端 `MessageInfo`。
- 部分页面通过匹配“商品不存在 / 文档不存在 / 帖子不存在”等中文字符串判断状态，语言切换后会破坏控制流。状态判断必须改用 `Code`、HTTP status 或明确数据状态。

F3 固定职责如下：

- `Code` 与 HTTP status 决定控制流，禁止用中英文消息文本决定业务分支。
- `MessageKey` 是稳定翻译契约；前端存在对应 key 时优先使用本地翻译。
- `MessageInfo` 是服务端按 `Accept-Language` 生成的安全兜底，不是前端业务判断依据。
- `TraceId` 只用于诊断复制与日志关联，不拼入普通主提示；可恢复错误按现有诊断规范提供显式入口。
- 未处理异常不得把异常消息直接暴露给用户；服务端记录完整异常，外部只返回稳定通用错误与 TraceId。
- 首批不新增任意结构的 `messageArgs`。需要动态参数的领域错误先使用服务端本地化兜底；后续只有在参数白名单和类型契约明确后再单独评审。

### 2.4 日期、数字、金额、相对时间与复数

- client 同时存在固定 `zh-CN`、浏览器默认 `toLocaleString()`、局部 `en-US / zh-CN` 映射和 date-fns 相对时间四种口径。
- 现有 `formatDateTimeByTimeZone` 正确承接用户时区，但格式 locale 固定为 `zh-CN`；语言与时区尚未解耦为两个明确输入。
- Console 多个用户、角色、订单、商品、文档和系统设置页面固定使用 `zh-CN`。
- 金额 / 胡萝卜与统计数字多由页面自行拼接，API helper 中也存在中文单位和“原价”文案。
- 英文 count 文案普遍只有单一 `{{count}} items / posts / replies`，没有使用 i18next `_one / _other` 复数规则；Auth Consent 仅手工处理了一处 permission 单复数。

F3 统一通过 `Intl.DateTimeFormat`、`Intl.NumberFormat` 和 i18next plural 规则完成展示；时区决定“显示哪个时刻”，locale 决定“如何呈现”，两者不得混为一个设置。

### 2.5 中英文长文本、表格与 PC / mobile

- client 正式 Web Header 已有横向滚动、ellipsis、移动图标化和底部导航基础，可作为英文长标签的适配基座；但默认导航仍是中文常量，尚未经过英文真实渲染验证。
- Console 已有 `scroll.x`、ellipsis、移动功能抽屉等表格与壳层基础；当前没有英文资源，无法证明列头、筛选项、状态标签和操作按钮在英文下稳定。
- Auth 三页已有 `920 / 720 / 640` 等响应式断点与部分 `word-break`，但英文缺键和 Razor 内联双语使长文本覆盖不可自动验证。
- 当前没有覆盖 `zh / en × PC / mobile × 表格 / 长文本` 的测试或专题验收矩阵。

## 3. 目标状态模型

正式 Web 只接受中性语言代码：

```ts
type SupportedLanguage = 'zh' | 'en';
```

展示层映射集中维护：

```text
zh -> Intl / Ant Design / date-fns: zh-CN
en -> Intl / Ant Design / date-fns: en-US
```

### 3.1 解析优先级

```text
当前显式入口参数
  ?lang=zh|en（SPA）或 culture / ui-culture（Auth / OIDC handoff）
        ↓
同设备显式偏好
  localStorage.radish_lang（client / Console）
  .AspNetCore.Culture（Auth）
        ↓
浏览器语言
        ↓
默认 zh
```

规则：

- 所有输入先归一化为 `zh / en`，非法值不进入状态。
- client 与 Console 在同源 Gateway 下共享 `radish_lang`，切换后通过 `storage` 事件同步其他标签页。
- Auth 显式切换必须写文化 cookie，并保留安全的本地 return URL；OIDC handoff 继续携带中性语言参数。
- 初始化完成后同步更新 `document.documentElement.lang`、i18next、Ant Design locale、date-fns / Intl locale 和 API `Accept-Language`。
- F3 不新增服务端用户语言字段；未来若需要跨设备偏好，必须另行定义 Profile、Claim、匿名设备偏好与冲突优先级。

## 4. 模块职责

### 4.1 `radish.client`

- 拥有产品端语言状态、正式 Web Header 语言入口和 client 业务资源。
- 继续使用 `radish_lang`，保留现有 `/desktop` Dock 入口，但正式入口以 Web Header 为准。
- 语言切换必须同时刷新页面文案、Ant Design locale、格式化结果、document title 和后续 API 请求语言。

### 4.2 `radish.console`

- 建立独立的 i18next 资源与管理端业务域，不跨 workspace 引用 client 资源。
- 与 client 共享语言代码和同源设备偏好键，但资源、路由标题和管理术语由 Console 自己维护。
- 把当前只读“语言”占位改为本设备有效设置；不冒充服务端 Profile 偏好。

### 4.3 `@radish/ui`

- 不读取 i18next、`localStorage`、认证或宿主设置。
- `ThemeProvider` 接收宿主解析后的 Ant Design locale；共享组件通过 `labels` / formatter 参数消费文案。
- 只有跨宿主语义完全一致的默认反馈才允许形成共享 label contract，业务文案留在宿主。

### 4.4 `@radish/http`

- 不依赖 React 或任一宿主的 i18next 实例。
- 由宿主配置 `getLanguage()` 与可选 `translateMessage(key)`；统一请求自动写 `Accept-Language`。
- 解析结果保留 `Code / MessageKey / MessageInfo / TraceId / HTTP status`，并按“本地 key 优先、服务端安全消息兜底”生成用户提示。
- 网络、超时、非 JSON 和未知异常使用宿主提供的稳定通用 key，不在库内写死中文。

### 4.5 `Radish.Api`

- 全局认证、权限、验证、限流、依赖和系统异常维持稳定 `Code / MessageKey`。
- 首批高频正式 Web Controller 迁移领域错误；成功读取类消息不要求全部展示或全部资源化。
- `MessageInfo` 按请求文化生成；日志和内部异常不进入 UI 文案。

### 4.6 `Radish.Auth`

- 请求文化、Auth cookie 与 `.resx` 是服务端页面真相源。
- Login / Register / Consent 不再自行维护 `isZh` 双分支；Controller 可见反馈也必须走资源键。
- 客户端名称、描述、用户输入和 redirect host 属于动态内容，只做安全编码，不进行机器翻译。

## 5. 翻译键与资源组织

### 5.1 业务域

- `common.*`：确实跨域的加载、保存、取消、删除、重试、分页和诊断动作。
- `shell.* / lang.* / auth.* / oidc.*`：壳层、语言与认证。
- `discover.* / forum.* / messages.* / notification.* / profile.* / me.* / shop.* / wiki.*`：产品业务域。
- `console.<domain>.*`：Console 路由、页面、表格、筛选、状态和动作。
- `error.<domain>.* / info.<domain>.*`：与服务端 `MessageKey` 对齐的跨层消息。

同一句中文或英文相同不代表 key 应合并；只有语义、生命周期和所有者都相同的文案才能进入 `common.*`。

### 5.2 文件组织

client 首批把现有大文件拆成“语言 × 业务域”资源模块，由单一 registry 合并，保持现有 key 不做无关重命名。建议分组：

```text
locales/
  resources.ts
  en/{core,shell,discover,community,account,commerce,docs}.ts
  zh/{core,shell,discover,community,account,commerce,docs}.ts
  welcome*.ts
```

Console 使用同样的 registry 形态，但只包含 Console 业务域。自动化必须阻断：

- 中英文缺 key；
- 同一语言重复 key；
- 非法顶级域；
- 资源重新膨胀为超大单文件；
- `error.*` 与首批服务端公开 `MessageKey` 不一致。

## 6. 成组实施批次

### F3-A：基础设施与正式入口

1. 拆分 client 资源 registry，补 key parity、重复键和语言归一化测试。
2. client 新增正式 Web Header 语言切换；统一根语言状态、`document.lang`、Ant Design locale 与跨标签同步。
3. Console 接入与 client 对齐版本的 i18next 依赖，建立 Console registry、根 Provider、管理壳入口和本设备语言设置。
4. `@radish/ui` 改为由宿主传入 Ant Design locale；优先治理正式 Web 实际使用的反馈、通知、确认与上传 labels。
5. `@radish/http` 增加宿主注入的语言 / 翻译配置，统一 `Accept-Language` 和响应本地化；禁止页面继续按消息文本判断状态。
6. Auth 补齐英文资源，迁移 Login / Register / Consent 与注册反馈，建立文化 cookie 持久化和 OIDC 语言往返测试。

### F3-B：首批高频正式 Web 链路

在 F3-A 同一连续开发批次内完成以下垂直链路，不拆成逐页小修：

- client：共享 Header / mobile tab、Discover、Forum 列表与详情、Messages、Notifications、Me、公开商城、订单与背包。
- Console：登录回流、AdminLayout、路由 / 面包屑 / 全局搜索、Dashboard、用户、内容治理、订单排障。
- Auth：Login、Register、Consent 的中英文 UI、校验反馈、长客户端名 / redirect URI 与单复数权限说明。
- API：上述链路实际消费的通用认证 / 权限 / 限流 / not-found / conflict 错误，以及高频领域失败的 `Code / MessageKey`。
- 格式化：共享日期时间、数字、胡萝卜金额、相对时间和 plural helper，并迁移上述链路。

F3-B 按连续业务面分两组推进：

- **F3-B1（已完成，2026-07-14）**：client 正式 Header、公开商城 / 论坛 / Docs、通知、订单与背包；Console Login、AdminLayout、路由、Dashboard、Settings 和用户 / 订单关键格式化；Auth Login / Register / Consent；共享 HTTP / UI 契约。
- **F3-B2（已完成，2026-07-15）**：Console 用户 / 内容治理 / 订单完整业务文案，client Messages / Me 的语言与格式化残余，以及这些链路实际消费的高频领域 `Code / MessageKey`。

F3-A + F3-B 共同构成首批可交付范围；只有基础库没有真实页面消费，或只有页面替换没有统一契约，都不算首批完成。

### F3-C：剩余正式 Web 业务域

- client：Docs 作者态、圈子、宠物、经验、萝卜坑、低频设置与公开承诺长文本。
- Console：商品配置、文档治理、角色权限、分类标签、表情、系统设置、经验与胡萝卜管理。
- 继续按业务域迁移服务端错误，不做全仓 Controller 一次性机械资源化。

### F3-D：专题验收与常态门禁

- 完成 `zh / en × PC / mobile × Public / Private / Console / Auth` 代表矩阵。
- 清理首批过渡资源和无效 fallback，更新长期规范与验证入口。
- 专题完成后只允许新业务域按同一 registry、错误和 formatter 契约扩展。

### 6.1 2026-07-15 首批实现状态

已完成：

- client 大资源文件按 `core / shell / discover / community / account / commerce / docs` 拆分，中英文 parity、跨域重复键和单文件上限进入测试；
- client 与 Console 建立同一 `radish_lang` 设备偏好、正式 Header / 管理壳语言入口、跨标签同步、`document.lang` 与 Ant Design locale 联动；
- `@radish/http` 统一发送 `Accept-Language`，保留 `Code / MessageKey / MessageInfo / TraceId / HTTP status`，支持宿主翻译与结构化 `ApiResponseError`；公开商品、文档、论坛和主页的 not-found 控制流已停止匹配中英文消息文本；
- `@radish/ui` 接收宿主 locale，提供数字、日期和相对时间 formatter；论坛 Reaction、StickerPicker、用户提及等首批共享组件改为宿主 labels；
- Auth Login / Register / Consent 可见文案和注册校验进入 `.resx`，补齐英文缺键，语言切换写入安全文化 cookie；
- Console 完成 Login、AdminLayout、路由 / 面包屑 / 搜索、Dashboard、Settings 的首批资源化，并在用户、订单链路接入 locale 日期与数字格式；
- client Header、通知、公开商城、排行榜、订单与背包完成首批 locale 格式化，高频商品 / Discover / Forum 数量文案开始使用 i18next plural。
- Console 用户、内容治理和订单形成独立 `users / moderation / orders` 中英文域资源；路由、表格、筛选、状态、动作、弹层与移动治理说明均从宿主语言状态解析；
- client Messages / Me 补齐品牌标记、资产与任务数量复数、日期时区和数字格式化，聊天展示 helper 不再保留中文默认翻译分支；
- 订单状态、失败阶段、商品类型、权益状态和有效期展示改用结构化枚举或快照字段；订单 / 治理 API helper 使用 `ApiResponseError` 保留控制流与诊断字段；
- API 为订单 not-found、重试拒绝、备注失败、权益操作拒绝和内容治理高频失败补齐稳定状态、`Code / MessageKey` 与中英文 `.resx`，资源 parity、结构化错误和 `0 / 1 / 2` 复数进入测试。

尚未宣告完成：

- `F3-C` 所列 client 与 Console 剩余业务域尚未按同一契约完成迁移，高频领域错误继续随真实消费面推进，不做 Controller 全仓机械资源化；
- 未执行 PC / mobile 真实布局或 OIDC 浏览器往返验收，待专题批次准备验收且获得当轮启动授权后执行。

## 7. 首批实际修改模块

| 模块 | 已完成改动与后续职责 |
| --- | --- |
| `Frontend/radish.client` | 已完成 registry、language runtime / switcher、Web Header、B1 页面及 B2 Messages / Me 格式化与结构化错误消费；F3-C 继续剩余业务域 |
| `Frontend/radish.console` | 已完成 i18n 初始化、管理壳、route meta、Dashboard、Settings 及 B2 用户 / 治理 / 订单业务域；F3-C 继续低频管理域 |
| `Frontend/radish.ui` | 已完成 `ThemeProvider` locale、共享 formatter 和首批 labels 契约；后续按正式宿主消费面扩展 |
| `Frontend/radish.http` | 已完成 language / translator 配置、`Accept-Language`、结构化响应与错误测试 |
| `Radish.Auth` | 已完成 `.resx`、三张 Razor View、文化持久化入口、Controller 反馈与测试 |
| `Radish.Api / Radish.Shared / Radish.Model` | 已补 B2 高频订单 / 权益 / 治理错误键、稳定错误码与订单有效期结构字段；后续随 F3-C 真实消费面扩展，不重做 Q1-B 已有错误管线 |
| `Docs/` | 已建立专题说明并同步规划入口；长期规范随实际契约继续维护 |

Console 已按当前任务授权加入与 client 对齐的 i18next 依赖，并复核 `Frontend/radish.console/package.json` 与根 `package-lock.json`；后续依赖更新仍须重新说明命令、版本和 lockfile 影响并取得当轮授权。

## 8. 停止线

- 不重新实施 F1 商城效力或 F2 主题运行时。
- 不把 F3 变成全站逐字符串清零；日志、注释、内部诊断和用户内容不因中文命中自动进入迁移。
- 不自动翻译用户帖子、评论、聊天、商品自定义内容或 Docs 正文。
- 不建设在线翻译管理平台、机器翻译流水线或多语言 CMS。
- 不新增 Profile 语言字段、数据库迁移、语言 Claim 或跨设备同步，除非另行确认。
- 不承诺多语言 SEO URL、`hreflang` 或服务端多语言 head 快照；英文 UI 的客户端 document title 可同步，爬虫默认语言另立 SEO 契约。
- 不解冻 Tauri，不扩展 Flutter；`/desktop` 只做阻断级兼容。
- 不借资源拆分重命名全部现有 key，不顺手重构页面结构或主题 token。
- 不把 Console mobile 做成桌面完整能力复制，只保证现有移动管理边界在英文下可操作。

## 9. 风险与控制

| 风险 | 控制方式 |
| --- | --- |
| 大资源文件拆分造成漏键或覆盖 | 保持 key 名不变；先加 registry parity / duplicate 测试，再机械迁移 |
| client / Console / Auth 语言互相漂移 | 中性代码单一映射；同源 storage + Auth culture cookie + OIDC 参数定向测试 |
| API 本地化改变业务判断 | 控制流只看 Code / status；测试中加入中英文不同消息但同码场景 |
| `MessageInfo` 历史中文造成混合语言 | 首批高频错误补 key；未迁移领域保留安全服务端兜底并进入后续域批次，不按字符串猜测 |
| locale 与时区混淆 | formatter 同时显式接收 locale 与 timeZone；保留现有 UTC / 用户时区契约 |
| 英文标签撑破 Header、表格或弹层 | PC / mobile 长文本 fixture；允许换行、ellipsis 或横向滚动按组件职责选择 |
| 共享 UI 反向持有宿主状态 | `@radish/ui` 只接收 locale / labels / formatter，不导入宿主 i18n |
| 新依赖扩大 lockfile | 仅在明确宿主缺少运行时能力时增加依赖；执行前单独授权并复核 lockfile |

## 10. 验证矩阵

### 10.1 开发中静态与定向验证

- client / Console 资源 parity、重复键、非法 locale、持久化优先级和跨标签同步测试。
- `@radish/http`：`Accept-Language`、本地 `MessageKey` 优先、服务端 fallback、Code / status / TraceId 保留、非 JSON 与网络错误测试。
- formatter：`zh / en` 日期、时区、数字、金额、相对时间，以及 `0 / 1 / 2` plural 测试。
- Auth：Query / cookie / header 优先级、Login / Register / Consent 资源完整性、切换持久化、POST 错误语言与 OIDC 往返测试。
- client / Console 首批页面定向测试、`@radish/ui` 与 `@radish/http` type-check / test。
- `npm run build --workspace=radish.client`、`npm run build --workspace=radish.console`。
- 涉及后端后执行 `dotnet test Radish.Api.Tests` 与 `dotnet build Radish.slnx -c Debug`。
- `npm run check:repo-hygiene:changed`、changed-only lint、`git diff --check`。

### 10.2 专题验收矩阵

专题成组完成并获得当轮启动授权后，再通过 Gateway 执行：

| 视图 | Client | Console | Auth |
| --- | --- | --- | --- |
| PC `1920 × 1080` | Header、Discover、Forum、Messages、Notifications、Me、Shop | 壳层、Dashboard、用户、治理、订单表格 | Login、Register、Consent |
| mobile `390 × 844` | Header 动作、mobile tab、列表 / 详情、长反馈 | 现有移动壳、功能抽屉、表格横向滚动 | 表单双列降单列、长应用名与 redirect URI |

每个代表页同时覆盖：

- `zh -> en -> 刷新 -> 新标签 -> OIDC 往返 -> en 保持`；
- 空态、加载、成功、失败、无权限、not-found 与可复制 TraceId；
- 日期 / 时区、数字 / 金额、相对时间、`0 / 1 / 2` 数量；
- 英文长标题、表格列头、按钮组、弹层、错误说明和用户动态内容混排。

本轮开发不启动服务、不执行浏览器 smoke；运行态矩阵保留到专题成组验收。
