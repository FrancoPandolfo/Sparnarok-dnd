# Architecture Decision Records (ADRs)

Este directorio almacena los Registros de Decisiones de Arquitectura (ADRs) para **Sparnarok: The Architect's Codex**.

## Formato y Convención
- Cada decisión arquitectónica o matemática clave debe quedar registrada como un archivo Markdown numerado: `XXXX-titulo-descriptivo.md`.
- El estado de un ADR puede ser: `Propuesto`, `Aceptado`, `Rechazado`, `Reemplazado` (con enlace al nuevo ADR).
- Usamos la plantilla [0000-template.md](file:///docs/adrs/0000-template.md).

## Índice de Decisiones
- [ADR-0001: Arquitectura Inicial de Monorepo y Stack Tecnológico](file:///docs/adrs/0001-monorepo-initial-architecture.md)
- [ADR-0002: Modelado del Dominio Central de D&D 5e (2024) y Entidades Clave](file:///docs/adrs/0002-dnd-5e-2024-core-domain-modeling.md)
- [ADR-0003: Estrategia de Mapeo y Persistencia con Entity Framework Core y PostgreSQL](file:///docs/adrs/0003-efcore-postgresql-persistence-mapping.md)
- [ADR-0004: Diseño de la Capa de Aplicación, Patrón Repositorio y Endpoints CRUD para Criaturas](file:///docs/adrs/0004-application-cqrs-and-crud-endpoints.md)
- [ADR-0005: Arquitectura Frontend y Cliente de Consumo para Gestión Táctica de Criaturas](file:///docs/adrs/0005-frontend-creatures-tactical-ui.md)
- [ADR-0006: Pipeline de Ingesta Masiva de Datos Mediante CSV y CsvHelper](file:///docs/adrs/0006-csv-data-ingestion-pipeline.md)
- [ADR-0007: Arquitectura de Generación Pseudoaleatoria (RNG) y Evaluación de Dados para el Motor de Combate](file:///docs/adrs/0007-combat-engine-rng-and-dice-evaluation.md)
- [ADR-0008: Concurrencia en Motor de Simulación Montecarlo y Agregación Estadística (Win Rate & DPR)](file:///docs/adrs/0008-montecarlo-simulation-concurrency-and-metrics.md)
- [ADR-0009: Sistema de Armaduras, Ingesta CSV Heterogénea y Cálculo Dinámico de Clase de Armadura (AC)](file:///docs/adrs/0009-armor-system-and-dynamic-ac-calculation.md)
- [ADR-0010: Integración de Bestiario Open5e y Perfiles Estáticos de Combatientes Custom (PCs)](file:///docs/adrs/0010-open5e-integration-and-custom-combatant-profiles.md)
- [ADR-0011: Abstracción de Personajes de Jugadores mediante Líneas Base de DPR Esperado (Expected DPR Baselines)](file:///docs/adrs/0011-dpr-baselines-and-archetype-abstraction.md)
- [ADR-0012: Arquitectura de Cola Global de Iniciativa y Estrategias Extensibles de Targeting AI para Encuentros Grupales (N vs. M)](file:///docs/adrs/0012-party-vs-encounter-initiative-and-targeting-ai.md)
- [ADR-0013: Autenticación Social con Google OAuth2 y Emisión de JWT Propios para Persistencia e Identidad en SPA](file:///docs/adrs/0013-google-oauth2-jwt-spa-authentication.md)
- [ADR-0014: Diseño de Relaciones Many-to-Many para Partys y Encuentros, Gestión de Tablas Intermedias en EF Core y Prevención de Ciclos de Serialización JSON](file:///docs/adrs/0014-many-to-many-party-encounter-persistence.md)

