import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Upload, Alert, Steps, Progress } from 'antd';
import type { RcFile, UploadChangeParam, UploadFile } from 'antd/es/upload/interface';
import {
  AntModal as Modal,
  Button,
  Space,
  Table,
  AntInput as Input,
  Switch,
  Tag,
  message,
  type TableColumnsType,
  attachmentImageAccept,
  isSupportedAttachmentImageFile,
} from '@radish/ui';
import {
  batchAddStickersWithDetails,
  normalizeStickerCode,
  type BatchAddStickerItemRequest,
} from '@/api/stickerApi';
import { uploadAttachmentImage } from '@/api/attachmentApi';
import { log } from '@/utils/logger';
import './StickerBatchUploadModal.css';

interface StickerBatchUploadModalProps {
  visible: boolean;
  groupId: string;
  onCancel: () => void;
  onSuccess: () => void;
}

type UploadStatus = 'pending' | 'uploading' | 'uploaded' | 'failed';

interface BatchUploadRow {
  rowId: string;
  originalIndex: number;
  fileName: string;
  file: RcFile;
  code: string;
  name: string;
  allowInline: boolean;
  uploadStatus: UploadStatus;
  uploadProgress: number;
  uploadError?: string;
  attachmentId?: string;
  serverMessage?: string;
}

interface UploadAttachmentResult {
  attachmentId: string;
}

const MAX_BATCH_COUNT = 50;
const CODE_PATTERN = /^[a-z0-9_]+$/;

function getFallbackCode(fileName: string): string {
  const withoutExt = fileName.replace(/\.[^/.]+$/, '').trim().toLowerCase();
  const normalized = withoutExt
    .replace(/[^a-z0-9_]+/g, '_')
    .replace(/_{2,}/g, '_')
    .replace(/^_+|_+$/g, '');
  return normalized || `sticker_${Date.now()}`;
}

function getFallbackName(fileName: string): string {
  const withoutExt = fileName.replace(/\.[^/.]+$/, '').trim();
  return withoutExt || fileName;
}

function uploadImageWithProgress(file: RcFile, onProgress: (percent: number) => void): Promise<UploadAttachmentResult> {
  return uploadAttachmentImage(file as File, { businessType: 'Sticker' }, onProgress);
}

export const StickerBatchUploadModal = ({ visible, groupId, onCancel, onSuccess }: StickerBatchUploadModalProps) => {
  const { t } = useTranslation();
  const [currentStep, setCurrentStep] = useState(0);
  const [selectedFiles, setSelectedFiles] = useState<UploadFile[]>([]);
  const [rows, setRows] = useState<BatchUploadRow[]>([]);
  const [uploading, setUploading] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const uploadedRows = useMemo(
    () => rows.filter((row) => row.uploadStatus === 'uploaded' && !!row.attachmentId),
    [rows]
  );
  const failedUploadRows = useMemo(
    () => rows.filter((row) => row.uploadStatus === 'failed'),
    [rows]
  );
  const retryRows = useMemo(
    () => rows.filter((row) => !!row.serverMessage && row.uploadStatus === 'uploaded' && !!row.attachmentId),
    [rows]
  );

  const updateRow = (rowId: string, updater: (row: BatchUploadRow) => BatchUploadRow) => {
    setRows((prev) => prev.map((row) => (row.rowId === rowId ? updater(row) : row)));
  };

  const resetState = () => {
    setCurrentStep(0);
    setSelectedFiles([]);
    setRows([]);
    setUploading(false);
    setSubmitting(false);
  };

  useEffect(() => {
    if (!visible) {
      resetState();
    }
  }, [visible]);

  const handleSafeCancel = () => {
    if (uploading || submitting) {
      message.warning(t('stickers.batch.processing'));
      return;
    }

    onCancel();
  };

  const handleFilesChange = (info: UploadChangeParam<UploadFile>) => {
    if (info.fileList.length > MAX_BATCH_COUNT) {
      message.warning(t('stickers.batch.maxSelect', { count: MAX_BATCH_COUNT }));
    }

    setSelectedFiles(info.fileList.slice(0, MAX_BATCH_COUNT));
  };

  const normalizeRowsFromFiles = async (files: RcFile[]): Promise<BatchUploadRow[]> => {
    const normalized = await Promise.all(
      files.map(async (file, index) => {
        let code = getFallbackCode(file.name);

        try {
          const normalizedCode = await normalizeStickerCode(file.name);
          if (normalizedCode.voNormalizedCode) {
            code = normalizedCode.voNormalizedCode;
          }
        } catch (error) {
          log.warn('StickerBatchUpload', `清洗编码失败，使用本地规则：${file.name}`, error);
        }

        return {
          rowId: `${Date.now()}-${index}-${file.uid}`,
          originalIndex: index,
          fileName: file.name,
          file,
          code,
          name: getFallbackName(file.name),
          allowInline: true,
          uploadStatus: 'pending' as UploadStatus,
          uploadProgress: 0,
        };
      })
    );

    return normalized;
  };

  const uploadRows = async (targetRows: BatchUploadRow[]) => {
    if (targetRows.length === 0) {
      return;
    }

    setUploading(true);
    setCurrentStep(1);

    let nextIndex = 0;
    let successCount = 0;
    let failedCount = 0;
    const concurrency = Math.min(3, targetRows.length);

    const workers = Array.from({ length: concurrency }).map(async () => {
      while (true) {
        const currentIndex = nextIndex;
        nextIndex += 1;
        if (currentIndex >= targetRows.length) {
          break;
        }

        const row = targetRows[currentIndex];
        updateRow(row.rowId, (item) => ({
          ...item,
          uploadStatus: 'uploading',
          uploadProgress: 0,
          uploadError: undefined,
          serverMessage: undefined,
        }));

        try {
          const uploadResult = await uploadImageWithProgress(row.file, (progress) => {
            updateRow(row.rowId, (item) => ({
              ...item,
              uploadProgress: progress,
            }));
          });

          updateRow(row.rowId, (item) => ({
            ...item,
            uploadStatus: 'uploaded',
            uploadProgress: 100,
            attachmentId: uploadResult.attachmentId,
            uploadError: undefined,
          }));
          successCount += 1;
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : t('stickers.batch.uploadFailed');
          updateRow(row.rowId, (item) => ({
            ...item,
            uploadStatus: 'failed',
            uploadProgress: 0,
            uploadError: errorMessage,
          }));
          failedCount += 1;
        }
      }
    });

    await Promise.all(workers);
    setUploading(false);
    setCurrentStep(2);
    message.info(t('stickers.batch.uploadComplete', { success: successCount, failed: failedCount }));
  };

  const handleStartUpload = async () => {
    if (!/^[1-9]\d*$/.test(groupId)) {
      message.error(t('stickers.batch.invalidGroup'));
      return;
    }

    const files = selectedFiles
      .map((item) => item.originFileObj)
      .filter((item): item is RcFile => !!item);

    if (files.length === 0) {
      message.warning(t('stickers.batch.selectFirst'));
      return;
    }

    if (files.length > MAX_BATCH_COUNT) {
      message.warning(t('stickers.batch.maxUpload', { count: MAX_BATCH_COUNT }));
      return;
    }

    try {
      const initializedRows = await normalizeRowsFromFiles(files);
      setRows(initializedRows);
      await uploadRows(initializedRows);
    } catch (error) {
      log.error('StickerBatchUpload', '初始化批量上传失败:', error);
      message.error(t('stickers.batch.initializeFailed'));
      setUploading(false);
    }
  };

  const handleRetryUploadFailed = async () => {
    const targets = rows.filter((row) => row.uploadStatus === 'failed');
    if (targets.length === 0) {
      message.info(t('stickers.batch.noRetryUploads'));
      return;
    }

    await uploadRows(targets);
  };

  const validateRows = (targets: BatchUploadRow[]): string | undefined => {
    for (const row of targets) {
      const normalizedCode = row.code.trim().toLowerCase();
      if (!normalizedCode) {
        return t('stickers.batch.validation.codeMissing', { row: row.originalIndex + 1 });
      }

      if (!CODE_PATTERN.test(normalizedCode)) {
        return t('stickers.batch.validation.codeInvalid', { row: row.originalIndex + 1 });
      }

      if (!row.name.trim()) {
        return t('stickers.batch.validation.nameMissing', { row: row.originalIndex + 1 });
      }

      if (!row.attachmentId) {
        return t('stickers.batch.validation.attachmentMissing', { row: row.originalIndex + 1 });
      }
    }

    return undefined;
  };

  const submitRows = async (targets: BatchUploadRow[]) => {
    if (targets.length === 0) {
      message.warning(t('stickers.batch.noData'));
      return;
    }

    const validationMessage = validateRows(targets);
    if (validationMessage) {
      message.error(validationMessage);
      return;
    }

    const targetIds = new Set(targets.map((item) => item.rowId));
    setRows((prev) => prev.map((row) => {
      if (!targetIds.has(row.rowId)) {
        return row;
      }

      return {
        ...row,
        serverMessage: undefined,
      };
    }));

    const requestIndexToRowId: string[] = [];
    const requestItems: BatchAddStickerItemRequest[] = targets.map((item, index) => {
      requestIndexToRowId[index] = item.rowId;
      return {
        attachmentId: item.attachmentId ?? '',
        code: item.code.trim().toLowerCase(),
        name: item.name.trim(),
        allowInline: item.allowInline,
      };
    });

    try {
      setSubmitting(true);

      const response = await batchAddStickersWithDetails({
        groupId,
        stickers: requestItems,
      });

      const responseData = response.data;
      const conflicts = responseData?.voConflicts ?? [];
      const failedItems = responseData?.voFailedItems ?? [];
      const hasIssues = conflicts.length > 0 || failedItems.length > 0;

      if (response.ok && !hasIssues) {
        message.success(t('stickers.batch.created', { count: responseData?.voCreatedCount ?? requestItems.length }));
        onSuccess();
        return;
      }

      if (!responseData) {
        message.error(response.message || t('stickers.batch.submitFailed'));
        return;
      }

      const issueMap = new Map<string, string>();
      conflicts.forEach((item) => {
        const rowId = requestIndexToRowId[item.voRowIndex];
        if (rowId) {
          issueMap.set(rowId, item.voMessage || t('stickers.batch.conflictFallback'));
        }
      });
      failedItems.forEach((item) => {
        const rowId = requestIndexToRowId[item.voRowIndex];
        if (rowId) {
          issueMap.set(rowId, item.voMessage || t('stickers.batch.failedFallback'));
        }
      });

      if (issueMap.size > 0) {
        setRows((prev) => prev.map((row) => {
          const issueMessage = issueMap.get(row.rowId);
          if (!issueMessage) {
            return row;
          }

          return {
            ...row,
            serverMessage: issueMessage,
            code: row.code.trim().toLowerCase(),
          };
        }));
        setCurrentStep(3);
        message.warning(t('stickers.batch.issues', { conflicts: conflicts.length, failed: failedItems.length }));
        return;
      }

      message.error(response.message || t('stickers.batch.submitFailed'));
    } catch (error) {
      log.error('StickerBatchUpload', '批量提交失败:', error);
      message.error(t('stickers.batch.submitFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitAllUploaded = async () => {
    const targets = rows.filter((row) => row.uploadStatus === 'uploaded' && !!row.attachmentId);
    await submitRows(targets);
  };

  const handleSubmitRetryRows = async () => {
    if (retryRows.length === 0) {
      message.info(t('stickers.batch.noRetryConflicts'));
      return;
    }

    await submitRows(retryRows);
  };

  const tableColumns: TableColumnsType<BatchUploadRow> = [
    {
      title: '#',
      key: 'index',
      width: 60,
      render: (_, record) => record.originalIndex + 1,
    },
    {
      title: t('stickers.batch.table.fileName'),
      dataIndex: 'fileName',
      key: 'fileName',
      width: 260,
    },
    {
      title: 'Code',
      key: 'code',
      width: 220,
      render: (_, record) => (
        <Input
          value={record.code}
          disabled={record.uploadStatus !== 'uploaded'}
          onChange={(event) => {
            const nextValue = event.target.value.toLowerCase().trim();
            updateRow(record.rowId, (row) => ({
              ...row,
              code: nextValue,
            }));
          }}
        />
      ),
    },
    {
      title: t('stickers.batch.table.displayName'),
      key: 'name',
      width: 220,
      render: (_, record) => (
        <Input
          value={record.name}
          disabled={record.uploadStatus !== 'uploaded'}
          onChange={(event) => {
            updateRow(record.rowId, (row) => ({
              ...row,
              name: event.target.value,
            }));
          }}
        />
      ),
    },
    {
      title: t('stickers.batch.table.inline'),
      key: 'allowInline',
      width: 120,
      render: (_, record) => (
        <Switch
          checked={record.allowInline}
          disabled={record.uploadStatus !== 'uploaded'}
          onChange={(checked) => {
            updateRow(record.rowId, (row) => ({
              ...row,
              allowInline: checked,
            }));
          }}
        />
      ),
    },
    {
      title: t('stickers.batch.table.uploadStatus'),
      key: 'uploadStatus',
      width: 140,
      render: (_, record) => {
        if (record.uploadStatus === 'uploaded') {
          return <Tag color="success">{t('stickers.batch.status.uploaded')}</Tag>;
        }

        if (record.uploadStatus === 'uploading') {
          return <Tag color="processing">{t('stickers.batch.status.uploading')}</Tag>;
        }

        if (record.uploadStatus === 'failed') {
          return <Tag color="error">{t('stickers.batch.status.failed')}</Tag>;
        }

        return <Tag>{t('stickers.batch.status.pending')}</Tag>;
      },
    },
    {
      title: t('stickers.batch.table.result'),
      key: 'issue',
      width: 260,
      render: (_, record) => {
        if (record.serverMessage) {
          return <span className="sticker-batch-issue-text">{record.serverMessage}</span>;
        }

        if (record.uploadError) {
          return <span className="sticker-batch-error-text">{record.uploadError}</span>;
        }

        return '-';
      },
    },
  ];

  const uploadProgressColumns: TableColumnsType<BatchUploadRow> = [
    {
      title: '#',
      key: 'index',
      width: 60,
      render: (_, record) => record.originalIndex + 1,
    },
    {
      title: t('stickers.batch.table.fileName'),
      dataIndex: 'fileName',
      key: 'fileName',
    },
    {
      title: t('stickers.batch.table.progress'),
      key: 'progress',
      width: 280,
      render: (_, record) => {
        if (record.uploadStatus === 'uploaded') {
          return <Progress percent={100} size="small" status="success" />;
        }

        if (record.uploadStatus === 'failed') {
          return <Progress percent={0} size="small" status="exception" />;
        }

        return <Progress percent={record.uploadProgress} size="small" status="active" />;
      },
    },
    {
      title: t('stickers.batch.table.status'),
      key: 'status',
      width: 140,
      render: (_, record) => {
        if (record.uploadStatus === 'uploaded') {
          return <Tag color="success">{t('stickers.batch.status.uploaded')}</Tag>;
        }
        if (record.uploadStatus === 'failed') {
          return <Tag color="error">{t('stickers.batch.status.failure')}</Tag>;
        }
        if (record.uploadStatus === 'uploading') {
          return <Tag color="processing">{t('stickers.batch.status.uploading')}</Tag>;
        }

        return <Tag>{t('stickers.batch.status.pending')}</Tag>;
      },
    },
  ];

  const renderStepBody = () => {
    if (currentStep === 0) {
      return (
        <div className="sticker-batch-step-body">
          <Alert
            type="info"
            showIcon
            title={t('stickers.batch.selectInfo', { count: MAX_BATCH_COUNT })}
          />
          <Upload.Dragger
            multiple
            accept={attachmentImageAccept}
            fileList={selectedFiles}
            beforeUpload={(file) => {
              const isImage = isSupportedAttachmentImageFile(file);

              if (!isImage) {
                message.error(t('stickers.common.imageOnly'));
                return Upload.LIST_IGNORE;
              }

              return false;
            }}
            onChange={handleFilesChange}
            onRemove={(file) => {
              setSelectedFiles((prev) => prev.filter((item) => item.uid !== file.uid));
              return true;
            }}
          >
            <p className="ant-upload-text">{t('stickers.batch.dropTitle')}</p>
            <p className="ant-upload-hint">{t('stickers.batch.dropHint')}</p>
          </Upload.Dragger>
          <div className="sticker-batch-summary">{t('stickers.batch.selected', { count: selectedFiles.length })}</div>
        </div>
      );
    }

    if (currentStep === 1) {
      const totalCount = rows.length;
      const successCount = rows.filter((row) => row.uploadStatus === 'uploaded').length;
      const failCount = rows.filter((row) => row.uploadStatus === 'failed').length;
      const overallPercent = totalCount === 0 ? 0 : Math.round((successCount / totalCount) * 100);

      return (
        <div className="sticker-batch-step-body">
          <Alert
            type={uploading ? 'info' : 'warning'}
            showIcon
            title={uploading ? t('stickers.batch.uploadingNotice') : t('stickers.batch.uploadComplete', { success: successCount, failed: failCount })}
          />
          <Progress percent={overallPercent} status={uploading ? 'active' : 'normal'} />
          <div className="sticker-batch-table-region">
            <Table<BatchUploadRow>
              rowKey="rowId"
              size="small"
              pagination={false}
              columns={uploadProgressColumns}
              dataSource={rows}
              scroll={{ x: 860 }}
            />
          </div>
        </div>
      );
    }

    if (currentStep === 2) {
      return (
        <div className="sticker-batch-step-body">
          <Alert
            type={failedUploadRows.length > 0 ? 'warning' : 'success'}
            showIcon
            title={t('stickers.batch.reviewSummary', { uploaded: uploadedRows.length, total: rows.length, failed: failedUploadRows.length })}
            description={t('stickers.batch.reviewDescription')}
          />
          <div className="sticker-batch-table-region">
            <Table<BatchUploadRow>
              rowKey="rowId"
              size="small"
              pagination={false}
              columns={tableColumns}
              dataSource={rows}
              rowClassName={(record) => (record.serverMessage ? 'sticker-batch-row-conflict' : '')}
              scroll={{ x: 1300, y: 360 }}
            />
          </div>
        </div>
      );
    }

    return (
      <div className="sticker-batch-step-body">
        <Alert
          type="error"
          showIcon
          title={t('stickers.batch.retrySummary', { count: retryRows.length })}
          description={t('stickers.batch.retryDescription')}
        />
        <div className="sticker-batch-table-region">
          <Table<BatchUploadRow>
            rowKey="rowId"
            size="small"
            pagination={false}
            columns={tableColumns}
            dataSource={retryRows}
            rowClassName={() => 'sticker-batch-row-conflict'}
            scroll={{ x: 1300 }}
          />
        </div>
      </div>
    );
  };

  const renderFooter = () => {
    if (currentStep === 0) {
      return (
        <Space>
          <Button onClick={handleSafeCancel}>{t('stickers.batch.actions.cancel')}</Button>
          <Button
            variant="primary"
            disabled={uploading || submitting}
            onClick={() => {
              void handleStartUpload();
            }}
          >
            {t('stickers.batch.actions.start')}
          </Button>
        </Space>
      );
    }

    if (currentStep === 1) {
      return (
        <Space>
          <Button
            onClick={() => {
              if (uploading) {
                message.warning(t('stickers.batch.actions.wait'));
                return;
              }
              setCurrentStep(2);
            }}
          >
            {t(uploading ? 'stickers.common.uploading' : 'stickers.batch.actions.review')}
          </Button>
        </Space>
      );
    }

    if (currentStep === 2) {
      return (
        <Space>
          <Button onClick={handleSafeCancel}>{t('stickers.batch.actions.close')}</Button>
          <Button
            disabled={failedUploadRows.length === 0 || uploading || submitting}
            onClick={() => {
              void handleRetryUploadFailed();
            }}
          >
            {t('stickers.batch.actions.retryUploads')}
          </Button>
          <Button
            variant="primary"
            disabled={uploadedRows.length === 0 || submitting || uploading}
            onClick={() => {
              void handleSubmitAllUploaded();
            }}
          >
            {t(submitting ? 'stickers.batch.actions.submitting' : 'stickers.batch.actions.submit')}
          </Button>
        </Space>
      );
    }

    return (
      <Space>
        <Button
          disabled={submitting || uploading}
          onClick={() => {
            setCurrentStep(2);
          }}
        >
          {t('stickers.batch.actions.back')}
        </Button>
        <Button
          variant="primary"
          disabled={retryRows.length === 0 || submitting || uploading}
          onClick={() => {
            void handleSubmitRetryRows();
          }}
        >
          {t(submitting ? 'stickers.batch.actions.submitting' : 'stickers.batch.actions.retryConflicts')}
        </Button>
      </Space>
    );
  };

  return (
    <Modal
      title={t('stickers.batch.title')}
      open={visible}
      width={1200}
      onCancel={() => {
        handleSafeCancel();
      }}
      footer={renderFooter}
      destroyOnHidden
      maskClosable={false}
    >
      <div className="sticker-batch-upload-modal">
        <Steps
          current={currentStep}
          items={[
            { title: t('stickers.batch.steps.select') },
            { title: t('stickers.batch.steps.upload') },
            { title: t('stickers.batch.steps.review') },
            { title: t('stickers.batch.steps.resolve') },
          ]}
        />
        {renderStepBody()}
      </div>
    </Modal>
  );
};
