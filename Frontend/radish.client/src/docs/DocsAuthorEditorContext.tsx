import { useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  MarkdownRenderer,
  buildAttachmentMarkdownUrl,
  type ProtectedMarkdownAttachmentOptions,
} from '@radish/ui';
import {
  WikiCollaboratorState,
  type WikiAuthorDraftDetailVo,
} from '@radish/http';
import type { LongId } from '@/api/user';
import {
  formatWikiTime,
  type EditorDraft,
  type ParentOption,
} from '@/apps/wiki/wikiApp.helpers';
import { log } from '@/utils/logger';
import type { DocsAuthorOutlineItem } from './docsAuthorEditorPresentation';
import { getDocsAuthorInitial } from './docsAuthorEditorPresentation';
import { buildDocsAuthorPath, type DocsAuthorRoute } from './docsAuthorRouteState';
import styles from './DocsAuthorEditorPage.module.css';

interface DocsAuthorEditorContextProps {
  route: DocsAuthorRoute & ({ kind: 'compose' } | { kind: 'edit' });
  draft: EditorDraft;
  document: WikiAuthorDraftDetailVo | null;
  outline: DocsAuthorOutlineItem[];
  parentOptions: ParentOption[];
  reviewStateText: string;
  readOnly: boolean;
  submitting: boolean;
  currentUserPublicId: string;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onParentChange: (parentId: string) => void;
  onSetDraft: (updater: (current: EditorDraft) => EditorDraft) => void;
  onInviteCollaborator: (publicId: string) => Promise<void>;
  onRemoveCollaborator: (collaboratorId: LongId) => Promise<void>;
  onRespondInvitation: (collaboratorId: LongId, accept: boolean) => Promise<void>;
  protectedAttachments: ProtectedMarkdownAttachmentOptions;
}

function getCollaboratorStateText(state: number, t: TFunction): string {
  switch (state) {
    case WikiCollaboratorState.Accepted:
      return t('wiki.author.collaboration.state.accepted');
    case WikiCollaboratorState.Declined:
      return t('wiki.author.collaboration.state.declined');
    case WikiCollaboratorState.Revoked:
      return t('wiki.author.collaboration.state.revoked');
    default:
      return t('wiki.author.collaboration.state.pending');
  }
}

export function DocsAuthorEditorContext({
  route,
  draft,
  document,
  outline,
  parentOptions,
  reviewStateText,
  readOnly,
  submitting,
  currentUserPublicId,
  onNavigate,
  onParentChange,
  onSetDraft,
  onInviteCollaborator,
  onRemoveCollaborator,
  onRespondInvitation,
  protectedAttachments,
}: DocsAuthorEditorContextProps) {
  const { t, i18n } = useTranslation();
  const [invitePublicId, setInvitePublicId] = useState('');
  const [collaborationBusy, setCollaborationBusy] = useState(false);
  const ownInvitation = document?.voCollaborators.find((collaborator) =>
    collaborator.voUserPublicId === currentUserPublicId
      && collaborator.voInviteState === WikiCollaboratorState.Pending
  ) ?? null;
  const disabled = readOnly || submitting;

  const runCollaborationAction = async (action: () => Promise<void>) => {
    setCollaborationBusy(true);
    try {
      await action();
    } catch (error) {
      log.error('DocsAuthorEditorContext', '文档协作动作失败:', error);
      const message = error instanceof Error && error.message.trim()
        ? error.message
        : t('wiki.author.feedback.collaborationActionFailed');
      toast.error(message);
    } finally {
      setCollaborationBusy(false);
    }
  };

  const handleInvite = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const publicId = invitePublicId.trim();
    if (!publicId) {
      return;
    }

    void runCollaborationAction(async () => {
      await onInviteCollaborator(publicId);
      setInvitePublicId('');
    });
  };

  return (
    <div className={styles.editorContextPanel}>
      <section className={styles.editorContextSection}>
        <div className={styles.editorContextHeading}>
          <Icon icon="mdi:format-list-bulleted" size={18} />
          <h2>{t('wiki.author.editor.outline')}</h2>
        </div>
        {outline.length ? (
          <ol className={styles.editorOutlineList}>
            {outline.map((item, index) => (
              <li
                key={item.id}
                className={`${styles.editorOutlineItem} ${index === 0 ? styles.editorOutlineItemActive : ''}`}
                data-level={item.level}
              >
                <span>H{item.level}</span>
                <strong>{item.text}</strong>
              </li>
            ))}
          </ol>
        ) : (
          <p className={styles.editorContextEmpty}>{t('wiki.author.editor.outlineEmpty')}</p>
        )}
      </section>

      <section className={styles.editorContextSection}>
        <div className={styles.editorContextHeading}>
          <Icon icon="mdi:file-document-check-outline" size={18} />
          <h2>{t('wiki.author.editor.documentStatus')}</h2>
          <span className={styles.editorStatusBadge}>{reviewStateText}</span>
        </div>
        <div className={styles.editorVersionFlow}>
          <span>{t('wiki.author.document.documentVersion', { version: document?.voDocumentVersion ?? 0 })}</span>
          <Icon icon="mdi:arrow-right" size={16} />
          <strong>{t('wiki.author.document.draftVersion', { version: document?.voDraftVersion ?? 0 })}</strong>
        </div>
        <p className={styles.editorContextText}>
          {document?.voReviewComment
            || (readOnly ? document?.voReadOnlyReason : null)
            || t('wiki.author.editor.statusDescription')}
        </p>
        {route.kind === 'edit' ? (
          <a
            className={styles.editorContextLink}
            href={buildDocsAuthorPath({ kind: 'revisions', documentId: route.documentId })}
            onClick={(event) => onNavigate(event, { kind: 'revisions', documentId: route.documentId })}
          >
            <Icon icon="mdi:history" size={16} />
            <span>{t('wiki.author.actions.revisions')}</span>
          </a>
        ) : null}
      </section>

      <section className={styles.editorContextSection}>
        <div className={styles.editorContextHeading}>
          <Icon icon="mdi:tune-variant" size={18} />
          <h2>{t('wiki.author.editor.properties')}</h2>
        </div>
        <div className={styles.editorPropertyGrid}>
          <div className={styles.editorPropertyReadOnly}>
            <span>{t('wiki.author.editor.documentType')}</span>
            <strong>{t('wiki.author.editor.customDocument')}</strong>
          </div>
          <label className={styles.editorCompactField}>
            <span>{t('wiki.author.form.parent')}</span>
            <select value={draft.parentId} disabled={disabled} onChange={(event) => onParentChange(event.target.value)}>
              <option value="">{t('wiki.author.form.root')}</option>
              {parentOptions.map((option) => <option key={option.id} value={String(option.id)}>{option.label}</option>)}
            </select>
          </label>
          <label className={styles.editorCompactField}>
            <span>{t('wiki.author.editor.draftSlug')}</span>
            <input value={draft.slug} disabled={disabled} onChange={(event) => onSetDraft((current) => ({ ...current, slug: event.target.value }))} placeholder={t('wiki.author.form.slugPlaceholder')} />
          </label>
          <label className={styles.editorCompactField}>
            <span>{t('wiki.author.form.summary')}</span>
            <textarea value={draft.summary} disabled={disabled} onChange={(event) => onSetDraft((current) => ({ ...current, summary: event.target.value }))} placeholder={t('wiki.author.form.summaryPlaceholder')} />
          </label>
          <label className={styles.editorCompactField}>
            <span>{t('wiki.author.form.coverAttachmentId')}</span>
            <input value={draft.coverAttachmentId} disabled={disabled} onChange={(event) => onSetDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))} placeholder={t('wiki.author.form.optional')} />
          </label>
          {route.kind === 'edit' ? (
            <label className={styles.editorCompactField}>
              <span>{t('wiki.author.form.changeSummary')}</span>
              <input value={draft.changeSummary} disabled={disabled} onChange={(event) => onSetDraft((current) => ({ ...current, changeSummary: event.target.value }))} placeholder={t('wiki.author.form.changeSummaryPlaceholder')} />
            </label>
          ) : null}
        </div>
        {/^[1-9]\d*$/.test(draft.coverAttachmentId.trim()) ? (
          <div className={styles.editorCoverPreview}>
            <MarkdownRenderer
              content={`![${t('wiki.author.form.coverPreview')}](${buildAttachmentMarkdownUrl(draft.coverAttachmentId.trim())})`}
              protectedAttachments={protectedAttachments}
            />
          </div>
        ) : null}
      </section>

      {document ? (
        <section className={styles.editorContextSection}>
          <div className={styles.editorContextHeading}>
            <Icon icon="mdi:account-multiple-outline" size={18} />
            <h2>{t('wiki.author.collaboration.title')}</h2>
            <span className={styles.editorSectionCount}>{document.voCollaborators.length + 1}</span>
          </div>
          <div className={styles.editorPeopleList}>
            <div className={styles.editorPersonRow}>
              <span className={styles.editorPersonAvatar}>{getDocsAuthorInitial(document.voOwnerUserName, 'O')}</span>
              <div>
                <strong>{document.voOwnerUserName}</strong>
                <span>{t('wiki.author.collaboration.ownerRole')}</span>
              </div>
            </div>
            {document.voCollaborators.map((collaborator) => (
              <div key={collaborator.voId} className={styles.editorPersonRow}>
                <span className={styles.editorPersonAvatar}>{getDocsAuthorInitial(collaborator.voUserName, 'E')}</span>
                <div>
                  <strong>{collaborator.voUserName}</strong>
                  <span>{getCollaboratorStateText(collaborator.voInviteState, t)}</span>
                </div>
                {document.voCanManageCollaborators && collaborator.voInviteState !== WikiCollaboratorState.Revoked ? (
                  <button type="button" disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRemoveCollaborator(collaborator.voId))}>
                    {t('wiki.author.collaboration.remove')}
                  </button>
                ) : null}
              </div>
            ))}
          </div>
          {document.voCanManageCollaborators ? (
            <form className={styles.editorInviteForm} onSubmit={handleInvite}>
              <input value={invitePublicId} onChange={(event) => setInvitePublicId(event.target.value)} placeholder={t('wiki.author.collaboration.publicIdPlaceholder')} />
              <button type="submit" disabled={collaborationBusy || !invitePublicId.trim()}>{t('wiki.author.collaboration.invite')}</button>
            </form>
          ) : null}
          {ownInvitation ? (
            <div className={styles.editorInvitationActions}>
              <p>{t('wiki.author.collaboration.pendingReadOnly')}</p>
              <button type="button" disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRespondInvitation(ownInvitation.voId, false))}>{t('wiki.author.collaboration.decline')}</button>
              <button type="button" className={styles.editorInvitationPrimary} disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRespondInvitation(ownInvitation.voId, true))}>{t('wiki.author.collaboration.accept')}</button>
            </div>
          ) : null}
        </section>
      ) : null}

      {document?.voReviewEvents.length ? (
        <section className={styles.editorContextSection}>
          <div className={styles.editorContextHeading}>
            <Icon icon="mdi:history" size={18} />
            <h2>{t('wiki.author.timeline.title')}</h2>
          </div>
          <ol className={styles.editorTimeline}>
            {document.voReviewEvents.map((event) => (
              <li key={event.voId}>
                <span className={styles.editorTimelineIcon}><Icon icon="mdi:circle-small" size={18} /></span>
                <div>
                  <strong>{event.voAction}</strong>
                  <span>{event.voActorName} · {formatWikiTime(event.voCreateTime, i18n.resolvedLanguage)}</span>
                  {event.voComment ? <p>{event.voComment}</p> : null}
                </div>
              </li>
            ))}
          </ol>
        </section>
      ) : null}

      <div className={styles.editorBoundaryNote}>
        <Icon icon="mdi:information-outline" size={17} />
        <span>{t('wiki.author.editor.authorBoundary')}</span>
      </div>
    </div>
  );
}
