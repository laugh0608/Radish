import { useEffect, useState } from 'react';
import { Icon as IconifyIcon, getIcon, addCollection } from '@iconify/react';
import type { IconProps as IconifyIconProps } from '@iconify/react';
import './Icon.css';

let mdiCollectionLoaded = false;
let mdiCollectionLoadingPromise: Promise<void> | null = null;

async function ensureMdiCollectionLoaded(): Promise<void> {
  if (mdiCollectionLoaded) {
    return;
  }

  if (!mdiCollectionLoadingPromise) {
    mdiCollectionLoadingPromise = import('./mdi-subset.json')
      .then((module) => {
        addCollection(module.default as any);
        mdiCollectionLoaded = true;
      })
      .finally(() => {
        mdiCollectionLoadingPromise = null;
      });
  }

  await mdiCollectionLoadingPromise;
}

export interface IconProps extends Omit<IconifyIconProps, 'icon'> {
  icon: string;
  size?: number | string;
  color?: string;
}

export const Icon = ({
  icon,
  size = 24,
  color = 'currentColor',
  className,
  style,
  ...props
}: IconProps) => {
  const [, setCollectionVersion] = useState(0);

  useEffect(() => {
    if (typeof icon !== 'string' || !icon.startsWith('mdi:') || mdiCollectionLoaded) {
      return;
    }

    void ensureMdiCollectionLoaded().then(() => {
      setCollectionVersion((version) => version + 1);
    });
  }, [icon]);

  const resolveIcon = () => {
    if (typeof icon !== 'string') {
      return icon;
    }

    const resolved = getIcon(icon);
    return resolved || icon;
  };

  const mergedClassName = ['radish-icon', className].filter(Boolean).join(' ');

  return (
    <IconifyIcon
      icon={resolveIcon()}
      width={size}
      height={size}
      color={color || undefined}
      inline={true}
      className={mergedClassName}
      style={style}
      {...props}
    />
  );
};
