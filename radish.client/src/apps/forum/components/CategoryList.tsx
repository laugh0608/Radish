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
  return (
    <div className={styles.container}>
      <h3 className={styles.title}>分类</h3>
      {loading && <p className={styles.loadingText}>加载分类中...</p>}
      {!loading && categories.length === 0 && <p className={styles.emptyText}>暂无分类</p>}
      <ul className={styles.list}>
        {categories.map(category => (
          <li key={category.id}>
            <button
              type="button"
              onClick={() => onSelectCategory(category.id)}
              className={`${styles.categoryButton} ${
                selectedCategoryId === category.id ? styles.active : ''
              }`}
            >
              {category.name}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
};
