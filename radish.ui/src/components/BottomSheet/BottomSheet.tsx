import { type ReactNode, type MouseEvent, useEffect } from 'react';
import './BottomSheet.css';

export interface BottomSheetProps {
  isOpen: boolean;
  onClose: () => void;
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  height?: '60%' | '70%' | '80%' | '90%';
  closeOnOverlayClick?: boolean;
  closeOnEscape?: boolean;
}

export const BottomSheet = ({
  isOpen,
  onClose,
  title,
  children,
  footer,
  height = '70%',
  closeOnOverlayClick = true,
  closeOnEscape = true
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
    <div className="radish-bottom-sheet-overlay" onClick={handleOverlayClick}>
      <div className="radish-bottom-sheet" style={{ height }}>
        <div className="radish-bottom-sheet-handle" aria-hidden="true" />
        {title && (
          <div className="radish-bottom-sheet-header">
            <h2 className="radish-bottom-sheet-title">{title}</h2>
            <button
              className="radish-bottom-sheet-close"
              onClick={onClose}
              aria-label="关闭"
            >
              ×
            </button>
          </div>
        )}
        <div className="radish-bottom-sheet-body">{children}</div>
        {footer && <div className="radish-bottom-sheet-footer">{footer}</div>}
      </div>
    </div>
  );
};
