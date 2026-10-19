# Código vendored de Watermelon UI

- Origen: https://ui.watermelon.sh (registry compatible con la CLI de shadcn); repo `WatermelonCorp/watermelon-platform`.
- Licencia (D-29, verificada 19/10/2026): **MIT**, ver `LICENSE-watermelon.md`. Uso comercial permitido; conservar el aviso de copyright.
- Las páginas NO importan de esta carpeta: usan los wrappers de `components/ui` (D-24, test T-UI-08).
- Registrar abajo, por archivo: componente, fecha de instalación y cambios manuales.

| Archivo | Instalado | Cambios manuales |
|---|---|---|

## Procedimiento de instalación (validado en el spike 0.9, 19/10/2026)

1. `tsconfig.json` raíz debe tener `compilerOptions.paths` con `@/*`; si no, la CLI crea una carpeta literal `@/`.
2. `cd web && npx shadcn@latest add https://registry.watermelon.sh/r/<slug>.json -y` (**sin `-o`**).
3. La CLI deja los componentes en `src/components/<slug>.tsx` (tipo `registry:component` usa el alias `components`, no `ui`): moverlos a `components/watermelon/`.
4. La CLI agrega la dependencia npm espuria `cn` y reemplaza `lib/utils.ts` por `export { cn } from "cn"`: revertir ambos (`git checkout src/lib/utils.ts package.json package-lock.json`, `npm uninstall cn`). `cn()` ya existe con clsx + tailwind-merge.
5. Verificar `npm run build` y `npm run lint`; registrar el archivo en la tabla de abajo.

## Hallazgos sobre floating-input (base)

- Compila sin cambios con el tsconfig estricto (TS 6, `verbatimModuleSyntax`, `erasableSyntaxOnly`); 0 warnings de lint; sin delta de bundle mientras nadie lo importe.
- **No sirve tal cual para los formularios:** el `<label>` no está asociado al input (sin `htmlFor`/`id`; rompe el trabajo de a11y de PR #35), `hasValue` es estado interno y no refleja valores controlados o seteados por React Hook Form (`reset`, `setValue`), usa `bg-input` (que en nuestro puente es un color de borde) y `rounded-lg` fijo en vez de `--radius`, y su `peer-focus` duplica la lógica del estado. El wrapper `FormField` deberá corregir todo esto (id/htmlFor, label flotante por CSS `:placeholder-shown`, forwardRef, `error`/`hint`, `aria-invalid`/`aria-describedby`).
