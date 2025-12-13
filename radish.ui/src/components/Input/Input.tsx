import { type InputHTMLAttributes, forwardRef } from 'react';
import './Input.css';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
  helperText?: string;
  fullWidth?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, helperText, fullWidth = false, className = '', ...props }, ref) => {
    const wrapperClasses = [
      'radish-input-wrapper',
      fullWidth && 'radish-input-wrapper--full-width',
      error && 'radish-input-wrapper--error'
    ].filter(Boolean).join(' ');

    const inputClasses = [
      'radish-input',
      error && 'radish-input--error',
      className
    ].filter(Boolean).join(' ');

    return (
      <div className={wrapperClasses}>
        {label && (
          <label className="radish-input-label">
            {label}
            {props.required && <span className="radish-input-required">*</span>}
          </label>
        )}
        <input ref={ref} className={inputClasses} {...props} />
        {error && <span className="radish-input-error">{error}</span>}
        {!error && helperText && <span className="radish-input-helper">{helperText}</span>}
      </div>
    );
  }
);

Input.displayName = 'Input';
