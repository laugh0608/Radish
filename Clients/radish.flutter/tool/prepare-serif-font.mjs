import { createHash } from 'node:crypto';
import { mkdir, mkdtemp, readFile, rename, rm, stat, writeFile } from 'node:fs/promises';
import http from 'node:http';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const filename = 'NotoSerifSC-VF.ttf';
const sourceUrl = 'https://raw.githubusercontent.com/notofonts/noto-cjk/9b0f1436e455d902de067a2501422e5dc71ad16b/Serif/Variable/TTF/Subset/NotoSerifSC-VF.ttf';
const expectedBytes = 25_125_232;
const expectedSha256 = '5326cfb097e3ab26fcb39329752b5c0a439bf8d5c4649520e4b492939c352a09';
const output = fileURLToPath(new URL(`../assets/fonts/noto/generated/${filename}`, import.meta.url));
const usage = 'node tool/prepare-serif-font.mjs [--verify | --from <file>]';

function matchesFont(bytes) {
  return bytes.length === expectedBytes && createHash('sha256').update(bytes).digest('hex') === expectedSha256;
}

async function hasValidFont() {
  try {
    const metadata = await stat(output);
    return metadata.isFile() && metadata.size === expectedBytes && matchesFont(await readFile(output));
  } catch (error) {
    if (error.code === 'ENOENT') return false;
    throw error;
  }
}

async function downloadFont() {
  http.setGlobalProxyFromEnv?.();
  const response = await fetch(sourceUrl, { redirect: 'error', signal: AbortSignal.timeout(90_000) });
  if (!response.ok || !response.body) throw new Error(`上游字体下载失败（HTTP ${response.status}）。`);

  const bytes = Buffer.alloc(expectedBytes);
  let offset = 0;
  for await (const chunk of response.body) {
    if (offset + chunk.length > expectedBytes) throw new Error('上游字体超出固定字节数。');
    bytes.set(chunk, offset);
    offset += chunk.length;
  }
  return bytes.subarray(0, offset);
}

async function prepare(args) {
  const [option, localFont] = args;
  if (option === '--verify' && args.length === 1) {
    if (!(await hasValidFont())) throw new Error(`缓存缺失或校验失败。请先运行 ${usage}。`);
    console.log(`已验证 ${filename}。`);
    return;
  }

  if (args.length !== 0 && (option !== '--from' || args.length !== 2 || !localFont)) {
    throw new Error(`用法为 ${usage}。`);
  }

  if (!localFont && (await hasValidFont())) {
    console.log(`已复用校验通过的 ${filename}。`);
    return;
  }

  const bytes = localFont ? await readFile(localFont) : await downloadFont();
  if (!matchesFont(bytes)) {
    throw new Error(`字体校验失败，需要 ${expectedBytes} 字节且 SHA-256 为 ${expectedSha256}。`);
  }

  const destination = dirname(output);
  await mkdir(destination, { recursive: true });
  const staging = await mkdtemp(join(destination, '.prepare-'));
  try {
    const stagedFont = join(staging, filename);
    await writeFile(stagedFont, bytes);
    await rename(stagedFont, output);
  } finally {
    await rm(staging, { recursive: true, force: true });
  }
  console.log(`已准备并验证 ${filename}。`);
}

try {
  await prepare(process.argv.slice(2));
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  if (error instanceof Error && error.cause instanceof Error) console.error(error.cause.message);
  process.exitCode = 1;
}
