# ADR-0005: Arquitectura Frontend y Cliente de Consumo para Gestión Táctica de Criaturas

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Franco Pandolfo, Arquitecto de Software Sparnarok

## Contexto y Problema
En el Paso 1.3 implementamos los endpoints RESTful para la gestión del agregado `Creature`, sus acciones de ataque y el catálogo de propiedades de maestría de armas (ADR-0004).

Para el **Paso 1.4**, el Roadmap y las directivas de `.agent-rules.md` exigen:
1. Una interfaz de usuario moderna en React 19 y TypeScript (`sparnarok-web`) para visualizar el Grimorio/Bestiario de criaturas.
2. Renderizado del Bloque de Estadísticas canónico de D&D 5e (2024), incluyendo desglose de CA, HP, modificadores de características y cálculo de CD de salvación (`8 + PB + Mod`).
3. Formulario modal guiado para dar de alta nuevas criaturas e incorporar acciones tácticas vinculadas a Maestrías de Armas (*Cleave*, *Graze*, *Nick*, *Push*, *Sap*, *Slow*, *Topple*, *Vex*).
4. Búsqueda y filtrado multidimensional interactivo (por nombre, tipo de criatura y tamaño).
5. Cero cadenas de texto fijas: soporte de internacionalización completa en Español e Inglés con conmutación en caliente.
6. Estética visual prémium: Glassmorphism, tipografía con Google Fonts (`Cinzel` y `Outfit`), micro-animaciones y paleta cósmico-medieval.
7. Cobertura de pruebas completa: Vitest/Testing Library y Playwright E2E.

## Opciones Evaluadas

1. **Frameworks CSS (TailwindCSS / Bootstrap):**
   - *Pros:* Clases utilitarias listas para maquetación rápida.
   - *Contras:* Agrega dependencias y capas de configuración; limita la personalización artística requerida para la estética del Códice (gradientes cósmicos, desenfoques y bordes dorados reactivos).

2. **Vanilla CSS con Sistema de Tokens de Diseño (`:root`):**
   - *Pros:*
     - Control absoluto de propiedades CSS modernas (`backdrop-filter`, `@keyframes`, variables de tema y sombras multicapa).
     - Cero dependencias adicionales, rendimiento óptimo y carga instantánea.
     - Cumple con las especificaciones de diseño estético de Sparnarok.
   - *Contras:* Requiere estructurar las hojas de estilo (`index.css`, `App.css`) de forma meticulosa.

3. **Gestor de Estado Global (Redux Toolkit / Zustand) vs React State con Servicio de API:**
   - *Pros de Redux:* Estado centralizado para aplicaciones masivas.
   - *Contras de Redux:* Sobrecarga innecesaria para la etapa actual; un cliente de API tipado (`apiClient.ts`) con estado local en React Hooks provee un flujo de datos unidireccional más claro y testeable.

## Decisión Adoptada
Se adopta **Vanilla CSS con Tokens de Diseño y un Cliente de Consumo Tipado**:

1. **Tipado Estricto de Dominio (`src/types/dnd.ts`):**
   - Interfaces TypeScript completas con documentación TSDoc reflejando exactamente los DTOs de C# (`CreatureResponseDto`, `CreateCreatureRequestDto`, `WeaponMasteryResponseDto`, etc.).

2. **Cliente de Consumo Desacoplado (`src/services/apiClient.ts`):**
   - Abstracción de llamadas HTTP hacia `/api/v1` mediante `fetch`.
   - Proxy configurado en `vite.config.ts` hacia `http://localhost:5000`.
   - Resiliencia y fallback en memoria con criaturas y maestrías canónicas para pruebas aisladas y entornos sin servicio de backend activo.

3. **Arquitectura de Componentes Modulares (`src/components/`):**
   - `Navbar`: Cabecera con marca, conmutación de pestañas (Bestiario / Simulador), botón de creación y conmutador i18n.
   - `CreatureCard`: Tarjeta táctica con indicadores visuales de CA, PG, Velocidad, CR, Bono de Competencia, 6 características D20 y etiquetas de maestría coloreadas.
   - `CreatureDetailModal`: Ficha táctica completa con cálculo de CD de salvación canónico, listado de ataques y aprendizaje de maestrías.
   - `CreatureFormModal`: Formulario guiado para creación de criaturas con cálculo en tiempo real de modificadores.
   - `ActionFormModal`: Modal para incorporar acciones y asociarlas a maestrías oficiales 2024.
   - `BestiaryView`: Orquestador con barra de búsqueda reactiva, selectores de filtrado y grid responsive.

4. **Internacionalización Integral (i18n):**
   - Diccionarios completos en `src/locales/es/translation.json` y `src/locales/en/translation.json` cubriendo toda la terminología táctica y de interfaz.

5. **Estrategia de Pruebas:**
   - 8 pruebas unitarias y de componentes con Vitest y `@testing-library/react`.
   - 3 pruebas E2E con Playwright validando carga, conmutación bilingüe y el flujo completo de exploración del Bestiario y ficha táctica.

## Consecuencias
- **Positivas:**
  - La **Fase 1: Core Domain & Foundation** queda íntegramente implementada en todos sus niveles (Dominio, Persistencia, API REST, Frontend, Documentación y Testing).
  - 87 pruebas automatizadas superadas al 100% en todo el repositorio.
  - La interfaz está lista para enlazar con el motor de combate probabilístico de la Fase 2.
