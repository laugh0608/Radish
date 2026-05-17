import {
  type ClipboardEvent,
  type CSSProperties,
  type KeyboardEvent,
  useEffect,
  useId,
  useRef,
  useState,
} from 'react';
import './PasscodeInput.css';

export interface PasscodeInputProps {
  value: string;
  onChange: (value: string) => void;
  length?: number;
  error?: string;
  helperText?: string;
  disabled?: boolean;
  autoFocus?: boolean;
  autoComplete?: string;
  id?: string;
  name?: string;
  className?: string;
  revealText?: string;
  concealText?: string;
  showRevealToggle?: boolean;
  onEnterPress?: () => void;
}

function sanitizeDigits(value: string): string {
  return value.replace(/\D/g, '');
}

export function PasscodeInput({
  value,
  onChange,
  length = 6,
  error,
  helperText,
  disabled = false,
  autoFocus = false,
  autoComplete = 'off',
  id,
  name,
  className = '',
  revealText = '显示',
  concealText = '隐藏',
  showRevealToggle = true,
  onEnterPress,
}: PasscodeInputProps) {
  const [revealed, setRevealed] = useState(false);
  const inputRefs = useRef<Array<HTMLInputElement | null>>([]);
  const descriptionId = useId();
  const digits = Array.from({ length }, (_, index) => value[index] ?? '');

  useEffect(() => {
    if (!autoFocus || disabled) {
      return;
    }

    const firstEmptyIndex = digits.findIndex((digit) => digit === '');
    const targetIndex = firstEmptyIndex >= 0 ? firstEmptyIndex : length - 1;
    inputRefs.current[targetIndex]?.focus();
    inputRefs.current[targetIndex]?.select();
  }, [autoFocus, digits, disabled, length]);

  const wrapperStyle = {
    '--radish-passcode-length': String(length),
  } as CSSProperties;

  const wrapperClasses = [
    'radish-passcode-input',
    error && 'radish-passcode-input--error',
    disabled && 'radish-passcode-input--disabled',
    className,
  ]
    .filter(Boolean)
    .join(' ');

  const focusInput = (index: number) => {
    if (index < 0 || index >= length) {
      return;
    }

    const target = inputRefs.current[index];
    target?.focus();
    target?.select();
  };

  const emitValue = (nextDigits: string[]) => {
    onChange(nextDigits.join('').slice(0, length));
  };

  const fillDigits = (startIndex: number, rawValue: string) => {
    const incomingDigits = sanitizeDigits(rawValue).slice(0, length - startIndex).split('');
    if (incomingDigits.length === 0) {
      return;
    }

    const nextDigits = digits.slice();
    incomingDigits.forEach((digit, offset) => {
      nextDigits[startIndex + offset] = digit;
    });

    emitValue(nextDigits);

    const focusIndex = startIndex + incomingDigits.length;
    requestAnimationFrame(() => {
      if (focusIndex < length) {
        focusInput(focusIndex);
        return;
      }

      focusInput(length - 1);
    });
  };

  const handleDigitChange = (index: number, nextValue: string) => {
    const incomingDigits = sanitizeDigits(nextValue);
    if (incomingDigits.length === 0) {
      const nextDigits = digits.slice();
      nextDigits[index] = '';
      emitValue(nextDigits);
      return;
    }

    if (incomingDigits.length > 1) {
      fillDigits(index, incomingDigits);
      return;
    }

    const nextDigits = digits.slice();
    nextDigits[index] = incomingDigits;
    emitValue(nextDigits);

    if (index < length - 1) {
      requestAnimationFrame(() => focusInput(index + 1));
    }
  };

  const handleKeyDown = (index: number, event: KeyboardEvent<HTMLInputElement>) => {
    switch (event.key) {
      case 'Backspace': {
        event.preventDefault();
        if (digits[index]) {
          const nextDigits = digits.slice();
          nextDigits[index] = '';
          emitValue(nextDigits);
          return;
        }

        if (index > 0) {
          const nextDigits = digits.slice();
          nextDigits[index - 1] = '';
          emitValue(nextDigits);
          requestAnimationFrame(() => focusInput(index - 1));
        }
        return;
      }
      case 'Delete': {
        event.preventDefault();
        const nextDigits = digits.slice();
        nextDigits[index] = '';
        emitValue(nextDigits);
        return;
      }
      case 'ArrowLeft':
        event.preventDefault();
        focusInput(index - 1);
        return;
      case 'ArrowRight':
        event.preventDefault();
        focusInput(index + 1);
        return;
      case 'Home':
        event.preventDefault();
        focusInput(0);
        return;
      case 'End':
        event.preventDefault();
        focusInput(length - 1);
        return;
      case 'Enter':
        onEnterPress?.();
        return;
      default:
        if (event.key.length === 1 && /\D/.test(event.key)) {
          event.preventDefault();
        }
    }
  };

  const handlePaste = (index: number, event: ClipboardEvent<HTMLInputElement>) => {
    event.preventDefault();
    fillDigits(index, event.clipboardData.getData('text'));
  };

  return (
    <div className={wrapperClasses} style={wrapperStyle}>
      <div className="radish-passcode-input__controls">
        <div className="radish-passcode-input__digits" role="group" aria-invalid={Boolean(error)}>
          {digits.map((digit, index) => (
            <input
              key={`${name ?? id ?? 'passcode'}-${index}`}
              ref={(element) => {
                inputRefs.current[index] = element;
              }}
              id={index === 0 ? id : undefined}
              name={index === 0 ? name : undefined}
              type={revealed ? 'text' : 'password'}
              className="radish-passcode-input__digit"
              value={digit}
              onChange={(event) => handleDigitChange(index, event.target.value)}
              onKeyDown={(event) => handleKeyDown(index, event)}
              onPaste={(event) => handlePaste(index, event)}
              onFocus={(event) => event.currentTarget.select()}
              inputMode="numeric"
              autoComplete={autoComplete}
              aria-label={`Passcode digit ${index + 1}`}
              aria-describedby={error || helperText ? descriptionId : undefined}
              disabled={disabled}
              maxLength={1}
            />
          ))}
        </div>

        {showRevealToggle && (
          <button
            type="button"
            className="radish-passcode-input__toggle"
            onClick={() => setRevealed((current) => !current)}
            disabled={disabled}
          >
            {revealed ? concealText : revealText}
          </button>
        )}
      </div>

      {error ? (
        <span id={descriptionId} className="radish-passcode-input__error">
          {error}
        </span>
      ) : helperText ? (
        <span id={descriptionId} className="radish-passcode-input__helper">
          {helperText}
        </span>
      ) : null}
    </div>
  );
}
