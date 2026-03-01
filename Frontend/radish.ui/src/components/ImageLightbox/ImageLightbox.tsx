import { useEffect, useMemo, useState } from 'react';
import { Icon } from '../Icon/Icon';
import styles from './ImageLightbox.module.css';

export interface LightboxImageItem {
  src: string;
  alt?: string;
}

interface ImageLightboxProps {
  isOpen: boolean;
  images: LightboxImageItem[];
  initialIndex?: number;
  onClose: () => void;
}

export const ImageLightbox = ({
  isOpen,
  images,
  initialIndex = 0,
  onClose,
}: ImageLightboxProps) => {
  const safeInitialIndex = useMemo(() => {
    if (images.length === 0) return 0;
    if (initialIndex < 0) return 0;
    if (initialIndex >= images.length) return images.length - 1;
    return initialIndex;
  }, [images.length, initialIndex]);

  const [currentIndex, setCurrentIndex] = useState(safeInitialIndex);

  useEffect(() => {
    if (!isOpen) return;
    setCurrentIndex(safeInitialIndex);
  }, [isOpen, safeInitialIndex]);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
        return;
      }

      if (images.length <= 1) return;

      if (event.key === 'ArrowLeft') {
        setCurrentIndex((prev) => (prev - 1 + images.length) % images.length);
      }

      if (event.key === 'ArrowRight') {
        setCurrentIndex((prev) => (prev + 1) % images.length);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [images.length, isOpen, onClose]);

  if (!isOpen || images.length === 0) {
    return null;
  }

  const currentImage = images[currentIndex];

  const handlePrev = () => {
    setCurrentIndex((prev) => (prev - 1 + images.length) % images.length);
  };

  const handleNext = () => {
    setCurrentIndex((prev) => (prev + 1) % images.length);
  };

  return (
    <div className={styles.overlay} role="dialog" aria-modal="true" onClick={onClose}>
      <div className={styles.content} onClick={(event) => event.stopPropagation()}>
        <img
          src={currentImage.src}
          alt={currentImage.alt || `image-${currentIndex + 1}`}
          className={styles.image}
        />

        <button type="button" className={styles.close} onClick={onClose} aria-label="关闭预览">
          <Icon icon="mdi:close" size={18} />
        </button>

        {images.length > 1 && (
          <>
            <button type="button" className={`${styles.nav} ${styles.navLeft}`} onClick={handlePrev} aria-label="上一张">
              <Icon icon="mdi:chevron-left" size={22} />
            </button>
            <button type="button" className={`${styles.nav} ${styles.navRight}`} onClick={handleNext} aria-label="下一张">
              <Icon icon="mdi:chevron-right" size={22} />
            </button>
            <div className={styles.counter}>{currentIndex + 1} / {images.length}</div>
          </>
        )}
      </div>
    </div>
  );
};
