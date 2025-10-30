import './SectionList.css'

export type SectionItem = {
  label: string
  count?: number
}

const SectionList = ({ items }: { items: SectionItem[] }) => {
  return (
    <ul className="section-list">
      {items.map((it, idx) => (
        <li key={idx} className="section-list__item">
          <span className="section-list__label">{it.label}</span>
          {typeof it.count === 'number' ? <span className="section-list__count">{it.count}</span> : null}
        </li>
      ))}
    </ul>
  )
}

export default SectionList

