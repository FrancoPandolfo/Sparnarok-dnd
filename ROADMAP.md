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

## Fase 1.5: Data Ingestion Pipeline (Combustible del Motor) [Completado]

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

## Fase 2: Módulo 1 - Combat Engine Core [Completado]

Desarrollo del motor fundacional de combate 1 contra 1 en el backend (`CombatSimulatorService`), modelando la resolución estocástica y determinista de iniciativa, ataques d20, daño contra Armor Class y ciclo de turnos hasta incapacitación.

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.1.1 Abstracción de Generación Aleatoria (IRandomProvider)│
   │ 2.1.2 Parser y Evaluador de Expresiones de Dados (DiceRoller)│
   │ 2.1.3 Dominio de Combate 1v1 (CombatSimulatorService)  │
   │ 2.1.4 Pruebas Unitarias Exhaustivas con RNG Determinista│
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - Abstracción de generación de números aleatorios (`IRandomProvider`, `DefaultRandomProvider`, `DeterministicRandomProvider`) permitiendo pruebas 100% deterministas y reproducibles.
  - Parser robusto de dados (`DiceParser` / `DiceRoller`) que interprete fórmulas estándar de D&D (ej. "1d8", "2d6", "1d4+2", "1") y soporte tiradas de ataque y daño crítico (duplicación de dados).
  - Servicio de simulación `CombatSimulatorService` que orqueste iniciativa, resolución de turnos de ataque, impacto crítico (natural 20), fallo automático (natural 1), cálculo de daño y logs cronológicos.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/combat-engine-core`.
  - Documentación: [ADR-0007](file:///docs/adrs/0007-combat-engine-rng-and-dice-evaluation.md) justificando la arquitectura de RNG desacoplado y el evaluador de dados.
  - Testing: Suite exhaustiva de pruebas unitarias en `Sparnarok.UnitTests` con semillas y secuencias RNG controladas para validar críticos, impactos, fallos y victoria.

---

## Fase 2: Módulo 2 - Monte Carlo Runner & Tactical UI [Completado]

Ejecución concurrente masiva de duelos 1v1 a través de hilos paralelos de CPU (`Parallel.For` con acumuladores locales por hilo), agregación matemática rigurosa (Win Rates, Average Rounds, DPR por combatiente) y visualización táctica en React.

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.2.1 Motor Montecarlo Concurrente (Parallel.For)      │
   │ 2.2.2 FastDuelResult de Asignación Cero en Stack       │
   │ 2.2.3 Endpoint REST POST /api/simulation/montecarlo    │
   │ 2.2.4 Vista React CombatSimulatorView & Métricas DPR   │
   │ 2.2.5 Test Suite Integral: Unitarias & Playwright E2E  │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - `MontecarloSimulationService` con particionamiento eficiente de CPU y reducción libre de contención mediante variables locales por hilo.
  - DTO de resultado agregado `MontecarloResultDto` con porcentajes de victoria de A y B, tasa de empates, promedio de rondas, y daño promedio por ronda (DPR) de cada combatiente.
  - Endpoint documentado `POST /api/simulation/montecarlo` con Swagger/XML comments.
  - Componente de frontend `CombatSimulatorView` con selectores tácticos, barra segmentada de Win Rate y tarjetas de métricas cuantitativas.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/montecarlo-runner-and-ui`.
  - Documentación: [ADR-0008](file:///docs/adrs/0008-montecarlo-simulation-concurrency-and-metrics.md).
  - Testing: Pruebas unitarias de agregación matemática en `MontecarloSimulationServiceTests`, pruebas de integración de endpoint en `SimulationControllerIntegrationTests`, pruebas de componente en `CombatSimulatorView.test.tsx` y test E2E de Playwright en `smoke.spec.ts` validando el ciclo de vida completo (Frontend -> Backend -> Motor de Combate -> DOM).

---

## Fase 2.1: Armor Ingestion & Dynamic AC [Completado]

Ingesta masiva del catálogo oficial de armaduras y escudos desde `/data/seeds/armors.csv` y actualización crucial del motor de combate con cálculo dinámico de la Clase de Armadura (CA / AC) según reglas de D&D 5e (2024).

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.1.1 Entidad Armor & Parser Heterogéneo de CA         │
   │ 2.1.2 Endpoint REST POST /api/ingestion/armors         │
   │ 2.1.3 Equipamiento en Creature (EquipArmor/EquipShield)│
   │ 2.1.4 Servicio de Dominio ArmorClassCalculator         │
   │ 2.1.5 Suites de Pruebas Unitarias, Integración & E2E   │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - Entidad de dominio `Armor` con soporte de categorías (`Light`, `Medium`, `Heavy`, `Shield`) y parser estático robusto para formatos textuales variados (`"11 + Dex modifier"`, `"15 + Dex modifier (max 2)"`, `"18"`, `"+2"`).
  - Endpoint `POST /api/ingestion/armors` vía `CsvHelper` con conversión de guiones `"-"` a nulos/vacíos y sanitización de pesos y costes.
  - Actualización del agregador `Creature` con ranuras tácticas independientes (`EquippedArmor`, `EquippedShield`) y métodos de dominio `EquipArmor`, `EquipShield`, `UnequipArmor`, `UnequipShield`.
  - Calculador dinámico `ArmorClassCalculator` implementando reglas canónicas:
    - Sin armadura: 10 + Modificador de Destreza.
    - Armadura Ligera: CA Base + Modificador de Destreza.
    - Armadura Media: CA Base + Modificador de Destreza (con un límite máximo de +2).
    - Armadura Pesada: CA Base pura (se ignora el modificador de Destreza).
    - Escudos: Suma un +2 a la CA total si hay uno equipado.
  - Integración en `CombatSimulatorService` y `MontecarloSimulationService`.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/armor-system-and-dynamic-ac`.
  - Documentación: [ADR-0009](file:///docs/adrs/0009-armor-system-and-dynamic-ac-calculation.md).
  - Testing: 141 tests unitarios en backend validando todas las combinaciones matemáticas (ej: Destreza 18 (+4) con Half Plate = 17), 20 tests de integración relacional con PostgreSQL/SQLite y test Playwright E2E.

---

## Fase 2.2: Bestiary API Integration & Custom Combatant Profiles [Completado]

Integración con la API externa pública de Open5e para sincronización masiva del Bestiario oficial de SRD y creación de perfiles estáticos de combatientes custom (Personajes de Jugadores - PCs) con estadísticas finales directas para simulación inmediata.

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.2.1 Open5e Bestiary Client (IHttpClientFactory)      │
   │ 2.2.2 Ingestión Sincronizada POST /api/ingestion/open5e│
   │ 2.2.3 Perfiles de Héroes POST /api/v1/creatures/custom │
   │ 2.2.4 Modal de Héroe Custom en CombatSimulatorView     │
   │ 2.2.5 Test Suites: Unitarios, Integración & E2E       │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - Cliente HTTP tipado `Open5eBestiaryClient` (`IOpen5eBestiaryClient`) configurado mediante `IHttpClientFactory` para consumir `/monsters/` desde `https://api.open5e.com/v1/`.
  - Servicio de sincronización `Open5eSyncService` con mapeo exhaustivo (HP, CA efectiva con `AllowsDexterityModifier = false`, atributos, bonos de ataque y dados de daño) y upsert idempotente por nombre en PostgreSQL.
  - Endpoint de sincronización `POST /api/ingestion/open5e/monsters/sync`.
  - Endpoint `POST /api/v1/creatures/custom` para dar de alta combatientes de jugadores (PCs) con estadísticas finales y flag de dominio `IsPlayerCharacter = true`.
  - Interfaz web con modal "Añadir Héroe Custom" (`CustomHeroModal`) en `CombatSimulatorView`, con selección inmediata e insignias temáticas (`🛡️ [Héroe]`).
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/open5e-integration-and-profiles`.
  - Documentación: [ADR-0010](file:///docs/adrs/0010-open5e-integration-and-custom-combatant-profiles.md).
  - Testing: 150 tests unitarios en `Sparnarok.UnitTests`, 23 tests de integración en `Sparnarok.IntegrationTests` con `HttpMessageHandler` mockeado y base de datos relacional, 21 tests de componentes en Vitest y 7 tests E2E en Playwright.

---

## Fase 2.3: DPR Baselines & Archetype Abstraction [Completado]

Abstracción estadística del daño de Personajes de Jugadores (PCs) mediante matrices de Daño por Ronda Esperado (Expected DPR Baselines) por nivel (1-20) y arquetipo táctico, omitiendo tiradas convencionales de d20 contra CA e inyectando un factor de variabilidad estocástica de ±15%.

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.3.1 Modelado en Creature (Level, ExpectedDpr, Arch)  │
   │ 2.3.2 Matriz y Servicio IDprBaselineService (CSV Seed) │
   │ 2.3.3 Endpoints REST /api/v1/dpr-baselines             │
   │ 2.3.4 Bucle de Combate Directo con ±15% de Varianza    │
   │ 2.3.5 UI Selectores de Arquetipo/Nivel y Live DPR Badge│
   │ 2.3.6 Suites de Pruebas Unitarias, Integración & E2E   │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - Extensión de la entidad de dominio `Creature` con `Level` (1-20), `ExpectedDpr` (double?), `Archetype` (string?) y método `ConfigurePlayerCharacterProfile(...)`.
  - Migración EF Core `20261021202132_AddDprBaselinesToCreature` aplicada sobre PostgreSQL.
  - Catálogo y semilla CSV `/data/seeds/dpr_baselines.csv` con 6 arquetipos canónicos (*Fighter 2H*, *Ranger Ranged*, *Paladin 2H*, *Barbarian 2H*, *Rogue Sneak Attack*, *Warlock Eldritch Blast*) para los 20 niveles.
  - Servicio `IDprBaselineService` / `DprBaselineService` con catálogo embebido resiliente y endpoints en `DprBaselinesController`.
  - Actualización del motor de combate `CombatSimulatorService` y `MontecarloSimulationService`: omisión de tiradas d20 contra CA para atacantes PC con `ExpectedDpr`, reducción directa de HP con factor de variabilidad de $\pm 15\%$ por ronda ($E[\text{Damage}] = \text{ExpectedDpr}$), sin requerir arma equipada.
  - Actualización de frontend: `CustomHeroModal` con selectores reactivos de Arquetipo y Nivel, indicador de DPR en vivo y soporte en `CombatSimulatorView` con inhabilitación de selector de armas y badge de DPR.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/dpr-baselines-integration`.
  - Documentación: [ADR-0011](file:///docs/adrs/0011-dpr-baselines-and-archetype-abstraction.md).
  - Testing: 156 tests unitarios en backend (incluyendo suite matemática de DPR y variabilidad $\pm 15\%$), 23 tests de integración en backend, 23 tests de componentes en Vitest y 8 tests E2E en Playwright.

---

## Fase 2.4: Party vs. Encounter Engine (N vs. M) [En Desarrollo / Completado]

Evolución del motor de combate para permitir enfrentamientos tácticos entre grupos de criaturas (Party de Héroes vs. Encuentro de Monstruos), con resolución de cola de iniciativa global, inteligencia artificial de targeting extensible y métricas agregadas de supervivencia de la party.

```
   ┌────────────────────────────────────────────────────────┐
   │ 2.4.1 Cola Global de Iniciativa (InitiativeQueue)      │
   │ 2.4.2 Targeting AI Extensible (Random & LowestHP)      │
   │ 2.4.3 Motor Grupal N vs M & TPK Condition Resolution   │
   │ 2.4.4 Montecarlo Concurrente & Party Survival Rate     │
   │ 2.4.5 UI React con Listas Dinámicas & Playwright E2E   │
   └────────────────────────────────────────────────────────┘
```

- **Alcance:**
  - `InitiativeQueue`: Tirada de iniciativa $1d20 + \text{DexMod}$ para todas las criaturas, con desempates por Destreza y orden de turno decreciente.
  - Interfaz `ITargetingStrategy` desacoplada y estrategias `RandomTargetingStrategy` y `LowestHpTargetingStrategy` (Focus Fire) con factoría `TargetingStrategyFactory`.
  - Simulación de encuentros grupales en `CombatSimulatorService` (`SimulateEncounter` y `SimulateFastEncounter`) con condición de victoria por eliminación total del equipo contrario.
  - Métricas agregadas de supervivencia en `MontecarloSimulationService`: Party Survival Rate porcentual promedio.
  - Interfaz gráfica en React con dos listas dinámicas (Team A - Party y Team B - Encuentro), permitiendo añadir/eliminar múltiples combatientes, configurar estrategia de aggro y visualizar métricas de supervivencia del grupo.
- **Entregables de Calidad & Directivas:**
  - Rama: `feat/party-vs-encounter-engine`.
  - Documentación: [ADR-0012](file:///docs/adrs/0012-party-vs-encounter-initiative-and-targeting-ai.md).
  - Testing: Tests unitarios de cola de iniciativa, heurísticas de targeting y victoria de grupo en `Sparnarok.UnitTests`, tests de integración en `Sparnarok.IntegrationTests`, tests de componentes en Vitest y test E2E de Playwright (4 Héroes vs 1 Jefe).

---

## Fases Futuras (Visión de Proyecto)


### Fase 2: Módulo 3 - Mathematical Simulation & DPR Matrices
- Modelado probabilístico de tiradas d20 con ventaja / desventaja.
- Matrices multidimensionales de distribución de daño por turno (*Damage per Round - DPR*).
- Simulador estocástico Monte Carlo para evaluar enfrentamientos tácticos entre grupos de criaturas (N vs M).

### Fase 3: World Orchestration & Campaign State Machine
- Máquina de estados determinista para eventos temporales, descansos cortos y largos.
- Rastreador de recursos de grupo (espacios de conjuro, dados de golpe, consumibles).
- Motor de orquestación de eventos climatológicos y encuentros dinámicos.

### Fase 4: Telemetría, Optimización y Exportación
- Exportación de métricas matemáticas en PDF / Markdown / JSON.
- Panel de analítica de combate en tiempo real.
