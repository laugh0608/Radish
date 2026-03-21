import { useTranslation } from 'react-i18next';
import type { Category } from '@/api/forum';
import styles from './CategoryList.module.css';

interface CategoryListProps {
  categories: Category[];
  selectedCategoryId: number | null;
  onSelectCategory: (categoryId: number) => void;
  loading?: boolean;
}

export const CategoryList = ({
  categories,
  selectedCategoryId,
  onSelectCategory,
  loading = false
}: CategoryListProps) => {
  const { t } = useTranslation();

  return (
    <div className={styles.container}>
      <h3 className={styles.title}>{t('forum.category.title')}</h3>
      {loading && <p className={styles.loadingText}>{t('forum.category.loading')}</p>}
      {!loading && categories.length === 0 && <p className={styles.emptyText}>{t('forum.category.empty')}</p>}
      <ul className={styles.list}>
        {categories.map(category => (
          <li key={category.voId}>
            <button
              type="button"
              onClick={() => onSelectCategory(category.voId)}
              className={`${styles.categoryButton} ${
                selectedCategoryId === category.voId ? styles.active : ''
              }`}
            >
              {category.voName}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};
