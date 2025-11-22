using System.Reflection;
using Castle.DynamicProxy;
using Newtonsoft.Json;
using Radish.Common;

namespace Radish.Extension.AopExtension;

/// <summary>服务 AOP 切面</summary>
/// <remarks>继承 IInterceptor 接口</remarks>
public class ServiceAop : IInterceptor
{
    /// <summary>实例化 IInterceptor 唯一方法</summary>
    /// <remarks>invocation 包含被拦截方法的信息</remarks>
    /// <param name="invocation"></param>
    public void Intercept(IInvocation invocation)
    {
        string json;
        try
        {
            json = JsonConvert.SerializeObject(invocation.Arguments);
        }
        catch (Exception ex)
        {
            json = "无法序列化，可能是 lambda 表达式等原因造成，按照框架优化代码" + ex.ToString();
        }

        DateTime startTime = DateTime.Now;
        AopLogInfoTool apiLogAopInfo = new AopLogInfoTool
        {
            RequestTime = startTime.ToString("yyyy-MM-dd hh:mm:ss fff"),
            OpUserName = "",
            RequestMethodName = invocation.Method.Name,
            RequestParamsName = string.Join(", ", invocation.Arguments.Select(a => (a ?? "").ToString()).ToArray()),
            ResponseJsonData = json
        };

        try
        {
            // 在被拦截的方法执行完毕后 继续执行当前方法，注意是被拦截的是异步的
            invocation.Proceed();

            // 异步获取异常，先执行
            if (IsAsyncMethod(invocation.Method))
            {
                // Wait task execution and modify return value
                if (invocation.Method.ReturnType == typeof(Task))
                {
                    invocation.ReturnValue = InternalAsyncHelper.AwaitTaskWithPostActionAndFinally(
                        (Task)invocation.ReturnValue,
                        async () => await SuccessAction(invocation, apiLogAopInfo, startTime), /* 成功时执行 */
                        ex => { LogEx(ex, apiLogAopInfo); });
                }
                // Task<TResult>
                else
                {
                    invocation.ReturnValue = InternalAsyncHelper.CallAwaitTaskWithPostActionAndFinallyAndGetResult(
                        invocation.Method.ReturnType.GenericTypeArguments[0],
                        invocation.ReturnValue,
                        async (o) => await SuccessAction(invocation, apiLogAopInfo, startTime, o), /* 成功时执行 */
                        ex => { LogEx(ex, apiLogAopInfo); });
                }
            }
            // 同步
            else
            {
                string jsonResult;
                try
                {
                    jsonResult = JsonConvert.SerializeObject(invocation.ReturnValue);
                }
                catch (Exception ex)
                {
                    jsonResult = "无法序列化，可能是 lambda 表达式等原因造成，按照框架优化代码" + ex.ToString();
                }

                DateTime endTime = DateTime.Now;
                string responseTime = (endTime - startTime).Milliseconds.ToString();
                apiLogAopInfo.ResponseTime = endTime.ToString("yyyy-MM-dd hh:mm:ss fff");
                apiLogAopInfo.ResponseIntervalTime = responseTime + "ms";
                apiLogAopInfo.ResponseJsonData = jsonResult;
                Console.WriteLine(JsonConvert.SerializeObject(apiLogAopInfo));
            }
        }
        catch (Exception ex)
        {
            LogEx(ex, apiLogAopInfo);
            throw;
        }
    }

    private async Task SuccessAction(IInvocation invocation, AopLogInfoTool apiLogAopInfo, DateTime startTime,
        object o = null)
    {
        DateTime endTime = DateTime.Now;
        string responseTime = (endTime - startTime).Milliseconds.ToString();
        apiLogAopInfo.ResponseTime = endTime.ToString("yyyy-MM-dd hh:mm:ss fff");
        apiLogAopInfo.ResponseIntervalTime = responseTime + "ms";
        apiLogAopInfo.ResponseJsonData = JsonConvert.SerializeObject(o);

        await Task.CompletedTask;
        // await Task.Run(() => { Console.WriteLine("执行成功-->" + JsonConvert.SerializeObject(apiLogAopInfo)); });
    }

    private void LogEx(Exception ex, AopLogInfoTool dataIntercept)
    {
        if (ex != null)
        {
            Console.WriteLine("[ERROR]:" + ex.Message + JsonConvert.SerializeObject(dataIntercept));
        }
    }

    public static bool IsAsyncMethod(MethodInfo method)
    {
        return
            method.ReturnType == typeof(Task) ||
            method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
    }
}

internal static class InternalAsyncHelper
{
    public static async Task AwaitTaskWithPostActionAndFinally(Task actualReturnValue, Func<Task> postAction,
        Action<Exception> finalAction)
    {
        Exception exception = null;

        try
        {
            await actualReturnValue;
            await postAction();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            finalAction(exception);
        }
    }

    public static async Task<T> AwaitTaskWithPostActionAndFinallyAndGetResult<T>(Task<T> actualReturnValue,
        Func<object, Task> postAction,
        Action<Exception> finalAction)
    {
        Exception exception = null;
        try
        {
            var result = await actualReturnValue;
            await postAction(result);
            return result;
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            finalAction(exception);
        }
    }

    public static object CallAwaitTaskWithPostActionAndFinallyAndGetResult(Type taskReturnType,
        object actualReturnValue,
        Func<object, Task> action, Action<Exception> finalAction)
    {
        return typeof(InternalAsyncHelper)
            .GetMethod("AwaitTaskWithPostActionAndFinallyAndGetResult", BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(taskReturnType)
            .Invoke(null, new object[] { actualReturnValue, action, finalAction });
    }
}