import fs from 'node:fs';
import path from 'node:path';
import process from 'node:process';
import { pathToFileURL } from 'node:url';

const args = new Set(process.argv.slice(2));
const repoRoot = process.cwd();
const localDocumentBasenames = new Set([
  'AGENTS.md',
  'CLAUDE.md',
  'CODE_OF_CONDUCT.md',
  'CONTRIBUTING.md',
  'LICENSE',
  'README.md',
  'SECURITY.md',
]);

async function readStdin() {
  const chunks = [];
  for await (const chunk of process.stdin) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }
  return Buffer.concat(chunks);
}

async function getCandidateFiles() {
  const fileArgs = process.argv.slice(2).filter((arg) => !arg.startsWith('--'));
  if (fileArgs.length > 0) {
    return fileArgs;
  }

  const stdin = process.stdin.isTTY ? Buffer.alloc(0) : await readStdin();
  const files = args.has('--stdin-z')
    ? stdin.toString('utf8').split('\0')
    : stdin.toString('utf8').split(/\r?\n/u);

  return files.map((file) => file.trim()).filter(Boolean);
}

function normalizeDestination(rawDestination) {
  let destination = rawDestination.trim();
  if (destination.startsWith('<') && destination.endsWith('>')) {
    destination = destination.slice(1, -1).trim();
  }

  destination = destination.replace(/\\([\\()])/gu, '$1');
  const suffixIndex = destination.search(/[?#]/u);
  if (suffixIndex >= 0) {
    destination = destination.slice(0, suffixIndex);
  }

  try {
    return decodeURIComponent(destination);
  } catch {
    return destination;
  }
}

function isCheckableLocalPath(destination) {
  if (
    destination.length === 0 ||
    destination.startsWith('#') ||
    destination.startsWith('/') ||
    destination.startsWith('\\') ||
    /^[A-Za-z][A-Za-z0-9+.-]*:/u.test(destination)
  ) {
    return false;
  }

  return (
    destination.startsWith('./') ||
    destination.startsWith('../') ||
    destination.startsWith('Docs/') ||
    destination.endsWith('/') ||
    path.extname(destination).length > 0 ||
    localDocumentBasenames.has(destination)
  );
}

function collectLineDestinations(line) {
  const destinations = [];
  const inlineLinkPattern = /!?\[[^\]]*\]\(\s*(<[^>\n]+>|[^\s)\n]+)(?:\s+[^)]*)?\)/gu;
  const referenceLinkPattern = /^\s{0,3}\[[^\]]+\]:\s*(<[^>\n]+>|\S+)/u;

  for (const match of line.matchAll(inlineLinkPattern)) {
    destinations.push(match[1]);
  }

  const referenceMatch = line.match(referenceLinkPattern);
  if (referenceMatch) {
    destinations.push(referenceMatch[1]);
  }

  return destinations;
}

export function findBrokenMarkdownLinks({ rootDir, filePaths }) {
  const failures = [];
  let checkedLinkCount = 0;
  const uniqueFilePaths = [...new Set(filePaths)].sort();

  for (const filePath of uniqueFilePaths) {
    const normalizedFilePath = filePath.replace(/\\/gu, '/');
    if (path.extname(normalizedFilePath).toLowerCase() !== '.md') {
      continue;
    }

    const sourcePath = path.resolve(rootDir, normalizedFilePath);
    if (!fs.existsSync(sourcePath)) {
      failures.push(`${normalizedFilePath}: 文件不存在，无法检查链接。`);
      continue;
    }

    const lines = fs.readFileSync(sourcePath, 'utf8').split(/\r?\n/u);
    let activeFence = null;

    for (const [lineIndex, line] of lines.entries()) {
      const fenceMatch = line.match(/^\s*(`{3,}|~{3,})/u);
      if (fenceMatch) {
        const fenceCharacter = fenceMatch[1][0];
        activeFence = activeFence === null
          ? fenceCharacter
          : activeFence === fenceCharacter
            ? null
            : activeFence;
        continue;
      }

      if (activeFence !== null) {
        continue;
      }

      for (const rawDestination of collectLineDestinations(line)) {
        const destination = normalizeDestination(rawDestination);
        if (!isCheckableLocalPath(destination)) {
          continue;
        }

        checkedLinkCount += 1;
        const targetPath = path.resolve(path.dirname(sourcePath), destination);
        const relativeToRoot = path.relative(rootDir, targetPath);
        const escapesRepository = relativeToRoot === '..' || relativeToRoot.startsWith(`..${path.sep}`);

        if (escapesRepository) {
          failures.push(`${normalizedFilePath}:${lineIndex + 1}: 链接越出仓库边界：${rawDestination}`);
          continue;
        }

        if (!fs.existsSync(targetPath)) {
          failures.push(`${normalizedFilePath}:${lineIndex + 1}: 本地链接目标不存在：${rawDestination}`);
        }
      }
    }
  }

  return {
    checkedFileCount: uniqueFilePaths.filter((filePath) => path.extname(filePath).toLowerCase() === '.md').length,
    checkedLinkCount,
    failures,
  };
}

async function main() {
  const filePaths = await getCandidateFiles();
  const result = findBrokenMarkdownLinks({ rootDir: repoRoot, filePaths });

  if (result.failures.length > 0) {
    console.error('[markdown-links] 检查失败。');
    for (const failure of result.failures) {
      console.error(`- ${failure}`);
    }
    process.exit(1);
  }

  console.log(
    `[markdown-links] 已检查 ${result.checkedFileCount} 个 Markdown 文件、${result.checkedLinkCount} 个本地相对链接，未发现无效目标。`
  );
}

const isMainModule = process.argv[1]
  ? pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url
  : false;

if (isMainModule) {
  await main();
}
