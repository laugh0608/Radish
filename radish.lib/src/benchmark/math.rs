use rayon::iter::IntoParallelIterator;
use rayon::prelude::*;
use std::sync::atomic::{AtomicI64, Ordering};

/// 计算从 1 到 `iterations` 的所有整数之和
///
/// # 参数
/// * `iterations` - 迭代次数（上限值）
///
/// # 返回
/// 所有整数之和
#[unsafe(no_mangle)]
pub extern "C" fn calculate_sum_rust(iterations: i64) -> i64 {
    let mut sum: i64 = 0;
    for i in 1..=iterations {
        sum += i;
    }
    sum
}

/// 模拟计算斐波那契数列的第 `iterations` 项
///
/// 主要目的是模拟 CPU 密集型计算
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
    let mut result_sum: i64 = 0;

    for _ in 2..=iterations {
        let next_fib = a + b;
        a = b;
        b = next_fib;
        result_sum += next_fib % 100;
    }

    a + b + result_sum
}

/// 使用埃拉托斯特尼筛法计算小于等于 `limit` 的质数数量
///
/// # 参数
/// * `limit` - 查找质数的上限
///
/// # 返回
/// 小于等于 `limit` 的质数数量
#[unsafe(no_mangle)]
pub extern "C" fn count_primes_sieve_rust(limit: i64) -> i64 {
    if limit < 2 {
        return 0;
    }

    let mut is_prime = vec![true; (limit + 1) as usize];
    is_prime[0] = false;
    is_prime[1] = false;

    let mut p: i64 = 2;
    while p * p <= limit {
        if is_prime[p as usize] {
            let mut i = p * p;
            while i <= limit {
                is_prime[i as usize] = false;
                i += p;
            }
        }
        p += 1;
    }

    let mut count: i64 = 0;
    for i in 2..=limit {
        if is_prime[i as usize] {
            count += 1;
        }
    }
    count
}

/// 优化过的试除法判断一个数是否为质数
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

/// 并行计算小于等于 `limit` 的质数数量（通过试除法）
///
/// # 参数
/// * `limit` - 查找质数的上限
///
/// # 返回
/// 小于等于 `limit` 的质数数量
#[unsafe(no_mangle)]
pub extern "C" fn count_primes_parallel_rust(limit: i64) -> i64 {
    if limit < 2 {
        return 0;
    }

    let prime_count = AtomicI64::new(0);

    (2..=limit).into_par_iter().for_each(|n| {
        if is_prime(n) {
            prime_count.fetch_add(1, Ordering::Relaxed);
        }
    });

    prime_count.load(Ordering::Relaxed)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_calculate_sum() {
        assert_eq!(calculate_sum_rust(10), 55);
        assert_eq!(calculate_sum_rust(100), 5050);
    }

    #[test]
    fn test_count_primes_sieve() {
        assert_eq!(count_primes_sieve_rust(10), 4); // 2, 3, 5, 7
        assert_eq!(count_primes_sieve_rust(100), 25);
    }

    #[test]
    fn test_is_prime() {
        assert!(is_prime(2));
        assert!(is_prime(3));
        assert!(is_prime(5));
        assert!(is_prime(7));
        assert!(!is_prime(4));
        assert!(!is_prime(9));
    }
}
