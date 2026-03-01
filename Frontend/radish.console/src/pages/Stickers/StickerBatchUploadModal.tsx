import { useEffect, useMemo, useState } from 'react';
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
} from '@radish/ui';
import { getApiClientConfig } from '@radish/http';
import {
  batchAddStickersWithDetails,
  normalizeStickerCode,
  type BatchAddStickerItemRequest,
} from '@/api/stickerApi';
import { log } from '@/utils/logger';
import './StickerBatchUploadModal.css';

interface StickerBatchUploadModalProps {
  visible: boolean;
  groupId: number;
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
  attachmentId?: number;
  imageUrl?: string;
  thumbnailUrl?: string;
  serverMessage?: string;
}

interface UploadAttachmentResult {
  attachmentId: number;
  imageUrl?: string;
  thumbnailUrl?: string;
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

function toStringOrUndefined(value: unknown): string | undefined {
  if (typeof value === 'string' && value.trim()) {
    return value;
  }

  return undefined;
}

function toNumberOrUndefined(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }

  return undefined;
}

function buildApiUrl(path: string): string {
  const config = getApiClientConfig();
  const normalizedBaseUrl = (config.baseUrl || '').trim().replace(/\/$/, '');
  if (!normalizedBaseUrl) {
    throw new Error('API baseUrl 未配置');
  }

  return `${normalizedBaseUrl}${path}`;
}

function uploadImageWithProgress(file: RcFile, onProgress: (percent: number) => void): Promise<UploadAttachmentResult> {
  return new Promise((resolve, reject) => {
    let uploadUrl = '';
    try {
      uploadUrl = buildApiUrl('/api/v1/Attachment/UploadImage');
    } catch (error) {
      reject(error instanceof Error ? error : new Error('上传地址构建失败'));
      return;
    }

    const config = getApiClientConfig();
    const token = config.getToken?.();
    const xhr = new XMLHttpRequest();
    xhr.open('POST', uploadUrl, true);

    if (token) {
      xhr.setRequestHeader('Authorization', `Bearer ${token}`);
    }

    xhr.upload.onprogress = (event) => {
      if (!event.lengthComputable) {
        return;
      }

      const percent = Math.round((event.loaded / event.total) * 100);
      onProgress(Math.min(100, Math.max(0, percent)));
    };

    xhr.onerror = () => {
      reject(new Error('上传失败，请检查网络连接'));
    };

    xhr.onload = () => {
      if (xhr.status < 200 || xhr.status >= 300) {
        reject(new Error(`上传失败（HTTP ${xhr.status}）`));
        return;
      }

      let payload: Record<string, unknown> | null = null;
      try {
        payload = JSON.parse(xhr.responseText) as Record<string, unknown>;
      } catch {
        reject(new Error('上传响应解析失败'));
        return;
      }

      const isSuccess = Boolean(payload.isSuccess ?? payload.IsSuccess);
      const messageText = toStringOrUndefined(payload.messageInfo ?? payload.MessageInfo) || '上传失败';
      if (!isSuccess) {
        reject(new Error(messageText));
        return;
      }

      const responseData = (payload.responseData ?? payload.ResponseData) as Record<string, unknown> | undefined;
      if (!responseData) {
        reject(new Error('上传成功但未返回附件信息'));
        return;
      }

      const attachmentId = toNumberOrUndefined(
        responseData.voId
        ?? responseData.VoId
        ?? responseData.id
        ?? responseData.Id
      );

      if (!attachmentId || attachmentId <= 0) {
        reject(new Error('上传成功但未获取到附件 ID'));
        return;
      }

      resolve({
        attachmentId,
        imageUrl: toStringOrUndefined(responseData.voUrl ?? responseData.VoUrl ?? responseData.url ?? responseData.Url),
        thumbnailUrl: toStringOrUndefined(
          responseData.voThumbnailUrl
          ?? responseData.VoThumbnailUrl
          ?? responseData.thumbnailUrl
          ?? responseData.ThumbnailUrl
        ),
      });
    };

    const formData = new FormData();
    formData.append('file', file as File);
    formData.append('businessType', 'Sticker');
    formData.append('generateThumbnail', 'true');
    formData.append('removeExif', 'true');
    xhr.send(formData);
  });
}

export const StickerBatchUploadModal = ({ visible, groupId, onCancel, onSuccess }: StickerBatchUploadModalProps) => {
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
      message.warning('正在处理请求，请稍候');
      return;
    }

    onCancel();
  };

  const handleFilesChange = (info: UploadChangeParam<UploadFile>) => {
    if (info.fileList.length > MAX_BATCH_COUNT) {
      message.warning(`单次最多选择 ${MAX_BATCH_COUNT} 张图片`);
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
            imageUrl: uploadResult.imageUrl,
            thumbnailUrl: uploadResult.thumbnailUrl,
            uploadError: undefined,
          }));
          successCount += 1;
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : '上传失败';
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
    message.info(`上传完成：成功 ${successCount}，失败 ${failedCount}`);
  };

  const handleStartUpload = async () => {
    if (groupId <= 0) {
      message.error('分组 ID 无效');
      return;
    }

    const files = selectedFiles
      .map((item) => item.originFileObj)
      .filter((item): item is RcFile => !!item);

    if (files.length === 0) {
      message.warning('请先选择图片文件');
      return;
    }

    if (files.length > MAX_BATCH_COUNT) {
      message.warning(`单次最多上传 ${MAX_BATCH_COUNT} 张图片`);
      return;
    }

    try {
      const initializedRows = await normalizeRowsFromFiles(files);
      setRows(initializedRows);
      await uploadRows(initializedRows);
    } catch (error) {
      log.error('StickerBatchUpload', '初始化批量上传失败:', error);
      message.error('初始化批量上传失败');
      setUploading(false);
    }
  };

  const handleRetryUploadFailed = async () => {
    const targets = rows.filter((row) => row.uploadStatus === 'failed');
    if (targets.length === 0) {
      message.info('没有可重传的失败项');
      return;
    }

    await uploadRows(targets);
  };

  const validateRows = (targets: BatchUploadRow[]): string | undefined => {
    for (const row of targets) {
      const normalizedCode = row.code.trim().toLowerCase();
      if (!normalizedCode) {
        return `第 ${row.originalIndex + 1} 行缺少表情标识符`;
      }

      if (!CODE_PATTERN.test(normalizedCode)) {
        return `第 ${row.originalIndex + 1} 行标识符格式不合法（仅允许小写字母、数字、下划线）`;
      }

      if (!row.name.trim()) {
        return `第 ${row.originalIndex + 1} 行缺少显示名称`;
      }

      if (!row.attachmentId) {
        return `第 ${row.originalIndex + 1} 行缺少附件 ID，请先完成上传`;
      }
    }

    return undefined;
  };

  const submitRows = async (targets: BatchUploadRow[]) => {
    if (targets.length === 0) {
      message.warning('没有可提交的数据');
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
        attachmentId: item.attachmentId ?? 0,
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
        message.success(`批量新增成功，共创建 ${responseData?.voCreatedCount ?? requestItems.length} 条表情`);
        onSuccess();
        return;
      }

      if (!responseData) {
        message.error(response.message || '批量提交失败');
        return;
      }

      const issueMap = new Map<string, string>();
      conflicts.forEach((item) => {
        const rowId = requestIndexToRowId[item.voRowIndex];
        if (rowId) {
          issueMap.set(rowId, item.voMessage || '存在标识符冲突');
        }
      });
      failedItems.forEach((item) => {
        const rowId = requestIndexToRowId[item.voRowIndex];
        if (rowId) {
          issueMap.set(rowId, item.voMessage || '服务端处理失败');
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
        message.warning(
          `存在待修复项：冲突 ${conflicts.length}，失败 ${failedItems.length}。请在第 4 步修复后重提。`
        );
        return;
      }

      message.error(response.message || '批量提交失败');
    } catch (error) {
      log.error('StickerBatchUpload', '批量提交失败:', error);
      message.error('批量提交失败');
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
      message.info('没有需要重提的冲突项');
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
      title: '文件名',
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
      title: '显示名',
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
      title: '允许内嵌',
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
      title: '上传状态',
      key: 'uploadStatus',
      width: 140,
      render: (_, record) => {
        if (record.uploadStatus === 'uploaded') {
          return <Tag color="success">已上传</Tag>;
        }

        if (record.uploadStatus === 'uploading') {
          return <Tag color="processing">上传中</Tag>;
        }

        if (record.uploadStatus === 'failed') {
          return <Tag color="error">上传失败</Tag>;
        }

        return <Tag>待上传</Tag>;
      },
    },
    {
      title: '处理结果',
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
      title: '文件名',
      dataIndex: 'fileName',
      key: 'fileName',
    },
    {
      title: '进度',
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
      title: '状态',
      key: 'status',
      width: 140,
      render: (_, record) => {
        if (record.uploadStatus === 'uploaded') {
          return <Tag color="success">已上传</Tag>;
        }
        if (record.uploadStatus === 'failed') {
          return <Tag color="error">失败</Tag>;
        }
        if (record.uploadStatus === 'uploading') {
          return <Tag color="processing">上传中</Tag>;
        }

        return <Tag>待上传</Tag>;
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
            message={`单次最多上传 ${MAX_BATCH_COUNT} 张，建议保持图片名称语义化，系统会自动清洗 code。`}
          />
          <Upload.Dragger
            multiple
            accept="image/*"
            fileList={selectedFiles}
            beforeUpload={(file) => {
              const isImage = file.type
                ? file.type.startsWith('image/')
                : /\.(jpe?g|png|gif|webp|bmp|svg)$/i.test(file.name);

              if (!isImage) {
                message.error('仅支持上传图片文件');
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
            <p className="ant-upload-text">点击或拖拽图片到此区域</p>
            <p className="ant-upload-hint">支持多选，上传后可在下一步确认 code 和显示名</p>
          </Upload.Dragger>
          <div className="sticker-batch-summary">已选择 {selectedFiles.length} 张</div>
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
            message={uploading ? '正在上传附件，请勿关闭弹窗' : `上传完成：成功 ${successCount}，失败 ${failCount}`}
          />
          <Progress percent={overallPercent} status={uploading ? 'active' : 'normal'} />
          <Table<BatchUploadRow>
            rowKey="rowId"
            size="small"
            pagination={false}
            columns={uploadProgressColumns}
            dataSource={rows}
          />
        </div>
      );
    }

    if (currentStep === 2) {
      return (
        <div className="sticker-batch-step-body">
          <Alert
            type={failedUploadRows.length > 0 ? 'warning' : 'success'}
            showIcon
            message={`已上传 ${uploadedRows.length} / ${rows.length}，失败 ${failedUploadRows.length}`}
            description="确认 code、显示名和允许内嵌开关后提交。上传失败项可先重传。"
          />
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
      );
    }

    return (
      <div className="sticker-batch-step-body">
        <Alert
          type="error"
          showIcon
          message={`待修复 ${retryRows.length} 项`}
          description="请修改冲突项后重提。"
        />
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
    );
  };

  const renderFooter = () => {
    if (currentStep === 0) {
      return (
        <Space>
          <Button onClick={handleSafeCancel}>取消</Button>
          <Button
            variant="primary"
            disabled={uploading || submitting}
            onClick={() => {
              void handleStartUpload();
            }}
          >
            开始上传
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
                message.warning('正在上传中，请稍候');
                return;
              }
              setCurrentStep(2);
            }}
          >
            {uploading ? '上传中...' : '进入确认'}
          </Button>
        </Space>
      );
    }

    if (currentStep === 2) {
      return (
        <Space>
          <Button onClick={handleSafeCancel}>关闭</Button>
          <Button
            disabled={failedUploadRows.length === 0 || uploading || submitting}
            onClick={() => {
              void handleRetryUploadFailed();
            }}
          >
            重传失败项
          </Button>
          <Button
            variant="primary"
            disabled={uploadedRows.length === 0 || submitting || uploading}
            onClick={() => {
              void handleSubmitAllUploaded();
            }}
          >
            {submitting ? '提交中...' : '提交批量新增'}
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
          返回确认
        </Button>
        <Button
          variant="primary"
          disabled={retryRows.length === 0 || submitting || uploading}
          onClick={() => {
            void handleSubmitRetryRows();
          }}
        >
          {submitting ? '提交中...' : '重新提交冲突项'}
        </Button>
      </Space>
    );
  };

  return (
    <Modal
      title="批量上传表情"
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
            { title: '选择文件' },
            { title: '上传进度' },
            { title: '确认表格' },
            { title: '冲突修复' },
          ]}
        />
        {renderStepBody()}
      </div>
    </Modal>
  );
};
