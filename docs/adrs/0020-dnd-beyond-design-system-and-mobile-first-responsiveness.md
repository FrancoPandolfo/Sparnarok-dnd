# ADR-0020: Sistema de Diseño Visual Inspirado en D&D Beyond, Estandarización de Botones, Responsividad Mobile-First y Calidad de Código Pre-Commit

- **Fecha:** 2026-10-27
- **Estado:** Aceptado
- **Decisores:** Equipo de Frontend, UI/UX y Arquitectura de Sparnarok

---

## Contexto y Problema

A medida que el frontend de **Sparnarok: The Architect's Codex** creció con nuevas capacidades tácticas (Bestiario, Simulador Montecarlo, Alijo Colaborativo, Modos de Partida en Vivo y Tracker de Iniciativa con Drag & Drop), surgieron tres desafíos críticos de calidad visual y mantenibilidad:

1. **Fragmentación Estética de Botones:**
   - Coexistían múltiples clases de botones no estandarizadas (`.action-btn`, `.btn-inspect`, `.btn-delete`, `.btn-cancel`, `.btn-submit-gold`, `.run-simulation-btn`, `.hero-create-btn`, `.btn-add-member`, etc.).
   - Muchos botones carecían de estados de accesibilidad consistentes (`:focus-visible`), estados `:active` o micro-interacciones uniformes.
   - La disposición espacial en formularios y barras de acciones carecía de un flujo lógico estandarizado.

2. **Carencia de Responsividad Fluida y Desbordamientos en Pantallas Móviles:**
   - Grillas complejas (Bestiario, setup de enfrentamientos N vs M en el Simulador) no colapsaban adecuadamente en pantallas pequeñas.
   - Las tablas del Tracker de Combate y la barra de navegación generaban desbordamientos (overflow) y scroll horizontal indeseado en dispositivos móviles.

3. **Ausencia de una Barrera Automatizada de Calidad Visual:**
   - Existía linting para TypeScript (`oxlint`), pero no había validación ni formateo automatizado de sintaxis y consultas de medios para CSS, permitiendo que estilos rotos o mal formateados llegaran al repositorio.

---

## Decisión de Arquitectura

Se adoptan tres directivas estratégicas permanentes:

### 1. Sistema de Diseño Visual Inspirado en D&D Beyond
- **Paleta y Contrastes:**
  - Rojo canónico de D&D Beyond: `--dnd-red: #bc252c` (con hover `#d83138`, active `#96191f` y resplandor `rgba(188, 37, 44, 0.45)`).
  - Fondos pizarra y carbón profundo: `--bg-primary: #12181c`, `--bg-surface: #18222b`, `--bg-card: #1c2732`.
  - Acentos de pergamino y oro: `--dnd-gold: #c9a050`, `--dnd-gold-light: #e2c079`.
  - Bordes afilados geométricos: `--radius-sm: 3px` en lugar de bordes hiper-redondeados.
- **Tipografía:**
  - Encabezados: `Cinzel` (serif épico de fantasía).
  - Interfaz y cuerpo: `Roboto` y `Outfit` (sans-serif de máxima legibilidad).
  - Valores numéricos y tiradas: `JetBrains Mono`.

### 2. Sistema Canónico de Botones (`.btn`)
- Toda interacción de botón debe derivar de la clase base `.btn`:
  - `.btn-primary`: Acciones de confirmación prioritarias (Rojo D&D Beyond).
  - `.btn-secondary`: Acciones secundarias o de cancelación (Pizarra Carbón).
  - `.btn-danger`: Acciones destructivas o irreversibles (Carmesí).
  - `.btn-gold`: Destacados de botín/tesoro o llamada especial (Oro pergamino).
  - `.btn-outline`: Bordes sutiles transparentes.
  - Modificadores de escala: `.btn-xs`, `.btn-sm`, `.btn-lg`, `.btn-icon`, `.btn-block`.
- **Accesibilidad Obligatoria:** Todos los botones deben definir `:hover`, `:active`, `:disabled` y `:focus-visible` (halo dorado accesible).
- **Flujo Lógico:** Cancelaciones a la izquierda, confirmaciones a la derecha. En pantallas móviles, colapso fluido (`column-reverse`).

### 3. Responsividad Mobile-First / Fluid Design
- Breakpoints estandarizados:
  - Móvil: `< 640px` (grillas a 1 columna, navbar en columna, botones a 100% o 50% de ancho).
  - Tablet: `640px - 1024px` (grillas a 2 columnas).
  - Escritorio: `> 1024px` (grillas auto-fill y layouts horizontales completos).
- **Tablas Responsivas:** Toda tabla (especialmente el Tracker de Combate) debe encapsularse en `.combatants-table-container` con `overflow-x: auto; -webkit-overflow-scrolling: touch;` y `min-width: 640px`.

### 4. Barrera de Calidad Pre-Commit con Husky + lint-staged + Stylelint
- Integración de `stylelint` y `stylelint-config-standard` con reglas en `.stylelintrc.json`.
- Hook pre-commit en `.husky/pre-commit` que ejecuta `lint-staged` antes de cada commit.
- Si un commit contiene CSS con sintaxis rota o reglas no válidas, el commit es bloqueado automáticamente.

---

## Regla de Negocio Permanente para Agentes y Desarrolladores

> **"En cualquier desarrollo futuro de frontend, el agente debe obligatoriamente verificar que la vista sea 100% responsiva y utilice el sistema de botones estandarizado antes de dar la tarea por terminada."**

---

## Consecuencias y Beneficios

- **Positivas:**
  - Consistencia visual inmediata en toda la plataforma.
  - Experiencia de usuario de nivel comercial (D&D Beyond).
  - Cero fallos por desbordamiento horizontal en smartphones o tablets.
  - Automatización de la calidad CSS en pre-commit sin fricción para el desarrollador.
- **Negativas / Mitigaciones:**
  - Obliga a los desarrolladores a no usar clases ad-hoc. Se mitiga mediante documentación explícita en `.agent-rules.md`, `.cursorrules` y `ADR-0020`.
