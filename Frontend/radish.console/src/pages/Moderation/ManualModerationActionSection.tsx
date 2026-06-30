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
} from '@radish/ui';
import {
  MANUAL_ACTION_OPTIONS,
  MANUAL_ACTION_TYPE,
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
  if (!action) {
    return (
      <div className="moderation-manual-status-card">
        <div className="moderation-manual-status-card__title">{title}</div>
        <Tag>当前未生效</Tag>
        <div className="moderation-manual-status-card__empty">没有生效中的{title}动作。</div>
      </div>
    );
  }

  return (
    <div className="moderation-manual-status-card">
      <div className="moderation-manual-status-card__head">
        <div className="moderation-manual-status-card__title">{title}</div>
        <Tag color="processing">生效中</Tag>
      </div>
      <div className="moderation-manual-status-card__meta">动作单 #{action.actionId}</div>
      <div className="moderation-manual-status-card__meta">
        截止时间：{action.endTime || '永久'}
      </div>
      {action.sourceReportId ? (
        <div className="moderation-manual-status-card__meta">来源举报单：#{action.sourceReportId}</div>
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
              hint: `已带入用户 #${targetUserId} 当前生效中的${title}动作日志。`,
            });
          }}
        >
          查看当前动作
        </Button>
        <Button
          size="small"
          variant="primary"
          onClick={() => {
            onApplyManualActionPreset({
              targetUserId,
              sourceReportId: action.sourceReportId ? String(action.sourceReportId) : undefined,
              actionType: cancelActionType,
              reason: `参考动作单 #${action.actionId}，人工复核后${getManualActionTypeText(cancelActionType)}`,
              hint: `已根据当前生效中的${title}动作单 #${action.actionId} 预填${getManualActionTypeText(cancelActionType)}表单。`,
            });
          }}
        >
          {getManualActionTypeText(cancelActionType)}
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
  return (
    <section className="admin-feature-card" ref={sectionRef}>
      <div className="admin-feature-header">
        <div>
          <h3>手动治理动作</h3>
          <p className="admin-feature-subtle">从举报队列或治理日志一键带入目标用户、来源举报单和解除建议，补齐人工禁言 / 封禁 / 解除动作闭环。</p>
        </div>
        <Space>
          <Button onClick={onResetForm}>
            清空表单
          </Button>
          <Button variant="primary" disabled={submitting} onClick={onSubmit}>
            {submitting ? '执行中...' : '执行治理动作'}
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
            <div className="moderation-manual-status__title">当前生效状态</div>
            <div className="moderation-manual-status__subtitle">输入或带入目标用户 ID 后，可直接查看该用户当前是否仍处于禁言或封禁中。</div>
          </div>
          <Button size="small" onClick={onRefreshStatus}>
            刷新状态
          </Button>
        </div>

        {!statusSnapshot && !statusLoading && !statusError ? (
          <div className="moderation-manual-status__empty">先输入目标用户 ID，或从上方举报队列 / 动作日志一键带入。</div>
        ) : null}
        {statusLoading ? <div className="moderation-manual-status__empty">正在加载当前治理状态...</div> : null}
        {statusError ? <div className="moderation-manual-status__error">{statusError}</div> : null}
        {statusSnapshot ? (
          <div className="moderation-manual-status__grid">
            <ManualStatusCard
              title="禁言"
              action={statusSnapshot.muteAction}
              cancelActionType={MANUAL_ACTION_TYPE.unmute}
              targetUserId={statusSnapshot.targetUserId}
              onApplyActionLogPreset={onApplyActionLogPreset}
              onApplyManualActionPreset={onApplyManualActionPreset}
            />
            <ManualStatusCard
              title="封禁"
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
            label="目标用户 ID"
            rules={[
              { required: true, message: '请输入目标用户 ID' },
              {
                validator: (_, value) => {
                  if (typeof value !== 'string' || value.trim().length === 0) {
                    return Promise.resolve();
                  }

                  if (toPositiveLongString(value)) {
                    return Promise.resolve();
                  }

                  return Promise.reject(new Error('请输入有效的目标用户 ID'));
                },
              },
            ]}
          >
            <Input
              placeholder="输入目标用户 ID，或从上方队列 / 日志一键带入"
              onChange={onTargetUserInputChange}
              onBlur={onRefreshStatus}
              onPressEnter={onRefreshStatus}
            />
          </Form.Item>

          <Form.Item
            name="sourceReportId"
            label="关联举报单 ID"
            rules={[
              {
                validator: (_, value) => {
                  if (value === undefined || value === null || String(value).trim().length === 0) {
                    return Promise.resolve();
                  }

                  return toPositiveLongString(String(value))
                    ? Promise.resolve()
                    : Promise.reject(new Error('请输入有效的关联举报单 ID'));
                },
              },
            ]}
          >
            <Input placeholder="可选，保留动作与举报单的关联" />
          </Form.Item>
        </div>

        <div className="moderation-manual-action-form__grid">
          <Form.Item name="actionType" label="治理动作" rules={[{ required: true, message: '请选择治理动作' }]}>
            <Select options={MANUAL_ACTION_OPTIONS} placeholder="选择禁言、封禁或解除动作" />
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
                  label={currentActionType === MANUAL_ACTION_TYPE.mute ? '持续时长（小时）' : '持续时长（小时，可留空表示永久封禁）'}
                  rules={currentActionType === MANUAL_ACTION_TYPE.mute
                    ? [{ required: true, message: '请输入禁言时长' }]
                    : []}
                >
                  <InputNumber min={1} max={720} className="moderation-full-width-control" placeholder={currentActionType === MANUAL_ACTION_TYPE.mute ? '例如 24' : '留空表示永久封禁'} />
                </Form.Item>
              );
            }}
          </Form.Item>
        </div>

        <Form.Item name="reason" label="动作原因">
          <Input.TextArea rows={4} maxLength={500} showCount placeholder="补充人工治理依据；若从上方队列或日志带入，会自动预填推荐说明。" />
        </Form.Item>

        <div className="moderation-manual-action-form__footnote">
          禁言必须填写时长，封禁可留空表示永久；解除禁言 / 解除封禁会记录新的治理动作单，并自动回跳到对应日志。
        </div>
      </Form>
    </section>
  );
}
