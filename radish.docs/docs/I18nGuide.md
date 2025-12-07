# 国际化与多语言规范（i18n）

> 本文描述 Radish 当前的前后端国际化（仅简体中文与英文）设计与实现约定。所有新接口、新页面应优先遵循本规范。

## 总体设计

- 支持语言：**zh-CN（简体中文）** 与 **en（英语）**。
- 责任划分：
  - **前端（radish.client）**：负责「界面文案、交互提示」等 UI 文本的多语言管理与切换；统一使用 `react-i18next`。
  - **后端（Radish.Api / Radish.Auth）**：负责错误消息、系统提示等的多语言资源（.resx），并通过 `MessageModel<T>` 将 `code + messageKey + messageInfo` 返回给前端。
  - 前后端通过 **`messageKey`（i18n key）与 `code`（业务错误码）协作**，而不是直接依赖“中文/英文字符串”。
- 语言传递通道：
  - 前端请求统一携带 HTTP 头：`Accept-Language: zh-CN` 或 `en`。
  - 后端通过 `RequestLocalizationOptions + UseRequestLocalization` 按 `Accept-Language` 设置 `CultureInfo.CurrentUICulture`，并据此选择对应 `.resx` 文案。

## 前端国际化规范（radish.client）

### 1. 技术栈与初始化

- 使用库：
  - `i18next`
  - `react-i18next`
  - `i18next-browser-languagedetector`
- 初始化入口：`radish.client/src/i18n.ts`。
- 在 `main.tsx` 中全局挂载：
  - `import './i18n';`

### 2. 语言检测与切换

- 检测策略（参见 `i18n.ts`）：
  - 依次从 **URL Query** (`?lang=en`) → **localStorage** (`radish_lang`) → **浏览器语言** 读取。
- 缓存策略：
  - 当前选择写入 `localStorage.radish_lang`，避免每次刷新重新检测。
- 切换接口：
  - 统一使用 `i18n.changeLanguage('zh-CN' | 'en')`；
  - 建议在全局布局中提供语言切换 UI，例如：
    - `lang.zhCN` → "中文"
    - `lang.en` → "EN"。

### 3. 文案与 key 命名

- 使用 `react-i18next` 的 `t('key')` 调用方式：
  - 例：`t('app.title')`、`t('auth.login')`、`t('error.user.not_found')`。
- 命名规范：
  - 统一采用 **小写 + 点号分隔** 的层级结构：`{domain}[.{subDomain}].{meaning}`。
  - 约定的顶级域（示例）：
    - `app.*`：应用整体文案，如标题、描述等。
    - `auth.*`：登录/注销/认证相关文案。
    - `user.*`：用户相关 UI 文案（列表列头、按钮等）。
    - `weather.*`：Weather 示例模块。
    - `oidc.*`：前端 OIDC 流程提示文案（回调页面等）。
    - `error.*`：错误类文案（通常对应后端错误 key）。
    - `info.*`：信息类提示文案（通常对应后端成功提示 key）。
    - `lang.*`：语言切换标签。
- 建议示例：
  - 登录页：`auth.loginForm.usernameLabel`、`auth.loginForm.passwordLabel`、`auth.loginForm.submitBtn`。
  - 用户列表：`user.list.title`、`user.list.columns.name`、`user.list.empty`。
  - 错误类：`error.user.not_found`、`error.auth.invalid_credentials`。

### 4. API 响应与前端 i18n 的协作

- 后端统一使用 `MessageModel<T>` 返回：
  - `statusCode: number`
  - `isSuccess: boolean`
  - `messageInfo: string`（后端按当前语言翻译的提示）
  - `code?: string`（业务错误码，如 `User.NotFound`）
  - `messageKey?: string`（i18n key，如 `error.user.not_found`）
  - `responseData?: T`
- 前端统一约定响应类型：
  - `src/api/client.ts` 中定义 `ApiResponse<T>` 与解析 helper：

```ts
export interface ApiResponse<T> {
    statusCode: number;
    isSuccess: boolean;
    messageInfo: string;
    messageInfoDev?: string;
    code?: string;
    messageKey?: string;
    responseData?: T;
}

export function parseApiResponse<T>(json: ApiResponse<T>, t: (key: string) => string) {
    let message = json.messageInfo;

    if (json.messageKey) {
        const localized = t(json.messageKey);
        if (localized && localized !== json.messageKey) {
            message = localized;
        }
    }

    return {
        ok: json.isSuccess,
        data: json.isSuccess ? json.responseData : undefined,
        message,
        code: json.code,
    } as const;
}
```

- 高层封装 `requestJson<T>`：

```ts
export interface RequestJsonOptions extends RequestInit {
    withAuth?: boolean;
}

export async function requestJson<T>(
    input: RequestInfo | URL,
    t: (key: string) => string,
    options: RequestJsonOptions = {}
) {
    const { withAuth, headers, ...rest } = options;

    const finalHeaders: HeadersInit = {
        Accept: 'application/json',
        'Accept-Language': i18n.language || 'zh-CN',
        ...headers,
    };

    if (withAuth && typeof window !== 'undefined') {
        const token = window.localStorage.getItem('access_token');
        if (token) {
            (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
        }
    }

    const response = await fetch(input, {
        ...rest,
        headers: finalHeaders,
    });

    const json = await response.json() as ApiResponse<T>;
    return parseApiResponse(json, t);
}
```

- 组件中使用示例（以 Weather 为例）：

```ts
const { t } = useTranslation();

const parsed = await requestJson<Forecast[]>(`${apiBaseUrl}/api/WeatherForecast/GetStandard`, t);
if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || t('error.weather.load_failed'));
}
setForecasts(parsed.data);
```

- 若需要针对特定错误码做逻辑判断（例如用户不存在）：

```ts
const parsed = await requestJson<UserVo[]>(`${apiBaseUrl}/api/v1/User/GetUserById/${id}`, t);
if (!parsed.ok) {
    if (parsed.code === 'User.NotFound') {
        // TODO: 显示“用户不存在”的专用提示
    }
    throw new Error(parsed.message);
}
```

## 后端国际化与错误返回规范

### 1. RequestLocalization 与 Accept-Language

- 在 Radish.Api / Radish.Auth 统一配置：
  - `builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");`
  - `builder.Services.Configure<RequestLocalizationOptions>(...)` 中：
    - 支持 `zh-CN` 与 `en-US` 两种文化；
    - `DefaultRequestCulture = zh-CN`；
    - 把 `AcceptLanguageHeaderRequestCultureProvider` 放在 `RequestCultureProviders` 首位。
  - 中间件顺序：在 `UseCors` 后调用 `UseRequestLocalization`：

```csharp
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);
```

- 前端通过 `Accept-Language` 指定语言后，后端的：
  - `CultureInfo.CurrentCulture`
  - `CultureInfo.CurrentUICulture`

  会被设置为对应文化，`IStringLocalizer` 会自动选择对应 `.resx` 文件中的文案。

### 2. 多语言资源文件与 Errors 类型

- 资源路径：`Radish.Api/Resources/Errors.*.resx`：
  - `Errors.zh-Hans.resx`
  - `Errors.en-US.resx`
- 占位类型：`Radish.Api/Resources/Errors.cs`：

```csharp
namespace Radish.Api.Resources;

public class Errors
{
}
```

- 在 Controller 中通过 `IStringLocalizer<Errors>` 访问：

```csharp
using Microsoft.Extensions.Localization;
using Radish.Api.Resources;

public class LoginController : ControllerBase
{
    private readonly IStringLocalizer<Errors> _errorsLocalizer;

    public LoginController(..., IStringLocalizer<Errors> errorsLocalizer)
    {
        _errorsLocalizer = errorsLocalizer;
    }
}
```

### 3. MessageModel 扩展（三件套）

- 泛型版本：`Radish.Model/MessageModel.cs:32-81`：

```csharp
public class MessageModel<T>
{
    public int StatusCode { get; set; } = (int)HttpStatusCodeEnum.Success;
    public bool IsSuccess { get; set; } = false;
    public string MessageInfo { get; set; } = "Nothing happened here.";
    public string MessageInfoDev { get; set; } = "Nothing happened here.";
    public T ResponseData { get; set; } = default!;

    public string? Code { get; set; }
    public string? MessageKey { get; set; }
    // ... Success/Failed 工厂方法略
}
```

- 非泛型版本：`Radish.Model/MessageModel.cs:220-244`：

```csharp
public class MessageModel
{
    public int StatusCode { get; set; } = (int)HttpStatusCodeEnum.Success;
    public bool IsSuccess { get; set; } = false;
    public string MessageInfo { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? MessageKey { get; set; }
    public object? ResponseData { get; set; }
}
```

- 使用规范：
  - `Code`：面向前端/日志的**业务错误码**，建议大驼峰命名，如：`Auth.InvalidCredentials`、`User.NotFound`、`Weather.LoadFailed`。
  - `MessageKey`：与 `.resx` 以及前端 i18n 对应的 key，沿用 `error.*` / `info.*` 规范，如：`error.auth.invalid_credentials`。
  - `MessageInfo`：当前文化下的完整提示句子（由 `IStringLocalizer` 提供），作为兜底展示。

### 4. 控制器使用示例

#### 4.1 登录接口（LoginController.GetJwtToken）

- 成功：

```csharp
var successMessage = _errorsLocalizer["error.auth.login_success"];
return MessageModel<TokenInfoVo>.Success(
    successMessage,
    token,
    code: "Auth.LoginSuccess",
    messageKey: "error.auth.login_success");
```

- 失败：

```csharp
var failMessage = _errorsLocalizer["error.auth.invalid_credentials"];
return MessageModel<TokenInfoVo>.Failed(
    failMessage,
    code: "Auth.InvalidCredentials",
    messageKey: "error.auth.invalid_credentials");
```

#### 4.2 用户查询（UserController.GetUserById）

- 用户不存在：

```csharp
var notFoundMessage = localizer["error.user.not_found"];
return new MessageModel
{
    IsSuccess = false,
    StatusCode = (int)HttpStatusCodeEnum.NotFound,
    MessageInfo = notFoundMessage,
    Code = "User.NotFound",
    MessageKey = "error.user.not_found",
    ResponseData = null
};
```

- 查询成功：

```csharp
var successMessage = localizer["info.user.get_by_id_success"];
return new MessageModel
{
    IsSuccess = true,
    StatusCode = (int)HttpStatusCodeEnum.Success,
    MessageInfo = successMessage,
    Code = "User.GetByIdSuccess",
    MessageKey = "info.user.get_by_id_success",
    ResponseData = userInfo
};
```

#### 4.3 Weather 示例（WeatherForecastController.GetStandard）

- 成功：

```csharp
var successMessage = localizer["info.weather.load_success"];
var successResult = MessageModel<IEnumerable<WeatherForecast>>.Success(
    successMessage,
    forecasts,
    code: "Weather.LoadSuccess",
    messageKey: "info.weather.load_success");
```

- 失败（模拟）：

```csharp
var failMessage = localizer["error.weather.load_failed"];
var failResult = MessageModel<IEnumerable<WeatherForecast>>.Failed(
    failMessage,
    code: "Weather.LoadFailed",
    messageKey: "error.weather.load_failed");
```

## 推荐实践与注意事项

1. **所有对外错误/提示都应有对应的 `Code` 与 `MessageKey`**：
   - 便于前端做精细化逻辑判断（如 `User.NotFound` 时给出专门的提示）。
   - 便于将来统一梳理错误码与多语言资源。

2. **前端优先使用 `messageKey` 做 i18n 映射**，仅在缺失 key 时退回到 `messageInfo`。

3. **保持 key 的语义稳定**：
   - 修改文案时尽量只改 `.resx` / 前端 i18n 文件中的 value，而不要轻易修改 `messageKey`/`code` 本身。

4. **新接口默认走统一模式**：
   - Controller 返回 `MessageModel<T>`；
   - 通过 `IStringLocalizer<Errors>` 提供文案；
   - 前端通过 `requestJson<T>` + `parseApiResponse` 解析。

5. **文档维护**：
   - 本文为 i18n 规范的唯一来源；如有变更（新增语言、调整命名规则等），请同步更新本文件并在 `DevelopmentLog.md` 中记录决策。