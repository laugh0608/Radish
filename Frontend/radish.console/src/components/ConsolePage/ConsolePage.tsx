import type { ReactNode } from 'react';
import './ConsolePage.css';

export type ConsoleStatusTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

export interface ConsolePageHeaderProps {
  title: string;
  description?: string;
  eyebrow?: string;
  icon?: ReactNode;
  status?: ReactNode;
  actions?: ReactNode;
}

export interface ConsoleStatusChipProps {
  children: ReactNode;
  tone?: ConsoleStatusTone;
}

export interface ConsoleMetricGridProps {
  children: ReactNode;
  label?: string;
}

export interface ConsoleMetricCardProps {
  label: string;
  value: ReactNode;
  description?: string;
  tone?: ConsoleStatusTone;
}

export interface ConsoleToolbarProps {
  title: string;
  description?: string;
  meta?: ReactNode;
  actions?: ReactNode;
  children?: ReactNode;
}

export function ConsoleStatusChip({ children, tone = 'neutral' }: ConsoleStatusChipProps) {
  return (
    <span className={`console-status-chip console-status-chip--${tone}`}>
      {children}
    </span>
  );
}

export function ConsolePageHeader({ title, description, eyebrow, icon, status, actions }: ConsolePageHeaderProps) {
  return (
    <section className="console-page-header">
      <div className="console-page-header__main">
        {icon ? <span className="console-page-header__icon">{icon}</span> : null}
        <div className="console-page-header__copy">
          {eyebrow ? <span className="console-page-header__eyebrow">{eyebrow}</span> : null}
          <div className="console-page-header__title-row">
            <h1>{title}</h1>
            {status}
          </div>
          {description ? <p>{description}</p> : null}
        </div>
      </div>
      {actions ? <div className="console-page-header__actions">{actions}</div> : null}
    </section>
  );
}

export function ConsoleMetricGrid({ children, label }: ConsoleMetricGridProps) {
  return (
    <section className="console-metric-grid" aria-label={label}>
      {children}
    </section>
  );
}

export function ConsoleMetricCard({ label, value, description, tone = 'neutral' }: ConsoleMetricCardProps) {
  return (
    <article className={`console-metric-card console-metric-card--${tone}`}>
      <span className="console-metric-card__label">{label}</span>
      <strong>{value}</strong>
      {description ? <span className="console-metric-card__description">{description}</span> : null}
    </article>
  );
}

export function ConsoleToolbar({ title, description, meta, actions, children }: ConsoleToolbarProps) {
  return (
    <section className="console-toolbar">
      <div className="console-toolbar__header">
        <div className="console-toolbar__copy">
          <h2>{title}</h2>
          {description ? <p>{description}</p> : null}
        </div>
        <div className="console-toolbar__meta">
          {meta}
          {actions}
        </div>
      </div>
      {children ? <div className="console-toolbar__body">{children}</div> : null}
    </section>
  );
}
