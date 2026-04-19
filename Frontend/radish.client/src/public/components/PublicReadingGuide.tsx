import styles from './PublicReadingGuide.module.css';

export interface PublicReadingGuideItem {
  label: string;
  value: string;
}

interface PublicReadingGuideProps {
  label: string;
  title: string;
  description: string;
  items: readonly PublicReadingGuideItem[];
  className?: string;
}

export const PublicReadingGuide = ({
  label,
  title,
  description,
  items,
  className
}: PublicReadingGuideProps) => {
  const sectionClassName = className
    ? `${styles.section} ${className}`
    : styles.section;

  return (
    <section className={sectionClassName} aria-label={title}>
      <div className={styles.header}>
        <div className={styles.heading}>
          <span className={styles.label}>{label}</span>
          <h2 className={styles.title}>{title}</h2>
        </div>
        <p className={styles.description}>{description}</p>
      </div>

      <div className={styles.grid}>
        {items.map((item) => (
          <article key={item.label} className={styles.item}>
            <span className={styles.itemLabel}>{item.label}</span>
            <span className={styles.itemValue}>{item.value}</span>
          </article>
        ))}
      </div>
    </section>
  );
};
