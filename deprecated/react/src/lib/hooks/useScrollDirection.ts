import { useEffect, useRef, useState } from 'react'

// 返回滚动方向和是否显示（根据方向与距离做去抖与阈值）
export function useScrollDirection(options?: { threshold?: number; minScrollTop?: number; idleMs?: number }) {
  const threshold = options?.threshold ?? 8
  const minScrollTop = options?.minScrollTop ?? 64
  const idleMs = options?.idleMs ?? 240

  const lastY = useRef<number>(typeof window !== 'undefined' ? window.scrollY : 0)
  const [direction, setDirection] = useState<'up' | 'down'>('up')
  const [visible, setVisible] = useState<boolean>(true)
  const idleTimer = useRef<number | null>(null)

  useEffect(() => {
    const onScroll = () => {
      const y = window.scrollY
      const diff = y - lastY.current
      if (Math.abs(diff) < threshold) return

      if (diff > 0) {
        // 向下
        if (direction !== 'down') setDirection('down')
        if (y > minScrollTop) setVisible(false)
      } else {
        // 向上
        if (direction !== 'up') setDirection('up')
        setVisible(true)
      }

      lastY.current = y

      // 停止滚动后自动显示（无论方向）
      if (idleTimer.current) window.clearTimeout(idleTimer.current)
      idleTimer.current = window.setTimeout(() => setVisible(true), idleMs)
    }

    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [direction, idleMs, minScrollTop, threshold])

  return { direction, visible }
}
