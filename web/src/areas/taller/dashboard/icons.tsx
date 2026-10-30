import type { SVGProps } from 'react'

/** Íconos de trazo del Inicio (24×24, `currentColor`). Propios, sin biblioteca de íconos (D-25). */
function Icon({ children, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className="h-5 w-5"
      {...props}
    >
      {children}
    </svg>
  )
}

type P = SVGProps<SVGSVGElement>

export const PlusIcon = (p: P) => (
  <Icon {...p}>
    <path d="M12 5v14M5 12h14" />
  </Icon>
)

export const BanknoteIcon = (p: P) => (
  <Icon {...p}>
    <rect x="2.5" y="6" width="19" height="12" rx="1" />
    <circle cx="12" cy="12" r="2.5" />
    <path d="M6 9.5v.01M18 14.5v.01" />
  </Icon>
)

export const AlertIcon = (p: P) => (
  <Icon {...p}>
    <path d="M12 3.5 2.5 20h19L12 3.5Z" />
    <path d="M12 10v4.5M12 17.5v.01" />
  </Icon>
)

export const WalletIcon = (p: P) => (
  <Icon {...p}>
    <path d="M4 7.5A2.5 2.5 0 0 1 6.5 5H19v3" />
    <rect x="3.5" y="8" width="17" height="11.5" rx="1" />
    <path d="M16 13.5h2" />
  </Icon>
)

export const WrenchIcon = (p: P) => (
  <Icon {...p}>
    <path d="M14.5 6.5a4 4 0 0 0-5.3 5L3.5 17.2a1.8 1.8 0 0 0 2.6 2.6l5.7-5.7a4 4 0 0 0 5-5.3l-2.6 2.6-2.2-.5-.5-2.2 2.5-2.2Z" />
  </Icon>
)

export const DocClockIcon = (p: P) => (
  <Icon {...p}>
    <path d="M13 3.5H7A1.5 1.5 0 0 0 5.5 5v14A1.5 1.5 0 0 0 7 20.5h4" />
    <path d="M13 3.5 18.5 9H14a1 1 0 0 1-1-1V3.5Z" />
    <circle cx="17" cy="17" r="4" />
    <path d="M17 15.2V17l1.2.8" />
  </Icon>
)

export const CalendarIcon = (p: P) => (
  <Icon {...p}>
    <rect x="3.5" y="5" width="17" height="15" rx="1" />
    <path d="M3.5 10h17M8 3v4M16 3v4" />
  </Icon>
)

export const FlameIcon = (p: P) => (
  <Icon {...p}>
    <path d="M12 3c.5 3-1.5 4.5-3 6.5S7 13 8.5 15.5a3.5 3.5 0 0 0 7 0c1.5-.5 2.5-2.5 2-5C16.5 8 14 6 12 3Z" />
  </Icon>
)

export const BoxIcon = (p: P) => (
  <Icon {...p}>
    <path d="M12 3 3.5 7.5v9L12 21l8.5-4.5v-9L12 3Z" />
    <path d="M3.5 7.5 12 12l8.5-4.5M12 12v9" />
  </Icon>
)

export const ChartIcon = (p: P) => (
  <Icon {...p}>
    <path d="M4 20V10M10 20V4M16 20v-7M21 20H3" />
  </Icon>
)

export const CardIcon = (p: P) => (
  <Icon {...p}>
    <rect x="2.5" y="5.5" width="19" height="13" rx="1" />
    <path d="M2.5 10h19M6 15h4" />
  </Icon>
)

export const StoreIcon = (p: P) => (
  <Icon {...p}>
    <path d="M4 9.5 5.5 4h13L20 9.5" />
    <path d="M4 9.5a2.7 2.7 0 0 0 5.3 0 2.7 2.7 0 0 0 5.4 0 2.7 2.7 0 0 0 5.3 0" />
    <path d="M5 12.5V20h14v-7.5M10 20v-4h4v4" />
  </Icon>
)

export const ChecklistIcon = (p: P) => (
  <Icon {...p}>
    <path d="M4 7l1.8 1.8L9 5.5M4 15l1.8 1.8L9 13.5M12.5 7.5H20M12.5 15.5H20" />
  </Icon>
)

export const WhatsappIcon = (p: P) => (
  <Icon {...p}>
    <path d="M4 20l1.2-4.1A8 8 0 1 1 8.3 18.9L4 20Z" />
    <path d="M9 9.2c.3 2.2 2.6 4.5 4.8 4.8l1.2-1.2-1.8-1-.8.7c-.9-.4-1.7-1.2-2.1-2.1l.7-.8-1-1.8L9 9.2Z" />
  </Icon>
)

export const ClockIcon = (p: P) => (
  <Icon {...p}>
    <circle cx="12" cy="12" r="8.5" />
    <path d="M12 7.5V12l3 1.8" />
  </Icon>
)

export const UserCogIcon = (p: P) => (
  <Icon {...p}>
    <circle cx="12" cy="8" r="3.5" />
    <path d="M5 20a7 7 0 0 1 14 0" />
  </Icon>
)
