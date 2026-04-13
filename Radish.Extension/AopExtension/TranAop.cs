using System.Reflection;
using System.Runtime.ExceptionServices;
using Castle.DynamicProxy;
using Microsoft.Extensions.Logging;
using Radish.Common.AttributeTool;
using Radish.Repository.UnitOfWorks;

namespace Radish.Extension.AopExtension;

/// <summary>事务 AOP 切面</summary>
/// <remarks>继承 IInterceptor 接口</remarks>
public class TranAop : IInterceptor
{
    private readonly ILogger<TranAop> _logger;
    private readonly IUnitOfWorkManage _unitOfWorkManage;

    public TranAop(IUnitOfWorkManage unitOfWorkManage, ILogger<TranAop> logger)
    {
        _unitOfWorkManage = unitOfWorkManage;
        _logger = logger;
    }

    /// <summary>实例化 IInterceptor 唯一方法</summary>
    /// <param name="invocation">包含被拦截方法的信息</param>
    public void Intercept(IInvocation invocation)
    {
        var method = invocation.MethodInvocationTarget ?? invocation.Method;
        // 对当前方法的特性验证
        // 如果需要验证
        if (method.GetCustomAttribute<UseTranAttribute>(true) is { } uta)
        {
            try
            {
                Before(method, uta.Propagation);

                invocation.Proceed();

                // 异步方法必须返回包装后的 Task，不能同步 Wait，否则会把业务异常包装成 AggregateException。
                if (IsAsyncMethod(method))
                {
                    if (method.ReturnType == typeof(Task))
                    {
                        invocation.ReturnValue = InterceptAsync((Task)invocation.ReturnValue, method);
                    }
                    else
                    {
                        invocation.ReturnValue = TranAsyncHelper.CallInterceptAsyncWithResult(
                            method.ReturnType.GenericTypeArguments[0],
                            invocation.ReturnValue,
                            this,
                            method);
                    }

                    return;
                }

                After(method);
            }
            catch (Exception ex)
            {
                var resolvedException = UnwrapException(ex);
                _logger.LogError(resolvedException, resolvedException.ToString());
                AfterException(method);
                ExceptionDispatchInfo.Capture(resolvedException).Throw();
                throw;
            }
        }
        else
        {
            invocation.Proceed(); // 直接执行被拦截方法
        }
    }

    private void Before(MethodInfo method, Propagation propagation)
    {
        switch (propagation)
        {
            case Propagation.Required:
                if (_unitOfWorkManage.TranCount <= 0)
                {
                    _logger.LogDebug($"Begin Transaction");
                    Console.WriteLine($"Begin Transaction");
                    _unitOfWorkManage.BeginTran(method);
                }

                break;
            case Propagation.Mandatory:
                if (_unitOfWorkManage.TranCount <= 0)
                {
                    throw new Exception("事务传播机制为:[Mandatory],当前不存在事务");
                }

                break;
            case Propagation.Nested:
                _logger.LogDebug($"Begin Transaction");
                Console.WriteLine($"Begin Transaction");
                _unitOfWorkManage.BeginTran(method);
                break;
            case Propagation.RequiresNew:
                // TODO: 实现真正的独立事务（需要使用独立的数据库连接或保存点）
                // 当前实现：总是开启新事务，类似 Nested
                _logger.LogDebug($"Begin Independent Transaction (RequiresNew)");
                Console.WriteLine($"Begin Independent Transaction (RequiresNew)");
                _unitOfWorkManage.BeginTran(method);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propagation), propagation, null);
        }
    }

    private void After(MethodInfo method)
    {
        _unitOfWorkManage.CommitTran(method);
    }

    private void AfterException(MethodInfo method)
    {
        _unitOfWorkManage.RollbackTran(method);
    }

    /// <summary>获取变量的默认值</summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public object GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private async Task SuccessAction(IInvocation invocation)
    {
        await Task.Run(() =>
        {
            //...
        });
    }

    public static bool IsAsyncMethod(MethodInfo method)
    {
        return
            method.ReturnType == typeof(Task) ||
            method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
            ;
    }

    private async Task TestActionAsync(IInvocation invocation)
    {
        await Task.Run(null);
    }

    internal async Task InterceptAsync(Task task, MethodInfo method)
    {
        try
        {
            await task.ConfigureAwait(false);
            After(method);
        }
        catch (Exception ex)
        {
            var resolvedException = UnwrapException(ex);
            _logger.LogError(resolvedException, resolvedException.ToString());
            AfterException(method);
            ExceptionDispatchInfo.Capture(resolvedException).Throw();
            throw;
        }
    }

    internal async Task<T> InterceptAsync<T>(Task<T> task, MethodInfo method)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            After(method);
            return result;
        }
        catch (Exception ex)
        {
            var resolvedException = UnwrapException(ex);
            _logger.LogError(resolvedException, resolvedException.ToString());
            AfterException(method);
            ExceptionDispatchInfo.Capture(resolvedException).Throw();
            throw;
        }
    }

    private static Exception UnwrapException(Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            var flattened = aggregateException.Flatten();
            if (flattened.InnerExceptions.Count == 1 && flattened.InnerException != null)
            {
                return flattened.InnerException;
            }
        }

        return exception;
    }
}

internal static class TranAsyncHelper
{
    public static object CallInterceptAsyncWithResult(
        Type taskReturnType,
        object actualReturnValue,
        TranAop interceptor,
        MethodInfo method)
    {
        var helperMethod = typeof(TranAsyncHelper)
            .GetMethod(nameof(InterceptAsyncWithResult), BindingFlags.Public | BindingFlags.Static);

        if (helperMethod == null)
        {
            throw new InvalidOperationException("未找到 TranAsyncHelper.InterceptAsyncWithResult 方法");
        }

        return helperMethod
            .MakeGenericMethod(taskReturnType)
            .Invoke(null, [actualReturnValue, interceptor, method])!;
    }

    public static Task<T> InterceptAsyncWithResult<T>(
        Task<T> actualReturnValue,
        TranAop interceptor,
        MethodInfo method)
    {
        return interceptor.InterceptAsync(actualReturnValue, method);
    }
}
