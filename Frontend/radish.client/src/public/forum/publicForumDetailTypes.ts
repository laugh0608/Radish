import type { LongId } from '@/api/user';
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
  intent?: PublicForumDetailIntent;
  sourceState?: PublicRouteSourceState | null;
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  onBack: () => void;
  isAnswerEditorUploading: boolean;
  onAnswerEditorUploadingChange: (uploading: boolean) => void;
  onOpenAuthorProfile?: (userId: string) => void;
  onOpenTag?: (tagSlug: string) => void;
  onOpenQuestion?: () => void;
  onOpenPoll?: () => void;
  onOpenLottery?: () => void;
}
