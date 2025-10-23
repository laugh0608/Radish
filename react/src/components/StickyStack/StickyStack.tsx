import { Children, cloneElement, isValidElement, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'

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

    // 找到所有 sticky 索引，便于向后看间距
    const stickyIdx: number[] = []
    items.forEach((c, i) => {
      if (isValidElement(c) && (c.props as StickyChildProps).sticky) stickyIdx.push(i)
    })

    stickyIdx.forEach((i, order) => {
      const node = refs.current[i]
      // 当前卡片吸附位置 = 已累计高度
      newOffsets[i] = Math.round(acc)
      const h = node ? node.offsetHeight : 0

      // 与下一张 sticky 卡片之间的间距
      let g = typeof gap === 'number' ? gap : 0
      if (g === 0) {
        // 使用下一张卡片自身的 margin-top 作为间隙（若存在）
        const nextIdx = stickyIdx[order + 1]
        const nextNode = nextIdx !== undefined ? refs.current[nextIdx] : null
        g = nextNode ? toPx(getComputedStyle(nextNode).marginTop) : 0
      }
      acc += h + g
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

  // ResizeObserver：当任一 sticky 卡片尺寸变化（如图片加载）时，重新测量
  useEffect(() => {
    if (typeof ResizeObserver === 'undefined') return
    const ro = new ResizeObserver(() => {
      requestAnimationFrame(measure)
    })
    refs.current.forEach((n) => n && ro.observe(n))
    return () => ro.disconnect()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items, gap, baseOffset])

  return (
    <>
      {items.map((child, i) => {
        if (!isValidElement(child)) return child
        const sticky = (child.props as StickyChildProps).sticky
        const refCb = (node: HTMLElement | null) => {
          refs.current[i] = node
        }
        const extraProps: Partial<StickyChildProps> & { ref?: any; style?: React.CSSProperties } = {}
        if (sticky) {
          extraProps.stickyTop = offsets[i]
          // z-index：先出现的卡片层级更低，后出现的更高（1,2,3...），且低于导航栏(z=10)
          const stickyOnly = items.filter((c) => isValidElement(c) && (c.props as StickyChildProps).sticky)
          const stickyOrder = stickyOnly.indexOf(child)
          const z = 1 + stickyOrder
          extraProps.style = { zIndex: z } as React.CSSProperties
        }
        extraProps.ref = refCb
        return cloneElement(child, extraProps)
      })}
    </>
  )
}

export default StickyStack
