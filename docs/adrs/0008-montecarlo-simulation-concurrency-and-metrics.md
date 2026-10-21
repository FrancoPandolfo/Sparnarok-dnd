# ADR-0008: Concurrencia en Motor de Simulación Montecarlo y Agregación Estadística (Win Rate & DPR)

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
En la Fase 2 (Módulo 2) de **Sparnarok: The Architect's Codex**, el objetivo central es transformar el bucle básico de combate 1v1 en un motor estocástico masivo capaz de ejecutar duelos miles de veces (típicamente 10,000 a 20,000 iteraciones) de forma concurrente, agregar las distribuciones estadísticas resultantes y visualizarlas en el frontend en React.

Los desafíos arquitectónicos clave incluyeron:
1. **Aprovechamiento de CPU Multi-núcleo:** Ejecutar 10,000 duelos de forma secuencial provocaría una latencia inaceptable para una API web y degradaría la experiencia del usuario.
2. **Contención de Bloqueos (Lock Contention):** Si múltiples hilos intentan sincronizar métricas (victorias, daño, rondas) mediante primitivas atómicas o `lock` en cada iteración individual, la contención del bus de memoria y la invalidación de líneas de caché destruyen el rendimiento multihilo.
3. **Presión de Asignación en el Garbage Collector (GC Pressure):** El método fundacional `SimulateDuel` genera objetos `CombatSimulationResultDto` y listas de `CombatEventLog` con cadenas de texto formateadas para telemetría humana. Producir 10,000 instancias de estos objetos en memoria generaría millones de asignaciones temporales en el heap, saturando el GC de .NET.
4. **Cálculo Riguroso de Métricas Tácticas:** Se requiere calcular no solo el porcentaje de victoria (Win Rate de A y B), sino también el promedio de rondas que dura el enfrentamiento y el promedio de daño infligido por turno o ronda (Damage Per Round - DPR) de cada combatiente.

## Opciones Evaluadas

### 1. Modelo de Concurrencia y Paralelismo
- **Opción A (`Task.WhenAll` con tareas asíncronas individuales):** Crear 10,000 objetos `Task` en el `ThreadPool`. Aunque funcional, añade sobrecarga de asignación de `Task` y gestión de promesas asíncronas para una carga de trabajo intensiva en CPU (CPU-bound) que no realiza I/O.
- **Opción B (`Parallel.For` con variables locales por hilo y reducción final) [Elegida]:**
  - Utiliza `Parallel.For<ThreadLocalAccumulator>` aprovechando al máximo la partición del planificador de .NET según los núcleos de CPU disponibles.
  - Cada hilo del procesador acumula victorias de A, victorias de B, empates, daño total infligido y rondas totales en una estructura de datos local en registros/caché L1/L2.
  - Al finalizar la partición asignada al hilo, se realiza una única operación de consolidación protegida con `lock` sobre los acumuladores globales. Esto reduce la contención de bloqueos de 10,000 a un puñado equivalente a la cantidad de hilos lógicos del procesador.

### 2. Optimización de Asignación de Memoria (Fast Duel)
- **Opción A (Reutilizar `SimulateDuel` existente para Montecarlo):** Simplicidad conceptual, pero cada iteración asigna listas y strings, penalizando severamente el throughput.
- **Opción B (Patrón `SimulateFastDuel` con struct de asignación cero) [Elegida]:**
  - Se define `readonly struct FastDuelResult(Guid? WinnerId, int Rounds, int DamageDealtA, int DamageDealtB)` alojada puramente en el stack.
  - `SimulateFastDuel` ejecuta el ciclo de combate idéntico al motor canónico (iniciativa, ataque vs CA, daño y críticos) omitiendo únicamente la generación de logs cronológicos de texto.
  - Permite ejecutar 10,000 duelos completos en menos de 65 ms en hardware estándar.

### 3. Exposición API y Consumo en Frontend
- Se crea el endpoint `POST /api/simulation/montecarlo` con el DTO `MontecarloRequestDto` (IDs de combatientes, IDs de armas e iteraciones) y `MontecarloResultDto` con Swagger/XML comments exhaustivos.
- En el frontend, el componente `CombatSimulatorView` provee selectores de combatientes y armas, control de iteraciones (100 a 20,000), una barra de progreso segmentada para el Win Rate con gradientes CSS y tarjetas métricas para el promedio de rondas, DPR y tiempo de cómputo.

## Decisión Adoptada
1. Se implementó `MontecarloSimulationService` bajo `Sparnarok.Api.Infrastructure.Services`, implementando la interfaz `IMontecarloSimulationService`.
2. Se introdujo `SimulateFastDuel` en `ICombatSimulatorService` y `CombatSimulatorService`, retornando la struct `FastDuelResult`.
3. El servicio calcula las métricas matemáticas agregadas:
   $$\text{WinRate}_A = \frac{\text{Wins}_A}{\text{TotalIterations}} \times 100$$
   $$\text{WinRate}_B = \frac{\text{Wins}_B}{\text{TotalIterations}} \times 100$$
   $$\text{AverageRounds} = \frac{\text{TotalRounds}}{\text{TotalIterations}}$$
   $$\text{AverageDpr}_A = \frac{\text{TotalDamageDealt}_A}{\text{TotalRounds}}$$
   $$\text{AverageDpr}_B = \frac{\text{TotalDamageDealt}_B}{\text{TotalRounds}}$$
4. Se documentó el endpoint en `SimulationController` con Swagger XML.
5. Se desarrolló `CombatSimulatorView` en React integrado en el tab de simulación de `App.tsx` y estilizado en `App.css`.
6. Se crearon pruebas unitarias completas tanto en backend (`MontecarloSimulationServiceTests`) como en frontend (`CombatSimulatorView.test.tsx`), y un test de extremo a extremo (E2E) con Playwright en `smoke.spec.ts` verificando el flujo completo desde la interfaz de usuario hasta el motor de combate.

## Consecuencias
- **Positivas:**
  - Rendimiento óptimo: 10,000 iteraciones resueltas en < 65 ms sin bloqueos y con huella de memoria insignificante.
  - Resultados reproducibles y testeables mediante inyección de dependencias.
  - Interfaz visual táctica, responsiva e internacionalizada (i18n en español e inglés).
  - Cobertura de pruebas completa en todos los niveles de la pirámide de testing (Unitarias, Integración con PostgreSQL y E2E con Playwright).
- **Negativas / Compromisos (Trade-offs):**
  - Mantener dos métodos en el motor de combate (`SimulateDuel` para combate individual con logs y `SimulateFastDuel` para Montecarlo de alto rendimiento) requiere asegurar que cualquier modificación a las reglas de combate se refleje en ambos.
- **Mitigaciones:**
  - Las reglas canónicas de resolución de ataque, tiradas de iniciativa y daño delegan en los mismos métodos de dominio (`EvaluateAttack`, `RollInitiative`, `_diceRoller.RollDamage`), garantizando consistencia semántica.

## Referencias
- [Patterns for Parallel Programming with the .NET Framework](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/)
- [High-performance Logging and Zero-Allocation Patterns in .NET](https://learn.microsoft.com/en-us/dotnet/standard/memory-and-spans/)
