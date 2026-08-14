using System.Text.Json.Serialization;

namespace Radish.Model;

/// <summary>仅供认证与密码校验链路使用的内部用户凭据快照。</summary>
/// <remarks>该类型不得作为 Controller 响应模型；密码哈希额外使用 JsonIgnore 防止误序列化。</remarks>
public sealed record UserCredentialSnapshot(
    long UserId,
    string DisplayName,
    string DisplayHandle,
    string Email,
    [property: JsonIgnore] string PasswordHash,
    long TenantId);
