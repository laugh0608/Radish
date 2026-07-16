import React, { useState, useCallback, useId, useRef } from 'react';
import Cropper from 'react-easy-crop';
import type { Area, Point } from 'react-easy-crop';
import styles from './ImageCropper.module.css';

export interface ImageCropperLabels {
  zoom: string;
  cancel: string;
  confirm: string;
  cropFailed: string;
}

export interface ImageCropperProps {
  image: string | File;
  labels: ImageCropperLabels;
  aspect?: number;
  onCropComplete: (croppedBlob: Blob) => void | Promise<void>;
  onCancel: () => void;
  onError?: (error: unknown) => void;
}

export const ImageCropper: React.FC<ImageCropperProps> = ({
  image,
  labels,
  aspect = 1,
  onCropComplete,
  onCancel,
  onError,
}) => {
  const [crop, setCrop] = useState<Point>({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState<Area | null>(null);
  const [imageUrl, setImageUrl] = useState<string>('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [processing, setProcessing] = useState(false);
  const processingRef = useRef(false);
  const zoomInputId = useId();

  React.useEffect(() => {
    if (typeof image === 'string') {
      setImageUrl(image);
    } else {
      const url = URL.createObjectURL(image);
      setImageUrl(url);
      return () => URL.revokeObjectURL(url);
    }
  }, [image]);

  const onCropCompleteCallback = useCallback(
    (_croppedArea: Area, croppedAreaPixels: Area) => {
      setCroppedAreaPixels(croppedAreaPixels);
    },
    []
  );

  const handleConfirm = async () => {
    if (!croppedAreaPixels || processingRef.current) return;

    processingRef.current = true;
    setProcessing(true);
    setErrorMessage(null);
    try {
      const croppedBlob = await getCroppedImg(imageUrl, croppedAreaPixels);
      await onCropComplete(croppedBlob);
    } catch (error) {
      setErrorMessage(labels.cropFailed);
      onError?.(error);
    } finally {
      processingRef.current = false;
      setProcessing(false);
    }
  };

  return (
    <div className={styles.container} aria-busy={processing}>
      <div
        className={processing
          ? `${styles.cropperWrapper} ${styles.cropperWrapperProcessing}`
          : styles.cropperWrapper}
        aria-disabled={processing}
      >
        <Cropper
          image={imageUrl}
          crop={crop}
          zoom={zoom}
          aspect={aspect}
          onCropChange={setCrop}
          onZoomChange={setZoom}
          onCropComplete={onCropCompleteCallback}
        />
      </div>
      <div className={styles.controls}>
        <div className={styles.controlGroup}>
          <label className={styles.controlLabel} htmlFor={zoomInputId}>{labels.zoom}</label>
          <input
            id={zoomInputId}
            type="range"
            min={1}
            max={3}
            step={0.1}
            value={zoom}
            onChange={(e) => setZoom(Number(e.target.value))}
            className={styles.slider}
            disabled={processing}
          />
        </div>
      </div>
      {errorMessage && (
        <div className={styles.error} role="alert">
          {errorMessage}
        </div>
      )}
      <div className={styles.actions}>
        <button type="button" onClick={onCancel} className={styles.cancelButton} disabled={processing}>
          {labels.cancel}
        </button>
        <button
          type="button"
          onClick={handleConfirm}
          className={styles.confirmButton}
          disabled={processing || !croppedAreaPixels}
        >
          {labels.confirm}
        </button>
      </div>
    </div>
  );
};

async function getCroppedImg(imageSrc: string, pixelCrop: Area): Promise<Blob> {
  const image = await createImage(imageSrc);
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');

  if (!ctx) {
    throw new Error('Unable to get the canvas rendering context.');
  }

  canvas.width = pixelCrop.width;
  canvas.height = pixelCrop.height;

  ctx.drawImage(
    image,
    pixelCrop.x,
    pixelCrop.y,
    pixelCrop.width,
    pixelCrop.height,
    0,
    0,
    pixelCrop.width,
    pixelCrop.height
  );

  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob) {
        resolve(blob);
      } else {
        reject(new Error('The canvas could not produce an image blob.'));
      }
    }, 'image/jpeg', 0.95);
  });
}

function createImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.addEventListener('load', () => resolve(image));
    image.addEventListener('error', (error) => reject(error));
    image.setAttribute('crossOrigin', 'anonymous');
    image.src = url;
  });
}
