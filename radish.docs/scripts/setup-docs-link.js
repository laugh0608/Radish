#!/usr/bin/env node

/**
 * 跨平台设置文档目录链接
 *
 * 此脚本在构建前自动运行，确保 docs 目录可访问：
 * 1. 优先尝试创建符号链接（Linux/macOS 原生支持）
 * 2. Windows 上如果没有权限，回退到目录连接（junction）
 * 3. 如果都失败，输出警告信息
 */

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const docsLinkPath = path.join(__dirname, '..', 'docs');
const docsSourcePath = path.join(__dirname, '..', '..', 'docs');

// 检查源目录是否存在
if (!fs.existsSync(docsSourcePath)) {
  console.error(`❌ 错误: 源文档目录不存在: ${docsSourcePath}`);
  process.exit(1);
}

// 如果链接已存在且有效，直接返回
if (fs.existsSync(docsLinkPath)) {
  try {
    const stats = fs.lstatSync(docsLinkPath);
    if (stats.isSymbolicLink() || stats.isDirectory()) {
      // 验证链接是否有效
      fs.readdirSync(docsLinkPath);
      console.log('✓ 文档目录链接已存在且有效');
      return;
    }
  } catch (err) {
    // 链接无效，删除后重新创建
    console.log('⚠ 文档目录链接无效，重新创建...');
    try {
      fs.unlinkSync(docsLinkPath);
    } catch (e) {
      // 可能是目录，尝试删除目录
      try {
        fs.rmSync(docsLinkPath, { recursive: true, force: true });
      } catch (e2) {
        console.error(`❌ 无法删除无效链接: ${e2.message}`);
      }
    }
  }
}

// 尝试创建符号链接
try {
  const isWindows = process.platform === 'win32';

  if (isWindows) {
    // Windows: 尝试使用 junction（不需要管理员权限）
    try {
      execSync(`mklink /J "${docsLinkPath}" "${docsSourcePath}"`, {
        stdio: 'inherit',
        shell: 'cmd.exe'
      });
      console.log('✓ 已创建目录连接 (Windows junction)');
    } catch (err) {
      // Junction 失败，尝试符号链接（需要管理员权限）
      try {
        fs.symlinkSync(docsSourcePath, docsLinkPath, 'junction');
        console.log('✓ 已创建符号链接 (symlink)');
      } catch (err2) {
        console.error('❌ 创建链接失败，尝试使用相对路径配置...');
        console.error('提示: 在 Windows 上，你可能需要以管理员身份运行，或者使用 WSL');
        console.error(`    原始路径将直接使用: ${docsSourcePath}`);
        // 不退出，让 VitePress 尝试使用配置中的路径
      }
    }
  } else {
    // Linux/macOS: 使用相对路径创建符号链接
    fs.symlinkSync('../docs', docsLinkPath);
    console.log('✓ 已创建符号链接');
  }
} catch (err) {
  console.error(`❌ 创建符号链接失败: ${err.message}`);
  console.error('    VitePress 将尝试使用备用配置');
}
