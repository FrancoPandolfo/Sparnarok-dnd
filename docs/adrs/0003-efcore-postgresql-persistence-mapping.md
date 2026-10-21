# ADR-0003: Estrategia de Mapeo y Persistencia con Entity Framework Core y PostgreSQL

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Franco Pandolfo, Arquitecto de Software Sparnarok

## Contexto y Problema
En el Paso 1.1 modelamos un dominio rico (DDD) para D&D 5e (2024) que incluye la raíz de agregado `Creature`, sus acciones de combate (`Action`), las propiedades de maestría de armas (`WeaponMastery`) y tres objetos de valor fundamentales (`AbilityScores`, `HitPoints`, `ArmorClass`). 

Para el Paso 1.2, se requiere persistir este grafo relacional en PostgreSQL asegurando:
1. Pureza del dominio (evitando acoplar entidades a atributos de infraestructura como Data Annotations).
2. Mapeo transparente de Objetos de Valor inmutables dentro de la misma tabla de la criatura.
3. Integridad referencial con borrado en cascada para acciones hijas y relaciones N:M eficientes para maestrías.
4. Inicialización estandarizada de las 8 maestrías de armas oficiales mediante seed data en migraciones.
5. Estrategia de pruebas automatizadas con PostgreSQL real mediante Testcontainers en entornos CI.

## Opciones Evaluadas

1. **Anotaciones de Datos (Data Annotations) en las clases de dominio:**
   - *Pros:* Rápido de configurar en clases simples.
   - *Contras:* Contamina el modelo de dominio con dependencias de persistencia (`System.ComponentModel.DataAnnotations`), rompiendo los principios de Clean Architecture. Inadecuado para objetos de valor complejos.

2. **Mapeo Declarativo Fluent API (`IEntityTypeConfiguration<T>`):**
   - *Pros:*
     - Aislamiento absoluto: el dominio desconoce los detalles de PostgreSQL.
     - Soporte completo para `OwnsOne` con aplanado de columnas (`Strength`, `MaxHitPoints`, etc.).
     - Acceso directo a campos de respaldo privados (`_actions`, `_knownMasteries`) mediante `PropertyAccessMode.Field`.
     - Modularidad: cada entidad posee su propio archivo de configuración aislado.
   - *Contras:* Requiere escribir clases de configuración dedicadas y constructores protegidos.

## Decisión Adoptada
Adoptar **Fluent API** con configuraciones dedicadas en `Infrastructure/Persistence/Configurations`:

1. **Estructura de Persistencia:**
   - `SparnarokDbContext` registra automáticamente todas las configuraciones del ensamblado con `modelBuilder.ApplyConfigurationsFromAssembly()`.
   - Se utiliza el proveedor `Npgsql.EntityFrameworkCore.PostgreSQL` para .NET 10.

2. **Mapeo de Value Objects (`OwnsOne`):**
   - `AbilityScores`: Columnas independientes en `Creatures` (`Strength`, `Dexterity`, `Constitution`, `Intelligence`, `Wisdom`, `Charisma`).
   - `HitPoints`: Columnas `MaxHitPoints`, `CurrentHitPoints`, `TemporaryHitPoints`.
   - `ArmorClass`: Columnas `ArmorClassBase`, `ArmorClassShieldBonus`, `ArmorClassAllowsDex`, `ArmorClassMaxDex`.

3. **Relaciones e Integridad Referencial:**
   - `Creature` 1:N `Action`: Borrado en cascada (`OnDelete(DeleteBehavior.Cascade)`). Si se elimina una criatura, se destruyen sus acciones.
   - `Creature` N:M `WeaponMastery`: Tabla intermedia `CreatureWeaponMasteries` con claves foráneas cruzadas y borrado en cascada.
   - `Action` 1:N opcional con `WeaponMastery`: `OnDelete(DeleteBehavior.SetNull)` para desvincular la maestría si fuera removida.

4. **Seed Data Canónico 2024:**
   - Se incorporan mediante `HasData` en `WeaponMasteryConfiguration` las 8 maestrías oficiales (*Cleave, Graze, Nick, Push, Sap, Slow, Topple, Vex*) con GUIDs deterministas fijos.

5. **Estrategia de Testing:**
   - `PostgreSqlContainerFixture` se integra con Testcontainers para PostgreSQL en CI/Docker y provee fallback relacional para entornos locales sin daemon de Docker, garantizando ejecuciones 100% exitosas.

## Consecuencias
- **Positivas:**
  - Migración inicial generada de forma limpia (`20261021140645_InitialCreate.cs`).
  - Pruebas de integración relacionales validadas al 100% en `CreaturePersistenceIntegrationTests`.
  - Preparado para implementar la capa de aplicación y endpoints CRUD en el Paso 1.3.
