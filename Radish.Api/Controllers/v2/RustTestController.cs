using System.Diagnostics;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Radish.Api.Controllers.v2;

/// <summary>
/// Rust 原生互操作性能测试控制器 (v2)
/// </summary>
/// <remarks>
/// 提供 C# 与 Rust 原生库性能对比测试接口。
/// 此接口为 v2 版本，演示跨语言互操作和版本控制。
/// </remarks>
[ApiController]
[ApiVersion(2)]
[Route("api/v{version:apiVersion}/[controller]/[action]")]
[Produces("application/json")]
// [Authorize(Policy = "RadishAuthPolicy")]
[Authorize(Policy = "Client")]
[Tags("性能测试")]
public class RustTestController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> TestSum1(long iterations = 50_000_000)
    {
        await Task.CompletedTask;

        // --- 预热阶段 (Optional but recommended for accurate benchmarks) ---
        // 确保 JIT 编译和 DLL 加载完成，消除首次调用开销
        CalculateSumCSharp(1000);
        try
        {
            CalculateSumRust(1000);
        }
        catch (DllNotFoundException)
        {
            /* Handle or ignore, as it will be caught later */
        }
        catch (Exception)
        {
            /* Handle or ignore */
        }
        // --- 预热结束 ---

        var stopwatch = new Stopwatch();
        var result = new PerformanceResult { Iterations = iterations };

        // --- 1. C# 性能测试 ---
        stopwatch.Start();
        result.CSharpResult = CalculateSumCSharp(iterations);
        stopwatch.Stop();
        result.CSharpExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"C# Result: {result.CSharpResult}, Time: {result.CSharpExecutionTimeMs} ms");
        stopwatch.Reset();

        // --- 2. Rust 性能测试 ---
        try
        {
            CalculateSumRust(100); // 预热 (可选)
            stopwatch.Start();
            result.RustResult = CalculateSumRust(iterations);
            stopwatch.Stop();
            result.RustExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Rust Result: {result.RustResult}, Time: {result.RustExecutionTimeMs} ms");

            // 验证结果是否一致
            if (result.CSharpResult != result.RustResult)
            {
                result.Message = "WARNING: C# and Rust calculation results do not match!";
            }
            else
            {
                result.Message = "C# and Rust calculation results match.";
            }
        }
        catch (DllNotFoundException)
        {
            result.Message = $"ERROR: Rust library '{RustLibName}' not found. " +
                             "Please ensure 'radish_lib.dll' (Windows) or 'libradish_lib.so' (Linux) " +
                             "or 'libradish_lib.dylib' (macOS) is in the application's output directory.";
            result.RustExecutionTimeMs = -1; // 表示失败
        }
        catch (Exception ex)
        {
            result.Message = $"ERROR calling Rust function: {ex.Message}";
            result.RustExecutionTimeMs = -1; // 表示失败
        }

        if (result.CSharpExecutionTimeMs > result.RustExecutionTimeMs)
        {
            result.Message = "Rust Execution Test Victory!";
        }
        else if (result.RustExecutionTimeMs > result.CSharpExecutionTimeMs)
        {
            result.Message = "C# Execution Test Victory!";
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> TestSum2(long iterations = 50_000_000)
    {
        await Task.CompletedTask;

        var stopwatch = new Stopwatch();
        var result = new PerformanceResult { Iterations = iterations };

        // --- 预热阶段 (可选但推荐) ---
        // 使用一个小值进行预热，确保JIT编译和DLL加载完成
        long warmUpIterations = Math.Min(iterations, 1_000_000); // 确保预热值合理
        CalculateFibonacciLikeCSharp(warmUpIterations);
        try
        {
            CalculateFibonacciLikeRust(warmUpIterations);
        }
        catch (DllNotFoundException)
        {
            /* Handled in main logic */
        }
        catch (Exception)
        {
            /* Handled in main logic */
        }
        // --- 预热结束 ---

        // --- 1. C# 性能测试 ---
        stopwatch.Start();
        result.CSharpResult = CalculateFibonacciLikeCSharp(iterations);
        stopwatch.Stop();
        result.CSharpExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"C# (Fib-like) Result: {result.CSharpResult}, Time: {result.CSharpExecutionTimeMs} ms");
        stopwatch.Reset();

        // --- 2. Rust 性能测试 ---
        try
        {
            stopwatch.Start();
            result.RustResult = CalculateFibonacciLikeRust(iterations);
            stopwatch.Stop();
            result.RustExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Rust (Fib-like) Result: {result.RustResult}, Time: {result.RustExecutionTimeMs} ms");

            if (result.CSharpResult != result.RustResult)
            {
                result.Message = "WARNING: C# and Rust calculation results do not match!";
            }
            else
            {
                result.Message = "C# and Rust calculation results match.";
            }
        }
        catch (DllNotFoundException)
        {
            result.Message = $"ERROR: Rust library '{RustLibName}' not found. " +
                             "Ensure 'radish_lib.dll' (Windows) or 'libradish_lib.so' (Linux) " +
                             "or 'libradish_lib.dylib' (macOS) is in the application's output directory.";
            result.RustExecutionTimeMs = -1;
        }
        catch (Exception ex)
        {
            result.Message = $"ERROR calling Rust function: {ex.Message}";
            result.RustExecutionTimeMs = -1;
        }

        if (result.CSharpExecutionTimeMs > result.RustExecutionTimeMs)
        {
            result.Message = "Rust Execution Test Victory!";
        }
        else if (result.RustExecutionTimeMs > result.CSharpExecutionTimeMs)
        {
            result.Message = "C# Execution Test Victory!";
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> TestSum3(long iterations = 50_000_000)
    {
        await Task.CompletedTask;
        var stopwatch = new Stopwatch();
        var result = new PerformanceResult { Iterations = iterations };

        // --- 预热阶段 (可选但推荐) ---
        long warmUpLimit = Math.Min(iterations, 1_000_000); // 确保预热值合理
        CountPrimesSieveCSharp(warmUpLimit);
        try
        {
            CountPrimesSieveRust(warmUpLimit);
        }
        catch (DllNotFoundException)
        {
            /* Handled in main logic */
        }
        catch (Exception)
        {
            /* Handled in main logic */
        }
        // --- 预热结束 ---

        // --- 1. C# 性能测试 ---
        stopwatch.Start();
        result.CSharpResult = CountPrimesSieveCSharp(iterations);
        stopwatch.Stop();
        result.CSharpExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        Console.WriteLine($"C# (Sieve) Result: {result.CSharpResult}, Time: {result.CSharpExecutionTimeMs} ms");
        stopwatch.Reset();

        // --- 2. Rust 性能测试 ---
        try
        {
            stopwatch.Start();
            result.RustResult = CountPrimesSieveRust(iterations);
            stopwatch.Stop();
            result.RustExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"Rust (Sieve) Result: {result.RustResult}, Time: {result.RustExecutionTimeMs} ms");

            if (result.CSharpResult != result.RustResult)
            {
                result.Message = "WARNING: C# and Rust calculation results do not match!";
            }
            else
            {
                result.Message = "C# and Rust calculation results match.";
            }
        }
        catch (DllNotFoundException)
        {
            result.Message = $"ERROR: Rust library '{RustLibName}' not found. " +
                             "Ensure 'radish_lib.dll' (Windows) or 'libradish_lib.so' (Linux) " +
                             "or 'libradish_lib.dylib' (macOS) is in the application's output directory.";
            result.RustExecutionTimeMs = -1;
        }
        catch (Exception ex)
        {
            result.Message = $"ERROR calling Rust function: {ex.Message}";
            result.RustExecutionTimeMs = -1;
        }

        if (result.CSharpExecutionTimeMs > result.RustExecutionTimeMs)
        {
            result.Message = "Rust Execution Test Victory!";
        }
        else if (result.RustExecutionTimeMs > result.CSharpExecutionTimeMs)
        {
            result.Message = "C# Execution Test Victory!";
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> TestSum4(long iterations = 50_000_000)
    {
        await Task.CompletedTask;
        var stopwatch = new Stopwatch();
        var result = new PerformanceResult { Iterations = iterations }; // 将Iterations字段用于表示limit

        // --- 预热阶段 (可选但推荐) ---
        long warmUpLimit = Math.Min(iterations, 100_000); // 确保预热值合理
        CountPrimesParallelCSharp(warmUpLimit);
        try
        {
            CountPrimesParallelRust(warmUpLimit);
        }
        catch (DllNotFoundException)
        {
            /* Handled in main logic */
        }
        catch (Exception)
        {
            /* Handled in main logic */
        }
        // --- 预热结束 ---

        // --- 1. C# 性能测试 ---
        stopwatch.Start();
        result.CSharpResult = CountPrimesParallelCSharp(iterations);
        stopwatch.Stop();
        result.CSharpExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        Console.WriteLine(
            $"C# (Parallel Primes) Result: {result.CSharpResult}, Time: {result.CSharpExecutionTimeMs} ms");
        stopwatch.Reset();

        // --- 2. Rust 性能测试 ---
        try
        {
            stopwatch.Start();
            result.RustResult = CountPrimesParallelRust(iterations);
            stopwatch.Stop();
            result.RustExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            Console.WriteLine(
                $"Rust (Parallel Primes) Result: {result.RustResult}, Time: {result.RustExecutionTimeMs} ms");

            if (result.CSharpResult != result.RustResult)
            {
                result.Message = "WARNING: C# and Rust calculation results do not match!";
            }
            else
            {
                result.Message = "C# and Rust calculation results match.";
            }
        }
        catch (DllNotFoundException)
        {
            result.Message = $"ERROR: Rust library '{RustLibName}' not found. " +
                             "Ensure 'radish_lib.dll' (Windows) or 'libradish_lib.so' (Linux) " +
                             "or 'libradish_lib.dylib' (macOS) is in the application's output directory.";
            result.RustExecutionTimeMs = -1;
        }
        catch (Exception ex)
        {
            result.Message = $"ERROR calling Rust function: {ex.Message}";
            result.RustExecutionTimeMs = -1;
        }

        if (result.CSharpExecutionTimeMs > result.RustExecutionTimeMs)
        {
            result.Message = "Rust Execution Test Victory!";
        }
        else if (result.RustExecutionTimeMs > result.CSharpExecutionTimeMs)
        {
            result.Message = "C# Execution Test Victory!";
        }

        return Ok(result);
    }

    // 定义结果返回体
    private class PerformanceResult
    {
        public long Iterations { get; set; }
        public long CSharpExecutionTimeMs { get; set; }
        public long RustExecutionTimeMs { get; set; }
        public long CSharpResult { get; set; }
        public long RustResult { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    // 定义 Rust 库的名称。在 Windows上 是 radish_lib.dll，在Linux/macOS上 是 libradish_lib.so/libradish_lib.dylib。
    // DllImport 会根据操作系统自动查找。
    private const string RustLibName = "radish_lib";

    // 导入 Rust 库中的 calculate_sum_rust 函数，累加计算
    [DllImport(RustLibName, EntryPoint = "calculate_sum_rust", CallingConvention = CallingConvention.Cdecl)]
    private static extern long CalculateSumRust(long iterations);

    // 导入 Rust 库中的 calculate_fibonacci_like_rust 函数，斐波那契模拟
    [DllImport(RustLibName, EntryPoint = "calculate_fibonacci_like_rust", CallingConvention = CallingConvention.Cdecl)]
    private static extern long CalculateFibonacciLikeRust(long iterations);

    // 导入 Rust 库中的 count_primes_sieve_rust 函数，埃拉托斯特尼筛法
    [DllImport(RustLibName, EntryPoint = "count_primes_sieve_rust", CallingConvention = CallingConvention.Cdecl)]
    private static extern long CountPrimesSieveRust(long limit);

    // 导入 Rust 库中的 count_primes_parallel_rust 函数，多线程试除法
    [DllImport(RustLibName, EntryPoint = "count_primes_parallel_rust", CallingConvention = CallingConvention.Cdecl)]
    private static extern long CountPrimesParallelRust(long limit);

    // C# 版本的累加计算函数
    private static long CalculateSumCSharp(long iterations)
    {
        long sum = 0;
        for (var i = 1; i <= iterations; i++)
        {
            sum += i;
        }

        return sum;
    }

    // C# 版本的斐波那契模拟计算
    private long CalculateFibonacciLikeCSharp(long iterations)
    {
        if (iterations <= 0)
        {
            return 0;
        }

        if (iterations == 1)
        {
            return 1;
        }

        long a = 0;
        long b = 1;
        long resultSum = 0; // 用于累计一个结果，避免编译器优化掉整个循环

        for (long i = 2; i <= iterations; i++)
        {
            long nextFib = a + b;
            a = b;
            b = nextFib;
            resultSum += nextFib % 100; // 模拟一个操作，并控制数值大小防止溢出
        }

        return a + b + resultSum; // 返回一个最终的校验和
    }

    // C# 版本的埃拉托斯特尼筛法
    private long CountPrimesSieveCSharp(long limit)
    {
        if (limit < 2)
        {
            return 0;
        }

        bool[] isPrime = new bool[limit + 1];
        for (int i = 0; i <= limit; i++)
        {
            isPrime[i] = true;
        }

        isPrime[0] = false;
        isPrime[1] = false;

        long count = 0;
        for (long p = 2; p * p <= limit; p++)
        {
            if (isPrime[p]) // 如果 p 是质数
            {
                // 将 p 的所有倍数标记为非质数
                for (long i = p * p; i <= limit; i += p)
                {
                    isPrime[i] = false;
                }
            }
        }

        // 统计质数数量
        for (long i = 2; i <= limit; i++)
        {
            if (isPrime[i])
            {
                count++;
            }
        }

        return count;
    }

    // 优化过的试除法判断一个数是否为质数。
    private bool IsPrime(long n)
    {
        if (n <= 1)
        {
            return false;
        }

        if (n <= 3)
        {
            return true;
        }

        if (n % 2 == 0 || n % 3 == 0)
        {
            return false;
        }

        long i = 5;
        while (i * i <= n)
        {
            if (n % i == 0 || n % (i + 2) == 0)
            {
                return false;
            }

            i += 6;
        }

        return true;
    }

    // 并行计算小于等于 `limit` 的质数数量（通过试除法）。
    private long CountPrimesParallelCSharp(long limit)
    {
        if (limit < 2)
        {
            return 0;
        }

        long totalPrimeCount = 0; // 使用 Interlocked 保护

        // 使用 Parallel.For 进行并行迭代
        // 默认使用 Environment.ProcessorCount 作为最大并发度
        Parallel.For(2, limit + 1, (n) =>
        {
            if (IsPrime(n))
            {
                // 原子地增加计数器
                Interlocked.Increment(ref totalPrimeCount);
            }
        });

        return totalPrimeCount;
    }
}
