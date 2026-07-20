import type { RefObject } from 'react';
import type { FormInstance } from 'antd';
import {
  AntInput as Input,
  AntSelect as Select,
  Button,
  Form,
  InputNumber,
  Space,
  Tag,
  formatLocalizedDateTime,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import {
  MANUAL_ACTION_TYPE,
  getManualActionOptions,
  getManualActionTypeText,
  toPositiveLongString,
  type ActionLogPreset,
  type ManualActionPreset,
  type ManualActionTypeValue,
  type ManualActiveModerationStatus,
  type ManualModerationStatusSnapshot,
} from './moderationPageHelpers';

interface ManualModerationActionSectionProps {
  sectionRef: RefObject<HTMLElement | null>;
  form: FormInstance;
  contextHint: string | null;
  submitting: boolean;
  statusLoading: boolean;
  statusError: string | null;
  statusSnapshot: ManualModerationStatusSnapshot | null;
  onResetForm: () => void;
  onSubmit: () => void;
  onRefreshStatus: () => void;
  onTargetUserInputChange: () => void;
  onApplyActionLogPreset: (preset: ActionLogPreset) => void;
  onApplyManualActionPreset: (preset: ManualActionPreset) => void;
}

interface ManualStatusCardProps {
  title: string;
  action: ManualActiveModerationStatus | null;
  cancelActionType: ManualActionTypeValue;
  targetUserId?: string;
  onApplyActionLogPreset: (preset: ActionLogPreset) => void;
  onApplyManualActionPreset: (preset: ManualActionPreset) => void;
}

function ManualStatusCard({
  title,
  action,
  cancelActionType,
  targetUserId,
  onApplyActionLogPreset,
  onApplyManualActionPreset,
}: ManualStatusCardProps) {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  if (!action) {
    return (
      <div className="moderation-manual-status-card">
        <div className="moderation-manual-status-card__title">{title}</div>
        <Tag>{t('moderation.manual.notActive')}</Tag>
        <div className="moderation-manual-status-card__empty">{t('moderation.manual.noActiveAction', { action: title })}</div>
      </div>
    );
  }

  return (
    <div className="moderation-manual-status-card">
      <div className="moderation-manual-status-card__head">
        <div className="moderation-manual-status-card__title">{title}</div>
        <Tag color="processing">{t('moderation.action.active')}</Tag>
      </div>
      <div className="moderation-manual-status-card__meta">{t('moderation.manual.actionRecord', { id: action.actionId })}</div>
      <div className="moderation-manual-status-card__meta">
        {t('moderation.manual.endTime', {
          time: action.endTime ? formatLocalizedDateTime(action.endTime, language) : t('moderation.manual.permanent'),
        })}
      </div>
      {action.sourceReportId ? (
        <div className="moderation-manual-status-card__meta">{t('moderation.manual.sourceReport', { id: action.sourceReportId })}</div>
      ) : null}
      <div className="moderation-manual-status-card__reason">{action.reason}</div>
      <Space wrap>
        <Button
          size="small"
          onClick={() => {
            onApplyActionLogPreset({
              targetUserId,
              actionType: action.actionType,
              isActive: 'active',
              hint: t('moderation.manual.hint.currentLog', { userId: targetUserId, action: title }),
            });
          }}
        >
          {t('moderation.manual.viewCurrent')}
        </Button>
        <Button
          size="small"
          variant="primary"
          onClick={() => {
            onApplyManualActionPreset({
              targetUserId,
              sourceReportId: action.sourceReportId ? String(action.sourceReportId) : undefined,
              actionType: cancelActionType,
              reason: t('moderation.reason.fromActionUndo', {
                actionId: action.actionId,
                action: getManualActionTypeText(cancelActionType, t),
              }),
              hint: t('moderation.manual.hint.prefillUndo', {
                action: title,
                actionId: action.actionId,
                undoAction: getManualActionTypeText(cancelActionType, t),
              }),
            });
          }}
        >
          {getManualActionTypeText(cancelActionType, t)}
        </Button>
      </Space>
    </div>
  );
}

export function ManualModerationActionSection({
  sectionRef,
  form,
  contextHint,
  submitting,
  statusLoading,
  statusError,
  statusSnapshot,
  onResetForm,
  onSubmit,
  onRefreshStatus,
  onTargetUserInputChange,
  onApplyActionLogPreset,
  onApplyManualActionPreset,
}: ManualModerationActionSectionProps) {
  const { t } = useTranslation();
  return (
    <section className="admin-feature-card" ref={sectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>{t('moderation.manual.title')}</h3>
          <p className="admin-feature-subtle">{t('moderation.manual.description')}</p>
        </div>
        <Space wrap>
          <Button onClick={onResetForm}>
            {t('moderation.manual.clear')}
          </Button>
          <Button variant="primary" disabled={submitting} onClick={onSubmit}>
            {t(submitting ? 'moderation.manual.executing' : 'moderation.manual.execute')}
          </Button>
        </Space>
      </div>

      {contextHint ? (
        <div className="admin-feature-banner moderation-section-banner">
          {contextHint}
        </div>
      ) : null}

      <div className="moderation-manual-status">
        <div className="moderation-manual-status__header">
          <div>
            <div className="moderation-manual-status__title">{t('moderation.manual.statusTitle')}</div>
            <div className="moderation-manual-status__subtitle">{t('moderation.manual.statusDescription')}</div>
          </div>
          <Button size="small" onClick={onRefreshStatus}>
            {t('moderation.manual.refreshStatus')}
          </Button>
        </div>

        {!statusSnapshot && !statusLoading && !statusError ? (
          <div className="moderation-manual-status__empty">{t('moderation.manual.statusEmpty')}</div>
        ) : null}
        {statusLoading ? <div className="moderation-manual-status__empty">{t('moderation.manual.statusLoading')}</div> : null}
        {statusError ? <div className="moderation-manual-status__error">{statusError}</div> : null}
        {statusSnapshot ? (
          <div className="moderation-manual-status__grid">
            <ManualStatusCard
              title={t('moderation.action.mute')}
              action={statusSnapshot.muteAction}
              cancelActionType={MANUAL_ACTION_TYPE.unmute}
              targetUserId={statusSnapshot.targetUserId}
              onApplyActionLogPreset={onApplyActionLogPreset}
              onApplyManualActionPreset={onApplyManualActionPreset}
            />
            <ManualStatusCard
              title={t('moderation.action.ban')}
              action={statusSnapshot.banAction}
              cancelActionType={MANUAL_ACTION_TYPE.unban}
              targetUserId={statusSnapshot.targetUserId}
              onApplyActionLogPreset={onApplyActionLogPreset}
              onApplyManualActionPreset={onApplyManualActionPreset}
            />
          </div>
        ) : null}
      </div>

      <Form form={form} layout="vertical" className="moderation-manual-action-form">
        <div className="moderation-manual-action-form__grid">
          <Form.Item
            name="targetUserId"
            label={t('moderation.manual.targetUserId')}
            rules={[
              { required: true, message: t('moderation.manual.targetUserRequired') },
              {
                validator: (_, value) => {
                  if (typeof value !== 'string' || value.trim().length === 0) {
                    return Promise.resolve();
                  }

                  if (toPositiveLongString(value)) {
                    return Promise.resolve();
                  }

                  return Promise.reject(new Error(t('moderation.invalidTargetUserId')));
                },
              },
            ]}
          >
            <Input
              placeholder={t('moderation.manual.targetUserPlaceholder')}
              onChange={onTargetUserInputChange}
              onBlur={onRefreshStatus}
              onPressEnter={onRefreshStatus}
            />
          </Form.Item>

          <Form.Item
            name="sourceReportId"
            label={t('moderation.manual.sourceReportId')}
            rules={[
              {
                validator: (_, value) => {
                  if (value === undefined || value === null || String(value).trim().length === 0) {
                    return Promise.resolve();
                  }

                  return toPositiveLongString(String(value))
                    ? Promise.resolve()
                    : Promise.reject(new Error(t('moderation.invalidSourceReportId')));
                },
              },
            ]}
          >
            <Input placeholder={t('moderation.manual.sourceReportPlaceholder')} />
          </Form.Item>
        </div>

        <div className="moderation-manual-action-form__grid">
          <Form.Item name="actionType" label={t('moderation.manual.action')} rules={[{ required: true, message: t('moderation.manual.actionRequired') }]}>
            <Select options={getManualActionOptions(t)} placeholder={t('moderation.manual.actionPlaceholder')} />
          </Form.Item>

          <Form.Item
            noStyle
            shouldUpdate={(prev, next) => prev.actionType !== next.actionType}
          >
            {({ getFieldValue }) => {
              const currentActionType = getFieldValue('actionType') as ManualActionTypeValue | undefined;
              if (currentActionType !== MANUAL_ACTION_TYPE.mute && currentActionType !== MANUAL_ACTION_TYPE.ban) {
                return <div className="moderation-manual-action-form__field-placeholder" />;
              }

              return (
                <Form.Item
                  name="durationHours"
                  label={t(currentActionType === MANUAL_ACTION_TYPE.mute ? 'moderation.manual.duration' : 'moderation.manual.banDuration')}
                  rules={currentActionType === MANUAL_ACTION_TYPE.mute
                    ? [{ required: true, message: t('moderation.manual.muteDurationRequired') }]
                    : []}
                >
                  <InputNumber
                    min={1}
                    max={720}
                    className="moderation-full-width-control"
                    placeholder={t(currentActionType === MANUAL_ACTION_TYPE.mute
                      ? 'moderation.manual.durationExample'
                      : 'moderation.manual.permanentPlaceholder')}
                  />
                </Form.Item>
              );
            }}
          </Form.Item>
        </div>

        <Form.Item name="reason" label={t('moderation.manual.reason')}>
          <Input.TextArea rows={4} maxLength={500} showCount placeholder={t('moderation.manual.reasonPlaceholder')} />
        </Form.Item>

        <div className="moderation-manual-action-form__footnote">
          {t('moderation.manual.footnote')}
        </div>
      </Form>
    </section>
  );
}
