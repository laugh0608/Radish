import { env } from '@/config/env';

/**
 * 日志级别
 */
export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
}

/**
 * 日志工具类
 */
class Logger {
  private enabled: boolean;
  private minLevel: LogLevel;

  constructor() {
    this.enabled = env.debug;
    this.minLevel = LogLevel.DEBUG;
  }

  /**
   * 格式化日志前缀
   */
  private formatPrefix(level: string, tag?: string): string {
    const timestamp = new Date().toLocaleTimeString();
    const tagStr = tag ? `[${tag}]` : '';
    return `[${timestamp}] [${level}]${tagStr}`;
  }

  /**
   * 调试日志（仅在 debug 模式下输出）
   */
  debug(message: string, ...args: any[]): void;
  debug(tag: string, message: string, ...args: any[]): void;
  debug(tagOrMessage: string, messageOrArg?: any, ...args: any[]): void {
    if (!this.enabled || this.minLevel > LogLevel.DEBUG) {
      return;
    }

    const hasTag = typeof messageOrArg === 'string';
    const tag = hasTag ? tagOrMessage : undefined;
    const message = hasTag ? messageOrArg : tagOrMessage;
    const finalArgs = hasTag ? args : [messageOrArg, ...args];

    console.log(this.formatPrefix('DEBUG', tag), message, ...finalArgs);
  }

  /**
   * 信息日志（仅在 debug 模式下输出）
   */
  info(message: string, ...args: any[]): void;
  info(tag: string, message: string, ...args: any[]): void;
  info(tagOrMessage: string, messageOrArg?: any, ...args: any[]): void {
    if (!this.enabled || this.minLevel > LogLevel.INFO) {
      return;
    }

    const hasTag = typeof messageOrArg === 'string';
    const tag = hasTag ? tagOrMessage : undefined;
    const message = hasTag ? messageOrArg : tagOrMessage;
    const finalArgs = hasTag ? args : [messageOrArg, ...args];

    console.info(this.formatPrefix('INFO', tag), message, ...finalArgs);
  }

  /**
   * 警告日志（总是输出）
   */
  warn(message: string, ...args: any[]): void;
  warn(tag: string, message: string, ...args: any[]): void;
  warn(tagOrMessage: string, messageOrArg?: any, ...args: any[]): void {
    if (this.minLevel > LogLevel.WARN) {
      return;
    }

    const hasTag = typeof messageOrArg === 'string';
    const tag = hasTag ? tagOrMessage : undefined;
    const message = hasTag ? messageOrArg : tagOrMessage;
    const finalArgs = hasTag ? args : [messageOrArg, ...args];

    console.warn(this.formatPrefix('WARN', tag), message, ...finalArgs);
  }

  /**
   * 错误日志（总是输出）
   */
  error(message: string, ...args: any[]): void;
  error(tag: string, message: string, ...args: any[]): void;
  error(tagOrMessage: string, messageOrArg?: any, ...args: any[]): void {
    if (this.minLevel > LogLevel.ERROR) {
      return;
    }

    const hasTag = typeof messageOrArg === 'string';
    const tag = hasTag ? tagOrMessage : undefined;
    const message = hasTag ? messageOrArg : tagOrMessage;
    const finalArgs = hasTag ? args : [messageOrArg, ...args];

    console.error(this.formatPrefix('ERROR', tag), message, ...finalArgs);
  }

  /**
   * 分组日志（仅在 debug 模式下输出）
   */
  group(label: string, collapsed = false): void {
    if (!this.enabled) {
      return;
    }

    if (collapsed) {
      console.groupCollapsed(label);
    } else {
      console.group(label);
    }
  }

  /**
   * 结束分组
   */
  groupEnd(): void {
    if (!this.enabled) {
      return;
    }

    console.groupEnd();
  }

  /**
   * 表格日志（仅在 debug 模式下输出）
   */
  table(data: any): void {
    if (!this.enabled) {
      return;
    }

    console.table(data);
  }

  /**
   * 性能计时开始
   */
  time(label: string): void {
    if (!this.enabled) {
      return;
    }

    console.time(label);
  }

  /**
   * 性能计时结束
   */
  timeEnd(label: string): void {
    if (!this.enabled) {
      return;
    }

    console.timeEnd(label);
  }
}

/**
 * 全局日志实例
 */
export const logger = new Logger();

/**
 * 便捷导出
 */
export const log = {
  debug: logger.debug.bind(logger),
  info: logger.info.bind(logger),
  warn: logger.warn.bind(logger),
  error: logger.error.bind(logger),
  group: logger.group.bind(logger),
  groupEnd: logger.groupEnd.bind(logger),
  table: logger.table.bind(logger),
  time: logger.time.bind(logger),
  timeEnd: logger.timeEnd.bind(logger),
};
