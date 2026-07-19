import { type ReactNode, type MouseEvent, useEffect, useId, useRef } from 'react';
import './BottomSheet.css';

const FOCUSABLE_SELECTOR = [
  'button:not([disabled])',
  'a[href]',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

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
  const sheetRef = useRef<HTMLDivElement | null>(null);
  const restoreFocusRef = useRef<HTMLElement | null>(null);
  const onCloseRef = useRef(onClose);
  const titleId = useId();

  useEffect(() => {
    onCloseRef.current = onClose;
  }, [onClose]);

  useEffect(() => {
    if (!isOpen) return;

    restoreFocusRef.current = document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
    const focusFrame = window.requestAnimationFrame(() => {
      const focusable = sheetRef.current?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR);
      (focusable ?? sheetRef.current)?.focus();
    });

    const handleDialogKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && closeOnEscape) {
        e.preventDefault();
        onCloseRef.current();
        return;
      }

      if (e.key !== 'Tab' || !sheetRef.current) {
        return;
      }

      const focusable = Array.from(
        sheetRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)
      ).filter((element) => element.getClientRects().length > 0);
      if (focusable.length === 0) {
        e.preventDefault();
        sheetRef.current.focus();
        return;
      }

      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (!sheetRef.current.contains(document.activeElement)) {
        e.preventDefault();
        first.focus();
      } else if (e.shiftKey && document.activeElement === first) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', handleDialogKeyDown);
    return () => {
      window.cancelAnimationFrame(focusFrame);
      document.removeEventListener('keydown', handleDialogKeyDown);
      restoreFocusRef.current?.focus();
      restoreFocusRef.current = null;
    };
  }, [isOpen, closeOnEscape]);

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
      <div
        ref={sheetRef}
        className={`radish-bottom-sheet ${className}`.trim()}
        style={{ height }}
        role="dialog"
        aria-modal="true"
        aria-labelledby={title ? titleId : undefined}
        aria-label={title ? undefined : closeLabel}
        tabIndex={-1}
      >
        <div className="radish-bottom-sheet-handle" aria-hidden="true" />
        {title && (
          <div className="radish-bottom-sheet-header">
            <h2 id={titleId} className="radish-bottom-sheet-title">{title}</h2>
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
