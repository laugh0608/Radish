import { useState } from 'react';
import './ExperienceBar.css';

/**
 * 经验条的稳定展示数据结构。
 * 字段沿用后端 Vo 命名，阈值可由宿主从累计值与剩余值推导。
 */
export interface ExperienceData {
  voUserId: string;
  voCurrentExp: ExperienceNumericValue;
  voCurrentLevel: number;
  voNextLevelExp: ExperienceNumericValue;
  voTotalExp: ExperienceNumericValue;
  voLevelProgress: number;
  voCurrentLevelName: string;
  voLevelDescription?: string;
  voCanLevelUp?: boolean;
  voNextLevelName: string;
  voExpToNextLevel: ExperienceNumericValue;
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

export type ExperienceNumericValue = number | string;

export interface ExperienceBarLabels {
  rank: (rank: string) => string;
  currentProgress: string;
  nextLevel: string;
  remainingExperience: (value: string) => string;
  totalExperience: string;
  frozen: string;
  frozenUntil: (value: string) => string;
  frozenReason: (value: string) => string;
  lastLevelUp: (value: string) => string;
}

export interface ExperienceBarPresentation {
  labels: ExperienceBarLabels;
  formatNumber: (value: ExperienceNumericValue) => string;
  formatPercentage: (value: number) => string;
  formatDateTime: (value: string | number | Date) => string;
}

export interface ExperienceBarProps {
  data: ExperienceData;
  size?: 'small' | 'medium' | 'large';
  showLevel?: boolean;
  showProgress?: boolean;
  showTooltip?: boolean;
  animated?: boolean;
  className?: string;
  presentation: ExperienceBarPresentation;
}

export const ExperienceBar = ({
  data,
  size = 'medium',
  showLevel = true,
  showProgress = true,
  showTooltip = true,
  animated = true,
  className = '',
  presentation,
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
  const progressRatio = Math.min(1, Math.max(0, data.voLevelProgress));
  const progressPercentage = progressRatio * 100;
  const formattedCurrentExp = presentation.formatNumber(data.voCurrentExp);
  const formattedNextLevelExp = presentation.formatNumber(data.voNextLevelExp);

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
            <span className="radish-exp-bar__current">{formattedCurrentExp}</span>
            <span className="radish-exp-bar__separator">/</span>
            <span className="radish-exp-bar__max">{formattedNextLevelExp}</span>
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
                {presentation.labels.rank(presentation.formatNumber(data.voRank))}
              </span>
            )}
          </div>

          <div className="radish-exp-bar__tooltip-progress">
            <div className="radish-exp-bar__tooltip-label">{presentation.labels.currentProgress}</div>
            <div className="radish-exp-bar__tooltip-value">
              {formattedCurrentExp} / {formattedNextLevelExp} ({presentation.formatPercentage(progressRatio)})
            </div>
          </div>

          <div className="radish-exp-bar__tooltip-next">
            <div className="radish-exp-bar__tooltip-label">{presentation.labels.nextLevel}</div>
            <div className="radish-exp-bar__tooltip-value">
              {data.voNextLevelName} (Lv.{data.voCurrentLevel + 1})
            </div>
            <div className="radish-exp-bar__tooltip-remaining">
              {presentation.labels.remainingExperience(presentation.formatNumber(data.voExpToNextLevel))}
            </div>
          </div>

          <div className="radish-exp-bar__tooltip-total">
            <div className="radish-exp-bar__tooltip-label">{presentation.labels.totalExperience}</div>
            <div className="radish-exp-bar__tooltip-value">{presentation.formatNumber(data.voTotalExp)}</div>
          </div>

          {data.voExpFrozen && (
            <div className="radish-exp-bar__tooltip-frozen">
              <div className="radish-exp-bar__tooltip-frozen-icon">🔒</div>
              <div className="radish-exp-bar__tooltip-frozen-text">
                {presentation.labels.frozen}
                {data.voFrozenUntil && presentation.labels.frozenUntil(presentation.formatDateTime(data.voFrozenUntil))}
              </div>
              {data.voFrozenReason && (
                <div className="radish-exp-bar__tooltip-frozen-reason">
                  {presentation.labels.frozenReason(data.voFrozenReason)}
                </div>
              )}
            </div>
          )}

          {data.voLevelUpAt && (
            <div className="radish-exp-bar__tooltip-levelup">
              {presentation.labels.lastLevelUp(presentation.formatDateTime(data.voLevelUpAt))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
