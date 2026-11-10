import type { ComponentType, SVGProps } from 'react'
import {
  ArrowLeft,
  ArrowRight,
  Banknote,
  Bell,
  Calendar,
  ChartColumn,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  ChevronsUpDown,
  Ellipsis,
  LoaderCircle,
  LogOut,
  Search,
  UserRoundX,
  Clock,
  Copy,
  CreditCard,
  Eye,
  EyeOff,
  FileClock,
  Flame,
  Link,
  ListChecks,
  Lock,
  Mail,
  Menu,
  Package,
  Plus,
  Store,
  TriangleAlert,
  User,
  UserCog,
  Wallet,
  Wrench,
  X,
  type LucideProps,
} from 'lucide-react'
import { cn } from '@/lib/utils'

/**
 * Íconos de la app (D-25 reabierta, 2026-11-10): Lucide con trazo de 1,8 px.
 * `className` reemplaza el tamaño por defecto (h-4 w-4); todos llevan `aria-hidden`.
 * Cada pantalla importa de acá, no de `lucide-react`, para poder cambiar la biblioteca en un solo lugar.
 */
type IconComponent = ComponentType<LucideProps>

function wrap(Glyph: IconComponent, size = 'h-4 w-4') {
  return function AppIcon({ className, ...rest }: LucideProps) {
    return <Glyph strokeWidth={1.8} aria-hidden="true" className={cn(size, className)} {...rest} />
  }
}

export const MailIcon = wrap(Mail)
export const LockIcon = wrap(Lock)
export const EyeIcon = wrap(Eye)
export const EyeOffIcon = wrap(EyeOff)
export const UserIcon = wrap(User)
export const WrenchIcon = wrap(Wrench)
export const ArrowRightIcon = wrap(ArrowRight)
export const ArrowLeftIcon = wrap(ArrowLeft)
export const CopyIcon = wrap(Copy)
export const CheckIcon = wrap(Check)
export const LinkIcon = wrap(Link)
export const PlusIcon = wrap(Plus)
export const CloseIcon = wrap(X)
export const MenuIcon = wrap(Menu)
export const BellIcon = wrap(Bell)
export const ChevronDownIcon = wrap(ChevronDown)
export const ChevronUpIcon = wrap(ChevronUp)
export const ChevronLeftIcon = wrap(ChevronLeft)
export const ChevronRightIcon = wrap(ChevronRight)
export const SortIcon = wrap(ChevronsUpDown)
export const MoreIcon = wrap(Ellipsis, 'h-5 w-5')
export const SearchIcon = wrap(Search)
export const LogOutIcon = wrap(LogOut)
export const SpinnerIcon = wrap(LoaderCircle)
export const UserOffIcon = wrap(UserRoundX)
export const AlertIcon = wrap(TriangleAlert)
export const BanknoteIcon = wrap(Banknote)
export const WalletIcon = wrap(Wallet)
export const DocClockIcon = wrap(FileClock)
export const CalendarIcon = wrap(Calendar)
export const FlameIcon = wrap(Flame)
export const BoxIcon = wrap(Package)
export const ChartIcon = wrap(ChartColumn)
export const CardIcon = wrap(CreditCard)
export const StoreIcon = wrap(Store)
export const ChecklistIcon = wrap(ListChecks)
export const ClockIcon = wrap(Clock)
export const UserCogIcon = wrap(UserCog)

/** Marcas (Lucide no incluye logotipos de terceros). */
export const WhatsappIcon = (p: SVGProps<SVGSVGElement>) => (
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth={1.8}
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
    {...p}
    className={cn('h-4 w-4', p.className)}
  >
    <path d="M4 20l1.2-4.1A8 8 0 1 1 8.3 18.9L4 20Z" />
    <path d="M9 9.2c.3 2.2 2.6 4.5 4.8 4.8l1.2-1.2-1.8-1-.8.7c-.9-.4-1.7-1.2-2.1-2.1l.7-.8-1-1.8L9 9.2Z" />
  </svg>
)

/** Logotipo de Excel: hoja con la X (relleno blanco, para usar sobre el verde de Excel). */
export const ExcelIcon = (p: SVGProps<SVGSVGElement>) => (
  <svg viewBox="0 0 24 24" aria-hidden="true" {...p} className={cn('h-4 w-4', p.className)}>
    <path fill="currentColor" fillOpacity="0.35" d="M13 3h7a1 1 0 0 1 1 1v16a1 1 0 0 1-1 1h-7z" />
    <path fill="currentColor" d="M2 4.5 13 3v18L2 19.5z" />
    <path fill="#15803d" d="m5.6 8.6 1.7-.1 1.2 2.4 1.2-2.6 1.7-.1-2 3.5 2 3.6-1.8-.1-1.2-2.6-1.2 2.5-1.7-.1 2-3.4z" />
  </svg>
)
