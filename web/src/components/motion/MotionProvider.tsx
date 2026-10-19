import type { ReactNode } from 'react'
import { MotionConfig } from 'motion/react'

// D-27: respeta la preferencia del sistema; en tests se fuerza "always" desde setup.ts.
export function MotionProvider({
  children,
  reducedMotion = 'user',
}: {
  children: ReactNode
  reducedMotion?: 'user' | 'always' | 'never'
}) {
  return <MotionConfig reducedMotion={reducedMotion}>{children}</MotionConfig>
}
