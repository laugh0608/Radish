# 萝卜坑应用前端设计

> 版本：v1.0 | 最后更新：2026-01-24 | 状态：设计中

本文档详细描述萝卜坑应用的前端技术实现方案。

---

## 9. 前端架构设计

### 9.1 整体架构

萝卜坑应用采用模块化的前端架构，基于React 19和TypeScript构建，集成到现有的WebOS桌面系统中。

```
radish.client/src/apps/radish-pit/
├── index.ts                    # 应用入口和注册
├── RadishPit.tsx              # 主组件容器
├── components/                 # 功能组件
│   ├── AccountOverview/       # 账户总览模块
│   │   ├── index.tsx
│   │   ├── BalanceCard.tsx
│   │   ├── StatisticsCard.tsx
│   │   └── RecentTransactions.tsx
│   ├── Transfer/              # 转账功能模块
│   │   ├── index.tsx
│   │   ├── TransferForm.tsx
│   │   ├── TransferConfirm.tsx
│   │   └── TransferResult.tsx
│   ├── TransactionHistory/    # 交易记录模块
│   │   ├── index.tsx
│   │   ├── TransactionList.tsx
│   │   ├── TransactionFilter.tsx
│   │   └── TransactionDetail.tsx
│   ├── SecuritySettings/      # 安全设置模块
│   │   ├── index.tsx
│   │   ├── PaymentPassword.tsx
│   │   ├── SecurityLog.tsx
│   │   └── DeviceManagement.tsx
│   ├── Statistics/            # 统计分析模块
│   │   ├── index.tsx
│   │   ├── IncomeChart.tsx
│   │   └── CategoryChart.tsx
│   └── NotificationCenter/    # 通知中心模块
│       ├── index.tsx
│       └── NotificationList.tsx
├── hooks/                     # 自定义Hooks
│   ├── useBalance.ts
│   ├── useTransactions.ts
│   ├── useTransfer.ts
│   └── usePaymentPassword.ts
├── utils/                     # 工具函数
│   ├── coinFormatter.ts
│   ├── transactionHelper.ts
│   └── securityHelper.ts
├── types/                     # 类型定义
│   ├── balance.ts
│   ├── transaction.ts
│   └── security.ts
└── styles/                    # 样式文件
    ├── index.css
    └── components/
```

### 9.2 应用注册

在WebOS桌面系统中注册萝卜坑应用：

```typescript
// radish.client/src/apps/radish-pit/index.ts
import { lazy } from 'react';

const RadishPit = lazy(() => import('./RadishPit'));

export default RadishPit;

// radish.client/src/desktop/AppRegistry.tsx
{
  id: 'radish-pit',
  name: '萝卜坑',
  icon: 'mdi:treasure-chest',
  description: '萝卜币管理中心',
  component: RadishPit,
  type: 'window',
  defaultSize: { width: 1200, height: 800 },
  minSize: { width: 800, height: 600 },
  requiredRoles: ['User'],
  category: 'finance'
}
```

### 9.3 主组件设计

```typescript
// RadishPit.tsx
import React, { useState } from 'react';
import { Tabs } from '@radish/ui';
import AccountOverview from './components/AccountOverview';
import Transfer from './components/Transfer';
import TransactionHistory from './components/TransactionHistory';
import SecuritySettings from './components/SecuritySettings';
import Statistics from './components/Statistics';
import NotificationCenter from './components/NotificationCenter';

const RadishPit: React.FC = () => {
  const [activeTab, setActiveTab] = useState('overview');

  const tabs = [
    { key: 'overview', label: '账户总览', icon: 'mdi:account-balance' },
    { key: 'transfer', label: '转移萝卜币', icon: 'mdi:transfer' },
    { key: 'history', label: '交易记录', icon: 'mdi:history' },
    { key: 'statistics', label: '收支统计', icon: 'mdi:chart-line' },
    { key: 'security', label: '安全设置', icon: 'mdi:security' },
    { key: 'notifications', label: '通知中心', icon: 'mdi:bell' }
  ];

  const renderContent = () => {
    switch (activeTab) {
      case 'overview':
        return <AccountOverview />;
      case 'transfer':
        return <Transfer />;
      case 'history':
        return <TransactionHistory />;
      case 'statistics':
        return <Statistics />;
      case 'security':
        return <SecuritySettings />;
      case 'notifications':
        return <NotificationCenter />;
      default:
        return <AccountOverview />;
    }
  };

  return (
    <div className="radish-pit">
      <div className="radish-pit-header">
        <h1>萝卜坑</h1>
        <p>您的萝卜币管理中心</p>
      </div>

      <Tabs
        items={tabs}
        activeKey={activeTab}
        onChange={setActiveTab}
        type="card"
      />

      <div className="radish-pit-content">
        {renderContent()}
      </div>
    </div>
  );
};

export default RadishPit;
```

---

## 10. 核心组件设计

### 10.1 账户总览组件

```typescript
// components/AccountOverview/index.tsx
import React from 'react';
import { Card, Button } from '@radish/ui';
import BalanceCard from './BalanceCard';
import StatisticsCard from './StatisticsCard';
import RecentTransactions from './RecentTransactions';
import { useBalance } from '../../hooks/useBalance';

const AccountOverview: React.FC = () => {
  const { balance, loading, refresh } = useBalance();

  return (
    <div className="account-overview">
      <div className="overview-grid">
        <div className="balance-section">
          <BalanceCard balance={balance} loading={loading} onRefresh={refresh} />
        </div>

        <div className="statistics-section">
          <StatisticsCard balance={balance} />
        </div>

        <div className="quick-actions">
          <Card title="快速操作">
            <div className="action-buttons">
              <Button type="primary" icon="mdi:transfer">
                转移萝卜币
              </Button>
              <Button icon="mdi:history">
                收纳记录
              </Button>
              <Button icon="mdi:send">
                发送记录
              </Button>
              <Button icon="mdi:security">
                安全设置
              </Button>
            </div>
          </Card>
        </div>

        <div className="recent-transactions">
          <RecentTransactions />
        </div>
      </div>
    </div>
  );
};
```

```typescript
// components/AccountOverview/BalanceCard.tsx
import React, { useState } from 'react';
import { Card, Switch, Button } from '@radish/ui';
import { formatCoinAmount } from '../../utils/coinFormatter';
import type { UserBalance } from '../../types/balance';

interface BalanceCardProps {
  balance: UserBalance | null;
  loading: boolean;
  onRefresh: () => void;
}

const BalanceCard: React.FC<BalanceCardProps> = ({ balance, loading, onRefresh }) => {
  const [showInCarrot, setShowInCarrot] = useState(true);

  if (loading || !balance) {
    return <Card title="当前存量" loading />;
  }

  return (
    <Card
      title="当前存量"
      extra={
        <div className="balance-controls">
          <Switch
            checked={showInCarrot}
            onChange={setShowInCarrot}
            checkedChildren="胡萝卜"
            unCheckedChildren="白萝卜"
          />
          <Button icon="mdi:refresh" onClick={onRefresh} />
        </div>
      }
    >
      <div className="balance-display">
        <div className="main-balance">
          <span className="amount">
            {formatCoinAmount(balance.balance, showInCarrot)}
          </span>
          <span className="unit">
            {showInCarrot ? '胡萝卜' : '白萝卜'}
          </span>
        </div>

        {!showInCarrot && (
          <div className="sub-balance">
            ({formatCoinAmount(balance.balance, true)} 胡萝卜)
          </div>
        )}

        {balance.frozenBalance > 0 && (
          <div className="frozen-balance">
            🧊 冻结存量: {formatCoinAmount(balance.frozenBalance, showInCarrot)} {showInCarrot ? '胡萝卜' : '白萝卜'}
          </div>
        )}

        <div className="total-balance">
          📊 总资产: {formatCoinAmount(balance.balance + balance.frozenBalance, showInCarrot)} {showInCarrot ? '胡萝卜' : '白萝卜'}
        </div>
      </div>
    </Card>
  );
};
```

### 10.2 转账组件

```typescript
// components/Transfer/index.tsx
import React, { useState } from 'react';
import TransferForm from './TransferForm';
import TransferConfirm from './TransferConfirm';
import TransferResult from './TransferResult';
import type { TransferData, TransferResult } from '../../types/transaction';

type TransferStep = 'form' | 'confirm' | 'result';

const Transfer: React.FC = () => {
  const [currentStep, setCurrentStep] = useState<TransferStep>('form');
  const [transferData, setTransferData] = useState<TransferData | null>(null);
  const [transferResult, setTransferResult] = useState<TransferResult | null>(null);

  const handleFormSubmit = (data: TransferData) => {
    setTransferData(data);
    setCurrentStep('confirm');
  };

  const handleConfirm = async (paymentPassword: string) => {
    if (!transferData) return;

    try {
      const result = await transferAPI.transfer({
        ...transferData,
        paymentPassword
      });

      setTransferResult(result);
      setCurrentStep('result');
    } catch (error) {
      // 处理错误
      console.error('Transfer failed:', error);
    }
  };

  const handleReset = () => {
    setCurrentStep('form');
    setTransferData(null);
    setTransferResult(null);
  };

  return (
    <div className="transfer-container">
      {currentStep === 'form' && (
        <TransferForm onSubmit={handleFormSubmit} />
      )}

      {currentStep === 'confirm' && transferData && (
        <TransferConfirm
          data={transferData}
          onConfirm={handleConfirm}
          onBack={() => setCurrentStep('form')}
        />
      )}

      {currentStep === 'result' && transferResult && (
        <TransferResult
          result={transferResult}
          onReset={handleReset}
        />
      )}
    </div>
  );
};
```

```typescript
// components/Transfer/TransferForm.tsx
import React, { useState } from 'react';
import { Form, Input, Button, Card, Select } from '@radish/ui';
import { useBalance } from '../../hooks/useBalance';
import { useTransferLimit } from '../../hooks/useTransferLimit';
import { formatCoinAmount } from '../../utils/coinFormatter';

interface TransferFormProps {
  onSubmit: (data: TransferData) => void;
}

const TransferForm: React.FC<TransferFormProps> = ({ onSubmit }) => {
  const [form] = Form.useForm();
  const [amountUnit, setAmountUnit] = useState<'carrot' | 'radish'>('carrot');
  const { balance } = useBalance();
  const { limit } = useTransferLimit();

  const handleSubmit = (values: any) => {
    const amount = amountUnit === 'carrot' ? values.amount : values.amount * 1000;

    onSubmit({
      toUserId: values.toUserId,
      amount,
      remark: values.remark
    });
  };

  return (
    <Card title="转移萝卜币" className="transfer-form">
      <Form form={form} onFinish={handleSubmit} layout="vertical">
        <Form.Item
          name="toUserId"
          label="接收方"
          rules={[{ required: true, message: '请输入接收方用户ID或用户名' }]}
        >
          <Input
            placeholder="输入用户名或ID"
            prefix="🔍"
          />
        </Form.Item>

        <Form.Item
          name="amount"
          label="转移数量"
          rules={[
            { required: true, message: '请输入转移数量' },
            { type: 'number', min: 1, message: '转移数量必须大于0' }
          ]}
        >
          <Input
            type="number"
            placeholder="输入金额"
            prefix="💰"
            suffix={
              <Select
                value={amountUnit}
                onChange={setAmountUnit}
                options={[
                  { value: 'carrot', label: '胡萝卜' },
                  { value: 'radish', label: '白萝卜' }
                ]}
              />
            }
          />
        </Form.Item>

        <div className="balance-info">
          <p>可用存量: {formatCoinAmount(balance?.balance || 0, true)} 胡萝卜</p>
          {limit && (
            <p>日剩余限额: {formatCoinAmount(limit.remainingAmount, true)} 胡萝卜</p>
          )}
        </div>

        <Form.Item
          name="remark"
          label="备注信息 (可选)"
        >
          <Input.TextArea
            placeholder="添加转移说明..."
            rows={3}
            maxLength={200}
          />
        </Form.Item>

        <Form.Item>
          <Button type="primary" htmlType="submit" block>
            下一步
          </Button>
        </Form.Item>
      </Form>
    </Card>
  );
};
```

### 10.3 自定义Hooks

```typescript
// hooks/useBalance.ts
import { useState, useEffect, useCallback } from 'react';
import { coinAPI } from '../../../api/coin';
import type { UserBalance } from '../types/balance';

export const useBalance = () => {
  const [balance, setBalance] = useState<UserBalance | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchBalance = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const result = await coinAPI.getBalance();
      setBalance(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : '获取余额失败');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchBalance();

    // 每30秒自动刷新余额
    const interval = setInterval(fetchBalance, 30000);

    return () => clearInterval(interval);
  }, [fetchBalance]);

  return {
    balance,
    loading,
    error,
    refresh: fetchBalance
  };
};
```

```typescript
// hooks/useTransfer.ts
import { useState } from 'react';
import { coinAPI } from '../../../api/coin';
import type { TransferData, TransferResult } from '../types/transaction';

export const useTransfer = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const transfer = async (data: TransferData & { paymentPassword: string }): Promise<TransferResult> => {
    try {
      setLoading(true);
      setError(null);

      const result = await coinAPI.transfer(data);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '转账失败';
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const validateTransfer = async (toUserId: number, amount: number) => {
    try {
      const result = await coinAPI.validateTransfer({ toUserId, amount });
      return result;
    } catch (err) {
      throw new Error(err instanceof Error ? err.message : '验证失败');
    }
  };

  return {
    transfer,
    validateTransfer,
    loading,
    error
  };
};
```

### 10.4 工具函数

```typescript
// utils/coinFormatter.ts

/**
 * 格式化萝卜币金额显示
 * @param amount 金额(胡萝卜)
 * @param showInCarrot 是否显示为胡萝卜单位
 * @returns 格式化后的金额字符串
 */
export const formatCoinAmount = (amount: number, showInCarrot: boolean = true): string => {
  if (showInCarrot) {
    // 胡萝卜单位，添加千分位分隔符
    return amount.toLocaleString('zh-CN');
  } else {
    // 白萝卜单位，转换并保留3位小数
    const radishAmount = amount / 1000;
    return radishAmount.toLocaleString('zh-CN', {
      minimumFractionDigits: 3,
      maximumFractionDigits: 3
    });
  }
};

/**
 * 解析用户输入的金额
 * @param input 用户输入
 * @param unit 单位类型
 * @returns 胡萝卜单位的金额
 */
export const parseAmountInput = (input: string, unit: 'carrot' | 'radish'): number => {
  const amount = parseFloat(input.replace(/,/g, ''));

  if (isNaN(amount) || amount <= 0) {
    throw new Error('请输入有效的金额');
  }

  return unit === 'carrot' ? Math.floor(amount) : Math.floor(amount * 1000);
};

/**
 * 获取交易类型的显示名称和图标
 * @param type 交易类型
 * @returns 显示信息
 */
export const getTransactionTypeInfo = (type: string) => {
  const typeMap: Record<string, { name: string; icon: string; color: string }> = {
    'system_reward': { name: '系统奖励', icon: '🎁', color: '#52c41a' },
    'like_reward': { name: '点赞奖励', icon: '👍', color: '#1890ff' },
    'comment_reward': { name: '评论奖励', icon: '💬', color: '#722ed1' },
    'transfer_in': { name: '转账收入', icon: '↙️', color: '#52c41a' },
    'transfer_out': { name: '转账支出', icon: '↗️', color: '#ff4d4f' },
    'admin_adjust': { name: '管理员调整', icon: '⚙️', color: '#faad14' }
  };

  return typeMap[type] || { name: '未知类型', icon: '❓', color: '#d9d9d9' };
};
```

---

## 相关文档
- [萝卜坑应用总体设计](/guide/radish-pit-system)
- [萝卜坑核心概念](/guide/radish-pit-core-concepts)
- [萝卜坑后端设计](/guide/radish-pit-backend)

---

> 本文档随萝卜坑应用开发持续更新，如有变更请同步修改 [更新日志](/changelog/)。