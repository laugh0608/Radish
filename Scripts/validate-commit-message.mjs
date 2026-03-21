import { readFileSync } from 'node:fs';
import process from 'node:process';

const commitHeaderPattern =
  /^(feat|fix|docs|refactor|test|chore|ci|build|perf|revert)(\([a-z0-9._/-]+\))?!?: .{1,72}$/u;

function normalizeMessage(rawMessage) {
  return rawMessage
    .split(/\r?\n/u)
    .filter((line) => !line.startsWith('#'))
    .join('\n')
    .trim();
}

function main() {
  const messageFile = process.argv[2];

  if (!messageFile) {
    console.error('[commit-msg] 缺少提交信息文件路径参数。');
    process.exit(1);
  }

  const message = normalizeMessage(readFileSync(messageFile, 'utf8'));
  const lines = message.split('\n');
  const header = lines[0] ?? '';

  if (!header) {
    console.error('[commit-msg] 提交标题不能为空。');
    process.exit(1);
  }

  if (!commitHeaderPattern.test(header)) {
    console.error('[commit-msg] 提交标题必须符合 Conventional Commits。');
    console.error('[commit-msg] 示例：feat(ui): 添加主题切换');
    process.exit(1);
  }

  if (/Co-Authored-By:/iu.test(message) && /(Claude|Anthropic|OpenAI|Codex|ChatGPT)/iu.test(message)) {
    console.error('[commit-msg] 禁止在提交信息中加入 AI 协作者署名。');
    process.exit(1);
  }
}

main();
