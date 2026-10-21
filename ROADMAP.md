# Sparnarok: The Architect's Codex - Roadmap de Desarrollo

Este documento establece la hoja de ruta técnica y arquitectónica para el desarrollo modular de **Sparnarok**, el motor de simulación matemática y orquestación de mundos para **D&D 5e (Reglas 2024)**.

Todos los desarrollos se rigen de forma estricta por la [Regla de Oro (.agent-rules.md)](file:///.agent-rules.md):
> *"Ningún feature puede ser commiteado sin tests completos (Unit, Integration, E2E), cobertura total, y sin la documentación correspondiente (Swagger, TSDoc, XML comments o ADRs)."*

---

## Fase 1: Core Domain & Foundation (Enfoque Actual)

El objetivo de esta fase es asentar el modelo ontológico de reglas 5e (revisión 2024), la persistencia relacional optimizada en PostgreSQL y el primer ciclo vertical completo (Backend, Base de Datos, API, Frontend, Testing y Documentación).

```
   ┌────────────────────────────────────────────────────────┐
   │ 1.1 Entidades de Dominio (Creature, Action, Mastery)   │
   └──────────────────────────┬─────────────────────────────┘
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 1.2 Persistencia EF Core & Migraciones PostgreSQL      │
   └──────────────────────────┬─────────────────────────────┘
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 1.3 Servicios de Aplicación & Endpoints CRUD (Web API) │
   └──────────────────────────┬─────────────────────────────┘
                              ▼
   ┌────────────────────────────────────────────────────────┐
   │ 1.4 Interfaz de Usuario React (sparnarok-web)          │
   └────────────────────────────────────────────────────────┘
```

### Paso 1.1: Entidades Base de Dominio D&D 5e (2024) [Completado]
- **Alcance:**
  - `Creature`: Características (STR, DEX, CON, INT, WIS, CHA), Puntos de Golpe (HP actual, máximo, temporal), Clase de Armadura (AC), Velocidad, Sentidos, Alineamiento, Tamaño y Tipo de Criatura.
  - `Action`: Acciones estándar, bonus actions, reacciones, ataques cuerpo a cuerpo y a distancia, tiradas de salvación provocadas y condiciones impuestas.
  - `WeaponMastery`: Propiedades de Maestría de Armas 2024 (*Cleave*, *Graze*, *Nick*, *Push*, *Sap*, *Slow*, *Topple*, *Vex*).
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/core-dnd-entities` (completada y pusheada).
  - Documentación: [ADR-0002](file:///docs/adrs/0002-dnd-5e-2024-core-domain-modeling.md) (Modelado de Reglas D&D 5e 2024 e Inmutabilidad en el Dominio) y comentarios XML exhaustivos.
  - Testing: 57 pruebas unitarias en `Sparnarok.UnitTests`.

### Paso 1.2: Infraestructura de Persistencia & EF Core [Completado]
- **Alcance:**
  - Configuración de `SparnarokDbContext` con EF Core y PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).
  - Mapeo declarativo mediante Fluent API (evitando Data Annotations en el dominio puro).
  - Generación de la primera migración de esquema en PostgreSQL (`20261021140645_InitialCreate.cs`) con seed data canónico de 8 maestrías de armas.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/efcore-postgres-persistence` (completada y pusheada).
  - Testing: Tests de integración relacionales con `PostgreSqlContainerFixture` (PostgreSQL real y fallback SQLite in-memory).
  - Documentación: [ADR-0003](file:///docs/adrs/0003-efcore-postgresql-persistence-mapping.md) y XML comments en configuraciones.

### Paso 1.3: Capa de Aplicación & Endpoints CRUD (Web API) [Completado]
- **Alcance:**
  - Patrón Repositorio y DTOs fuertemente tipados para CRUD de Criaturas y subrecursos (Acciones y Maestrías de Armas).
  - Validaciones robustas con FluentValidation (`CreateCreatureRequestValidator`, etc.).
  - Controladores RESTful `CreaturesController` y `WeaponMasteriesController` con códigos de respuesta HTTP semánticos (200, 201 Created, 204 NoContent, 400 ValidationProblem, 404 ProblemDetails).
  - Localización nativa (i18n) de mensajes de error con `IStringLocalizer<SharedResource>`.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/creatures-crud-api`.
  - Documentación: [ADR-0004](file:///docs/adrs/0004-application-cqrs-and-crud-endpoints.md) y comentarios XML exhaustivos para Swagger/OpenAPI.
  - Testing: Tests de integración en `Sparnarok.IntegrationTests` con `CustomWebApplicationFactory` ejecutando el flujo táctico completo de punta a punta.

### Paso 1.4: Interfaz de Usuario React (sparnarok-web) [Completado]
- **Alcance:**
  - Vistas y componentes para listado, filtrado multidimensional (nombre, tamaño, tipo), detalle con bloque de estadísticas 2024 y modal de creación/edición de Criaturas.
  - Soporte visual de propiedades de D&D 5e 2024 (badges de Maestría de Armas con código de color temático, desglose de estadísticas y CD de salvación).
  - Cliente de API tipado (`apiClient.ts`) con resiliencia y datos canónicos.
  - Internacionalización completa (español e inglés) con conmutación en caliente sin cadenas fijas.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/creatures-ui-client`.
  - Documentación: [ADR-0005](file:///docs/adrs/0005-frontend-creatures-tactical-ui.md) y comentarios TSDoc exhaustivos en todos los tipos y componentes.
  - Testing: 8 pruebas unitarias/componentes en Vitest y 3 pruebas E2E en Playwright cubriendo exploración, filtrado y renderizado táctico.

---

## Estado Global del Proyecto: ¡Fase 1 Completa al 100%!
Con la conclusión de los Pasos 1.1, 1.2, 1.3 y 1.4, la base ontológica, de persistencia, de API REST y de interfaz gráfica de **Sparnarok** se encuentra totalmente operativa, testeada y documentada bajo arquitectura de monorepo.

---

## Fase 1.5: Data Ingestion Pipeline (Combustible del Motor) [Enfoque Actual]

Antes de iniciar las simulaciones estocásticas de la Fase 2, se requiere una arquitectura de ingesta de datos masiva y resiliente para cargar reglas, armas, hechizos y monstruos mediante archivos CSV estructurados.

```
   ┌────────────────────────────────────────────────────────┐
   │ 1.5.1 Capa de Ingestión Genérica & Streaming CsvHelper │
   │ 1.5.2 Entidad Weapon & Persistencia EF Core Bulk Upsert│
   │ 1.5.3 Endpoint REST POST /api/ingestion/weapons        │
   │ 1.5.4 Componente React de Ingesta & Test Suite E2E     │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - Servicio genérico de parseo y streaming de archivos delimitados mediante `CsvHelper`.
  - Entidad de dominio `Weapon` (Name, Damage, DamageType, Properties, Weight, Cost) y mapeo relacional en EF Core.
  - Endpoint de ingesta masiva `POST /api/ingestion/weapons` (`multipart/form-data`) con validación y estrategia de Bulk Upsert (inserción de nuevas armas y actualización idempotente de existentes).
  - Interfaz de usuario en React con modal de subida de archivos CSV, retroalimentación de estado y métricas de procesamiento.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/csv-ingestion-pipeline`.
  - Documentación: [ADR-0006](file:///docs/adrs/0006-csv-data-ingestion-pipeline.md) justificando el formato CSV y la biblioteca `CsvHelper`.
  - Testing: Tests unitarios de mapeo/parsing en `Sparnarok.UnitTests`, tests de integración relacional con persistencia real en `Sparnarok.IntegrationTests` y test Playwright E2E de carga de archivo desde la UI.

---

## Fases Futuras (Visión de Proyecto)

### Fase 2: Combat Engine & Mathematical Simulation
- Modelado probabilístico de tiradas d20 con ventaja / desventaja.
- Matrices de distribución de daño por turno (*Damage per Round - DPR*).
- Simulador estocástico Monte Carlo para evaluar enfrentamientos tácticos entre grupos de criaturas.

### Fase 3: World Orchestration & Campaign State Machine
- Máquina de estados determinista para eventos temporales, descansos cortos y largos.
- Rastreador de recursos de grupo (espacios de conjuro, dados de golpe, consumibles).
- Motor de orquestación de eventos climatológicos y encuentros dinámicos.

### Fase 4: Telemetría, Optimización y Exportación
- Exportación de métricas matemáticas en PDF / Markdown / JSON.
- Panel de analítica de combate en tiempo real.
