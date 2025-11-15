use rayon::prelude::*; // 导入 rayon 的并行迭代器
use std::sync::atomic::{AtomicI64, Ordering};

/// 计算从 1 到 `iterations` 的所有整数之和。
///
/// # 参数
/// * `iterations` - 迭代次数（上限值）。
///
/// # 返回
/// 所有整数之和。
#[unsafe(no_mangle)]
pub extern "C" fn calculate_sum_rust(iterations: i64) -> i64 {
    let mut sum: i64 = 0;
    for i in 1..=iterations {
        sum += i;
    }
    sum
}

/// 模拟计算斐波那契数列的第 `iterations` 项，但只进行迭代次数。
///
/// 实际返回的是每次迭代中计算出的两个值之和，不代表真正的斐波那契数。
/// 主要目的是模拟 CPU 密集型计算。
#[unsafe(no_mangle)]
pub extern "C" fn calculate_fibonacci_like_rust(iterations: i64) -> i64 {
    if iterations <= 0 {
        return 0;
    }
    if iterations == 1 {
        return 1;
    }

    let mut a: i64 = 0;
    let mut b: i64 = 1;
    let mut result_sum: i64 = 0; // 用于累计一个结果，避免编译器优化掉整个循环

    for _ in 2..=iterations {
        // 从第 2 项开始迭代，因为 0 和 1 项已初始化
        let next_fib = a + b;
        a = b;
        b = next_fib;
        result_sum += next_fib % 100; // 模拟一个操作，并控制数值大小防止溢出，同时避免编译器过度优化
    }
    // 返回一个有意义的值，但不是真正的斐波那契数，因为中间值可能溢出
    // 关键在于循环被执行了 iterations 次，并且内部有加法和赋值操作
    a + b + result_sum // 返回一个最终的校验和，确保所有计算未被优化掉
}

/// 使用埃拉托斯特尼筛法计算小于等于 `limit` 的质数数量。
///
/// # 参数
/// * `limit` - 查找质数的上限。
///
/// # 返回
/// 小于等于 `limit` 的质数数量。
#[unsafe(no_mangle)]
pub extern "C" fn count_primes_sieve_rust(limit: i64) -> i64 {
    if limit < 2 {
        return 0;
    }

    // 创建一个布尔向量，初始都为 true
    // 注意：Vec<bool> 在 Rust 中被特殊优化，可能不会是每个 bool 占一个字节
    // 为了和 C# 的 bool[] 行为更接近，或者避免某些编译器优化，可以使用 Vec<u8> 或 Vec<char>
    // 但对于这种场景，Vec<bool> 通常是没问题的。
    let mut is_prime = vec![true; (limit + 1) as usize];

    // 0 和 1 不是质数
    is_prime[0] = false;
    is_prime[1] = false;

    let mut p: i64 = 2;
    while p * p <= limit {
        // 如果 is_prime[p] 仍然为 true，则它是一个质数
        if is_prime[p as usize] {
            // 将 p 的所有倍数标记为 false
            let mut i = p * p;
            while i <= limit {
                is_prime[i as usize] = false;
                i += p;
            }
        }
        p += 1;
    }

    // 统计质数数量
    let mut count: i64 = 0;
    for i in 2..=limit {
        if is_prime[i as usize] {
            count += 1;
        }
    }
    count
}

/// 优化过的试除法判断一个数是否为质数。
fn is_prime(n: i64) -> bool {
    if n <= 1 {
        return false;
    }
    if n <= 3 {
        return true;
    }
    if n % 2 == 0 || n % 3 == 0 {
        return false;
    }
    let mut i = 5;
    while i * i <= n {
        if n % i == 0 || n % (i + 2) == 0 {
            return false;
        }
        i += 6;
    }
    true
}

/// 并行计算小于等于 `limit` 的质数数量（通过试除法）。
///
/// # 参数
/// * `limit` - 查找质数的上限。
///
/// # 返回
/// 小于等于 `limit` 的质数数量。
#[unsafe(no_mangle)]
pub extern "C" fn count_primes_parallel_rust(limit: i64) -> i64 {
    if limit < 2 {
        return 0;
    }

    let prime_count = AtomicI64::new(0);

    // Rayon 会自动根据可用的核心数进行并行化。
    // 如果 limit 很小，rayon 会自动选择串行执行，避免并行开销。
    (2..=limit).into_par_iter()
        .for_each(|n| {
            if is_prime(n) {
                prime_count.fetch_add(1, Ordering::Relaxed);
            }
        });

    prime_count.load(Ordering::Relaxed)
}
