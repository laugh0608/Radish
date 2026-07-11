using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Radish.Api.Controllers;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests.Security;

public class ProductionApiSurfaceTests
{
    private static readonly string[] RetiredControllerNames =
    [
        "RustTestController",
        "WeatherForecastController",
        "AppSettingController",
        "TransactionController"
    ];

    private static readonly (Type ControllerType, string ActionName)[] RetiredActionNames =
    [
        (typeof(TenantController), "AddTest"),
        (typeof(UserController), "TestPushUnreadCount")
    ];

    [Fact]
    public void ProductionApi_ShouldNotExposeRetiredDemoControllers()
    {
        var controllerNames = typeof(UserController).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var reintroducedControllers = RetiredControllerNames
            .Where(controllerNames.Contains)
            .ToArray();

        reintroducedControllers.ShouldBeEmpty(
            "已裁决退出的性能测试或演示 Controller 不得重新进入生产 API 表面");
    }

    [Fact]
    public void ProductionApi_ShouldNotExposeRetiredDemoActions()
    {
        var reintroducedActions = RetiredActionNames
            .Where(item => item.ControllerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Any(method => string.Equals(method.Name, item.ActionName, StringComparison.Ordinal)))
            .Select(item => $"{item.ControllerType.Name}.{item.ActionName}")
            .ToArray();

        reintroducedActions.ShouldBeEmpty(
            "已裁决退出的测试写入或手动推送 Action 不得重新进入生产 API 表面");
    }
}
