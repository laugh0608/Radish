#!/bin/bash
###############################################################################
# 速率限制中间件自动化测试脚本（Bash）
# 用途：自动测试 Radish.Api 和 Radish.Auth 的速率限制功能
###############################################################################

# 默认配置
API_URL="${API_URL:-http://localhost:5100}"
AUTH_URL="${AUTH_URL:-http://localhost:5200}"
SKIP_GLOBAL=false
SKIP_LOGIN=false
VERBOSE=false

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# 测试结果统计
PASSED=0
FAILED=0
SKIPPED=0

# 输出函数
print_success() { echo -e "${GREEN}✓ $1${NC}"; }
print_error() { echo -e "${RED}✗ $1${NC}"; }
print_info() { echo -e "${CYAN}ℹ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠ $1${NC}"; }

# 帮助信息
show_help() {
    cat << EOF
用法: $0 [选项]

选项:
    --api-url URL       API 服务地址（默认: http://localhost:5100）
    --auth-url URL      Auth 服务地址（默认: http://localhost:5200）
    --skip-global       跳过全局限流测试
    --skip-login        跳过登录限流测试
    --verbose           显示详细输出
    -h, --help          显示帮助信息

示例:
    $0
    $0 --api-url http://localhost:5100 --verbose
    $0 --skip-global --skip-login

EOF
}

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        --api-url)
            API_URL="$2"
            shift 2
            ;;
        --auth-url)
            AUTH_URL="$2"
            shift 2
            ;;
        --skip-global)
            SKIP_GLOBAL=true
            shift
            ;;
        --skip-login)
            SKIP_LOGIN=true
            shift
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            echo "未知选项: $1"
            show_help
            exit 1
            ;;
    esac
done

# 测试端点函数
test_endpoint() {
    local url="$1"
    local method="${2:-GET}"
    local data="${3:-}"

    if [ -n "$data" ]; then
        response=$(curl -s -w "\n%{http_code}" -X "$method" "$url" \
            -H "Content-Type: application/x-www-form-urlencoded" \
            -d "$data" 2>/dev/null)
    else
        response=$(curl -s -w "\n%{http_code}" -X "$method" "$url" 2>/dev/null)
    fi

    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | sed '$d')

    echo "$http_code"
}

###############################################################################
# 测试 1：全局限流（Fixed Window）
###############################################################################
test_global_rate_limit() {
    print_info "\n=== 测试 1：全局限流（Fixed Window 算法） ==="
    print_info "配置：每个 IP 每分钟最多 200 个请求"
    print_info "预期：第 201 个请求返回 429"

    local endpoint="$API_URL/health"
    local limit=200
    local test_count=$((limit + 5))
    local rejected_at=0

    print_info "发送 $test_count 个请求到 $endpoint ..."

    for ((i=1; i<=test_count; i++)); do
        status=$(test_endpoint "$endpoint")

        if [ "$VERBOSE" = true ]; then
            echo "  请求 $i : $status"
        fi

        if [ "$status" -eq 429 ]; then
            rejected_at=$i
            print_success "在第 $i 个请求时触发限流（预期：第 $((limit + 1)) 个）"
            ((PASSED++))
            return 0
        fi

        # 避免过快发送请求
        sleep 0.01
    done

    if [ $rejected_at -eq 0 ]; then
        print_error "未触发全局限流（发送了 $test_count 个请求）"
        print_warning "可能原因：1) 限流未启用 2) 限流配置过高 3) IP 在白名单中"
        ((FAILED++))
        return 1
    fi

    return 0
}

###############################################################################
# 测试 2：登录限流（Sliding Window）
###############################################################################
test_login_rate_limit() {
    print_info "\n=== 测试 2：登录限流（Sliding Window 算法） ==="
    print_info "配置：每个 IP 15 分钟最多 10 次登录尝试"
    print_info "预期：第 11 次登录请求返回 429"

    local endpoint="$AUTH_URL/Account/Login"
    local limit=10
    local test_count=$((limit + 2))
    local rejected_at=0

    local login_data="username=test_rate_limit_user&password=wrong_password_for_testing"

    print_info "发送 $test_count 次登录请求到 $endpoint ..."
    print_warning "注意：这些是故意失败的登录尝试（用于测试限流）"

    for ((i=1; i<=test_count; i++)); do
        status=$(test_endpoint "$endpoint" "POST" "$login_data")

        if [ "$VERBOSE" = true ]; then
            echo "  登录尝试 $i : $status"
        fi

        if [ "$status" -eq 429 ]; then
            rejected_at=$i
            print_success "在第 $i 次登录尝试时触发限流（预期：第 $((limit + 1)) 次）"
            ((PASSED++))
            return 0
        fi

        # 登录请求间隔稍长一些
        sleep 0.1
    done

    if [ $rejected_at -eq 0 ]; then
        print_error "未触发登录限流（发送了 $test_count 次登录请求）"
        print_warning "可能原因：1) 登录限流未启用 2) 限流配置过高 3) IP 在白名单中"
        ((FAILED++))
        return 1
    fi

    return 0
}

###############################################################################
# 测试 3：健康检查端点（应该不受限流影响）
###############################################################################
test_health_check_no_limit() {
    print_info "\n=== 测试 3：健康检查端点（验证 DisableRateLimiting） ==="
    print_info "预期：健康检查端点不受限流限制"

    local endpoint="$API_URL/health"
    local test_count=10

    print_info "快速发送 $test_count 个请求到 $endpoint ..."

    local all_success=true
    for ((i=1; i<=test_count; i++)); do
        status=$(test_endpoint "$endpoint")

        if [ "$status" -ne 200 ]; then
            print_error "请求 $i 失败: $status"
            all_success=false
        fi
    done

    if [ "$all_success" = true ]; then
        print_success "健康检查端点正常工作，未触发限流"
        ((PASSED++))
        return 0
    else
        print_error "健康检查端点测试失败"
        ((FAILED++))
        return 1
    fi
}

###############################################################################
# 测试 4：验证 429 响应格式
###############################################################################
test_rate_limit_response_format() {
    print_info "\n=== 测试 4：验证 429 响应格式 ==="

    local endpoint="$API_URL/health"
    print_info "触发限流以验证响应格式..."

    for ((i=1; i<=210; i++)); do
        response=$(curl -s -w "\n%{http_code}" "$endpoint" 2>/dev/null)
        status=$(echo "$response" | tail -n1)
        body=$(echo "$response" | sed '$d')

        if [ "$status" -eq 429 ]; then
            print_success "成功触发 429 响应"
            print_info "响应体: $body"

            # 验证响应格式（简单检查）
            if echo "$body" | grep -q '"status":429' && \
               echo "$body" | grep -q '"message"' && \
               echo "$body" | grep -q '"success":false'; then
                print_success "429 响应格式正确"
                ((PASSED++))
                return 0
            else
                print_error "429 响应格式不正确"
                ((FAILED++))
                return 1
            fi
        fi

        sleep 0.01
    done

    print_warning "未能触发 429 响应以验证格式"
    ((SKIPPED++))
    return 1
}

###############################################################################
# 测试 5：并发请求测试
###############################################################################
test_concurrent_requests() {
    print_info "\n=== 测试 5：并发请求测试 ==="
    print_info "配置：每个 IP 最多 100 个并发请求"
    print_info "发送 50 个并发请求（应该全部成功）..."

    local endpoint="$API_URL/health"
    local concurrent_count=50

    # 创建临时文件存储结果
    local temp_file=$(mktemp)

    # 并发发送请求
    for ((i=1; i<=concurrent_count; i++)); do
        (
            status=$(test_endpoint "$endpoint")
            echo "$status" >> "$temp_file"
        ) &
    done

    # 等待所有后台任务完成
    wait

    # 统计结果
    local success_count=$(grep -c "^200$" "$temp_file" 2>/dev/null || echo 0)
    local fail_count=$((concurrent_count - success_count))

    rm -f "$temp_file"

    print_info "成功: $success_count / $concurrent_count"

    if [ $success_count -eq $concurrent_count ]; then
        print_success "并发请求测试通过"
        ((PASSED++))
        return 0
    else
        print_error "并发请求测试失败（$fail_count 个请求失败）"
        ((FAILED++))
        return 1
    fi
}

###############################################################################
# 主测试流程
###############################################################################
main() {
    echo ""
    echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║         Radish 速率限制中间件自动化测试                        ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
    echo ""

    print_info "API 地址: $API_URL"
    print_info "Auth 地址: $AUTH_URL"
    print_info "详细输出: $VERBOSE"
    echo ""

    # 检查 curl 是否可用
    if ! command -v curl &> /dev/null; then
        print_error "curl 未安装，请先安装 curl"
        exit 1
    fi

    # 检查服务是否可用
    print_info "检查服务可用性..."

    api_status=$(test_endpoint "$API_URL/health")
    if [ "$api_status" -ne 200 ]; then
        print_error "API 服务不可用: $API_URL"
        print_warning "请确保 Radish.Api 正在运行"
        exit 1
    fi
    print_success "API 服务可用"

    auth_status=$(test_endpoint "$AUTH_URL/health")
    if [ "$auth_status" -ne 200 ]; then
        print_error "Auth 服务不可用: $AUTH_URL"
        print_warning "请确保 Radish.Auth 正在运行"
        exit 1
    fi
    print_success "Auth 服务可用"

    # 运行测试
    if [ "$SKIP_GLOBAL" = false ]; then
        test_global_rate_limit
        print_info "等待 5 秒后继续..."
        sleep 5
    else
        print_warning "跳过全局限流测试"
        ((SKIPPED++))
    fi

    if [ "$SKIP_LOGIN" = false ]; then
        test_login_rate_limit
        print_info "等待 5 秒后继续..."
        sleep 5
    else
        print_warning "跳过登录限流测试"
        ((SKIPPED++))
    fi

    test_health_check_no_limit
    test_concurrent_requests

    # 输出测试结果
    echo ""
    echo -e "${CYAN}╔════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║                        测试结果汇总                            ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════════╝${NC}"
    echo ""

    print_success "通过: $PASSED"
    print_error "失败: $FAILED"
    print_warning "跳过: $SKIPPED"

    local total=$((PASSED + FAILED + SKIPPED))
    local pass_rate=0
    if [ $total -gt 0 ]; then
        pass_rate=$(awk "BEGIN {printf \"%.2f\", ($PASSED / $total) * 100}")
    fi

    echo ""
    print_info "通过率: ${pass_rate}%"
    echo ""

    if [ $FAILED -gt 0 ]; then
        print_warning "部分测试失败，请检查配置和日志"
        exit 1
    else
        print_success "所有测试通过！"
        exit 0
    fi
}

# 运行主函数
main
