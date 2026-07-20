import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { runChunkUploadWorkers } from '../src/hooks/chunkedUploadWorkers.ts';

const testDirectory = path.dirname(fileURLToPath(import.meta.url));
const hookSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/hooks/useChunkedUpload.ts'),
  'utf8',
);
const componentSource = fs.readFileSync(
  path.resolve(testDirectory, '../src/components/ChunkedFileUpload/ChunkedFileUpload.tsx'),
  'utf8',
);

test('固定 worker 在并发槽释放后继续消费全部分片', async () => {
  const uploaded: number[] = [];
  let activeWorkers = 0;
  let maxActiveWorkers = 0;

  await runChunkUploadWorkers(
    [0, 1, 2, 3, 4, 5, 6],
    2,
    () => false,
    async (chunkIndex) => {
      activeWorkers += 1;
      maxActiveWorkers = Math.max(maxActiveWorkers, activeWorkers);
      await new Promise<void>((resolve) => setImmediate(resolve));
      uploaded.push(chunkIndex);
      activeWorkers -= 1;
    },
  );

  assert.deepEqual(uploaded.sort((left, right) => left - right), [0, 1, 2, 3, 4, 5, 6]);
  assert.equal(new Set(uploaded).size, 7);
  assert.ok(maxActiveWorkers <= 2);
});

test('首个分片失败向上抛出且不会隐式重试同一索引', async () => {
  const attempts = new Map<number, number>();

  await assert.rejects(
    runChunkUploadWorkers(
      [0, 1, 2, 3],
      2,
      () => false,
      async (chunkIndex) => {
        attempts.set(chunkIndex, (attempts.get(chunkIndex) ?? 0) + 1);
        if (chunkIndex === 1) throw new Error('chunk failed');
        await new Promise<void>((resolve) => setImmediate(resolve));
      },
    ),
    /chunk failed/,
  );

  assert.equal(attempts.get(1), 1);
  assert.ok([...attempts.values()].every((count) => count === 1));
});

test('Hook 将 AbortSignal 传给 XHR API 且任意终止错误都会回收服务端会话', () => {
  assert.match(hookSource, /abortControllerRef\.current\?\.signal,[\s\S]*?\);/);
  assert.match(hookSource, /const sessionId = sessionIdRef\.current;[\s\S]*?if \(sessionId\) \{[\s\S]*?await api\.cancelSession\(sessionId\)/);
  assert.doesNotMatch(hookSource, /signal\.aborted && sessionIdRef\.current/);
  assert.doesNotMatch(hookSource, /Promise\.race|断点续传/);
});

test('Hook 进入服务端合并后必须关闭取消入口，不能伪装已撤销最终化请求', () => {
  assert.match(
    hookSource,
    /finalizingRef\.current = true;[\s\S]*?abortControllerRef\.current = null;[\s\S]*?api\.mergeChunks/,
  );
  assert.match(hookSource, /if \(finalizingRef\.current \|\| !abortControllerRef\.current\) \{[\s\S]*?return false/);
  assert.match(componentSource, /onCancel\(\) === false\) return/);
  assert.match(componentSource, /status === 'uploading' && onCancel && !progress\.isFinalizing/);
});

test('共享组件不暴露尚未成立的暂停恢复，并隔离取消后的失效任务结果', () => {
  assert.doesNotMatch(componentSource, /handlePause|handleResume|'paused'/);
  assert.doesNotMatch(componentSource, /new AbortController\(\)/);
  assert.match(componentSource, /const attemptId = \+\+attemptIdRef\.current/);
  assert.match(componentSource, /if \(attemptIdRef\.current !== attemptId\) return/);
  assert.match(componentSource, /onCancel\?\.\(\);[\s\S]*?attemptIdRef\.current \+= 1/);
  assert.match(
    componentSource,
    /autoStartedFileRef\.current !== file[\s\S]*?autoStartedFileRef\.current = file/,
  );
});
