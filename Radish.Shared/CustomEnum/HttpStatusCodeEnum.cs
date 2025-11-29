namespace Radish.Shared.CustomEnum;

/// <summary>
/// 统一的 HTTP 状态码枚举，用于 MessageModel.StatusCode 等场景。
/// （如需更细粒度控制，可替换为 System.Net.HttpStatusCode。）
/// </summary>
public enum HttpStatusCodeEnum
{
    Success = 200,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    InternalServerError = 500,
}
