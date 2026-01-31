using Radish.Shared.CustomEnum;

namespace Radish.Model;

/// <summary>
/// 统一返回信息类（泛型版本）
/// </summary>
/// <typeparam name="T">响应数据的类型</typeparam>
/// <remarks>
/// <para>所有 API 接口统一使用此模型返回数据，确保客户端能够以一致的方式处理响应。</para>
/// <para>成功示例：</para>
/// <code>
/// {
///   "statusCode": 200,
///   "isSuccess": true,
///   "messageInfo": "操作成功",
///   "messageInfoDev": "",
///   "responseData": { "id": 1, "name": "张三" }
/// }
/// </code>
/// <para>失败示例：</para>
/// <code>
/// {
///   "statusCode": 400,
///   "isSuccess": false,
///   "messageInfo": "参数验证失败",
///   "messageInfoDev": "用户名不能为空",
///   "responseData": null
/// }
/// </code>
/// </remarks>
public class MessageModel<T>
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    /// <value>
    /// 200: 成功<br/>
    /// 400: 客户端请求错误<br/>
    /// 401: 未授权<br/>
    /// 403: 禁止访问<br/>
    /// 404: 资源不存在<br/>
    /// 500: 服务器内部错误
    /// </value>
    public int StatusCode { get; set; } = (int)HttpStatusCodeEnum.Success;

    /// <summary>
    /// 操作是否成功
    /// </summary>
    /// <value>true: 操作成功；false: 操作失败</value>
    public bool IsSuccess { get; set; } = false;

    /// <summary>
    /// 返回给用户的消息信息
    /// </summary>
    /// <value>用户友好的提示信息，可直接展示在 UI 上</value>
    public string MessageInfo { get; set; } = "Nothing happened here.";

    /// <summary>
    /// 开发者调试信息
    /// </summary>
    /// <value>详细的错误信息或调试信息，仅用于开发和调试阶段，生产环境可能为空</value>
    public string MessageInfoDev { get; set; } = "Nothing happened here.";

    /// <summary>
    /// 响应数据
    /// </summary>
    /// <value>实际的业务数据，类型由泛型参数 T 决定</value>
    public T ResponseData { get; set; } = default;

    /// <summary>
    /// 业务错误码
    /// </summary>
    /// <value>用于客户端精确识别错误类型，例如 "Auth.InvalidCredentials"</value>
    public string? Code { get; set; }

    /// <summary>
    /// 多语言消息 key
    /// </summary>
    /// <value>例如 "error.auth.invalid_credentials"，用于前端 i18n 匹配</value>
    public string? MessageKey { get; set; }

    /// <summary>
    /// 返回成功响应（无数据）
    /// </summary>
    /// <param name="msg">成功消息</param>
    /// <returns>包含成功状态和消息的响应对象</returns>
    /// <example>
    /// <code>
    /// return MessageModel&lt;object&gt;.Success("删除成功");
    /// </code>
    /// </example>
    public static MessageModel<T> Success(string msg)
    {
        return Message(true, msg, default);
    }

    /// <summary>
    /// 返回成功响应（无数据，带业务码和多语言 key）
    /// </summary>
    public static MessageModel<T> Success(string msg, string? code, string? messageKey)
    {
        return Message(true, msg, default, code, messageKey);
    }

    /// <summary>
    /// 返回成功响应（包含数据）
    /// </summary>
    /// <param name="msg">成功消息</param>
    /// <param name="responseData">响应数据</param>
    /// <returns>包含成功状态、消息和数据的响应对象</returns>
    /// <example>
    /// <code>
    /// var user = new UserVo { Id = 1, Name = "张三" };
    /// return MessageModel&lt;UserVo&gt;.Success("获取成功", user);
    /// </code>
    /// </example>
    public static MessageModel<T> Success(string msg, T responseData)
    {
        return Message(true, msg, responseData);
    }

    /// <summary>
    /// 返回成功响应（包含数据，带业务码和多语言 key）
    /// </summary>
    public static MessageModel<T> Success(string msg, T responseData, string? code, string? messageKey)
    {
        return Message(true, msg, responseData, code, messageKey);
    }

    /// <summary>
    /// 返回失败响应（无数据）
    /// </summary>
    /// <param name="msg">失败消息</param>
    /// <returns>包含失败状态和消息的响应对象</returns>
    /// <example>
    /// <code>
    /// return MessageModel&lt;object&gt;.Failed("用户名或密码错误");
    /// </code>
    /// </example>
    public static MessageModel<T> Failed(string msg)
    {
        return Message(false, msg, default);
    }

    /// <summary>
    /// 返回失败响应（无数据，带业务码和多语言 key）
    /// </summary>
    public static MessageModel<T> Failed(string msg, string? code, string? messageKey)
    {
        return Message(false, msg, default, code, messageKey);
    }

    /// <summary>
    /// 返回失败响应（包含数据）
    /// </summary>
    /// <param name="msg">失败消息</param>
    /// <param name="responseData">响应数据（通常为错误详情）</param>
    /// <returns>包含失败状态、消息和数据的响应对象</returns>
    /// <example>
    /// <code>
    /// var errors = new { Field = "username", Error = "用户名已存在" };
    /// return MessageModel&lt;object&gt;.Failed("验证失败", errors);
    /// </code>
    /// </example>
    public static MessageModel<T> Failed(string msg, T responseData)
    {
        return Message(false, msg, responseData);
    }

    /// <summary>
    /// 返回失败响应（包含数据，带业务码和多语言 key）
    /// </summary>
    public static MessageModel<T> Failed(string msg, T responseData, string? code, string? messageKey)
    {
        return Message(false, msg, responseData, code, messageKey);
    }

    /// <summary>
    /// 创建自定义响应消息
    /// </summary>
    /// <param name="isSuccess">操作是否成功</param>
    /// <param name="msg">消息内容</param>
    /// <param name="responseData">响应数据</param>
    /// <returns>自定义的响应对象</returns>
    public static MessageModel<T> Message(bool isSuccess, string msg, T responseData)
    {
        return Message(isSuccess, msg, responseData, null, null);
    }

    /// <summary>
    /// 创建自定义响应消息（带业务码和多语言 key）
    /// </summary>
    /// <param name="isSuccess">操作是否成功</param>
    /// <param name="msg">消息内容</param>
    /// <param name="responseData">响应数据</param>
    /// <param name="code">业务错误码</param>
    /// <param name="messageKey">多语言消息 key</param>
    /// <returns>自定义的响应对象</returns>
    public static MessageModel<T> Message(bool isSuccess, string msg, T responseData, string? code, string? messageKey)
    {
        return new MessageModel<T>
        {
            MessageInfo = msg,
            ResponseData = responseData,
            IsSuccess = isSuccess,
            Code = code,
            MessageKey = messageKey
        };
    }
}

/// <summary>
/// 统一返回信息类（非泛型版本）
/// </summary>
/// <remarks>
/// 适用于返回数据类型不确定的场景，ResponseData 为 object 类型。
/// 推荐优先使用泛型版本 <see cref="MessageModel{T}"/> 以获得更好的类型安全。
/// </remarks>
public class MessageModel
{
    /// <summary>
    /// HTTP 状态码
    /// </summary>
    public int StatusCode { get; set; } = (int)HttpStatusCodeEnum.Success;

    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool IsSuccess { get; set; } = false;

    /// <summary>
    /// 返回给用户的消息信息
    /// </summary>
    public string MessageInfo { get; set; } = string.Empty;

    /// <summary>
    /// 业务错误码
    /// </summary>
    /// <value>用于客户端精确识别错误类型，例如 "Auth.InvalidCredentials"</value>
    public string? Code { get; set; }

    /// <summary>
    /// 多语言消息 key
    /// </summary>
    /// <value>例如 "error.auth.invalid_credentials"，用于前端 i18n 匹配</value>
    public string? MessageKey { get; set; }

    /// <summary>
    /// 返回的数据对象
    /// </summary>
    public object? ResponseData { get; set; }

    /// <summary>
    /// 返回成功响应
    /// </summary>
    /// <param name="msg">成功消息</param>
    /// <returns>包含成功状态和消息的响应对象</returns>
    public static MessageModel Success(string msg)
    {
        return new MessageModel
        {
            IsSuccess = true,
            MessageInfo = msg,
            StatusCode = (int)HttpStatusCodeEnum.Success
        };
    }

    /// <summary>
    /// 返回失败响应
    /// </summary>
    /// <param name="msg">失败消息</param>
    /// <returns>包含失败状态和消息的响应对象</returns>
    public static MessageModel Failed(string msg)
    {
        return new MessageModel
        {
            IsSuccess = false,
            MessageInfo = msg,
            StatusCode = (int)HttpStatusCodeEnum.BadRequest
        };
    }
}