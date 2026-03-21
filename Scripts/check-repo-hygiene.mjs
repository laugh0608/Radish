import { readFileSync } from 'node:fs';
import path from 'node:path';
import process from 'node:process';

const decoder = new TextDecoder('utf-8', { fatal: true });
const repoRoot = process.cwd();
const args = new Set(process.argv.slice(2));

const binaryExtensions = new Set([
  '.7z',
  '.bmp',
  '.db',
  '.dll',
  '.dylib',
  '.eot',
  '.exe',
  '.gif',
  '.gz',
  '.ico',
  '.jar',
  '.jpeg',
  '.jpg',
  '.pdb',
  '.pdf',
  '.pfx',
  '.png',
  '.snk',
  '.so',
  '.sqlite',
  '.tar',
  '.tgz',
  '.ttf',
  '.woff',
  '.woff2',
  '.zip'
]);

const explicitTextExtensions = new Set([
  '.bat',
  '.cmd',
  '.config',
  '.cs',
  '.cshtml',
  '.csproj',
  '.css',
  '.editorconfig',
  '.env',
  '.example',
  '.gitignore',
  '.gitattributes',
  '.html',
  '.http',
  '.ini',
  '.js',
  '.json',
  '.jsonc',
  '.jsx',
  '.md',
  '.mjs',
  '.props',
  '.ps1',
  '.psd1',
  '.psm1',
  '.resx',
  '.sh',
  '.sln',
  '.slnx',
  '.sql',
  '.targets',
  '.toml',
  '.ts',
  '.tsx',
  '.txt',
  '.xml',
  '.yaml',
  '.yml'
]);

const explicitTextBasenames = new Set([
  '.editorconfig',
  '.gitattributes',
  '.gitignore',
  'AGENTS.md',
  'CLAUDE.md',
  'COPYRIGHT',
  'Dockerfile',
  'LICENSE',
  'NuGet.Config',
  'README',
  'README.md'
]);

const crlfExtensions = new Set([
  '.bat',
  '.cmd',
  '.ps1',
  '.psd1',
  '.psm1',
  '.sln',
  '.slnx'
]);

const markdownExtensions = new Set(['.md']);
const documentationExtensions = new Set(['.cshtml', '.html', '.md', '.txt']);
const suspiciousMojibakePatterns = [
  /ï»¿/u,
  /锟斤拷/u,
  /Ã[\u0080-\u00ff]/u,
  /Â[\u0000-\u007f]/u,
  /â[\u0080-\u00ff]/u,
  /ð[\u0080-\u00ff]/u
];

function getCandidateFiles() {
  const fileArgs = process.argv.slice(2).filter((arg) => !arg.startsWith('--'));

  if (fileArgs.length > 0) {
    return fileArgs;
  }

  const stdin = process.stdin.isTTY
    ? ''
    : readFileSync(process.stdin.fd, args.has('--stdin-z') ? null : 'utf8');

  const chunks = args.has('--stdin-z')
    ? stdin.toString('utf8').split('\0')
    : stdin.toString().split(/\r?\n/u);

  return chunks
    .map((file) => file.trim())
    .filter(Boolean)
    .filter((file) => !file.startsWith('node_modules/'))
    .filter((file) => !file.startsWith('.nuget/'));
}

function hasBinaryControlBytes(buffer) {
  const sampleLength = Math.min(buffer.length, 8192);

  for (let index = 0; index < sampleLength; index += 1) {
    const byte = buffer[index];
    if (byte === 0) {
      return true;
    }

    const isAllowedControl =
      byte === 9 || byte === 10 || byte === 13 || byte === 12;

    if (byte < 32 && !isAllowedControl) {
      return true;
    }
  }

  return false;
}

function isTextFile(filePath, buffer) {
  const normalizedPath = filePath.replace(/\\/g, '/');
  const ext = path.extname(filePath).toLowerCase();
  const baseName = path.basename(filePath);

  if (binaryExtensions.has(ext)) {
    return false;
  }

  if (explicitTextExtensions.has(ext) || explicitTextBasenames.has(baseName)) {
    return true;
  }

  if (normalizedPath.startsWith('.github/') || normalizedPath.startsWith('.githooks/')) {
    return true;
  }

  return !hasBinaryControlBytes(buffer);
}

function expectedLineEnding(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  return crlfExtensions.has(ext) ? 'crlf' : 'lf';
}

function hasLoneLf(text) {
  return /(^|[^\r])\n/u.test(text);
}

function hasCrlf(text) {
  return /\r\n/u.test(text);
}

function hasTrailingWhitespace(text) {
  return /[ \t]+$/mu.test(text);
}

function isDocumentationFile(filePath) {
  const normalizedPath = filePath.replace(/\\/g, '/');
  const ext = path.extname(filePath).toLowerCase();

  return normalizedPath.startsWith('Docs/') || documentationExtensions.has(ext);
}

function hasSuspiciousMojibake(text) {
  return suspiciousMojibakePatterns.some((pattern) => pattern.test(text));
}

function validateFile(filePath) {
  const absolutePath = path.join(repoRoot, filePath);
  const buffer = readFileSync(absolutePath);

  if (!isTextFile(filePath, buffer)) {
    return [];
  }

  const issues = [];

  if (buffer.length >= 3 && buffer[0] === 0xef && buffer[1] === 0xbb && buffer[2] === 0xbf) {
    issues.push('包含 UTF-8 BOM，请改为无 BOM UTF-8。');
  }

  let content = '';

  try {
    content = decoder.decode(buffer);
  } catch (error) {
    issues.push(`不是合法的 UTF-8 文本：${error instanceof Error ? error.message : String(error)}`);
    return issues;
  }

  if (content.includes('\uFFFD')) {
    issues.push('包含 Unicode 替换字符（U+FFFD），疑似已有乱码。');
  }

  if (isDocumentationFile(filePath) && hasSuspiciousMojibake(content)) {
    issues.push('检测到疑似乱码/编码错乱片段，请人工确认文档内容。');
  }

  const eol = expectedLineEnding(filePath);

  if (eol === 'lf' && hasCrlf(content)) {
    issues.push('应使用 LF 换行，但检测到 CRLF。');
  }

  if (eol === 'crlf' && hasLoneLf(content)) {
    issues.push('应使用 CRLF 换行，但检测到 LF。');
  }

  if (content.length > 0 && !content.endsWith('\n')) {
    issues.push('文件末尾缺少换行。');
  }

  const ext = path.extname(filePath).toLowerCase();
  if (!markdownExtensions.has(ext) && hasTrailingWhitespace(content)) {
    issues.push('存在行尾空格或制表符。');
  }

  return issues;
}

function main() {
  const files = getCandidateFiles();

  if (files.length === 0) {
    console.log('[repo-hygiene] 没有需要检查的文件。');
    return;
  }

  const failures = [];

  for (const file of files) {
    const issues = validateFile(file);
    if (issues.length > 0) {
      failures.push({ file, issues });
    }
  }

  if (failures.length === 0) {
    console.log(`[repo-hygiene] 已检查 ${files.length} 个文件，未发现文本卫生问题。`);
    return;
  }

  console.error(`[repo-hygiene] 发现 ${failures.length} 个文件存在问题：`);
  for (const failure of failures) {
    console.error(`- ${failure.file}`);
    for (const issue of failure.issues) {
      console.error(`  - ${issue}`);
    }
  }

  process.exit(1);
}

main();
