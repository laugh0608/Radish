import type { LongId } from '@/api/user';
import type { PostAnswerSort } from '@radish/http';
import type { PublicForumDetailIntent } from '../forumRouteState';
import type { PublicRouteSourceState } from '../publicRouteNavigation';

export interface PublicForumCommentNavigationTarget {
  commentId: LongId;
  expandedRootCommentId?: LongId;
  navigationKey: string;
}

export interface PublicForumDetailProps {
  postId: string;
  commentId?: string;
  answerPublicId?: string;
  answerPage?: number;
  answerSort?: PostAnswerSort;
  intent?: PublicForumDetailIntent;
  sourceState?: PublicRouteSourceState | null;
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  onBack: () => void;
  isAnswerEditorUploading: boolean;
  onAnswerEditorUploadingChange: (uploading: boolean) => void;
  onAnswerStateChange: (pageIndex: number, sort: PostAnswerSort, replace?: boolean) => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}
