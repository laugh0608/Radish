// 基础组件导出
export { Button } from './Button/Button';
export type { ButtonProps } from './Button/Button';

export { Input } from './Input/Input';
export type { InputProps } from './Input/Input';

export { Select } from './Select/Select';
export type { SelectProps, SelectOption } from './Select/Select';

export { Modal } from './Modal/Modal';
export type { ModalProps } from './Modal/Modal';

export { Icon } from './Icon/Icon';
export type { IconProps } from './Icon/Icon';

export { ContextMenu } from './ContextMenu/ContextMenu';
export type { ContextMenuProps, ContextMenuItem } from './ContextMenu/ContextMenu';

export { DataTable } from './DataTable/DataTable';
export type { DataTableProps } from './DataTable/DataTable';

export { GlassPanel } from './GlassPanel/GlassPanel';
export type { GlassPanelProps } from './GlassPanel/GlassPanel';

export { MarkdownRenderer } from './MarkdownRenderer/MarkdownRenderer';

export { MarkdownEditor } from './MarkdownEditor/MarkdownEditor';
export type { MarkdownEditorProps } from './MarkdownEditor/MarkdownEditor';

export { ConfirmDialog } from './ConfirmDialog/ConfirmDialog';
export type { ConfirmDialogProps } from './ConfirmDialog/ConfirmDialog';

export { UserMention } from './UserMention/UserMention';
export type { UserMentionProps, UserMentionOption } from './UserMention/UserMention';

export { FileUpload } from './FileUpload/FileUpload';
export type { FileUploadProps, UploadResult } from './FileUpload/FileUpload';

export { ChunkedFileUpload } from './ChunkedFileUpload/ChunkedFileUpload';
export type {
  ChunkedFileUploadProps,
  ChunkedUploadOptions,
  ChunkedUploadResult,
  UploadProgress
} from './ChunkedFileUpload/ChunkedFileUpload';

export { Toast, ToastContainer, toast, toastManager } from './Toast/Toast';
export type { ToastProps } from './Toast/Toast';

export { Notification } from './Notification/Notification';
export type { NotificationProps, NotificationItemData } from './Notification/Notification';

export { NotificationBadge } from './Notification/NotificationBadge';
export type { NotificationBadgeProps } from './Notification/NotificationBadge';

export { NotificationCenter } from './Notification/NotificationCenter';
export type { NotificationCenterProps } from './Notification/NotificationCenter';

export { NotificationList } from './Notification/NotificationList';
export type { NotificationListProps } from './Notification/NotificationList';

export { ExperienceBar } from './ExperienceBar/ExperienceBar';
export type { ExperienceBarProps, ExperienceData } from './ExperienceBar/ExperienceBar';

export { LevelUpModal } from './LevelUpModal/LevelUpModal';
export type { LevelUpModalProps, LevelUpData } from './LevelUpModal/LevelUpModal';

export { TableSkeleton, SimpleSkeleton, CardSkeleton } from './Skeleton';

// Re-export Ant Design 组件供 console 等项目使用
export {
  // Layout
  Layout,
  // Navigation
  Breadcrumb,
  Menu,
  // App
  App,
  // Form
  Form,
  Input as AntInput,
  InputNumber,
  Select as AntSelect,
  Switch,
  DatePicker,
  Checkbox,
  Radio,
  // Table
  Table,
  // Feedback
  Modal as AntModal,
  message,
  notification,
  Result,
  // Data Display
  Tag,
  Badge,
  Tooltip,
  Avatar,
  Dropdown,
  Image,
  Descriptions,
  // General
  Button as AntButton,
  Space,
  Divider,
  // Other
  Popconfirm,
} from 'antd';

export {
  // Icons
  EditOutlined,
  DeleteOutlined,
  EyeOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  DownOutlined,
  UpOutlined,
  LeftOutlined,
  CheckOutlined,
  CloseOutlined,
  UserOutlined,
  SettingOutlined,
  LogoutOutlined,
  MenuFoldOutlined,
  MenuUnfoldOutlined,
  AppstoreOutlined,
  TeamOutlined,
  SafetyOutlined,
  ClockCircleOutlined,
  DashboardOutlined,
  ShoppingOutlined,
  FileTextOutlined,
  SyncOutlined,
  HomeOutlined,
  LockOutlined,
  UnlockOutlined,
  KeyOutlined,
} from '@ant-design/icons';

// Re-export types
export type {
  FormProps,
  InputProps as AntInputProps,
  SelectProps as AntSelectProps,
  TableProps,
  TableColumnsType,
  ModalProps as AntModalProps,
  MenuProps,
  LayoutProps,
  ButtonProps as AntButtonProps,
  SpaceProps,
  BreadcrumbProps,
} from 'antd';
