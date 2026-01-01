import { type ButtonHTMLAttributes, type ReactNode } from 'react';
import './Button.css';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'small' | 'medium' | 'large';
  icon?: ReactNode;
  children: ReactNode;
}

export const Button = ({
  variant = 'primary',
  size = 'medium',
  icon,
  className = '',
  children,
  ...props
}: ButtonProps) => {
  const classes = [
    'radish-button',
    `radish-button--${variant}`,
    `radish-button--${size}`,
    className
  ].filter(Boolean).join(' ');

  return (
    <button className={classes} {...props}>
      {icon && <span className="radish-button__icon">{icon}</span>}
      {children}
    </button>
  );
};
