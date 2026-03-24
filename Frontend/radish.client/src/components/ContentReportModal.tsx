import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@radish/ui/button';
import { Modal } from '@radish/ui/modal';
import { Select } from '@radish/ui/select';
import { toast } from '@radish/ui/toast';
import { submitContentReport, type ContentReportTargetType } from '@/api/contentModeration';
import styles from './ContentReportModal.module.css';

interface ContentReportModalProps {
  isOpen: boolean;
  targetType: ContentReportTargetType;
  targetId: number;
  onClose: () => void;
}

export const ContentReportModal = ({
  isOpen,
  targetType,
  targetId,
  onClose,
}: ContentReportModalProps) => {
  const { t } = useTranslation();
  const [reasonType, setReasonType] = useState('Spam');
  const [reasonDetail, setReasonDetail] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    setReasonType('Spam');
    setReasonDetail('');
  }, [isOpen, targetId, targetType]);

  const targetLabel = useMemo(() => {
    switch (targetType) {
      case 'Comment':
        return t('report.target.comment');
      case 'ChatMessage':
        return t('report.target.chatMessage');
      case 'Product':
        return t('report.target.product');
      case 'Post':
      default:
        return t('report.target.post');
    }
  }, [t, targetType]);

  const reasonOptions = useMemo(() => ([
    { value: 'Spam', label: t('report.reason.spam') },
    { value: 'Abuse', label: t('report.reason.abuse') },
    { value: 'Pornography', label: t('report.reason.pornography') },
    { value: 'Illegal', label: t('report.reason.illegal') },
    { value: 'Fraud', label: t('report.reason.fraud') },
    { value: 'Other', label: t('report.reason.other') },
  ]), [t]);

  const handleSubmit = async () => {
    if (targetId <= 0 || submitting) {
      return;
    }

    setSubmitting(true);
    try {
      await submitContentReport({
        targetType,
        targetContentId: targetId,
        reasonType,
        reasonDetail: reasonDetail.trim() || undefined,
      });

      toast.success(t('report.submitSuccess'));
      onClose();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : t('report.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={`${t('report.title')} · ${targetLabel}`}
      footer={(
        <div className={styles.footer}>
          <Button variant="secondary" onClick={onClose} disabled={submitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={handleSubmit} disabled={submitting || targetId <= 0}>
            {submitting ? t('report.submitting') : t('report.submit')}
          </Button>
        </div>
      )}
    >
      <div className={styles.container}>
        <p className={styles.description}>{t('report.description')}</p>

        <Select
          label={t('report.reason.label')}
          value={reasonType}
          onChange={(event) => setReasonType(event.target.value)}
          options={reasonOptions}
        />

        <label className={styles.fieldLabel} htmlFor="content-report-detail">
          {t('report.detail.label')}
        </label>
        <textarea
          id="content-report-detail"
          className={styles.detailTextarea}
          value={reasonDetail}
          onChange={(event) => setReasonDetail(event.target.value)}
          placeholder={t('report.detail.placeholder')}
          maxLength={500}
          rows={5}
        />
      </div>
    </Modal>
  );
};
