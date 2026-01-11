# 7. 前端展示设计

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)

## 7.1 个人主页经验条

**位置**:`radish.client/src/apps/profile/components/ExperienceBar.tsx`

**功能**:
- 显示当前等级昵称和图标
- 显示经验条进度(当前经验/升级所需经验)
- 鼠标悬停显示详细信息(累计经验、排名等)
- 升级时播放动画效果

**示例**:
```tsx
import React from 'react';
import styles from './ExperienceBar.module.css';

interface ExperienceBarProps {
  currentLevel: number;
  currentLevelName: string;
  currentExp: number;
  expToNextLevel: number;
  levelProgress: number;
  themeColor: string;
  totalExp: number;
  rank?: number;
}

export const ExperienceBar: React.FC<ExperienceBarProps> = ({
  currentLevel,
  currentLevelName,
  currentExp,
  expToNextLevel,
  levelProgress,
  themeColor,
  totalExp,
  rank
}) => {
  return (
    <div className={styles.experienceBar}>
      <div className={styles.levelInfo}>
        <div className={styles.levelBadge} style={{ borderColor: themeColor }}>
          <span className={styles.levelText}>Lv.{currentLevel}</span>
          <span className={styles.levelName}>{currentLevelName}</span>
        </div>

        <div className={styles.expDetails}>
          <div className={styles.expText}>
            {currentExp} / {currentExp + expToNextLevel}
          </div>
          <div className={styles.expSubText}>
            距离下一级还需 {expToNextLevel} 经验
          </div>
        </div>
      </div>

      <div className={styles.progressBar}>
        <div
          className={styles.progressFill}
          style={{
            width: `${levelProgress * 100}%`,
            backgroundColor: themeColor
          }}
        >
          <div className={styles.progressGlow} />
        </div>
      </div>

      <div className={styles.statsRow}>
        <div className={styles.statItem}>
          <span className={styles.statLabel}>累计经验</span>
          <span className={styles.statValue}>{totalExp.toLocaleString()}</span>
        </div>
        {rank && (
          <div className={styles.statItem}>
            <span className={styles.statLabel}>经验排名</span>
            <span className={styles.statValue}>#{rank}</span>
          </div>
        )}
      </div>
    </div>
  );
};
```

**CSS 动画**(升级特效):
```css
/* ExperienceBar.module.css */
@keyframes levelUpGlow {
  0%, 100% {
    box-shadow: 0 0 10px rgba(255, 215, 0, 0.5);
  }
  50% {
    box-shadow: 0 0 30px rgba(255, 215, 0, 1);
  }
}

.levelBadge.levelUp {
  animation: levelUpGlow 1.5s ease-in-out 3;
}

@keyframes progressFill {
  0% {
    width: 0%;
  }
  100% {
    width: var(--target-width);
  }
}

.progressFill {
  animation: progressFill 1s ease-out;
}
```

## 7.2 升级动画弹窗

**位置**:`radish.client/src/components/LevelUpModal/LevelUpModal.tsx`

**触发时机**:
- WebSocket 推送升级事件时
- 或刷新页面时检测到等级变化

**功能**:
- 全屏烟花/光效动画
- 显示旧等级 → 新等级
- 显示解锁的新特权
- 自动 3 秒后关闭或手动关闭

**示例**:
```tsx
import React, { useEffect, useState } from 'react';
import { Modal } from '@radish/ui';
import Confetti from 'react-confetti';
import styles from './LevelUpModal.module.css';

interface LevelUpModalProps {
  visible: boolean;
  oldLevel: number;
  oldLevelName: string;
  newLevel: number;
  newLevelName: string;
  newPrivileges: string[];
  themeColor: string;
  onClose: () => void;
}

export const LevelUpModal: React.FC<LevelUpModalProps> = ({
  visible,
  oldLevel,
  oldLevelName,
  newLevel,
  newLevelName,
  newPrivileges,
  themeColor,
  onClose
}) => {
  const [showConfetti, setShowConfetti] = useState(false);

  useEffect(() => {
    if (visible) {
      setShowConfetti(true);
      const timer = setTimeout(() => {
        setShowConfetti(false);
        onClose();
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [visible, onClose]);

  return (
    <>
      {showConfetti && <Confetti recycle={false} numberOfPieces={500} />}

      <Modal visible={visible} onClose={onClose} showCloseButton={false}>
        <div className={styles.levelUpModal}>
          <div className={styles.title}>恭喜升级!</div>

          <div className={styles.levelTransition}>
            <div className={styles.oldLevel}>
              <span className={styles.levelNumber}>Lv.{oldLevel}</span>
              <span className={styles.levelName}>{oldLevelName}</span>
            </div>

            <div className={styles.arrow}>→</div>

            <div className={styles.newLevel} style={{ color: themeColor }}>
              <span className={styles.levelNumber}>Lv.{newLevel}</span>
              <span className={styles.levelName}>{newLevelName}</span>
            </div>
          </div>

          {newPrivileges.length > 0 && (
            <div className={styles.privileges}>
              <div className={styles.privilegesTitle}>解锁新特权</div>
              <ul className={styles.privilegesList}>
                {newPrivileges.map((privilege, index) => (
                  <li key={index} className={styles.privilegeItem}>
                    ✓ {privilege}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <button className={styles.closeButton} onClick={onClose}>
            确定
          </button>
        </div>
      </Modal>
    </>
  );
};
```

## 7.3 Dock 等级徽章显示

**位置**:`radish.client/src/desktop/Dock.tsx`

**功能**:
- 在用户头像旁显示当前等级徽章
- 点击跳转到个人主页经验详情
- 鼠标悬停显示等级进度

**示例**:
```tsx
// 在 Dock 用户信息区域添加
<div className={styles.userInfo}>
  <img src={user.avatarUrl} alt="头像" className={styles.avatar} />

  <div
    className={styles.levelBadge}
    style={{ backgroundColor: user.themeColor }}
    title={`Lv.${user.currentLevel} ${user.currentLevelName}`}
  >
    Lv.{user.currentLevel}
  </div>

  <span className={styles.userName}>{user.userName}</span>
</div>
```

## 7.4 经验值明细页面

**位置**:`radish.client/src/apps/profile/pages/ExperienceDetail.tsx`

**功能**:
- 经验值曲线图(最近 7 天/30 天)
- 经验值来源饼图(发帖/评论/点赞等占比)
- 经验值明细列表(分页)
- 等级排行榜

**使用图表库**:Recharts

**示例**:
```tsx
import React from 'react';
import { LineChart, Line, PieChart, Pie, Cell, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import { ExperienceBar } from '../components/ExperienceBar';
import styles from './ExperienceDetail.module.css';

export const ExperienceDetail: React.FC = () => {
  // 数据从 API 获取...
  const dailyStats = useDailyStats(7);
  const sourceDistribution = useExpSourceDistribution();
  const transactions = useExpTransactions(1, 20);

  return (
    <div className={styles.experienceDetail}>
      <h2>经验值详情</h2>

      {/* 经验条 */}
      <ExperienceBar {...userExp} />

      {/* 经验值曲线 */}
      <div className={styles.chartSection}>
        <h3>经验值趋势</h3>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={dailyStats}>
            <XAxis dataKey="statDate" />
            <YAxis />
            <Tooltip />
            <Line type="monotone" dataKey="expEarned" stroke="#8884d8" strokeWidth={2} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* 经验值来源饼图 */}
      <div className={styles.chartSection}>
        <h3>经验值来源</h3>
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie data={sourceDistribution} dataKey="value" nameKey="name" cx="50%" cy="50%" label>
              {sourceDistribution.map((entry, index) => (
                <Cell key={`cell-${index}`} fill={entry.color} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
      </div>

      {/* 经验值明细列表 */}
      <div className={styles.transactionList}>
        <h3>经验值明细</h3>
        {transactions.map(tx => (
          <div key={tx.id} className={styles.transactionItem}>
            <div className={styles.txInfo}>
              <span className={styles.txType}>{tx.remark}</span>
              <span className={styles.txTime}>{new Date(tx.createdAt).toLocaleString()}</span>
            </div>
            <span className={styles.txAmount}>+{tx.expAmount}</span>
          </div>
        ))}
      </div>
    </div>
  );
};
```

---
