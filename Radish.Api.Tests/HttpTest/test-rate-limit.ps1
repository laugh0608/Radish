#!/usr/bin/env pwsh
###############################################################################
# 速率限制中间件自动化测试脚本（PowerShell）
# 用途：自动测试 Radish.Api 和 Radish.Auth 的速率限制功能
###############################################################################

param(
    [string]$ApiUrl = "http://localhost:5100",
    [string]$AuthUrl = "http://localhost:5200",
    [switch]$SkipGlobal,
    [switch]$SkipLogin,
    [switch]$Verbose
)

# 颜色输出函数
function Write-Success { param($Message) Write-Host "✓ $Message" -ForegroundColor Green }
function Write-Error { param($Message) Write-Host "✗ $Message" -ForegroundColor Red }
function Write-Info { param($Message) Write-Host "ℹ $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠ $Message" -ForegroundColor Yellow }

# 测试结果统计
$script:TestResults = @{
    Passed = 0
    Failed = 0
    Skipped = 0
}

function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [hashtable]$Body = $null
    )

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
            TimeoutSec = 5
        }

        if ($Body) {
            $params.Body = $Body
            $params.ContentType = "application/x-www-form-urlencoded"
        }

        $response = Invoke-WebRequest @params
        return @{
            Success = $true
            StatusCode = $response.StatusCode
            Headers = $response.Headers
        }
    }
    catch {
        $statusCode = 0
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }
        return @{
            Success = $false
            StatusCode = $statusCode
            Error = $_.Exception.Message
        }
    }
}

###############################################################################
# 测试 1：全局限流（Fixed Window）
###############################################################################
function Test-GlobalRateLimit {
    Write-Info "`n=== 测试 1：全局限流（Fixed Window 算法） ==="
    Write-Info "配置：每个 IP 每分钟最多 200 个请求"
    Write-Info "预期：第 201 个请求返回 429"

    $endpoint = "$ApiUrl/health"
    $limit = 200
    $testCount = $limit + 5
    $rejectedAt = 0

    Write-Info "发送 $testCount 个请求到 $endpoint ..."

    for ($i = 1; $i -le $testCount; $i++) {
        $result = Test-Endpoint -Url $endpoint

        if ($Verbose) {
            Write-Host "  请求 $i : $($result.StatusCode)"
        }

        if ($result.StatusCode -eq 429) {
            $rejectedAt = $i
            Write-Success "在第 $i 个请求时触发限流（预期：第 $($limit + 1) 个）"

            # 检查 Retry-After 头
            if ($result.Headers -and $result.Headers["Retry-After"]) {
                $retryAfter = $result.Headers["Retry-After"]
                Write-Info "Retry-After 头: $retryAfter 秒"
            }

            $script:TestResults.Passed++
            return $true
        }

        # 避免过快发送请求
        Start-Sleep -Milliseconds 10
    }

    if ($rejectedAt -eq 0) {
        Write-Error "未触发全局限流（发送了 $testCount 个请求）"
        Write-Warning "可能原因：1) 限流未启用 2) 限流配置过高 3) IP 在白名单中"
        $script:TestResults.Failed++
        return $false
    }

    return $true
}

###############################################################################
# 测试 2：登录限流（Sliding Window）
###############################################################################
function Test-LoginRateLimit {
    Write-Info "`n=== 测试 2：登录限流（Sliding Window 算法） ==="
    Write-Info "配置：每个 IP 15 分钟最多 10 次登录尝试"
    Write-Info "预期：第 11 次登录请求返回 429"

    $endpoint = "$AuthUrl/Account/Login"
    $limit = 10
    $testCount = $limit + 2
    $rejectedAt = 0

    $loginBody = @{
        username = "test_rate_limit_user"
        password = "wrong_password_for_testing"
    }

    Write-Info "发送 $testCount 次登录请求到 $endpoint ..."
    Write-Warning "注意：这些是故意失败的登录尝试（用于测试限流）"

    for ($i = 1; $i -le $testCount; $i++) {
        $result = Test-Endpoint -Url $endpoint -Method POST -Body $loginBody

        if ($Verbose) {
            Write-Host "  登录尝试 $i : $($result.StatusCode)"
        }

        if ($result.StatusCode -eq 429) {
            $rejectedAt = $i
            Write-Success "在第 $i 次登录尝试时触发限流（预期：第 $($limit + 1) 次）"

            if ($result.Headers -and $result.Headers["Retry-After"]) {
                $retryAfter = $result.Headers["Retry-After"]
                Write-Info "Retry-After 头: $retryAfter 秒"
            }

            $script:TestResults.Passed++
            return $true
        }

        # 登录请求间隔稍长一些
        Start-Sleep -Milliseconds 100
    }

    if ($rejectedAt -eq 0) {
        Write-Error "未触发登录限流（发送了 $testCount 次登录请求）"
        Write-Warning "可能原因：1) 登录限流未启用 2) 限流配置过高 3) IP 在白名单中"
        $script:TestResults.Failed++
        return $false
    }

    return $true
}

###############################################################################
# 测试 3：健康检查端点（应该不受限流影响）
###############################################################################
function Test-HealthCheckNoLimit {
    Write-Info "`n=== 测试 3：健康检查端点（验证 DisableRateLimiting） ==="
    Write-Info "预期：健康检查端点不受限流限制"

    $endpoint = "$ApiUrl/health"
    $testCount = 10

    Write-Info "快速发送 $testCount 个请求到 $endpoint ..."

    $allSuccess = $true
    for ($i = 1; $i -le $testCount; $i++) {
        $result = Test-Endpoint -Url $endpoint

        if ($result.StatusCode -ne 200) {
            Write-Error "请求 $i 失败: $($result.StatusCode)"
            $allSuccess = $false
        }
    }

    if ($allSuccess) {
        Write-Success "健康检查端点正常工作，未触发限流"
        $script:TestResults.Passed++
    } else {
        Write-Error "健康检查端点测试失败"
        $script:TestResults.Failed++
    }

    return $allSuccess
}

###############################################################################
# 测试 4：验证 429 响应格式
###############################################################################
function Test-RateLimitResponseFormat {
    Write-Info "`n=== 测试 4：验证 429 响应格式 ==="

    # 先触发限流
    $endpoint = "$ApiUrl/health"
    Write-Info "触发限流以验证响应格式..."

    for ($i = 1; $i -le 210; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 5
        }
        catch {
            if ($_.Exception.Response.StatusCode -eq 429) {
                Write-Success "成功触发 429 响应"

                # 尝试读取响应体
                try {
                    $stream = $_.Exception.Response.GetResponseStream()
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Close()
                    $stream.Close()

                    Write-Info "响应体: $responseBody"

                    # 验证响应格式
                    $json = $responseBody | ConvertFrom-Json
                    if ($json.status -eq 429 -and $json.message -and $json.success -eq $false) {
                        Write-Success "429 响应格式正确"
                        $script:TestResults.Passed++
                        return $true
                    } else {
                        Write-Error "429 响应格式不正确"
                        $script:TestResults.Failed++
                        return $false
                    }
                }
                catch {
                    Write-Warning "无法解析响应体: $($_.Exception.Message)"
                    $script:TestResults.Skipped++
                    return $false
                }
            }
        }

        Start-Sleep -Milliseconds 10
    }

    Write-Warning "未能触发 429 响应以验证格式"
    $script:TestResults.Skipped++
    return $false
}

###############################################################################
# 测试 5：并发请求测试
###############################################################################
function Test-ConcurrentRequests {
    Write-Info "`n=== 测试 5：并发请求测试 ==="
    Write-Info "配置：每个 IP 最多 100 个并发请求"
    Write-Info "发送 50 个并发请求（应该全部成功）..."

    $endpoint = "$ApiUrl/health"
    $concurrentCount = 50

    $jobs = @()
    for ($i = 1; $i -le $concurrentCount; $i++) {
        $jobs += Start-Job -ScriptBlock {
            param($url)
            try {
                $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
                return $response.StatusCode
            }
            catch {
                return 0
            }
        } -ArgumentList $endpoint
    }

    Write-Info "等待所有并发请求完成..."
    $results = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job

    $successCount = ($results | Where-Object { $_ -eq 200 }).Count
    $failCount = $concurrentCount - $successCount

    Write-Info "成功: $successCount / $concurrentCount"

    if ($successCount -eq $concurrentCount) {
        Write-Success "并发请求测试通过"
        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "并发请求测试失败（$failCount 个请求失败）"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 主测试流程
###############################################################################
function Main {
    Write-Host "`n" -NoNewline
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║         Radish 速率限制中间件自动化测试                        ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-Info "API 地址: $ApiUrl"
    Write-Info "Auth 地址: $AuthUrl"
    Write-Info "详细输出: $Verbose"
    Write-Host ""

    # 检查服务是否可用
    Write-Info "检查服务可用性..."
    $apiHealth = Test-Endpoint -Url "$ApiUrl/health"
    $authHealth = Test-Endpoint -Url "$AuthUrl/health"

    if (-not $apiHealth.Success) {
        Write-Error "API 服务不可用: $ApiUrl"
        Write-Warning "请确保 Radish.Api 正在运行"
        exit 1
    }
    Write-Success "API 服务可用"

    if (-not $authHealth.Success) {
        Write-Error "Auth 服务不可用: $AuthUrl"
        Write-Warning "请确保 Radish.Auth 正在运行"
        exit 1
    }
    Write-Success "Auth 服务可用"

    # 运行测试
    if (-not $SkipGlobal) {
        Test-GlobalRateLimit
        Write-Info "等待 5 秒后继续..."
        Start-Sleep -Seconds 5
    } else {
        Write-Warning "跳过全局限流测试"
        $script:TestResults.Skipped++
    }

    if (-not $SkipLogin) {
        Test-LoginRateLimit
        Write-Info "等待 5 秒后继续..."
        Start-Sleep -Seconds 5
    } else {
        Write-Warning "跳过登录限流测试"
        $script:TestResults.Skipped++
    }

    Test-HealthCheckNoLimit
    Test-ConcurrentRequests

    # 输出测试结果
    Write-Host "`n" -NoNewline
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                        测试结果汇总                            ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-Success "通过: $($script:TestResults.Passed)"
    Write-Error "失败: $($script:TestResults.Failed)"
    Write-Warning "跳过: $($script:TestResults.Skipped)"

    $total = $script:TestResults.Passed + $script:TestResults.Failed + $script:TestResults.Skipped
    $passRate = if ($total -gt 0) { [math]::Round(($script:TestResults.Passed / $total) * 100, 2) } else { 0 }

    Write-Host ""
    Write-Info "通过率: $passRate%"
    Write-Host ""

    if ($script:TestResults.Failed -gt 0) {
        Write-Warning "部分测试失败，请检查配置和日志"
        exit 1
    } else {
        Write-Success "所有测试通过！"
        exit 0
    }
}

# 运行主函数
Main
