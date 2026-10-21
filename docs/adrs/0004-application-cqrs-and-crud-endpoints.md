# ADR-0004: Diseño de la Capa de Aplicación, Patrón Repositorio y Endpoints CRUD para Criaturas

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Franco Pandolfo, Arquitecto de Software Sparnarok

## Contexto y Problema
Tras implementar el modelado de dominio rico (ADR-0002) y la persistencia relacional con EF Core (ADR-0003), la Fase 1 del Roadmap requiere en el **Paso 1.3**:
1. Proveer una API HTTP RESTful para la creación, lectura, actualización y eliminación (CRUD) de criaturas (`Creature`).
2. Permitir la incorporación de acciones tácticas (`Action`) y el aprendizaje de propiedades de maestría de armas (`WeaponMastery`).
3. Cumplir con las directivas inquebrantables de `.agent-rules.md`:
   - Documentación exhaustiva en Swagger/OpenAPI mediante comentarios XML.
   - Respuestas semánticas de error conformes a RFC 7807 (`ProblemDetails` y `ValidationProblemDetails`).
   - Cero cadenas de texto hardcodeadas en mensajes de error del sistema (internacionalización nativa vía `IStringLocalizer<SharedResource>`).
   - Validación robusta de entrada antes de tocar el dominio.
   - Pruebas automatizadas de integración contra `WebApplicationFactory` con base de datos aislada.

## Opciones Evaluadas

1. **Minimal APIs con endpoints en línea:**
   - *Pros:* Sintaxis ligera y menor boilerplate.
   - *Contras:* En agregados complejos con múltiples subrecursos (`/actions`, `/masteries`) y documentación XML exhaustiva para Swagger, las Minimal APIs tienden a dispersar la lógica o concentrar excesivo código en archivos de mapeo.

2. **Controladores Clásicos (`[ApiController]`) con Inyección de Repositorios y Validadores FluentValidation:**
   - *Pros:*
     - Separación nítida de responsabilidades por recurso (`CreaturesController`, `WeaponMasteriesController`).
     - Compatibilidad directa con comentarios XML tripe barra (`///`) para OpenAPI/Swagger.
     - Facilidad para estructurar respuestas de validación (`ValidationProblem()`) y errores de dominio (`NotFound(ProblemDetails)`).
     - Testeabilidad directa mediante `CustomWebApplicationFactory`.
   - *Contras:* Requiere definición explícita de DTOs, interfaces de repositorio y validadores.

## Decisión Adoptada
Se adopta la arquitectura de **Controladores Clásicos desacoplados con Patrón Repositorio y FluentValidation**:

1. **Contratos y DTOs (`Application/DTOs`):**
   - DTOs implementados como `record` inmutables de C#: `CreateCreatureRequestDto`, `UpdateCreatureRequestDto`, `CreatureResponseDto`, `CreateActionRequestDto`, `ActionResponseDto`, `WeaponMasteryResponseDto`.
   - Mapeo bidireccional limpio entre entidades y DTOs manteniendo encapsulados los objetos de valor (`AbilityScores`, `HitPoints`, `ArmorClass`).

2. **Validación Declarativa (`Application/Validators`):**
   - Validadores dedicados con `FluentValidation`: `CreateCreatureRequestValidator`, `UpdateCreatureRequestValidator`, `CreateActionRequestValidator`.
   - Se validan reglas de D&D 5e (2024): límites de estadísticas (1 a 30), CA base (mínimo 1), CR (0 a 30), velocidad (>= 0), fórmulas de dados de daño (`1d8`, `2d6`, etc.).

3. **Abstracciones de Persistencia (`Application/Interfaces` & `Infrastructure/Repositories`):**
   - `ICreatureRepository`: operaciones asíncronas para agregación de criaturas (`GetAllAsync`, `GetByIdAsync`, `AddAsync`, `AddActionAsync`, `UpdateAsync`, `DeleteAsync`, `SaveChangesAsync`).
   - `IWeaponMasteryRepository`: operaciones de consulta para catálogo de maestrías.

4. **Internacionalización y Manejo de Errores (i18n + RFC 7807):**
   - Inyección de `IStringLocalizer<SharedResource>` en los controladores.
   - Incorporación de claves localizadas en `SharedResource.resx`, `SharedResource.es.resx` y `SharedResource.en.resx` (`CreatureNotFound`, `ActionNotFound`, `MasteryNotFound`).
   - Respuestas consistentes: `400 BadRequest` con `ValidationProblemDetails`, `404 NotFound` con `ProblemDetails`.

5. **Pruebas de Integración con `CustomWebApplicationFactory`:**
   - Subclase de `WebApplicationFactory<Program>` que sustituye la conexión de base de datos en tiempo de ejecución por `PostgreSqlContainerFixture` (PostgreSQL real en CI / SQLite in-memory para entornos locales sin Docker).
   - Verificación de la suite completa con cobertura del flujo táctico de punta a punta (Crear criatura -> Agregar acción -> Enseñar maestría -> Consultar grafo completo -> Eliminar criatura).

## Consecuencias
- **Positivas:**
  - Capa de API completamente funcional, documentada y tipada.
  - 100% de pruebas pasando (76 pruebas de backend entre unitarias e integración).
  - Base sólida y lista para construir la interfaz en React (Paso 1.4).
