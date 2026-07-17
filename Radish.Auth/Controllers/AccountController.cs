using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OpenIddict.Abstractions;
using Radish.Auth.Resources;
using Radish.Auth.ViewModels.Account;
using Radish.Common.HttpContextTool;
using Radish.Common.HelpTool;
using Radish.IService;
using Radish.Model;
using Radish.Model.OpenIddict;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;

namespace Radish.Auth.Controllers;

/// <summary>
/// 账号控制器：
/// - 使用业务用户表完成 Cookie 登录
/// - 为 OIDC 授权端点提供认证会话
/// - 提供一个基于 Razor View + .resx 文案的登录页面
/// </summary>
[Route("[controller]/[action]")]
public class AccountController : Controller
{
    private static readonly HashSet<string> ReservedDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin",
        "administrator",
        "system",
        "root",
        "support",
        "official",
        "bot",
        "moderator",
        "radish"
    };

    private readonly IStringLocalizer<Errors> _errorsLocalizer;
    private readonly IUserService _userService;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly ICoinService _coinService;
    private readonly ISystemSettingProvider _systemSettingProvider;

    public AccountController(
        IStringLocalizer<Errors> errorsLocalizer,
        IUserService userService,
        IOpenIddictApplicationManager applicationManager,
        ICoinService coinService,
        ISystemSettingProvider systemSettingProvider)
    {
        _errorsLocalizer = errorsLocalizer;
        _userService = userService;
        _applicationManager = applicationManager;
        _coinService = coinService;
        _systemSettingProvider = systemSettingProvider;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? email = null)
    {
        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl,
            PrefillEmail = email,
            ErrorMessage = TempData["LoginError"] as string,
            SuccessMessage = TempData["RegisterSuccess"] as string,
            Client = await ResolveClientAsync(returnUrl)
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult SetLanguage(string culture, string? returnUrl = null)
    {
        var normalizedCulture = NormalizeSupportedCulture(culture);
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(normalizedCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(RewriteLocalReturnUrlCulture(returnUrl, normalizedCulture));
        }

        return RedirectToAction(nameof(Login))!;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromForm] string email, [FromForm] string password, [FromForm] string? returnUrl = null)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var normalizedEmail = NormalizeEmail(email);

        if (normalizedEmail == null || string.IsNullOrWhiteSpace(password))
        {
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), CreateAccountFlowRouteValues(returnUrl, email));
        }

        var logEmail = MaskEmailForLog(normalizedEmail);
        Log.Information("[Account/Login] 开始处理登录请求，邮箱: {Email}", logEmail);

        // 1. 查询用户（单用户精确查询，避免列表物化）
        var userQueryStopwatch = Stopwatch.StartNew();
        var user = await _userService.GetEnabledUserByEmailAsync(normalizedEmail);
        Log.Information(
            "[Account/Login] 用户查询完成，邮箱: {Email}, 结果数: {UserCount}, 耗时: {ElapsedMs}ms, 总耗时: {TotalElapsedMs}ms",
            logEmail,
            user == null ? 0 : 1,
            userQueryStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        if (user is null)
        {
            Log.Warning(
                "[Account/Login] 用户不存在，邮箱: {Email}, 总耗时: {ElapsedMs}ms",
                logEmail,
                totalStopwatch.ElapsedMilliseconds);
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), CreateAccountFlowRouteValues(returnUrl, normalizedEmail));
        }

        // 2. 使用 Argon2id 验证密码
        var passwordVerifyStopwatch = Stopwatch.StartNew();
        var isPasswordValid = PasswordHasher.VerifyPassword(password, user.VoLoginPassword);
        Log.Information(
            "[Account/Login] 密码校验完成，邮箱: {Email}, 结果: {IsValid}, 耗时: {ElapsedMs}ms, 总耗时: {TotalElapsedMs}ms",
            logEmail,
            isPasswordValid,
            passwordVerifyStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        if (!isPasswordValid)
        {
            TempData["LoginError"] = _errorsLocalizer["auth.login.error.invalidCredentials"].Value;
            return RedirectToAction(nameof(Login), CreateAccountFlowRouteValues(returnUrl, normalizedEmail));
        }

        await _coinService.GrantRegistrationRewardAsync(user.Uuid);

        // 3. 密码验证成功，生成会话
        var userId = user.Uuid.ToString();
        var tenantId = user.VoTenantId.ToString();

        var roleQueryStopwatch = Stopwatch.StartNew();
        var roleNames = await _userService.GetUserRoleNamesAsync(user.Uuid);
        Log.Information(
            "[Account/Login] 角色查询完成，邮箱: {Email}, 角色数: {RoleCount}, 耗时: {ElapsedMs}ms, 总耗时: {TotalElapsedMs}ms",
            logEmail,
            roleNames.Count,
            roleQueryStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        var displayName = string.IsNullOrWhiteSpace(user.VoDisplayName)
            ? Radish.Model.User.NormalizeDisplayName(user.VoUserName, user.Uuid)
            : user.VoDisplayName.Trim();
        var displayHandle = string.IsNullOrWhiteSpace(user.VoDisplayHandle)
            ? displayName
            : user.VoDisplayHandle.Trim();

        var claims = new List<Claim>
        {
            // OIDC 标准 claims
            new(OpenIddictConstants.Claims.Subject, userId),
            new(OpenIddictConstants.Claims.Name, displayName),
            new(OpenIddictConstants.Claims.PreferredUsername, displayHandle),

            // 多租户标识（与 AuthenticationGuide 中的约定一致）
            new(UserClaimTypes.TenantId, tenantId)
        };

        // Email claim (如果存在)
        if (!string.IsNullOrWhiteSpace(user.VoUserEmail))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.VoUserEmail));
            claims.Add(new Claim(OpenIddictConstants.Claims.Email, user.VoUserEmail));
        }

        // 角色 claims
        foreach (var role in roleNames)
        {
            claims.Add(new Claim(OpenIddictConstants.Claims.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var signInStopwatch = Stopwatch.StartNew();
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        Log.Information(
            "[Account/Login] Cookie 登录完成，邮箱: {Email}, 耗时: {ElapsedMs}ms, 总耗时: {TotalElapsedMs}ms",
            logEmail,
            signInStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds);

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // 直接访问 /Account/Login 并登录成功时，返回一个简单的确认信息
        var successMessage = _errorsLocalizer["auth.login.success.message"];
        return Ok(successMessage);
    }

    /// <summary>
    /// 注册页面（GET）
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Register(string? returnUrl = null)
    {
        var displayNameLengthRule = await GetDisplayNameLengthRuleAsync();
        var model = new RegisterViewModel
        {
            ReturnUrl = returnUrl,
            ErrorMessage = TempData["RegisterError"] as string,
            SuccessMessage = TempData["RegisterSuccess"] as string,
            Client = await ResolveClientAsync(returnUrl),
            DisplayNameMinLength = displayNameLengthRule.MinLength,
            DisplayNameMaxLength = displayNameLengthRule.MaxLength
        };

        return View(model);
    }

    /// <summary>
    /// 注册提交（POST）
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register([FromForm] RegisterViewModel model)
    {
        // 1. 参数验证
        if (!ModelState.IsValid)
        {
            var errors = string.Join("; ", ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage));
            TempData["RegisterError"] = errors;
            return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
        }

        try
        {
            var displayName = model.DisplayName.Trim();
            var email = NormalizeEmail(model.Email);
            var displayNameLengthRule = await GetDisplayNameLengthRuleAsync();
            if (!IsDisplayNameValid(displayName, displayNameLengthRule.MinLength, displayNameLengthRule.MaxLength, out var displayNameError))
            {
                TempData["RegisterError"] = displayNameError;
                return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
            }

            if (ReservedDisplayNames.Contains(displayName))
            {
                TempData["RegisterError"] = _errorsLocalizer["auth.register.error.displayNameReserved"].Value;
                return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
            }

            if (email == null)
            {
                TempData["RegisterError"] = _errorsLocalizer["auth.register.error.emailInvalid"].Value;
                return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
            }

            // 2. 检查邮箱是否已存在
            var existingEmails = await _userService.QueryAsync(u => u.UserEmail == email);
            if (existingEmails.Any())
            {
                TempData["RegisterError"] = _errorsLocalizer["auth.register.error.emailRegistered"].Value;
                return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
            }

            // 4. 创建用户（使用 Argon2id 哈希密码）
            var hashedPassword = PasswordHasher.HashPassword(model.Password);
            var newUser = new Radish.Model.User
            {
                LoginPassword = hashedPassword,
                UserEmail = email,
                UserName = displayName,
                UserBirth = null,  // 明确设置为 null
                UserAddress = string.Empty,
                TenantId = 0,  // 默认租户
                DepartmentId = 0,
                StatusCode = 0,
                IsEnable = true,
                IsDeleted = false,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now,
                ErrorCount = 0
            };

            var userId = await _userService.AddAsync(newUser);

            Log.Information("用户 {DisplayName} (ID: {UserId}) 注册成功", displayName, userId);

            // 5. 发放注册奖励（50 胡萝卜）
            var transactionNo = await _coinService.GrantRegistrationRewardAsync(userId);
            Log.Information("用户 {UserId} 注册奖励发放成功，流水号: {TransactionNo}", userId, transactionNo);

            // 6. 注册成功，跳转到登录页
            TempData["RegisterSuccess"] = _errorsLocalizer["auth.register.success"].Value;
            return RedirectToAction(nameof(Login), CreateAccountFlowRouteValues(model.ReturnUrl, email));
        }
        catch (Exception ex)
        {
            Log.Error(
                ex,
                "用户注册失败: {DisplayName}, TraceId: {TraceId}",
                model.DisplayName,
                HttpContext.TraceIdentifier);
            TempData["RegisterError"] = _errorsLocalizer[
                "auth.register.error.unexpected",
                HttpContext.TraceIdentifier].Value;
            return RedirectToAction(nameof(Register), CreateAccountFlowRouteValues(model.ReturnUrl));
        }
    }

    private async Task<(int MinLength, int MaxLength)> GetDisplayNameLengthRuleAsync()
    {
        var minLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameMinLengthKey);
        var maxLength = await _systemSettingProvider.GetInt32Async(SystemConfigDefaults.DisplayNameMaxLengthKey);
        if (minLength > maxLength)
        {
            throw new InvalidOperationException("展示名长度系统设置无效：最小长度不能大于最大长度");
        }

        return (minLength, maxLength);
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        try
        {
            _ = new MailAddress(normalized);
            return normalized;
        }
        catch
        {
            return null;
        }
    }

    private static string MaskEmailForLog(string email)
    {
        var normalized = email.Trim();
        var atIndex = normalized.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0 || atIndex == normalized.Length - 1)
        {
            return "***";
        }

        var local = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        var maskedLocal = local.Length <= 2
            ? new string('*', local.Length)
            : $"{local[0]}***{local[^1]}";
        return $"{maskedLocal}@{domain}";
    }

    private bool IsDisplayNameValid(string displayName, int minLength, int maxLength, out string errorMessage)
    {
        if (displayName.Length < minLength || displayName.Length > maxLength)
        {
            errorMessage = _errorsLocalizer[
                "auth.register.error.displayNameLength",
                minLength,
                maxLength].Value;
            return false;
        }

        if (!displayName.All(IsValidDisplayNameCharacter))
        {
            errorMessage = _errorsLocalizer["auth.register.error.displayNameCharacters"].Value;
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsValidDisplayNameCharacter(char value)
    {
        return (value >= '0' && value <= '9') ||
               (value >= 'a' && value <= 'z') ||
               (value >= 'A' && value <= 'Z') ||
               (value >= '\u4e00' && value <= '\u9fff');
    }

    private static string NormalizeSupportedCulture(string? culture)
    {
        return culture?.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? "en"
            : "zh";
    }

    private static RouteValueDictionary CreateAccountFlowRouteValues(string? returnUrl, string? email = null)
    {
        var values = new RouteValueDictionary
        {
            ["returnUrl"] = returnUrl,
            ["culture"] = NormalizeSupportedCulture(CultureInfo.CurrentUICulture.Name)
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            values["email"] = email;
        }

        return values;
    }

    private string RewriteLocalReturnUrlCulture(string returnUrl, string culture, int depth = 0)
    {
        if (depth >= 3 || !Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        if (!Uri.TryCreate(new Uri("https://radish.local"), returnUrl, out var parsedUrl))
        {
            return returnUrl;
        }

        var queryValues = QueryHelpers.ParseQuery(parsedUrl.Query)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Where(value => value is not null).Select(value => value!).ToList(),
                StringComparer.OrdinalIgnoreCase);

        if (queryValues.TryGetValue("returnUrl", out var nestedValues) && nestedValues.Count == 1)
        {
            nestedValues[0] = RewriteLocalReturnUrlCulture(nestedValues[0], culture, depth + 1);
        }

        queryValues["culture"] = [culture];
        queryValues["ui-culture"] = [culture];

        var queryString = QueryString.Create(queryValues.SelectMany(pair =>
            pair.Value.Select(value => new KeyValuePair<string, string?>(pair.Key, value))));

        return $"{parsedUrl.AbsolutePath}{queryString}{parsedUrl.Fragment}";
    }

    private async Task<ClientSummaryViewModel> ResolveClientAsync(string? returnUrl)
    {
        var clientId = TryGetClientIdFromReturnUrl(returnUrl);

        if (string.IsNullOrWhiteSpace(clientId))
        {
            var fallback = HttpContext.Request.Query["client_id"].FirstOrDefault();
            clientId = string.IsNullOrWhiteSpace(fallback) ? null : fallback;
        }

        if (string.IsNullOrWhiteSpace(clientId))
        {
            Log.Debug("[ResolveClientAsync] No client_id found in URL or query string");
            return ClientSummaryViewModel.Empty;
        }

        Log.Debug("[ResolveClientAsync] Resolved client_id: {ClientId}", clientId);

        try
        {
            var applicationObject = await _applicationManager.FindByClientIdAsync(clientId, HttpContext.RequestAborted);
            if (applicationObject is null)
            {
                Log.Warning("[ResolveClientAsync] Client '{ClientId}' not found in database", clientId);
                return ClientSummaryViewModel.Empty;
            }

            Log.Debug("[ResolveClientAsync] Found application object, type: {TypeName}", applicationObject.GetType().Name);

            if (applicationObject is RadishApplication application)
            {
                Log.Debug("[ResolveClientAsync] Using RadishApplication: {DisplayName}", application.DisplayName);
                return ClientSummaryViewModel.FromApplication(application);
            }

            var clientIdValue = await _applicationManager.GetClientIdAsync(applicationObject, HttpContext.RequestAborted);
            var displayName = await _applicationManager.GetDisplayNameAsync(applicationObject, HttpContext.RequestAborted);
            var properties = await _applicationManager.GetPropertiesAsync(applicationObject, HttpContext.RequestAborted);

            Log.Debug("[ResolveClientAsync] Client: {ClientId}, DisplayName: {DisplayName}, Properties count: {PropertiesCount}",
                clientIdValue, displayName, properties?.Count ?? 0);

            return ClientSummaryViewModel.FromStoreData(clientIdValue, displayName, properties);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ResolveClientAsync] Failed to resolve client from returnUrl");
            return ClientSummaryViewModel.Empty;
        }
    }

    private static string? TryGetClientIdFromReturnUrl(string? returnUrl)
    {
        Log.Debug("[TryGetClientIdFromReturnUrl] Input returnUrl: {ReturnUrl}", returnUrl);

        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        try
        {
            // 查找查询字符串的起始位置
            var queryIndex = returnUrl.IndexOf('?');
            if (queryIndex == -1)
            {
                Log.Debug("[TryGetClientIdFromReturnUrl] No query string found in returnUrl");
                return null;
            }

            // 提取查询字符串部分（包括 '?'）
            var queryString = returnUrl.Substring(queryIndex);

            // 使用 QueryHelpers.ParseQuery 解析查询字符串（会自动处理 URL 编码）
            var query = QueryHelpers.ParseQuery(queryString);
            Log.Debug("[TryGetClientIdFromReturnUrl] Parsed {Count} query parameters", query.Count);

            if (!query.TryGetValue("client_id", out var clientIdValues))
            {
                Log.Debug("[TryGetClientIdFromReturnUrl] client_id not found in query parameters");
                return null;
            }

            var clientId = clientIdValues.FirstOrDefault();
            Log.Debug("[TryGetClientIdFromReturnUrl] Found client_id: {ClientId}", clientId);
            return clientId;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TryGetClientIdFromReturnUrl] Failed to parse returnUrl");
            return null;
        }
    }
}
