import React, { useState } from 'react';
import { Modal, ImageCropper } from '@radish/ui';
import { uploadImage } from '@/api/attachment';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import styles from './AvatarUploadModal.module.css';

interface AvatarUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
  apiBaseUrl: string;
}

export const AvatarUploadModal: React.FC<AvatarUploadModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
  apiBaseUrl,
}) => {
  const { t } = useTranslation();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showCropper, setShowCropper] = useState(false);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // 验证文件类型
    if (!file.type.startsWith('image/')) {
      setError('请选择图片文件');
      return;
    }

    // 验证文件大小（限制 5MB）
    if (file.size > 5 * 1024 * 1024) {
      setError('图片大小不能超过 5MB');
      return;
    }

    setSelectedFile(file);
    setShowCropper(true);
    setError(null);
  };

  const handleCropComplete = async (croppedBlob: Blob) => {
    try {
      setUploading(true);
      setError(null);

      // 将 Blob 转换为 File
      const croppedFile = new File([croppedBlob], 'avatar.jpg', {
        type: 'image/jpeg',
        lastModified: Date.now(),
      });

      log.debug('AvatarUploadModal', '开始上传裁切后的头像');

      // 上传图片 - 修复：传递正确的参数格式
      const uploadResult = await uploadImage(
        {
          file: croppedFile,
          businessType: 'Avatar',
          generateThumbnail: true,
          generateMultipleSizes: false,
          addWatermark: false,
          removeExif: true,
        },
        t
      );
      log.debug('AvatarUploadModal', '图片上传成功:', uploadResult);

      // 设置头像
      const token = localStorage.getItem('access_token');
      const res = await fetch(`${apiBaseUrl}/api/v1/User/SetMyAvatar`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ attachmentId: uploadResult.id }),
      });

      const json = await res.json();
      log.debug('AvatarUploadModal', 'SetMyAvatar 响应:', json);

      if (!res.ok || !json.isSuccess) {
        throw new Error(json.message || '设置头像失败');
      }

      log.debug('AvatarUploadModal', '头像设置成功');
      onSuccess();
    } catch (error) {
      log.error('AvatarUploadModal', '上传头像失败:', error);
      setError(error instanceof Error ? error.message : '上传失败，请重试');
      setShowCropper(false);
    } finally {
      setUploading(false);
    }
  };

  const handleCropCancel = () => {
    setShowCropper(false);
    setSelectedFile(null);
  };

  const handleRemoveAvatar = async () => {
    try {
      setUploading(true);
      setError(null);

      const token = localStorage.getItem('access_token');
      const res = await fetch(`${apiBaseUrl}/api/v1/User/SetMyAvatar`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({ attachmentId: 0 }),
      });

      const json = await res.json();
      log.debug('AvatarUploadModal', '移除头像响应:', json);

      if (!res.ok || !json.isSuccess) {
        throw new Error(json.message || '移除头像失败');
      }

      log.debug('AvatarUploadModal', '头像移除成功');
      onSuccess();
    } catch (error) {
      log.error('AvatarUploadModal', '移除头像失败:', error);
      setError(error instanceof Error ? error.message : '移除失败，请重试');
    } finally {
      setUploading(false);
    }
  };

  const handleClose = () => {
    if (!uploading) {
      setSelectedFile(null);
      setShowCropper(false);
      setError(null);
      onClose();
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="更换头像">
      <div className={styles.container}>
        {!showCropper ? (
          <div className={styles.uploadSection}>
            <div className={styles.uploadArea}>
              <input
                type="file"
                accept="image/*"
                onChange={handleFileSelect}
                className={styles.fileInput}
                id="avatar-upload"
                disabled={uploading}
              />
              <label htmlFor="avatar-upload" className={styles.uploadLabel}>
                <div className={styles.uploadIcon}>📷</div>
                <div className={styles.uploadText}>点击选择图片</div>
                <div className={styles.uploadHint}>支持 JPG、PNG 格式，最大 5MB</div>
              </label>
            </div>

            {error && <div className={styles.error}>{error}</div>}

            <div className={styles.actions}>
              <button
                onClick={handleRemoveAvatar}
                className={styles.removeButton}
                disabled={uploading}
              >
                {uploading ? '处理中...' : '移除头像'}
              </button>
            </div>
          </div>
        ) : (
          <div className={styles.cropperSection}>
            {selectedFile && (
              <ImageCropper
                image={selectedFile}
                aspect={1}
                onCropComplete={handleCropComplete}
                onCancel={handleCropCancel}
              />
            )}
            {uploading && (
              <div className={styles.uploadingOverlay}>
                <div className={styles.uploadingText}>上传中...</div>
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
};
