import { useState } from 'react';
import './ExperienceBar.css';

/**
 * 经验条数据接口
 * 直接使用后端 Vo 字段名，前端适配后端
 */
export interface ExperienceData {
  voUserId: string;
  voCurrentExp: number;
  voCurrentLevel: number;
  voNextLevelExp: number;
  voTotalExp: number;
  voLevelProgress: number;
  voCurrentLevelName: string;
  voLevelDescription?: string;
  voCanLevelUp?: boolean;
  voNextLevelName: string;
  voExpToNextLevel: number;
  voExpGainedToday?: number;
  voDailyExpLimit?: number;
  voRemainingDailyExp?: number;
  voIsMaxLevel?: boolean;
  voRank?: number;
  voPercentile?: number;
  voCreateTime?: string;
  voUpdateTime?: string;
  voExpFrozen?: boolean;
  voFrozenUntil?: string;
  voFrozenReason?: string;
  voThemeColor?: string;
  voLevelUpAt?: string;
}

export interface ExperienceBarProps {
  data: ExperienceData;
  size?: 'small' | 'medium' | 'large';
  showLevel?: boolean;
  showProgress?: boolean;
  showTooltip?: boolean;
  animated?: boolean;
  className?: string;
}

export const ExperienceBar = ({
  data,
  size = 'medium',
  showLevel = true,
  showProgress = true,
  showTooltip = true,
  animated = true,
  className = ''
}: ExperienceBarProps) => {
  const [showDetail, setShowDetail] = useState(false);

  const themeColor = data.voThemeColor || '#4CAF50';

  const classes = [
    'radish-exp-bar',
    `radish-exp-bar--${size}`,
    animated && 'radish-exp-bar--animated',
    data.voExpFrozen && 'radish-exp-bar--frozen',
    className
  ].filter(Boolean).join(' ');

  // voLevelProgress 是 0-1 之间的小数，需要转换为百分比
  const progressPercentage = Math.min(100, Math.max(0, data.voLevelProgress * 100));

  return (
    <div
      className={classes}
      onMouseEnter={() => showTooltip && setShowDetail(true)}
      onMouseLeave={() => showTooltip && setShowDetail(false)}
    >
      {/* 等级徽章 */}
      {showLevel && (
        <div className="radish-exp-bar__level" style={{ color: themeColor }}>
          <span className="radish-exp-bar__level-number">Lv.{data.voCurrentLevel}</span>
          {data.voCurrentLevelName && (
            <span className="radish-exp-bar__level-name">{data.voCurrentLevelName}</span>
          )}
        </div>
      )}

      {/* 经验条容器 */}
      <div className="radish-exp-bar__container">
        {/* 背景轨道 */}
        <div className="radish-exp-bar__track">
          {/* 进度条 */}
          <div
            className="radish-exp-bar__progress"
            style={{
              width: `${progressPercentage}%`,
              backgroundColor: themeColor
            }}
          >
            {/* 光泽效果 */}
            <div className="radish-exp-bar__shine" />
          </div>
        </div>

        {/* 经验值文本 */}
        {showProgress && (
          <div className="radish-exp-bar__text">
            <span className="radish-exp-bar__current">{data.voCurrentExp}</span>
            <span className="radish-exp-bar__separator">/</span>
            <span className="radish-exp-bar__max">{data.voNextLevelExp}</span>
          </div>
        )}
      </div>

      {/* 悬停提示 */}
      {showTooltip && showDetail && (
        <div className="radish-exp-bar__tooltip">
          <div className="radish-exp-bar__tooltip-header">
            <span className="radish-exp-bar__tooltip-level">
              {data.voCurrentLevelName} (Lv.{data.voCurrentLevel})
            </span>
            {data.voRank && (
              <span className="radish-exp-bar__tooltip-rank">
                排名 #{data.voRank}
              </span>
            )}
          </div>

          <div className="radish-exp-bar__tooltip-progress">
            <div className="radish-exp-bar__tooltip-label">当前进度</div>
            <div className="radish-exp-bar__tooltip-value">
              {data.voCurrentExp} / {data.voNextLevelExp} ({progressPercentage.toFixed(1)}%)
            </div>
          </div>

          <div className="radish-exp-bar__tooltip-next">
            <div className="radish-exp-bar__tooltip-label">下一等级</div>
            <div className="radish-exp-bar__tooltip-value">
              {data.voNextLevelName} (Lv.{data.voCurrentLevel + 1})
            </div>
            <div className="radish-exp-bar__tooltip-remaining">
              还需 {data.voExpToNextLevel} 经验值
            </div>
          </div>

          <div className="radish-exp-bar__tooltip-total">
            <div className="radish-exp-bar__tooltip-label">总经验值</div>
            <div className="radish-exp-bar__tooltip-value">{data.voTotalExp}</div>
          </div>

          {data.voExpFrozen && (
            <div className="radish-exp-bar__tooltip-frozen">
              <div className="radish-exp-bar__tooltip-frozen-icon">🔒</div>
              <div className="radish-exp-bar__tooltip-frozen-text">
                经验值已冻结
                {data.voFrozenUntil && ` 至 ${new Date(data.voFrozenUntil).toLocaleDateString()}`}
              </div>
              {data.voFrozenReason && (
                <div className="radish-exp-bar__tooltip-frozen-reason">
                  原因：{data.voFrozenReason}
                </div>
              )}
            </div>
          )}

          {data.voLevelUpAt && (
            <div className="radish-exp-bar__tooltip-levelup">
              上次升级：{new Date(data.voLevelUpAt).toLocaleString()}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
