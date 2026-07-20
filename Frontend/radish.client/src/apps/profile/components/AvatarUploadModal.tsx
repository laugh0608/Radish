import React, { useCallback, useLayoutEffect, useRef, useState } from 'react';
import { Modal } from '@radish/ui/modal';
import { ImageCropper } from '@radish/ui/image-cropper';
import { attachmentImageAccept, isSupportedAttachmentImageFile } from '@radish/ui';
import { setMyAvatar, uploadImage } from '@/api/attachment';
import { resolveAttachmentUploadErrorMessage } from '@/attachments/attachmentPresentation';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import styles from './AvatarUploadModal.module.css';

interface AvatarUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const AvatarUploadModal: React.FC<AvatarUploadModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { t } = useTranslation();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCropper, setShowCropper] = useState(false);
  const [cropperProcessing, setCropperProcessing] = useState(false);
  const mountedRef = useRef(false);
  const operationGenerationRef = useRef(0);
  const uploadingRef = useRef(false);
  const cropperProcessingRef = useRef(false);
  const activeRequestControllerRef = useRef<AbortController | null>(null);

  const invalidateActiveOperation = useCallback(() => {
    operationGenerationRef.current += 1;
    activeRequestControllerRef.current?.abort();
    activeRequestControllerRef.current = null;
  }, []);

  useLayoutEffect(() => {
    mountedRef.current = true;

    return () => {
      mountedRef.current = false;
      invalidateActiveOperation();
      uploadingRef.current = false;
      cropperProcessingRef.current = false;
    };
  }, [invalidateActiveOperation]);

  useLayoutEffect(() => {
    if (isOpen) {
      return;
    }

    invalidateActiveOperation();
    uploadingRef.current = false;
    cropperProcessingRef.current = false;
    setUploading(false);
    setCropperProcessing(false);
    setSelectedFile(null);
    setShowCropper(false);
    setError(null);
  }, [invalidateActiveOperation, isOpen]);

  const interactionLocked = uploading || cropperProcessing;

  const isOperationCurrent = (operationGeneration: number): boolean => (
    mountedRef.current && operationGeneration === operationGenerationRef.current
  );

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (uploadingRef.current || cropperProcessingRef.current) {
      return;
    }

    const file = e.target.files?.[0];
    if (!file) return;

    if (!isSupportedAttachmentImageFile(file)) {
      setError(t('profile.avatar.selectImageFile'));
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setError(t('profile.avatar.fileTooLarge'));
      return;
    }

    invalidateActiveOperation();
    setSelectedFile(file);
    setShowCropper(true);
    setError(null);
  };

  const handleCropComplete = async (croppedBlob: Blob) => {
    if (!mountedRef.current || uploadingRef.current) {
      return;
    }

    const operationGeneration = operationGenerationRef.current + 1;
    operationGenerationRef.current = operationGeneration;
    const requestController = new AbortController();
    activeRequestControllerRef.current = requestController;
    let failureFallback = t('profile.avatar.uploadFailed');
    let succeeded = false;

    try {
      uploadingRef.current = true;
      setUploading(true);
      setError(null);

      const croppedFile = new File([croppedBlob], 'avatar.jpg', {
        type: 'image/jpeg',
        lastModified: Date.now(),
      });

      log.debug('AvatarUploadModal', '开始上传裁切后的头像');

      const uploadResult = await uploadImage(
        {
          file: croppedFile,
          businessType: 'Avatar',
          generateThumbnail: true,
          generateMultipleSizes: false,
          addWatermark: false,
          removeExif: true,
          signal: requestController.signal,
        },
        t
      );

      if (!isOperationCurrent(operationGeneration)) {
        return;
      }

      log.debug('AvatarUploadModal', '图片上传成功:', uploadResult);

      const attachmentId = uploadResult.voId;
      failureFallback = t('profile.avatar.setAvatarFailed');
      await setMyAvatar(String(attachmentId), t, requestController.signal);

      if (!isOperationCurrent(operationGeneration)) {
        return;
      }

      log.debug('AvatarUploadModal', '头像设置成功');
      succeeded = true;
    } catch (error) {
      if (!isOperationCurrent(operationGeneration)) {
        return;
      }

      log.error('AvatarUploadModal', '上传头像失败:', error);
      setError(resolveAttachmentUploadErrorMessage(error, failureFallback));
      setSelectedFile(null);
      setShowCropper(false);
      cropperProcessingRef.current = false;
      setCropperProcessing(false);
    } finally {
      if (isOperationCurrent(operationGeneration)) {
        uploadingRef.current = false;
        setUploading(false);
      }

      if (activeRequestControllerRef.current === requestController) {
        activeRequestControllerRef.current = null;
      }
    }

    if (succeeded && isOperationCurrent(operationGeneration)) {
      onSuccess();
    }
  };

  const handleCropCancel = () => {
    if (uploadingRef.current || cropperProcessingRef.current) {
      return;
    }

    invalidateActiveOperation();
    setShowCropper(false);
    setSelectedFile(null);
  };

  const handleCropperProcessingChange = (processing: boolean) => {
    if (!mountedRef.current) {
      return;
    }

    cropperProcessingRef.current = processing;
    setCropperProcessing(processing);
  };

  const handleRemoveAvatar = async () => {
    if (uploadingRef.current || cropperProcessingRef.current) {
      return;
    }

    const operationGeneration = operationGenerationRef.current + 1;
    operationGenerationRef.current = operationGeneration;
    const requestController = new AbortController();
    activeRequestControllerRef.current = requestController;
    const failureFallback = t('profile.avatar.removeFailed');
    let succeeded = false;

    try {
      uploadingRef.current = true;
      setUploading(true);
      setError(null);

      await setMyAvatar(null, t, requestController.signal);

      if (!isOperationCurrent(operationGeneration)) {
        return;
      }

      log.debug('AvatarUploadModal', '头像移除成功');
      succeeded = true;
    } catch (error) {
      if (!isOperationCurrent(operationGeneration)) {
        return;
      }

      log.error('AvatarUploadModal', '移除头像失败:', error);
      setError(resolveAttachmentUploadErrorMessage(error, failureFallback));
    } finally {
      if (isOperationCurrent(operationGeneration)) {
        uploadingRef.current = false;
        setUploading(false);
      }

      if (activeRequestControllerRef.current === requestController) {
        activeRequestControllerRef.current = null;
      }
    }

    if (succeeded && isOperationCurrent(operationGeneration)) {
      onSuccess();
    }
  };

  const handleClose = () => {
    if (uploadingRef.current || cropperProcessingRef.current) {
      return;
    }

    invalidateActiveOperation();
    setSelectedFile(null);
    setShowCropper(false);
    setError(null);
    onClose();
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      closeLabel={t('common.close')}
      title={t('profile.avatar.title')}
      closeDisabled={interactionLocked}
      closeOnOverlayClick={!interactionLocked}
      closeOnEscape={!interactionLocked}
    >
      <div className={styles.container}>
        {!showCropper ? (
          <div className={styles.uploadSection}>
            <div className={styles.uploadArea}>
              <input
                type="file"
                accept={attachmentImageAccept}
                onChange={handleFileSelect}
                className={styles.fileInput}
                id="avatar-upload"
                disabled={uploading}
              />
              <label htmlFor="avatar-upload" className={styles.uploadLabel}>
                <div className={styles.uploadIcon}>📷</div>
                <div className={styles.uploadText}>{t('profile.avatar.selectImage')}</div>
                <div className={styles.uploadHint}>{t('profile.avatar.uploadHint')}</div>
              </label>
            </div>

            {error && <div className={styles.error}>{error}</div>}

            <div className={styles.actions}>
              <button
                type="button"
                onClick={handleRemoveAvatar}
                className={styles.removeButton}
                disabled={uploading}
              >
                {uploading ? t('profile.avatar.processing') : t('profile.avatar.removeAvatar')}
              </button>
            </div>
          </div>
        ) : (
          <div className={styles.cropperSection}>
            {selectedFile && (
              <ImageCropper
                image={selectedFile}
                labels={{
                  zoom: t('profile.avatar.cropZoom'),
                  cancel: t('common.cancel'),
                  confirm: t('common.confirm'),
                  cropFailed: t('profile.avatar.cropFailed'),
                }}
                aspect={1}
                onCropComplete={handleCropComplete}
                onCancel={handleCropCancel}
                onProcessingChange={handleCropperProcessingChange}
                onError={(cropError) => {
                  log.error('AvatarUploadModal', '裁切头像失败:', cropError);
                }}
              />
            )}
            {uploading && (
              <div className={styles.uploadingOverlay}>
                <div className={styles.uploadingText}>{t('profile.avatar.uploading')}</div>
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
};
