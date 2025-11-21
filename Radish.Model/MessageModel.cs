namespace Radish.Model;

/// <summary>通用返回信息类</summary>
public class MessageModel<T>
{
    /// <summary>状态码</summary>
    public int StatusCode { get; set; } = 500;

    /// <summary>操作是否成功</summary>
    public bool IsSuccess { get; set; } = false;

    /// <summary>返回信息</summary>
    public string MessageInfo { get; set; } = "Nothing happened here.";

    /// <summary>开发者信息</summary>
    public string MessageInfoDev { get; set; } = "Nothing happened here.";

    /// <summary>返回数据集合</summary>
    public T ResponseData { get; set; } = default;

    /// <summary>返回成功</summary>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    public static MessageModel<T> Success(string msg)
    {
        return Message(true, msg, default);
    }

    /// <summary>返回成功</summary>
    /// <param name="msg">消息</param>
    /// <param name="responseData">数据</param>
    /// <returns></returns>
    public static MessageModel<T> Success(string msg, T responseData)
    {
        return Message(true, msg, responseData);
    }

    /// <summary>返回失败</summary>
    /// <param name="msg">消息</param>
    /// <returns></returns>
    public static MessageModel<T> Fail(string msg)
    {
        return Message(false, msg, default);
    }

    /// <summary>返回失败</summary>
    /// <param name="msg">消息</param>
    /// <param name="responseData">数据</param>
    /// <returns></returns>
    public static MessageModel<T> Fail(string msg, T responseData)
    {
        return Message(false, msg, responseData);
    }

    /// <summary>返回消息</summary>
    /// <param name="isSuccess">失败/成功</param>
    /// <param name="msg">消息</param>
    /// <param name="responseData">数据</param>
    /// <returns></returns>
    public static MessageModel<T> Message(bool isSuccess, string msg, T responseData)
    {
        return new MessageModel<T>() { MessageInfo = msg, ResponseData = responseData, IsSuccess = isSuccess };
    }
}

public class MessageModel
{
    /// <summary>状态码</summary>
    public int StatusCode { get; set; } = 200;

    /// <summary>操作是否成功</summary>
    public bool IsSuccess { get; set; } = false;

    /// <summary>返回信息</summary>
    public string MessageInfo { get; set; } = string.Empty;

    /// <summary>返回数据集合</summary>
    public object ResponseData { get; set; } = new object();
}