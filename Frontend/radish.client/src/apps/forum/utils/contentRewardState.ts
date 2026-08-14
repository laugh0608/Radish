import type {
  ContentRewardTargetRequest,
  ContentRewardTargetStateVo,
  ContentRewardTargetType,
} from '@radish/http';
import type { CommentNode } from '@/api/forum';

export const buildContentRewardTargetKey = (
  targetType: ContentRewardTargetType,
  targetId: string | number,
): string => `${targetType}:${String(targetId)}`;

export const mapContentRewardTargetStates = (
  states: ContentRewardTargetStateVo[],
): Record<string, ContentRewardTargetStateVo> => Object.fromEntries(
  states.map((state) => [
    buildContentRewardTargetKey(state.voTargetType, state.voTargetId),
    state,
  ]),
);

export const collectCommentRewardTargets = (
  comments: CommentNode[],
): ContentRewardTargetRequest[] => {
  const targets: ContentRewardTargetRequest[] = [];
  const visited = new Set<string>();
  const queue = [...comments];

  while (queue.length > 0) {
    const comment = queue.shift();
    if (!comment) {
      continue;
    }

    const targetId = String(comment.voId);
    if (!visited.has(targetId)) {
      visited.add(targetId);
      targets.push({ targetType: 'Comment', targetId });
    }

    if (Array.isArray(comment.voChildren)) {
      queue.push(...comment.voChildren);
    }
  }

  return targets;
};

export const createContentRewardIdempotencyKey = (
  createUuid: () => string = () => crypto.randomUUID(),
): string => `content-reward:${createUuid()}`;
