using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace Radish.Auth.Controllers;

/// <summary>
/// 占位控制器（当前由 OpenIddict 自身处理 /connect/token）。
/// 若未来需要自定义 Token 响应结构，可以在此处接入 Passthrough 模式。
/// </summary>
public class TokenController
{
}
