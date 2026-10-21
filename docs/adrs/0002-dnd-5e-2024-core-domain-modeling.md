# ADR-0002: Modelado del Dominio Central de D&D 5e (2024) y Entidades Clave

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Franco Pandolfo, Arquitecto de Software Sparnarok

## Contexto y Problema
Para implementar "Sparnarok: The Architect's Codex", se requiere un núcleo de dominio que capture de forma fiel las reglas de combate y orquestación de criaturas según la revisión de **D&D 5e (2024)**. Las necesidades críticas del sistema incluyen:
1. Precisión matemática en el cálculo de modificadores de características (rango canónico 1 a 30, truncamiento inferior con enteros).
2. Manejo riguroso de la salud, absorción de puntos de golpe temporales y la nueva condición formal de 2024: *Bloodied* (Ensangrentado, cuando los HP caen al 50% o menos).
3. Integración nativa de la nueva mecánica central de **Weapon Mastery (Maestría de Armas)**: 8 propiedades oficiales (*Cleave, Graze, Nick, Push, Sap, Slow, Topple, Vex*).
4. Asociación coherente y extensible entre Criaturas, su repertorio de Acciones tácticas y las Maestrías de Armas vinculadas a sus ataques.

## Opciones Evaluadas

1. **Modelo Anémico (Anemic Domain Model) con Data Transfer Objects puros:**
   - *Pros:* Muy simple de mapear directamente con bases de datos relacionales sin configuración avanzada.
   - *Contras:* Dispersión de las reglas de negocio en servicios, alto riesgo de estados inconsistentes (ej. puntos de golpe negativos o scores fuera de 1..30) y ausencia de encapsulación matemática.
2. **Modelo de Dominio Rico (Rich Domain Model / DDD) con Value Objects Inmutables:**
   - *Pros:*
     - Entidades con identidad propia (`Creature`, `Action`, `WeaponMastery`).
     - Value Objects inmutables (`AbilityScores`, `HitPoints`, `ArmorClass`) que imponen invariantes y garantizan cálculos matemáticos deterministas y puros.
     - Facilidad para tests unitarios aislados sin necesidad de infraestructura ni base de datos.
     - Compatibilidad total con EF Core Owned Entity Types o Value Converters.
   - *Contras:* Requiere constructores protegidos para EF Core y un poco más de código ceremonial en los mapeos.

## Decisión Adoptada
Se adopta un **Modelo de Dominio Rico** orientado a DDD:

1. **Entidad Raíz de Agregado: `Creature`**:
   - Centraliza la identidad, el Valor de Desafío (CR), el cálculo del Bono de Competencia (+2 a +9), la Clase de Dificultad para salvaciones (CD = 8 + PB + Mod) y el bono de ataque derivado.
   - Posee una relación 1:N con `Action` y N:M conceptual con `WeaponMastery` mediante colecciones protegidas expuestas como `IReadOnlyCollection`.

2. **Entidad `Action`**:
   - Modela cualquier acción dentro de la economía del turno (Action, BonusAction, Reaction, LegendaryAction).
   - Puede asociarse opcionalmente a un `WeaponMastery` mediante clave foránea y propiedad de navegación, permitiendo evaluar efectos tácticos en tiempo de ataque (ej. *Topple*, *Vex*).

3. **Entidad `WeaponMastery`**:
   - Modela las 8 propiedades del Player's Handbook 2024.
   - Incluye preconfiguraciones estáticas (`WeaponMastery.Presets`) para estandarizar las reglas canónicas en simulaciones.

4. **Objetos de Valor Inmutables (Value Objects)**:
   - `AbilityScores`: Valida scores entre 1 y 30 y encapsula la fórmula canónica `floor((score - 10) / 2.0)`.
   - `HitPoints`: Implementa las reglas de D&D 2024 para absorción por puntos temporales (los temporales no se apilan; se toma el mayor) y cálculo de la condición *Bloodied*.
   - `ArmorClass`: Encapsula la base, bonos de escudo y límites de destreza según el tipo de protección.

## Consecuencias
- **Positivas:**
  - Lógica matemática blindada y determinista, libre de efectos secundarios.
  - Testabilidad del 100% en `Sparnarok.UnitTests`.
  - Preparado para persistencia en PostgreSQL con EF Core (Paso 1.2).
- **Compromisos (Trade-offs):**
  - La configuración en EF Core requerirá mapeos explícitos Fluent API (`OwnsOne` para `AbilityScores`, `HitPoints` y `ArmorClass`), lo cual documentaremos y probaremos en el Paso 1.2.
