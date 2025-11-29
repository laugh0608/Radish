(function () {
    const mainElement = document.querySelector("main");
    const apiBaseUrl = mainElement?.dataset.apiBaseUrl || "https://localhost:5101";
    const apiHealthPath = mainElement?.dataset.apiHealthPath || "/health";
    const frontendBaseUrl = mainElement?.dataset.frontendBaseUrl || "https://localhost:3000";
    const docsPath = mainElement?.dataset.docsPath || "/docs";
    const consolePath = mainElement?.dataset.consolePath || "/console";

    const originLabel = document.getElementById("current-origin");
    if (originLabel) {
        originLabel.textContent = window.location.origin;
    }

    const statusEl = document.getElementById("health-status");
    const downstreamEl = document.getElementById("downstream-status");
    const frontendStatusEl = document.getElementById("frontend-status");
    const docsStatusEl = document.getElementById("docs-status");
    const consoleStatusEl = document.getElementById("console-status");

    const isHttpUp = (response) => response.ok || (response.status >= 200 && response.status < 400);

    // 检查 Gateway 自身健康状态（以当前门户路径可访问为主）
    if (statusEl) {
        (async () => {
            const candidates = [window.location.pathname || "/", "/healthz", "/health"];
            for (const endpoint of candidates) {
                try {
                    const start = performance.now();
                    const response = await fetch(endpoint, { cache: "no-store" });
                    if (isHttpUp(response)) {
                        const duration = Math.round(performance.now() - start);
                        statusEl.textContent = `Gateway 可用 · ${endpoint} · ${duration} ms`;
                        statusEl.dataset.state = "ok";
                        return;
                    }
                } catch {
                    // 忽略并尝试下一个端点
                }
            }
            statusEl.textContent = "Gateway 健康检查端点未响应或返回异常状态";
            statusEl.dataset.state = "fail";
        })();
    }

    // 检查下游 API 服务状态（仍使用 API /health 作为主健康信号）
    if (downstreamEl && apiBaseUrl) {
        (async () => {
            try {
                const start = performance.now();
                const apiHealthUrl = `${apiBaseUrl}${apiHealthPath}`;
                const response = await fetch(apiHealthUrl, {
                    cache: "no-store",
                    mode: "cors"
                });
                if (response.ok) {
                    const duration = Math.round(performance.now() - start);
                    downstreamEl.textContent = `API 服务可用 · ${duration} ms`;
                    downstreamEl.dataset.state = "ok";
                } else {
                    downstreamEl.textContent = `API 服务响应异常 (${response.status})`;
                    downstreamEl.dataset.state = "fail";
                }
            } catch {
                downstreamEl.textContent = "API 服务未启动或无法访问";
                downstreamEl.dataset.state = "fail";
            }
        })();
    }

    // 检查前端应用状态（通过前端根地址，2xx/3xx 均视为可用）
    if (frontendStatusEl && frontendBaseUrl) {
        (async () => {
            try {
                const start = performance.now();
                const response = await fetch(frontendBaseUrl, {
                    cache: "no-store",
                    mode: "cors"
                });
                if (isHttpUp(response)) {
                    const duration = Math.round(performance.now() - start);
                    frontendStatusEl.textContent = `前端应用可用 · ${duration} ms`;
                    frontendStatusEl.dataset.state = "ok";
                } else {
                    frontendStatusEl.textContent = `前端应用响应异常 (${response.status})`;
                    frontendStatusEl.dataset.state = "fail";
                }
            } catch {
                frontendStatusEl.textContent = "前端应用未启动或无法访问";
                frontendStatusEl.dataset.state = "fail";
            }
        })();
    }

    // 检查 Docs 文档站状态（通过 Gateway /docs，2xx/3xx 视为可用）
    if (docsStatusEl) {
        (async () => {
            try {
                const start = performance.now();
                const docsHealthUrl = docsPath;
                const response = await fetch(docsHealthUrl, { cache: "no-store" });
                if (isHttpUp(response)) {
                    const duration = Math.round(performance.now() - start);
                    docsStatusEl.textContent = `Docs 文档站可用 · ${duration} ms`;
                    docsStatusEl.dataset.state = "ok";
                } else {
                    docsStatusEl.textContent = `Docs 文档站响应异常 (${response.status})`;
                    docsStatusEl.dataset.state = "fail";
                }
            } catch {
                docsStatusEl.textContent = "Docs 文档站未启动或无法访问";
                docsStatusEl.dataset.state = "fail";
            }
        })();
    }

    // 检查 Console 控制台状态（通过 Gateway /console，2xx/3xx 视为可用）
    if (consoleStatusEl) {
        (async () => {
            try {
                const start = performance.now();
                const consoleHealthUrl = consolePath;
                const response = await fetch(consoleHealthUrl, { cache: "no-store" });
                if (isHttpUp(response)) {
                    const duration = Math.round(performance.now() - start);
                    consoleStatusEl.textContent = `Console 控制台可用 · ${duration} ms`;
                    consoleStatusEl.dataset.state = "ok";
                } else {
                    consoleStatusEl.textContent = `Console 控制台响应异常 (${response.status})`;
                    consoleStatusEl.dataset.state = "fail";
                }
            } catch {
                consoleStatusEl.textContent = "Console 控制台未启动或无法访问";
                consoleStatusEl.dataset.state = "fail";
            }
        })();
    }
})();
