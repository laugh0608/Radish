import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AntInput as Input, AntModal as Modal, Button, Space, message } from '@radish/ui';
import { userManagementApi } from '@/api/userManagement';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import { useUser } from '@/hooks/useUser';
import type { WebSessionRevocation } from '@/types/user';

interface Props {
  user: { uuid: string; displayName: string; tenantId: string; roleNames: string[]; isEnabled: boolean; revocation?: WebSessionRevocation };
  ready: boolean;
  reload: () => Promise<void>;
}

export const DisableAccountAction = ({ user, ready, reload }: Props) => {
  const { t } = useTranslation();
  const { user: operator } = useUser();
  const hasPermission = usePermission(CONSOLE_PERMISSIONS.usersDisable);
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const pending = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => { mounted.current = false; };
  }, []);

  const allowed = hasPermission && operator?.voTenantId === user.tenantId && operator.voUserId !== user.uuid &&
    !(user.tenantId === '0' && ['20000', '20001'].includes(user.uuid)) &&
    !user.roleNames.some(role => ['admin', 'system'].includes(role.trim().toLowerCase()));
  if (!allowed) return null;

  const submit = async () => {
    if (!allowed || !ready || !open || user.revocation || !reason.trim() || pending.current) return;
    pending.current = true;
    setBusy(true);
    try {
      const result = await userManagementApi.disableAccount(user.uuid, reason.trim());
      if (!mounted.current) return;
      if (!result.ok || !result.data) throw new Error(result.message || t('users.disable.failed'));
      setOpen(false);
      setReason('');
      await reload();
    } catch (error) {
      if (mounted.current) {
        message.error(error instanceof Error ? error.message : t('users.disable.failed'));
        await reload();
      }
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  };

  return (
    <section className="admin-feature-card">
      {user.revocation ? (
        <Space wrap>
          <span role="status">{t(`users.disable.${user.revocation}`)}</span>
          {user.revocation !== 'Completed' && <Button onClick={reload}>{t('users.disable.refresh')}</Button>}
        </Space>
      ) : (
        <Button variant="danger" disabled={!ready || busy} onClick={() => setOpen(true)}>
          {t(user.isEnabled ? 'users.disable.action' : 'users.disable.recover')}
        </Button>
      )}
      <Modal open={open && !user.revocation} title={t('users.disable.action')} okText={t('users.disable.confirm')}
        okButtonProps={{ danger: true, disabled: !ready || !reason.trim() }} confirmLoading={busy}
        onOk={submit} onCancel={() => { if (!pending.current) { setOpen(false); setReason(''); } }}>
        <p>{t('users.disable.explain', { name: user.displayName, id: user.uuid })}</p>
        <label htmlFor="disable-account-reason">{t('users.disable.reason')}</label>
        <Input.TextArea id="disable-account-reason" value={reason} maxLength={500} disabled={busy}
          onChange={event => setReason(event.target.value)} />
      </Modal>
    </section>
  );
};
