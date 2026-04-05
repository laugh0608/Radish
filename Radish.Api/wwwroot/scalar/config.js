// wwwroot/scalar/config.js
const AUTO_LOGIN_QUERY_KEY = 'auto';
const AUTO_LOGIN_QUERY_VALUE = '1';
const AUTO_LOGIN_MAX_ATTEMPTS = 40;
const AUTO_LOGIN_RETRY_DELAY_MS = 250;

function shouldTriggerAutoLogin() {
    if (typeof window === 'undefined') {
        return false;
    }

    const currentUrl = new URL(window.location.href);
    return currentUrl.searchParams.get(AUTO_LOGIN_QUERY_KEY) === AUTO_LOGIN_QUERY_VALUE;
}

function clearAutoLoginFlag() {
    const currentUrl = new URL(window.location.href);
    currentUrl.searchParams.delete(AUTO_LOGIN_QUERY_KEY);
    window.history.replaceState({}, document.title, currentUrl.toString());
}

function isVisible(element) {
    return Boolean(element) && !element.disabled && element.offsetParent !== null;
}

function findAuthorizeButton() {
    const selectors = [
        'button[aria-label*="Authorize" i]',
        'button[title*="Authorize" i]',
        'button[data-testid*="authorize" i]',
        'button[id*="authorize" i]',
        '[role="button"][aria-label*="Authorize" i]',
    ];

    for (const selector of selectors) {
        const candidate = document.querySelector(selector);
        if (candidate instanceof HTMLElement && isVisible(candidate)) {
            return candidate;
        }
    }

    const textCandidates = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const candidate of textCandidates) {
        if (!(candidate instanceof HTMLElement) || !isVisible(candidate)) {
            continue;
        }

        const label = candidate.textContent?.trim().toLowerCase() ?? '';
        if (label === 'authorize' || label === 'login' || label === '登录') {
            return candidate;
        }
    }

    return null;
}

function triggerScalarAuthorize(attempt = 0) {
    const authorizeButton = findAuthorizeButton();
    if (authorizeButton) {
        clearAutoLoginFlag();
        authorizeButton.click();
        return;
    }

    if (attempt >= AUTO_LOGIN_MAX_ATTEMPTS) {
        clearAutoLoginFlag();
        return;
    }

    window.setTimeout(() => triggerScalarAuthorize(attempt + 1), AUTO_LOGIN_RETRY_DELAY_MS);
}

if (shouldTriggerAutoLogin()) {
    window.setTimeout(() => triggerScalarAuthorize(), 0);
}

export default {
    // 自定义接口锚点生成规则，确保多语言标题也能稳定跳转
    generateOperationSlug: (operation) => `custom-${operation.method.toLowerCase()}${operation.path}`,
}
