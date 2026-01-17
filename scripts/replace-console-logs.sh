#!/bin/bash

# 批量替换 console.log/info/warn/error 为 log.debug/info/warn/error
# 用于 Radish Client 项目

set -e

CLIENT_SRC="/home/luobo/Code/Radish/radish.client/src"

echo "开始替换 console 调用为 log 工具..."

# 查找所有包含 console. 的文件
files=$(grep -rl "console\." "$CLIENT_SRC" --include="*.ts" --include="*.tsx" || true)

if [ -z "$files" ]; then
  echo "没有找到需要替换的文件"
  exit 0
fi

echo "找到以下文件需要处理:"
echo "$files"
echo ""

# 对每个文件进行处理
for file in $files; do
  echo "处理: $file"

  # 检查文件是否已经导入了 log
  if ! grep -q "import.*log.*from.*@/utils/logger" "$file"; then
    # 在文件开头添加 import（在第一个 import 之后）
    sed -i "1a import { log } from '@/utils/logger';" "$file"
  fi

  # 替换 console.log -> log.debug
  sed -i "s/console\.log(/log.debug(/g" "$file"

  # 替换 console.info -> log.info
  sed -i "s/console\.info(/log.info(/g" "$file"

  # 替换 console.warn -> log.warn
  sed -i "s/console\.warn(/log.warn(/g" "$file"

  # 替换 console.error -> log.error
  sed -i "s/console\.error(/log.error(/g" "$file"

  echo "  ✓ 完成"
done

echo ""
echo "替换完成！共处理 $(echo "$files" | wc -l) 个文件"
echo ""
echo "请检查以下内容:"
echo "1. import 语句是否正确"
echo "2. 是否有重复的 import"
echo "3. 运行 npm run lint 检查语法"
