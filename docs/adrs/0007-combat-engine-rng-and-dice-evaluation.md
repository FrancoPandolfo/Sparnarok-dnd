# ADR-0007: Arquitectura de Generación Pseudoaleatoria (RNG) y Evaluación de Dados para el Motor de Combate

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
El motor de combate de **Sparnarok** requiere modelar mecánicas estocásticas canónicas de D&D 5e (2024):
- Tiradas de iniciativa ($1d20 + \text{Modificador de Destreza}$).
- Tiradas de ataque ($1d20 + \text{Bono de Ataque}$ frente a la Clase de Armadura del objetivo).
- Detección de impacto crítico (20 natural) y fallo automático (1 natural).
- Tiradas de daño algebraicas (ej. $1d8+3$, $2d6$, $1d4+2$, $1$).
- Duplicación de dados de daño ante impactos críticos ($1d8+3 \rightarrow 2d8+3$).

En el desarrollo de simuladores de juegos y motores de probabilidad, el uso directo de primitivas como `System.Random` o `Random.Shared` genera serios problemas arquitectónicos:
1. **No determinismo en pruebas:** Los tests unitarios no pueden validar con precisión matemática que un 20 natural active un crítico o que un resultado inferior a la CA falle, introduciendo intermitencia (flakiness).
2. **Imposibilidad de reproducibilidad:** Imposibilita depurar combates complejos o reproducir exactamente una secuencia de turnos anómala reportada en simulación.
3. **Acoplamiento rígido:** Dificulta la futura integración de algoritmos de reducción de varianza o generadores criptográficos/cuánticos para simulaciones Monte Carlo masivas.

## Opciones Evaluadas

### 1. Generación Pseudoaleatoria (RNG)
- **Opción A (Uso directo de `System.Random.Shared` en los servicios):** Simple y sin código adicional, pero destruye la testabilidad determinista y el aislamiento matemático.
- **Opción B (Mockeo de `System.Random` mediante frameworks dinámicos como Moq/NSubstitute):** `System.Random` no es una interfaz; mockear métodos virtuales o derivar clases en tiempo de ejecución resulta engorroso, frágil y poco expresivo.
- **Opción C (Abstracción `IRandomProvider` con implementaciones duales) [Elegida]:**
  - `IRandomProvider`: Define contratos claros para generación acotada (`Next(min, max)`), tiradas poliédricas (`RollDie(sides)`) y atajos canónicos (`RollD20()`).
  - `DefaultRandomProvider`: Diseñado para producción en tiempo de ejecución, apoyado en `Random.Shared` (óptimo en concurrencia y sin contención de bloqueos).
  - `DeterministicRandomProvider`: Diseñado para testing y depuración. Admite inicialización por **semilla entera fija (seed)** o por **secuencia finita predeterminada de tiradas** (e.g. `[20, 8, 15, ...]`).

### 2. Evaluación de Expresiones de Dados
- **Opción A (Parseo ad-hoc con `string.Split` dentro del servicio de combate):** Propenso a errores al procesar modificadores negativos, daños planos (ej. "1" de la cerbatana) o dados con multiplicadores.
- **Opción B (Servicio especializado `IDiceRoller` con expresiones regulares precompiladas) [Elegida]:** Emplea `Regex` generado en tiempo de compilación con C# source generators (`[GeneratedRegex]`), garantizando asignación casi nula de memoria, soporte de fórmulas como `1d8`, `2d6+2`, `1d4-1` o `1`, y duplicación nativa de dados para golpes críticos.

## Decisión Adoptada
1. Se introduce la interfaz `IRandomProvider` y su implementación determinista `DeterministicRandomProvider` bajo `Sparnarok.Api.Domain.Services.Rng`.
2. Se introduce el evaluador `IDiceRoller` y `DiceRoller` bajo `Sparnarok.Api.Domain.Services.Dice`, desacoplado de la infraestructura e inyectable mediante DI.
3. Se implementa `CombatSimulatorService` orquestando el ciclo completo de combate 1v1 (Iniciativa, Ataque vs CA, Daño crítico y Log cronológico) recibiendo `IRandomProvider` e `IDiceRoller`.
4. En el contenedor de inyección de dependencias (`Program.cs`), se registra `DefaultRandomProvider` como Singleton en entornos normales, mientras que en la suite de pruebas unitarias (`Sparnarok.UnitTests`) se inyecta `DeterministicRandomProvider` con secuencias de tiradas exactas.

## Consecuencias
- **Positivas:**
  - Pruebas unitarias 100% deterministas, rápidas y reproducibles sin depender del azar.
  - Verificación matemática estricta de las reglas de D&D 5e 2024 (20 natural duplica dados, 1 natural falla automáticamente).
  - Telemetría granular en `CombatEventLog` registrando la tirada cruda del d20, el modificador de característica y el daño absorbido.
  - Arquitectura preparada para simulaciones Monte Carlo por lotes en la Fase 2 Módulo 2.
- **Negativas / Compromisos (Trade-offs):**
  - Requiere mantener una abstracción adicional en lugar de invocar directamente métodos estáticos de `System.Random`.
- **Mitigaciones:**
  - El diseño de `DeterministicRandomProvider` permite suministrar tanto secuencias manuales simples como semillas enteras estándar de `Random`, minimizando el esfuerzo de configuración en pruebas.

## Referencias
- [D&D 5e (2024 Rules Glossary): D20 Test, Critical Hits & Damage Rolls](https://www.dndbeyond.com)
- [.NET Source Generators for Regular Expressions](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-source-generators)
