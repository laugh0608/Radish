import { useState, useCallback } from 'react';

/**
 * 布尔值切换 Hook
 * @param initialValue 初始值，默认 false
 * @returns [value, toggle, setTrue, setFalse] 元组
 */
export function useToggle(initialValue: boolean = false): [
  boolean,
  () => void,
  () => void,
  () => void
] {
  const [value, setValue] = useState(initialValue);

  const toggle = useCallback(() => setValue(v => !v), []);
  const setTrue = useCallback(() => setValue(true), []);
  const setFalse = useCallback(() => setValue(false), []);

  return [value, toggle, setTrue, setFalse];
}
