import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { ExperienceBar } from '@radish/ui/experience-bar';
import { experienceApi, type ExperienceData } from '@/api/experience';
import { useUserStore } from '@/stores/userStore';
import styles from './ExperienceDisplay.module.css';

/**
 * 经验值显示组件
 *
 * 显示用户的等级和经验值进度条
 */
export const ExperienceDisplay = () => {
  const { t } = useTranslation();
  const { isAuthenticated } = useUserStore();
  const [experience, setExperience] = useState<ExperienceData | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchExperience = async () => {
    if (!isAuthenticated()) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await experienceApi.getMyExperience();
      setExperience(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : t('desktop.experience.loadFailed'));
      log.error('获取经验值失败:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchExperience();
    // 每 60 秒刷新一次经验值
    const interval = setInterval(() => {
      void fetchExperience();
    }, 60000);

    return () => clearInterval(interval);
  }, [isAuthenticated()]);

  if (!isAuthenticated()) {
    return null;
  }

  if (loading && !experience) {
    return (
      <div className={styles.experienceDisplay}>
        <span className={styles.loading}>{t('desktop.experience.loading')}</span>
      </div>
    );
  }

  if (error || !experience) {
    return (
      <div className={styles.experienceDisplay} title={error || t('desktop.experience.loadFailed')}>
        <span className={styles.error}>{t('desktop.experience.loadFailed')}</span>
      </div>
    );
  }

  return (
    <div className={styles.experienceDisplay}>
      <ExperienceBar
        data={experience}
        size="small"
        showLevel={true}
        showProgress={true}
        showTooltip={true}
        animated={true}
      />
    </div>
  );
};
