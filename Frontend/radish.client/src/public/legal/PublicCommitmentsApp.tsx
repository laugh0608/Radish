import { useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { PublicShellHeader } from '../components/PublicShellHeader';
import { PrivacySafetyBoundaryPanel } from '../../privacy/PrivacySafetyBoundaryPanel';
import {
  publicCommitmentSections,
  publicCommitmentSummaries,
  publicCommitmentsHero,
  publicCommitmentsSourceLanguage,
} from './publicCommitmentsData';
import styles from './PublicCommitmentsApp.module.css';

export function PublicCommitmentsApp() {
  const { t } = useTranslation();
  const pageRef = useRef<HTMLDivElement | null>(null);

  return (
    <div className={styles.page} ref={pageRef}>
      <PublicShellHeader
        brandMark="规"
        brandName={t('legal.header.brandName')}
        brandSubline={t('legal.header.brandSubline')}
        activeKey="legal"
        onBrandClick={() => pageRef.current?.scrollTo({ top: 0, behavior: 'smooth' })}
        loginLabel={t('public.shell.loginAction')}
      />

      <main className={styles.main}>
        <section className={styles.hero}>
          <div className={styles.publishedContent} lang={publicCommitmentsSourceLanguage}>
            <span className={styles.eyebrow}>{publicCommitmentsHero.eyebrow}</span>
            <h1>{publicCommitmentsHero.title}</h1>
            <p>{publicCommitmentsHero.description}</p>
          </div>
          <p className={styles.originalLanguageNote}>{t('legal.originalLanguageNote')}</p>
          <div
            className={styles.anchorRail}
            aria-label={t('legal.sectionsLabel')}
            lang={publicCommitmentsSourceLanguage}
          >
            {publicCommitmentSections.map((section) => (
              <a key={section.id} href={`#${section.id}`}>
                {section.title}
              </a>
            ))}
          </div>
        </section>

        <section
          className={styles.summaryGrid}
          aria-label={t('legal.summaryLabel')}
          lang={publicCommitmentsSourceLanguage}
        >
          {publicCommitmentSummaries.map((summary) => (
            <div key={summary.label}>
              <span>{summary.label}</span>
              <strong>{summary.title}</strong>
              <p>{summary.description}</p>
            </div>
          ))}
        </section>

        <PrivacySafetyBoundaryPanel />

        <div className={styles.sectionList} lang={publicCommitmentsSourceLanguage}>
          {publicCommitmentSections.map((section) => (
            <section key={section.id} id={section.id} className={styles.section}>
              <div className={styles.sectionHeader}>
                <span>{section.eyebrow}</span>
                <h2>{section.title}</h2>
                <p>{section.description}</p>
              </div>
              <ul>
                {section.items.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </main>
    </div>
  );
}
