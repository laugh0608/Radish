import { useEffect, useState } from 'react';
import './LevelUpModal.css';

export interface LevelUpData {
  oldLevel: number;
  newLevel: number;
  oldLevelName: string;
  newLevelName: string;
  themeColor: string;
  rewards?: {
    coins?: number;
    items?: string[];
  };
  message?: string;
}

export interface LevelUpModalProps {
  isOpen: boolean;
  data: LevelUpData;
  onClose: () => void;
  autoClose?: boolean;
  autoCloseDelay?: number;
}

export const LevelUpModal = ({
  isOpen,
  data,
  onClose,
  autoClose = true,
  autoCloseDelay = 5000
}: LevelUpModalProps) => {
  const [isAnimating, setIsAnimating] = useState(false);
  const [showContent, setShowContent] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setIsAnimating(true);
      // 延迟显示内容，让动画更流畅
      setTimeout(() => setShowContent(true), 300);

      // 自动关闭
      if (autoClose) {
        const timer = setTimeout(() => {
          handleClose();
        }, autoCloseDelay);
        return () => clearTimeout(timer);
      }
    } else {
      setShowContent(false);
      setIsAnimating(false);
    }
  }, [isOpen, autoClose, autoCloseDelay]);

  const handleClose = () => {
    setShowContent(false);
    setTimeout(() => {
      setIsAnimating(false);
      onClose();
    }, 300);
  };

  if (!isOpen && !isAnimating) {
    return null;
  }

  return (
    <div
      className={`radish-levelup-modal ${isAnimating ? 'radish-levelup-modal--active' : ''}`}
      onClick={handleClose}
    >
      {/* 背景遮罩 */}
      <div className="radish-levelup-modal__overlay" />

      {/* 粒子效果背景 */}
      <div className="radish-levelup-modal__particles">
        {Array.from({ length: 30 }).map((_, i) => (
          <div
            key={i}
            className="radish-levelup-modal__particle"
            style={{
              left: `${Math.random() * 100}%`,
              animationDelay: `${Math.random() * 2}s`,
              animationDuration: `${2 + Math.random() * 3}s`
            }}
          />
        ))}
      </div>

      {/* 主内容 */}
      <div
        className={`radish-levelup-modal__content ${showContent ? 'radish-levelup-modal__content--show' : ''}`}
        onClick={(e) => e.stopPropagation()}
      >
        {/* 光环效果 */}
        <div
          className="radish-levelup-modal__glow"
          style={{ backgroundColor: data.themeColor }}
        />

        {/* 标题 */}
        <div className="radish-levelup-modal__header">
          <h2 className="radish-levelup-modal__title">🎉 恭喜升级！</h2>
          {data.message && (
            <p className="radish-levelup-modal__message">{data.message}</p>
          )}
        </div>

        {/* 等级变化 */}
        <div className="radish-levelup-modal__levels">
          <div className="radish-levelup-modal__level radish-levelup-modal__level--old">
            <div className="radish-levelup-modal__level-number">Lv.{data.oldLevel}</div>
            <div className="radish-levelup-modal__level-name">{data.oldLevelName}</div>
          </div>

          <div className="radish-levelup-modal__arrow">
            <svg width="40" height="40" viewBox="0 0 40 40" fill="none">
              <path
                d="M10 20 L30 20 M30 20 L24 14 M30 20 L24 26"
                stroke="currentColor"
                strokeWidth="3"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </div>

          <div
            className="radish-levelup-modal__level radish-levelup-modal__level--new"
            style={{ color: data.themeColor }}
          >
            <div className="radish-levelup-modal__level-number">Lv.{data.newLevel}</div>
            <div className="radish-levelup-modal__level-name">{data.newLevelName}</div>
          </div>
        </div>

        {/* 奖励信息 */}
        {data.rewards && (
          <div className="radish-levelup-modal__rewards">
            <h3 className="radish-levelup-modal__rewards-title">升级奖励</h3>
            <div className="radish-levelup-modal__rewards-list">
              {data.rewards.coins !== undefined && data.rewards.coins > 0 && (
                <div className="radish-levelup-modal__reward-item">
                  <span className="radish-levelup-modal__reward-icon">🥕</span>
                  <span className="radish-levelup-modal__reward-text">
                    萝卜币 +{data.rewards.coins}
                  </span>
                </div>
              )}
              {data.rewards.items?.map((item, index) => (
                <div key={index} className="radish-levelup-modal__reward-item">
                  <span className="radish-levelup-modal__reward-icon">🎁</span>
                  <span className="radish-levelup-modal__reward-text">{item}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* 关闭按钮 */}
        <button
          className="radish-levelup-modal__close"
          onClick={handleClose}
          aria-label="关闭"
        >
          ✕
        </button>

        {/* 自动关闭提示 */}
        {autoClose && (
          <div className="radish-levelup-modal__auto-close">
            {Math.ceil(autoCloseDelay / 1000)} 秒后自动关闭
          </div>
        )}
      </div>
    </div>
  );
};
