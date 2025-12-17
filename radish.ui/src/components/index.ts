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

export { ConfirmDialog } from './ConfirmDialog/ConfirmDialog';
export type { ConfirmDialogProps } from './ConfirmDialog/ConfirmDialog';

export { UserMention } from './UserMention/UserMention';
export type { UserMentionProps, UserMentionOption } from './UserMention/UserMention';

// Re-export Ant Design 组件供 console 等项目使用
export {
  // Layout
  Layout,
  // Menu
  Menu,
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
  // Data Display
  Tag,
  Badge,
  Tooltip,
  Avatar,
  Dropdown,
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
} from 'antd';
