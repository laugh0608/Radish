import { type ReactNode, type MouseEvent, useEffect } from 'react';
import './BottomSheet.css';

export interface BottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
  closeLabel: string;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  height?: string;
  closeOnOverlayClick?: boolean;
  closeOnEscape?: boolean;
  className?: string;
  bodyClassName?: string;
  footerClassName?: string;
  overlayClassName?: string;
}

export const BottomSheet = ({
  isOpen,
  onClose,
  closeLabel,
  title,
  children,
  footer,
  height = '70%',
  closeOnOverlayClick = true,
  closeOnEscape = true,
  className = '',
  bodyClassName = '',
  footerClassName = '',
  overlayClassName = ''
}: BottomSheetProps) => {
  useEffect(() => {
    if (!isOpen || !closeOnEscape) return;

    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen, closeOnEscape, onClose]);

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

  const handleOverlayClick = (e: MouseEvent<HTMLDivElement>) => {
    if (closeOnOverlayClick && e.target === e.currentTarget) {
      onClose();
    }
  };

  return (
    <div className={`radish-bottom-sheet-overlay ${overlayClassName}`.trim()} onClick={handleOverlayClick}>
      <div className={`radish-bottom-sheet ${className}`.trim()} style={{ height }}>
        <div className="radish-bottom-sheet-handle" aria-hidden="true" />
        {title && (
          <div className="radish-bottom-sheet-header">
            <h2 className="radish-bottom-sheet-title">{title}</h2>
            <button
              type="button"
              className="radish-bottom-sheet-close"
              onClick={onClose}
              aria-label={closeLabel}
              title={closeLabel}
            >
              ×
            </button>
          </div>
        )}
        <div className={`radish-bottom-sheet-body ${bodyClassName}`.trim()}>{children}</div>
        {footer && <div className={`radish-bottom-sheet-footer ${footerClassName}`.trim()}>{footer}</div>}
      </div>
    </div>
  );
};
