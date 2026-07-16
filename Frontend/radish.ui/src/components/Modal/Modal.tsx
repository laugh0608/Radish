import { type ReactNode, useEffect } from 'react';
import './Modal.css';

export interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  closeLabel: string;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  size?: 'small' | 'medium' | 'large';
  closeOnOverlayClick?: boolean;
  closeOnEscape?: boolean;
  closeDisabled?: boolean;
}

export const Modal = ({
  isOpen,
  onClose,
  closeLabel,
  title,
  children,
  footer,
  size = 'medium',
  closeOnOverlayClick = true,
  closeOnEscape = true,
  closeDisabled = false,
}: ModalProps) => {
  useEffect(() => {
    if (!isOpen || !closeOnEscape || closeDisabled) return;

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen, closeOnEscape, closeDisabled, onClose]);

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }

    return () => {
      document.body.style.overflow = '';
    };
  }, [isOpen]);

  if (!isOpen) return null;

  const handleOverlayClick = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!closeDisabled && closeOnOverlayClick && e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <div className="radish-modal-overlay" onClick={handleOverlayClick}>
      <div className={`radish-modal radish-modal--${size}`}>
        {title && (
          <div className="radish-modal-header">
            <h2 className="radish-modal-title">{title}</h2>
            <button
              type="button"
              className="radish-modal-close"
              onClick={onClose}
              aria-label={closeLabel}
              title={closeLabel}
              disabled={closeDisabled}
            >
              ×
            </button>
          </div>
        )}
        <div className="radish-modal-body">{children}</div>
        {footer && <div className="radish-modal-footer">{footer}</div>}
      </div>
    </div>
  );
};
