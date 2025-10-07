using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Radish.Extensions;

/// <summary>自定义路特性</summary>
/// <remarks> /api/{version}/[controller]/[action] </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CustomRouteAttribute : RouteAttribute, IApiDescriptionGroupNameProvider
{
    /// <summary>分组名称</summary>
    /// <remarks>实现接口 IApiDescriptionGroupNameProvider，
    /// 用来切换 API 集合，配合 Scalar 的多版本文档</remarks>
    public string? GroupName { get; set; }

    /// <summary>标签名称</summary>
    /// <remarks>用来替换 Scalar 侧边栏中的 API 名称，类似于备注</remarks>
    public string? TagsName { get; set; }
    
    /// <summary>自定义路由构造函数，继承基类路由</summary>
    /// <param name="actionName"></param>
    public CustomRouteAttribute(string actionName = "[action]") : base("/api/{version}/[controller]/" + actionName)
    {
    }

    // /// <summary>自定义路由构造函数，继承基类路由</summary>
    // /// <param name="tagsName"></param>
    // /// <param name="groupName"></param>
    // /// <param name="actionName"></param>
    // public CustomRouteAttribute(string tagsName = "[controller]", string groupName = "[controller]",
    //     string actionName = "[action]")
    //     : base("/api/{version}/[controller]/" + actionName)
    // {
    //     GroupName = groupName;
    //     TagsName = tagsName;
    // }

    /// <summary>自定义版本和路由构造函数，继承基类路由</summary>
    /// <param name="actionName"></param>
    /// <param name="apiVersion"></param>
    public CustomRouteAttribute(CustomApiVersion.ApiVersions apiVersion,
        string actionName = "")
        : base($"/api/{apiVersion.ToString().ToLowerInvariant()}/[controller]/{actionName}")
    {
        GroupName = apiVersion.ToString();
    }
}

/// <summary>自定义 API 版本</summary>
public abstract class CustomApiVersion
{
    /// <summary>API 接口版本，自定义</summary>
    public enum ApiVersions
    {
        // 这里不需要考虑大小写，enum 类型默认为大写开头

        // 上方的自定义特性 CustomRouteAttribute 中已经处理了 ToLowerInvariant() 转为小写
        /// <summary>V1 公开版本</summary>
        V1 = 1,

        /// <summary>V1Beta 预发布版本</summary>
        V1Beta = 2,

        // /// <summary>v2 公开版本</summary>
        // v2 = 3,
    }
    
    public class AutoApiVersions
    {
        // @luobo 2025.10.7 目前 ABP 框架只能对应整数版本号，不能加字符串
        public string V1 = "v1"; // 对应特性为 [ApiVersion(1)]
        public string V2 = "v2"; // 对应特性为 [ApiVersion(2)]
        
    }
}