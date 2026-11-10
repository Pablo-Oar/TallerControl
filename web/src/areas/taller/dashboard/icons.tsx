// Los íconos del Inicio viven en el kit compartido (Lucide); acá se reexportan con el tamaño propio del Inicio (h-5 w-5).
import type { ComponentType } from 'react'
import type { LucideProps } from 'lucide-react'
import { cn } from '@/lib/utils'
import * as base from '../../../components/ui/icons'

type Icon = ComponentType<LucideProps>

function big(Glyph: Icon) {
  return function DashboardIcon({ className, ...rest }: LucideProps) {
    return <Glyph className={cn('h-5 w-5', className)} {...rest} />
  }
}

export const PlusIcon = big(base.PlusIcon)
export const BanknoteIcon = big(base.BanknoteIcon)
export const AlertIcon = big(base.AlertIcon)
export const WalletIcon = big(base.WalletIcon)
export const WrenchIcon = big(base.WrenchIcon)
export const DocClockIcon = big(base.DocClockIcon)
export const CalendarIcon = big(base.CalendarIcon)
export const FlameIcon = big(base.FlameIcon)
export const BoxIcon = big(base.BoxIcon)
export const ChartIcon = big(base.ChartIcon)
export const CardIcon = big(base.CardIcon)
export const StoreIcon = big(base.StoreIcon)
export const ChecklistIcon = big(base.ChecklistIcon)
export const WhatsappIcon = base.WhatsappIcon
export const ClockIcon = big(base.ClockIcon)
export const UserCogIcon = big(base.UserCogIcon)
