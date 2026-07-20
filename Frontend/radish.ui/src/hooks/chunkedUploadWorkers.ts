/**
 * 使用固定数量 worker 消费分片队列。
 *
 * 首个失败会阻止 worker 继续领取新任务；已在执行的任务会等待结束，且不会隐式重试。
 */
export async function runChunkUploadWorkers(
  pendingChunks: readonly number[],
  concurrency: number,
  shouldStop: () => boolean,
  uploadChunk: (chunkIndex: number) => Promise<void>,
): Promise<void> {
  let nextChunkPosition = 0;
  let firstUploadFailure: unknown;
  const worker = async () => {
    while (firstUploadFailure === undefined) {
      if (shouldStop()) {
        firstUploadFailure = new Error('上传已取消');
        return;
      }

      const position = nextChunkPosition;
      nextChunkPosition += 1;
      if (position >= pendingChunks.length) {
        return;
      }

      try {
        await uploadChunk(pendingChunks[position]);
      } catch (error) {
        firstUploadFailure ??= error;
      }
    }
  };

  const workerCount = Math.min(Math.max(1, Math.floor(concurrency)), pendingChunks.length);
  await Promise.all(Array.from({ length: workerCount }, () => worker()));
  if (firstUploadFailure !== undefined) {
    throw firstUploadFailure;
  }
}
