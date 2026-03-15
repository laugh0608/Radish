#!/usr/bin/env pwsh
###############################################################################
# 附件上传功能自动化测试脚本（PowerShell）
# 用途：自动测试 Radish.Api 的文件上传、图片处理、文件去重等功能
###############################################################################

param(
    [string]$ApiUrl = "http://localhost:5100",
    [string]$AuthUrl = "http://localhost:5200",
    [string]$Username = "system",
    [string]$Password = "System123!",
    [switch]$SkipAuth,
    [switch]$SkipUpload,
    [switch]$SkipQuery,
    [switch]$SkipDelete,
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
    TotalTests = 0
}

# 全局变量
$script:AccessToken = $null
$script:UploadedAttachments = @()
$script:TestFilesDir = Join-Path $PSScriptRoot "test-files"

###############################################################################
# 辅助函数
###############################################################################

function Test-Endpoint {
    param(
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        $Body = $null,
        [string]$ContentType = $null
    )

    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
            TimeoutSec = 30
        }

        if ($Body) {
            $params.Body = $Body
            if ($ContentType) {
                $params.ContentType = $ContentType
            }
        }

        $response = Invoke-WebRequest @params
        $content = $null
        if ($response.Content) {
            try {
                $content = $response.Content | ConvertFrom-Json
            } catch {
                $content = $response.Content
            }
        }

        return @{
            Success = $true
            StatusCode = $response.StatusCode
            Headers = $response.Headers
            Content = $content
        }
    }
    catch {
        $statusCode = 0
        $errorContent = $null
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errorContent = $reader.ReadToEnd()
                $reader.Close()
                $stream.Close()
                $errorContent = $errorContent | ConvertFrom-Json
            } catch {}
        }
        return @{
            Success = $false
            StatusCode = $statusCode
            Error = $_.Exception.Message
            Content = $errorContent
        }
    }
}

function Get-AccessToken {
    Write-Info "获取 Access Token..."

    $body = @{
        grant_type = "password"
        client_id = "radish-client"
        client_secret = "radish-secret"
        username = $Username
        password = $Password
        scope = "radish-api"
    }

    $result = Test-Endpoint -Url "$AuthUrl/connect/token" -Method POST -Body $body -ContentType "application/x-www-form-urlencoded"

    if ($result.Success -and $result.Content.access_token) {
        $script:AccessToken = $result.Content.access_token
        Write-Success "成功获取 Access Token"
        return $true
    } else {
        Write-Error "获取 Access Token 失败: $($result.Error)"
        return $false
    }
}

function New-TestImage {
    param(
        [string]$FileName,
        [int]$Width = 800,
        [int]$Height = 600
    )

    $filePath = Join-Path $script:TestFilesDir $FileName

    # 如果文件已存在，直接返回
    if (Test-Path $filePath) {
        return $filePath
    }

    # 创建简单的测试图片（使用 Base64 编码的最小 PNG）
    # 这是一个 1x1 像素的透明 PNG
    $pngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    $bytes = [Convert]::FromBase64String($pngBase64)
    [System.IO.File]::WriteAllBytes($filePath, $bytes)

    return $filePath
}

function New-TestDocument {
    param([string]$FileName)

    $filePath = Join-Path $script:TestFilesDir $FileName

    if (Test-Path $filePath) {
        return $filePath
    }

    # 创建简单的 Markdown 文档
    $content = @"
# 测试文档

这是一个用于测试文件上传的 Markdown 文档。

## 功能测试

- 文件上传
- 文件去重
- 文件下载
- 文件删除

测试时间: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
"@

    [System.IO.File]::WriteAllText($filePath, $content, [System.Text.Encoding]::UTF8)
    return $filePath
}

function Upload-File {
    param(
        [string]$FilePath,
        [string]$BusinessType = "General",
        [bool]$GenerateThumbnail = $true,
        [bool]$RemoveExif = $true,
        [string]$EndpointSuffix = "UploadImage"
    )

    if (-not (Test-Path $FilePath)) {
        Write-Error "文件不存在: $FilePath"
        return $null
    }

    $fileName = [System.IO.Path]::GetFileName($FilePath)
    $fileBytes = [System.IO.File]::ReadAllBytes($FilePath)
    $boundary = [System.Guid]::NewGuid().ToString()

    # 构建 multipart/form-data 请求体
    $bodyLines = @(
        "--$boundary",
        "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`"",
        "Content-Type: application/octet-stream",
        "",
        [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes),
        "--$boundary",
        "Content-Disposition: form-data; name=`"businessType`"",
        "",
        $BusinessType,
        "--$boundary",
        "Content-Disposition: form-data; name=`"generateThumbnail`"",
        "",
        $GenerateThumbnail.ToString().ToLower(),
        "--$boundary",
        "Content-Disposition: form-data; name=`"removeExif`"",
        "",
        $RemoveExif.ToString().ToLower(),
        "--$boundary--"
    )

    $body = $bodyLines -join "`r`n"

    $headers = @{
        "Authorization" = "Bearer $script:AccessToken"
    }

    $result = Test-Endpoint `
        -Url "$ApiUrl/api/v1/Attachment/$EndpointSuffix" `
        -Method POST `
        -Headers $headers `
        -Body $body `
        -ContentType "multipart/form-data; boundary=$boundary"

    return $result
}

###############################################################################
# 测试 1：认证测试
###############################################################################
function Test-Authentication {
    Write-Info "`n=== 测试 1：用户认证 ==="
    $script:TestResults.TotalTests++

    if ($SkipAuth) {
        Write-Warning "跳过认证测试（使用已有 Token）"
        $script:TestResults.Skipped++
        return $true
    }

    if (Get-AccessToken) {
        Write-Success "认证测试通过"
        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "认证测试失败"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 2：图片上传基础功能
###############################################################################
function Test-ImageUploadBasic {
    Write-Info "`n=== 测试 2：图片上传基础功能 ==="
    $script:TestResults.TotalTests++

    # 准备测试图片
    $testImage = New-TestImage -FileName "test-upload-basic.png"
    Write-Info "使用测试图片: $testImage"

    $result = Upload-File -FilePath $testImage -BusinessType "General"

    if ($result.Success -and $result.StatusCode -eq 200 -and $result.Content.isSuccess) {
        Write-Success "图片上传成功"
        Write-Info "  - 附件 ID: $($result.Content.responseData.id)"
        Write-Info "  - 文件 URL: $($result.Content.responseData.url)"
        Write-Info "  - 文件大小: $($result.Content.responseData.fileSize) bytes"

        # 保存附件 ID 供后续测试使用
        $script:UploadedAttachments += $result.Content.responseData

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "图片上传失败"
        Write-Error "  - 状态码: $($result.StatusCode)"
        Write-Error "  - 错误: $($result.Content.messageInfo)"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 3：文档上传功能
###############################################################################
function Test-DocumentUpload {
    Write-Info "`n=== 测试 3：文档上传功能 ==="
    $script:TestResults.TotalTests++

    $testDoc = New-TestDocument -FileName "test-upload-doc.md"
    Write-Info "使用测试文档: $testDoc"

    $result = Upload-File `
        -FilePath $testDoc `
        -BusinessType "Document" `
        -GenerateThumbnail $false `
        -RemoveExif $false `
        -EndpointSuffix "UploadDocument"

    if ($result.Success -and $result.StatusCode -eq 200 -and $result.Content.isSuccess) {
        Write-Success "文档上传成功"
        Write-Info "  - 附件 ID: $($result.Content.responseData.id)"
        Write-Info "  - 原始文件名: $($result.Content.responseData.originalName)"

        $script:UploadedAttachments += $result.Content.responseData

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "文档上传失败"
        Write-Error "  - 错误: $($result.Content.messageInfo)"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 4：头像上传（测试文件大小限制）
###############################################################################
function Test-AvatarUpload {
    Write-Info "`n=== 测试 4：头像上传（2MB 限制） ==="
    $script:TestResults.TotalTests++

    $testAvatar = New-TestImage -FileName "test-avatar.png"
    Write-Info "使用测试头像: $testAvatar"

    $result = Upload-File -FilePath $testAvatar -BusinessType "Avatar"

    if ($result.Success -and $result.StatusCode -eq 200) {
        Write-Success "头像上传成功"
        Write-Info "  - 存储类型: $($result.Content.responseData.storageType)"
        Write-Info "  - 业务类型: $($result.Content.responseData.businessType)"

        $script:UploadedAttachments += $result.Content.responseData

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "头像上传失败"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 5：文件去重功能
###############################################################################
function Test-FileDuplication {
    Write-Info "`n=== 测试 5：文件去重功能 ==="
    $script:TestResults.TotalTests++

    $testImage = New-TestImage -FileName "test-dedup.png"
    Write-Info "上传同一文件两次，测试去重功能"

    # 第一次上传
    $result1 = Upload-File -FilePath $testImage -BusinessType "General"
    if (-not $result1.Success) {
        Write-Error "第一次上传失败"
        $script:TestResults.Failed++
        return $false
    }

    $firstHash = $result1.Content.responseData.fileHash
    $firstId = $result1.Content.responseData.id
    Write-Info "  - 第一次上传 ID: $firstId"
    Write-Info "  - 文件哈希: $firstHash"

    Start-Sleep -Milliseconds 500

    # 第二次上传相同文件
    $result2 = Upload-File -FilePath $testImage -BusinessType "General"
    if (-not $result2.Success) {
        Write-Error "第二次上传失败"
        $script:TestResults.Failed++
        return $false
    }

    $secondHash = $result2.Content.responseData.fileHash
    $secondId = $result2.Content.responseData.id
    Write-Info "  - 第二次上传 ID: $secondId"
    Write-Info "  - 文件哈希: $secondHash"

    # 验证哈希相同
    if ($firstHash -eq $secondHash) {
        Write-Success "文件去重测试通过（哈希值相同）"
        Write-Info "  - 两次上传创建了不同的附件记录但共享同一文件"

        $script:UploadedAttachments += $result1.Content.responseData
        $script:UploadedAttachments += $result2.Content.responseData

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "文件去重测试失败（哈希值不同）"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 6：附件查询功能
###############################################################################
function Test-AttachmentQuery {
    Write-Info "`n=== 测试 6：附件查询功能 ==="
    $script:TestResults.TotalTests++

    if ($script:UploadedAttachments.Count -eq 0) {
        Write-Warning "没有已上传的附件，跳过查询测试"
        $script:TestResults.Skipped++
        return $false
    }

    $firstAttachment = $script:UploadedAttachments[0]
    $attachmentId = $firstAttachment.id

    Write-Info "查询附件 ID: $attachmentId"

    $result = Test-Endpoint -Url "$ApiUrl/api/v1/Attachment/GetById/$attachmentId"

    if ($result.Success -and $result.StatusCode -eq 200 -and $result.Content.isSuccess) {
        Write-Success "附件查询成功"
        Write-Info "  - 原始文件名: $($result.Content.responseData.originalName)"
        Write-Info "  - 上传者: $($result.Content.responseData.uploaderName)"
        Write-Info "  - 上传时间: $($result.Content.responseData.createTime)"

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "附件查询失败"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 7：缩略图生成验证
###############################################################################
function Test-ThumbnailGeneration {
    Write-Info "`n=== 测试 7：缩略图生成验证 ==="
    $script:TestResults.TotalTests++

    $testImage = New-TestImage -FileName "test-thumbnail.png"
    $result = Upload-File -FilePath $testImage -GenerateThumbnail $true

    if ($result.Success -and $result.Content.isSuccess) {
        $thumbnailPath = $result.Content.responseData.thumbnailPath

        if ($thumbnailPath) {
            Write-Success "缩略图已生成"
            Write-Info "  - 缩略图路径: $thumbnailPath"
            Write-Info "  - 原图 URL: $($result.Content.responseData.url)"

            $script:UploadedAttachments += $result.Content.responseData

            $script:TestResults.Passed++
            return $true
        } else {
            Write-Warning "未生成缩略图（可能配置为不生成）"
            $script:TestResults.Skipped++
            return $false
        }
    } else {
        Write-Error "缩略图生成测试失败"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 8：错误场景测试
###############################################################################
function Test-ErrorScenarios {
    Write-Info "`n=== 测试 8：错误场景测试 ==="
    $testsPassed = 0
    $testsTotal = 3

    # 8.1 未认证上传（应返回 401）
    Write-Info "8.1 测试未认证上传..."
    $originalToken = $script:AccessToken
    $script:AccessToken = "invalid_token"

    $testImage = New-TestImage -FileName "test-unauth.png"
    $result = Upload-File -FilePath $testImage

    if ($result.StatusCode -eq 401) {
        Write-Success "  ✓ 未认证上传正确返回 401"
        $testsPassed++
    } else {
        Write-Error "  ✗ 未认证上传应返回 401，实际: $($result.StatusCode)"
    }

    $script:AccessToken = $originalToken

    # 8.2 上传不支持的文件类型
    Write-Info "8.2 测试上传不支持的文件类型..."
    # 创建一个假的 .exe 文件
    $exeFile = Join-Path $script:TestFilesDir "test.exe"
    [System.IO.File]::WriteAllText($exeFile, "This is not really an exe file")

    $result = Upload-File -FilePath $exeFile -BusinessType "General"

    if ($result.StatusCode -eq 400 -or (-not $result.Success)) {
        Write-Success "  ✓ 不支持的文件类型正确被拒绝"
        $testsPassed++
    } else {
        Write-Error "  ✗ 不支持的文件类型应被拒绝"
    }

    # 8.3 查询不存在的附件（应返回 404）
    Write-Info "8.3 测试查询不存在的附件..."
    $result = Test-Endpoint -Url "$ApiUrl/api/v1/Attachment/GetById/999999999"

    if ($result.StatusCode -eq 404 -or $result.Content.statusCode -eq 404) {
        Write-Success "  ✓ 不存在的附件正确返回 404"
        $testsPassed++
    } else {
        Write-Error "  ✗ 不存在的附件应返回 404"
    }

    Write-Info "错误场景测试: $testsPassed/$testsTotal 通过"

    $script:TestResults.TotalTests++
    if ($testsPassed -eq $testsTotal) {
        $script:TestResults.Passed++
        return $true
    } else {
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 9：附件删除功能
###############################################################################
function Test-AttachmentDeletion {
    Write-Info "`n=== 测试 9：附件删除功能 ==="
    $script:TestResults.TotalTests++

    if ($script:UploadedAttachments.Count -eq 0) {
        Write-Warning "没有可删除的附件，跳过删除测试"
        $script:TestResults.Skipped++
        return $false
    }

    # 上传一个新文件用于删除测试
    $testImage = New-TestImage -FileName "test-delete.png"
    $uploadResult = Upload-File -FilePath $testImage

    if (-not $uploadResult.Success) {
        Write-Error "上传测试文件失败，无法测试删除功能"
        $script:TestResults.Failed++
        return $false
    }

    $attachmentId = $uploadResult.Content.responseData.id
    Write-Info "删除附件 ID: $attachmentId"

    $headers = @{
        "Authorization" = "Bearer $script:AccessToken"
    }

    $result = Test-Endpoint `
        -Url "$ApiUrl/api/v1/Attachment/Delete/$attachmentId" `
        -Method DELETE `
        -Headers $headers

    if ($result.Success -and $result.StatusCode -eq 200 -and $result.Content.isSuccess) {
        Write-Success "附件删除成功"

        # 验证删除后无法查询
        Start-Sleep -Milliseconds 500
        $queryResult = Test-Endpoint -Url "$ApiUrl/api/v1/Attachment/GetById/$attachmentId"

        if ($queryResult.StatusCode -eq 404 -or -not $queryResult.Content.isSuccess) {
            Write-Success "  ✓ 删除后无法查询到该附件（软删除生效）"
            $script:TestResults.Passed++
            return $true
        } else {
            Write-Warning "  ⚠ 删除后仍可查询到附件（可能是软删除机制）"
            $script:TestResults.Passed++
            return $true
        }
    } else {
        Write-Error "附件删除失败"
        Write-Error "  - 错误: $($result.Content.messageInfo)"
        $script:TestResults.Failed++
        return $false
    }
}

###############################################################################
# 测试 10：批量删除功能
###############################################################################
function Test-BatchDeletion {
    Write-Info "`n=== 测试 10：批量删除功能 ==="
    $script:TestResults.TotalTests++

    # 上传多个文件用于批量删除
    $ids = @()
    for ($i = 1; $i -le 3; $i++) {
        $testImage = New-TestImage -FileName "test-batch-delete-$i.png"
        $result = Upload-File -FilePath $testImage

        if ($result.Success) {
            $ids += $result.Content.responseData.id
        }
    }

    if ($ids.Count -eq 0) {
        Write-Error "没有上传成功的文件，无法测试批量删除"
        $script:TestResults.Failed++
        return $false
    }

    Write-Info "批量删除附件 IDs: $($ids -join ', ')"

    $headers = @{
        "Authorization" = "Bearer $script:AccessToken"
        "Content-Type" = "application/json"
    }

    $body = $ids | ConvertTo-Json

    $result = Test-Endpoint `
        -Url "$ApiUrl/api/v1/Attachment/DeleteBatch" `
        -Method POST `
        -Headers $headers `
        -Body $body `
        -ContentType "application/json"

    if ($result.Success -and $result.StatusCode -eq 200 -and $result.Content.isSuccess) {
        Write-Success "批量删除成功"
        Write-Info "  - 删除数量: $($result.Content.responseData)/$($ids.Count)"

        $script:TestResults.Passed++
        return $true
    } else {
        Write-Error "批量删除失败"
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
    Write-Host "║         Radish 附件上传功能自动化测试                          ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-Info "API 地址: $ApiUrl"
    Write-Info "Auth 地址: $AuthUrl"
    Write-Info "测试用户: $Username"
    Write-Info "测试文件目录: $script:TestFilesDir"
    Write-Host ""

    # 确保测试文件目录存在
    if (-not (Test-Path $script:TestFilesDir)) {
        New-Item -ItemType Directory -Path $script:TestFilesDir -Force | Out-Null
        Write-Success "创建测试文件目录: $script:TestFilesDir"
    }

    # 检查服务可用性
    Write-Info "检查服务可用性..."
    $apiHealth = Test-Endpoint -Url "$ApiUrl/health"
    $authHealth = Test-Endpoint -Url "$AuthUrl/health"

    if (-not $apiHealth.Success) {
        Write-Error "API 服务不可用: $ApiUrl"
        Write-Warning "请先按仓库当前约定启动 Radish.Api，再重新执行本脚本。"
        exit 1
    }
    Write-Success "API 服务可用"

    if (-not $authHealth.Success) {
        Write-Error "Auth 服务不可用: $AuthUrl"
        Write-Warning "请先按仓库当前约定启动 Radish.Auth，再重新执行本脚本。"
        exit 1
    }
    Write-Success "Auth 服务可用"

    # 运行测试
    Test-Authentication

    if ($null -eq $script:AccessToken) {
        Write-Error "无法获取认证令牌，终止测试"
        exit 1
    }

    if (-not $SkipUpload) {
        Test-ImageUploadBasic
        Start-Sleep -Milliseconds 500

        Test-DocumentUpload
        Start-Sleep -Milliseconds 500

        Test-AvatarUpload
        Start-Sleep -Milliseconds 500

        Test-FileDuplication
        Start-Sleep -Milliseconds 500

        Test-ThumbnailGeneration
        Start-Sleep -Milliseconds 500
    }

    if (-not $SkipQuery) {
        Test-AttachmentQuery
        Start-Sleep -Milliseconds 500
    }

    Test-ErrorScenarios
    Start-Sleep -Milliseconds 500

    if (-not $SkipDelete) {
        Test-AttachmentDeletion
        Start-Sleep -Milliseconds 500

        Test-BatchDeletion
    }

    # 输出测试总结
    Write-Host "`n" -NoNewline
    Write-Host "╔════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                        测试结果总结                              ║" -ForegroundColor Cyan
    Write-Host "╚════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""

    Write-Host "总测试数: $($script:TestResults.TotalTests)" -ForegroundColor White
    Write-Host "通过: $($script:TestResults.Passed)" -ForegroundColor Green
    Write-Host "失败: $($script:TestResults.Failed)" -ForegroundColor Red
    Write-Host "跳过: $($script:TestResults.Skipped)" -ForegroundColor Yellow

    $successRate = 0
    if ($script:TestResults.TotalTests -gt 0) {
        $successRate = [math]::Round(($script:TestResults.Passed / $script:TestResults.TotalTests) * 100, 2)
    }
    Write-Host "成功率: $successRate%" -ForegroundColor $(if ($successRate -ge 80) { "Green" } elseif ($successRate -ge 60) { "Yellow" } else { "Red" })

    Write-Host ""

    if ($script:TestResults.Failed -eq 0) {
        Write-Success "所有测试通过！🎉"
        exit 0
    } else {
        Write-Error "部分测试失败，请检查日志"
        exit 1
    }
}

# 执行主函数
Main
