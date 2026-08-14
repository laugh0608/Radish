import { apiGet, apiPost } from './client';
import type { ParsedApiResponse } from './types';
import type {
  ChangePostAnswerAcceptanceRequest,
  CreatePostAnswerRequest,
  DeletePostAnswerRequest,
  GetPostAnswerPageRequest,
  PostAnswerAcceptanceMutationVo,
  PostAnswerMutationVo,
  PostAnswerPageVo,
  PostAnswerRevisionDetailVo,
  PostAnswerRevisionListVo,
  RestorePostAnswerRevisionRequest,
  RevokePostAnswerAcceptanceRequest,
  UpdatePostAnswerRequest,
} from './forum-question-contract';

const questionApiBase = '/api/v1/Question';
const authenticated = { withAuth: true } as const;

export function getPostAnswerPage(
  request: GetPostAnswerPageRequest,
): Promise<ParsedApiResponse<PostAnswerPageVo>> {
  const query = new URLSearchParams({
    postIdentifier: request.postIdentifier,
    pageIndex: String(request.pageIndex ?? 1),
    pageSize: String(request.pageSize ?? 20),
    sort: request.sort ?? 'default',
  });
  return apiGet<PostAnswerPageVo>(`${questionApiBase}/Page?${query}`, authenticated);
}

export function createPostAnswer(
  request: CreatePostAnswerRequest,
): Promise<ParsedApiResponse<PostAnswerMutationVo>> {
  return apiPost<PostAnswerMutationVo>(`${questionApiBase}/Answer`, request, authenticated);
}

export function updatePostAnswer(
  request: UpdatePostAnswerRequest,
): Promise<ParsedApiResponse<PostAnswerMutationVo>> {
  return apiPost<PostAnswerMutationVo>(`${questionApiBase}/Edit`, request, authenticated);
}

export function deletePostAnswer(
  request: DeletePostAnswerRequest,
): Promise<ParsedApiResponse<PostAnswerMutationVo>> {
  return apiPost<PostAnswerMutationVo>(`${questionApiBase}/Delete`, request, authenticated);
}

export function getPostAnswerRevisions(
  answerPublicId: string,
): Promise<ParsedApiResponse<PostAnswerRevisionListVo>> {
  const query = new URLSearchParams({ answerPublicId });
  return apiGet<PostAnswerRevisionListVo>(
    `${questionApiBase}/Revisions?${query}`,
    authenticated,
  );
}

export function getPostAnswerRevision(
  answerPublicId: string,
  revisionNumber: number,
): Promise<ParsedApiResponse<PostAnswerRevisionDetailVo>> {
  const query = new URLSearchParams({
    answerPublicId,
    revisionNumber: String(revisionNumber),
  });
  return apiGet<PostAnswerRevisionDetailVo>(
    `${questionApiBase}/Revision?${query}`,
    authenticated,
  );
}

export function restorePostAnswerRevision(
  request: RestorePostAnswerRevisionRequest,
): Promise<ParsedApiResponse<PostAnswerMutationVo>> {
  return apiPost<PostAnswerMutationVo>(`${questionApiBase}/Restore`, request, authenticated);
}

export function acceptPostAnswer(
  request: ChangePostAnswerAcceptanceRequest,
): Promise<ParsedApiResponse<PostAnswerAcceptanceMutationVo>> {
  return apiPost<PostAnswerAcceptanceMutationVo>(
    `${questionApiBase}/Accept`,
    request,
    authenticated,
  );
}

export function revokePostAnswerAcceptance(
  request: RevokePostAnswerAcceptanceRequest,
): Promise<ParsedApiResponse<PostAnswerAcceptanceMutationVo>> {
  return apiPost<PostAnswerAcceptanceMutationVo>(
    `${questionApiBase}/Revoke`,
    request,
    authenticated,
  );
}
