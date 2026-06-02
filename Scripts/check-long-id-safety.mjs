import { readdirSync, readFileSync } from 'node:fs';
import { dirname, extname, join, relative, resolve, sep } from 'node:path';
import process from 'node:process';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(scriptDir, '..');

const scanRoots = [
  'Frontend/radish.client/src',
  'Frontend/radish.console/src',
  'Frontend/radish.http/src',
  'Clients/radish.flutter/lib',
];

const textExtensions = new Set(['.ts', '.tsx', '.dart']);
const longIdNamePattern = '(?:vo)?(?:UserId|PostId|CommentId|ProductId|OrderId|NotificationId|TransactionId|ReportId|TargetContentId|TargetPostId|TargetCommentId|TargetChannelId|TargetMessageId|TargetUserId|ReporterUserId|ActionId|BusinessId|FromUserId|ToUserId|OperatorId|SourceReportId|SourceOrderId|SourceProductId|UserBenefitId|ReplyToCommentId|ChannelId|MessageId|uuid)';
const longIdExpressionPattern = '(?:userId|postId|commentId|productId|orderId|notificationId|transactionId|reportId|targetContentId|targetPostId|targetCommentId|targetUserId|reporterUserId|actionId|businessId|fromUserId|toUserId|operatorId|sourceReportId|sourceOrderId|sourceProductId|userBenefitId|replyToCommentId|channelId|messageId|uuid)';

const tsRules = [
  {
    id: 'ts-long-id-number-type',
    description: '外部 LongId 字段/参数不得声明为纯 number；请使用 string 或 string | number 过渡契约',
    test: (line) => new RegExp(`\\b${longIdNamePattern}\\??\\s*:\\s*number(?:\\s*[;,)=}]|\\s*\\|\\s*(?:null|undefined))`).test(line),
  },
  {
    id: 'ts-long-id-number-conversion',
    description: '外部 LongId 不得提前 Number/parseInt 转换；请保持字符串透传或使用字符串规范化函数',
    test: (line) => new RegExp(`\\b(?:Number|Number\\.parseInt|parseInt)\\s*\\([^\\n)]*${longIdExpressionPattern}`, 'i').test(line),
  },
];

const dartRules = [
  {
    id: 'dart-long-id-int-type',
    description: '外部 LongId 字段/参数不得声明为 int；请使用 String 字符串契约',
    test: (line) => new RegExp(`\\b(?:final\\s+)?int\\??\\s+${longIdExpressionPattern}\\b`, 'i').test(line),
  },
  {
    id: 'dart-long-id-int-conversion',
    description: '外部 LongId 不得提前 int.parse/int.tryParse 转换；请保持字符串透传',
    test: (line) => new RegExp(`\\bint\\.(?:parse|tryParse)\\s*\\([^\\n)]*${longIdExpressionPattern}`, 'i').test(line),
  },
];

function toRepoPath(filePath) {
  return relative(repoRoot, filePath).split(sep).join('/');
}

function getFiles(rootPath) {
  const files = [];
  const stack = [rootPath];

  while (stack.length > 0) {
    const current = stack.pop();
    if (!current) {
      continue;
    }

    for (const entry of readdirSync(current, { withFileTypes: true })) {
      const fullPath = join(current, entry.name);
      if (entry.isDirectory()) {
        stack.push(fullPath);
        continue;
      }

      if (textExtensions.has(extname(entry.name))) {
        files.push(fullPath);
      }
    }
  }

  return files.sort();
}

function shouldSkipLine(line) {
  const trimmed = line.trim();
  return !trimmed || trimmed.startsWith('//') || trimmed.includes('long-id-scan: ignore');
}

function collectFindings(filePath) {
  const repoPath = toRepoPath(filePath);
  const extension = extname(filePath);
  const rules = extension === '.dart' ? dartRules : tsRules;
  const lines = readFileSync(filePath, 'utf8').split(/\r?\n/);
  const findings = [];

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    if (shouldSkipLine(line)) {
      continue;
    }

    for (const rule of rules) {
      if (!rule.test(line)) {
        continue;
      }

      findings.push({
        file: repoPath,
        line: index + 1,
        rule: rule.id,
        description: rule.description,
        content: line.trim(),
      });
    }
  }

  return findings;
}

const scanFiles = scanRoots
  .map((root) => join(repoRoot, root))
  .flatMap(getFiles);

const findings = scanFiles.flatMap(collectFindings);

console.log('[LongId 字符串安全扫描]');
console.log(`- 扫描目录：${scanRoots.join(', ')}`);
console.log(`- 扫描文件：${scanFiles.length} 个`);
console.log('- 规则：外部对象 ID 禁止纯 number/int 类型与提前数值化转换');

if (findings.length === 0) {
  console.log('- 结果：未发现外部 LongId 字符串安全回归。');
  process.exit(0);
}

console.error(`- 结果：发现 ${findings.length} 条疑似 LongId 安全回归。`);
for (const finding of findings) {
  console.error(`  - [${finding.rule}] ${finding.file}:${finding.line} ${finding.description}`);
  console.error(`    ${finding.content}`);
}

process.exit(1);
