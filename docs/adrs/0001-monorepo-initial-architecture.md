# ADR-0001: Arquitectura Inicial de Monorepo y Stack Tecnológico

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Franco Pandolfo, Arquitecto de Software Sparnarok

## Contexto y Problema
"Sparnarok: The Architect's Codex" es una plataforma avanzada de simulación matemática y orquestación de mundos para D&D 5e. Requiere un motor de alta precisión numérica, baja latencia en cálculos probabilísticos de combate, gestión de estado persistente, interfaz visual reactiva e interactiva, e internacionalización nativa desde el día cero. Además, se requiere una política inquebrantable de calidad: ningún feature puede ser incorporado sin pruebas exhaustivas (Unit, Integration, E2E) y documentación completa.

## Opciones Evaluadas
1. **Multi-repo separado (Backend repo, Frontend repo, E2E repo):**
   - *Pros:* Independencia de pipelines.
   - *Contras:* Fricción en sincronización de tipos y contratos API, dificultad para mantener integridad atómica de features y trazabilidad de commits y tests E2E.
2. **Monorepo Unificado (src/backend, src/frontend, tests, docs):**
   - *Pros:* Integridad atómica, control de versiones unificado, CI/CD coordinado en una sola fuente de verdad, visibilidad transversal de ADRs y reglas.
   - *Contras:* Requiere disciplina y tooling adecuado en pipelines CI para no alargar tiempos de build innecesariamente.

## Decisión Adoptada
Adoptar una arquitectura de **Monorepo Unificado** con la siguiente distribución:
- **Backend:** .NET 10 Web API (`/src/backend/Sparnarok.Api`) utilizando C# y Entity Framework Core con PostgreSQL.
- **Frontend:** React con TypeScript sobre Vite (`/src/frontend/sparnarok-web`), maximizando rendimiento, HMR y tipado estricto.
- **Testing Multi-Nivel:**
  - `/tests/backend/Sparnarok.UnitTests`: xUnit + Moq/NSubstitute para cálculos puros de simulación D&D.
  - `/tests/backend/Sparnarok.IntegrationTests`: xUnit + Testcontainers (PostgreSQL) para persistencia real.
  - `/tests/e2e`: Playwright para validación de flujos completos de usuario.
- **i18n:** Soporte nativo desde el día cero en backend y frontend (cero cadenas hardcodeadas).
- **Documentación & ADRs:** `/docs/adrs` para decisiones de arquitectura, OpenAPI/Swagger con comentarios XML, y TSDoc para el frontend.

## Consecuencias
- **Positivas:**
  - Cumplimiento verificable de la Regla de Oro: features con cobertura total de unit, integration y e2e en el mismo árbol de commits.
  - Consistencia tipada entre esquemas de API y modelos de frontend.
  - Trazabilidad documental continua mediante ADRs versionados.
- **Trade-offs:**
  - El pipeline de CI debe compilar y probar múltiples runtimes (.NET SDK y Node.js/Playwright). Se mitiga usando caching y Docker en GitHub Actions.
