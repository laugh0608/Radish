import { Children, cloneElement, isValidElement, useLayoutEffect, useMemo, useRef, useState } from 'react'

type StickyChildProps = { sticky?: boolean; stickyTop?: number }

const toPx = (v: string | null | undefined): number => {
  if (!v) return 0
  const n = parseFloat(v.toString())
  return Number.isFinite(n) ? n : 0
}

type Props = {
  children: React.ReactNode
  gap?: number // px 间距（优先于卡片自身 margin-top）
  baseOffset?: number // 顶部基础偏移（在 navbar 高度基础上附加）
}

const StickyStack = ({ children, gap, baseOffset = 12 }: Props) => {
  const items = useMemo(() => Children.toArray(children), [children])
  const refs = useRef<(HTMLElement | null)[]>([])
  const [offsets, setOffsets] = useState<number[]>([])

  const measure = () => {
    const el = document.documentElement
    const cs = getComputedStyle(el)
    const nav = toPx(cs.getPropertyValue('--navbar-height')) || 72
    const base = nav + (baseOffset ?? 0) // 与卡片顶部留白协调

    const newOffsets: number[] = []
    let acc = base

    items.forEach((child, i) => {
      const node = refs.current[i]
      const isSticky = isValidElement(child) && (child.props as StickyChildProps).sticky
      if (!isSticky) return

      let mt = node ? toPx(getComputedStyle(node).marginTop) : 16
      if (typeof gap === 'number') mt = gap
      acc += mt
      newOffsets[i] = Math.round(acc)
      const h = node ? node.offsetHeight : 0
      acc += h
    })

    setOffsets(newOffsets)
  }

  useLayoutEffect(() => {
    measure()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, gap, baseOffset])

  useLayoutEffect(() => {
    const onResize = () => requestAnimationFrame(measure)
    window.addEventListener('resize', onResize)
    window.addEventListener('orientationchange', onResize)
    return () => {
      window.removeEventListener('resize', onResize)
      window.removeEventListener('orientationchange', onResize)
    }
  }, [items])

  return (
    <>
      {items.map((child, i) => {
        if (!isValidElement(child)) return child
        const sticky = (child.props as StickyChildProps).sticky
        const refCb = (node: HTMLElement | null) => {
          refs.current[i] = node
        }
        const extraProps: Partial<StickyChildProps> & { ref?: any } = {}
        if (sticky) extraProps.stickyTop = offsets[i]
        extraProps.ref = refCb
        return cloneElement(child, extraProps)
      })}
    </>
  )
}

export default StickyStack
