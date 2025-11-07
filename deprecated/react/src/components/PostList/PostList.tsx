import './PostList.css'
import { useI18n } from '../../lib/i18n/useI18n'

const PostList = ({ count = 24 }: { count?: number }) => {
  const { t } = useI18n()
  const items = Array.from({ length: count }, (_, i) => i + 1)
  return (
    <ul className="post-list">
      {items.map((n) => (
        <li key={n} className="post-list__item">
          <h3 className="post-list__title">{t('posts.placeholderTitle')} #{n}</h3>
          <p className="post-list__meta">{t('posts.placeholderMeta')}</p>
        </li>
      ))}
    </ul>
  )
}

export default PostList

