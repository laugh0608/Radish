// 同一 L1 隔离运行入口；原始传输复现只在显式参数下执行。
if (process.argv.includes('--raw-transport')) await import('./collector-transport-probe.mjs');
else await import('./collector-boundary-probe.mjs');
