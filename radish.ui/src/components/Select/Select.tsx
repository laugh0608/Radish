import { type SelectHTMLAttributes, forwardRef } from 'react';
import './Select.css';

export interface SelectOption {
  value: string | number;
  label: string;
  disabled?: boolean;
}

export interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'size'> {
  label?: string;
  error?: string;
  helperText?: string;
  options: SelectOption[];
  placeholder?: string;
  fullWidth?: boolean;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ label, error, helperText, options, placeholder, fullWidth = false, className = '', ...props }, ref) => {
    const wrapperClasses = [
      'radish-select-wrapper',
      fullWidth && 'radish-select-wrapper--full-width',
      error && 'radish-select-wrapper--error'
    ].filter(Boolean).join(' ');

    const selectClasses = [
      'radish-select',
      error && 'radish-select--error',
      className
    ].filter(Boolean).join(' ');

    return (
      <div className={wrapperClasses}>
        {label && (
          <label className="radish-select-label">
            {label}
            {props.required && <span className="radish-select-required">*</span>}
          </label>
        )}
        <select ref={ref} className={selectClasses} {...props}>
          {placeholder && (
            <option value="" disabled>
              {placeholder}
            </option>
          )}
          {options.map((option) => (
            <option key={option.value} value={option.value} disabled={option.disabled}>
              {option.label}
            </option>
          ))}
        </select>
        {error && <span className="radish-select-error">{error}</span>}
        {!error && helperText && <span className="radish-select-helper">{helperText}</span>}
      </div>
    );
  }
);

Select.displayName = 'Select';
