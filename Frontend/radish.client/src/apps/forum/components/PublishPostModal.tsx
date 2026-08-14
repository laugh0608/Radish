import { useTranslation } from 'react-i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { ForumPostComposer, type ForumPostComposerProps } from './ForumPostComposer';
import styles from './PublishPostModal.module.css';

export type PublishPostModalProps = Omit<ForumPostComposerProps, 'surface'>;

export const PublishPostModal = ({
  isOpen,
  isAuthenticated,
  onClose,
  ...props
}: PublishPostModalProps) => {
  const { t } = useTranslation();

  return (
    <BottomSheet
      isOpen={isOpen}
      onClose={onClose}
      closeLabel={t('common.close')}
      closeOnOverlayClick={false}
      closeOnEscape={false}
      height={isAuthenticated ? '76%' : '62%'}
      className={styles.sheet}
      bodyClassName={styles.sheetBody}
      footerClassName={styles.sheetFooter}
    >
      <ForumPostComposer
        {...props}
        isOpen={isOpen}
        surface="sheet"
        isAuthenticated={isAuthenticated}
        onClose={onClose}
      />
    </BottomSheet>
  );
};
