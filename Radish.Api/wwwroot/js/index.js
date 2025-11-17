(function () {
    const originLabel = document.getElementById("current-origin");
    originLabel.textContent = window.location.origin;

    const statusEl = document.getElementById("health-status");
    const candidates = ["/healthz", "/health", "/api/health"];

    (async () => {
        for (const endpoint of candidates) {
            try {
                const start = performance.now();
                const response = await fetch(endpoint, { cache: "no-store" });
                if (response.ok) {
                    const duration = Math.round(performance.now() - start);
                    statusEl.textContent = `服务可用 · ${endpoint} · ${duration} ms`;
                    statusEl.dataset.state = "ok";
                    return;
                }
            } catch (error) {
                // 忽略并尝试下一个端点
            }
        }
        statusEl.textContent = "暂未检测到健康检查端点，请确认 API 已启动或在 Program.cs 中注册 HealthChecks。";
        statusEl.dataset.state = "fail";
    })();
})();
