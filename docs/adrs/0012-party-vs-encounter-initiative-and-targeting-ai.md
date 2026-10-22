# ADR-0012: Arquitectura de Cola Global de Iniciativa y Estrategias Extensibles de Targeting AI para Encuentros Grupales (N vs. M)

- **Fecha:** 2026-10-22
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
En las Fases 2.0 a 2.3, el motor de combate de Sparnarok se modeló exclusivamente para duelos 1 contra 1 (`CombatSimulatorService.SimulateDuel` y `SimulateFastDuel`). Aunque este enfoque validó con rigor la matemática de tiradas d20, el sistema dinámico de CA, el generador estocástico determinista y las abstracciones de DPR por arquetipo, no permitía simular el escenario fundamental de D&D: un grupo coordinado de aventureros (La Party, $N \ge 1$) enfrentándose a una manada o grupo de antagonistas (El Encuentro, $M \ge 1$).

Para soportar combates grupales a gran escala mediante simulaciones masivas de Monte Carlo concurrentes (miles de iteraciones en milisegundos), se requerían resolver tres desafíos arquitectónicos esenciales:
1. **Cola de Iniciativa Global:** Cómo estructurar y resolver la secuencia cíclica de turnos entre múltiples combatientes heterogéneos, garantizando el cumplimiento canónico de las reglas de D&D 5e (2024) y desempates deterministas.
2. **Heurística de Selección de Objetivos (Targeting AI):** En combates 1v1 el objetivo es trivial (el único adversario), pero en combates $N \text{ vs. } M$ cada atacante debe decidir a qué enemigo vivo atacar. Se necesitaba una arquitectura limpia que permitiera tanto estrategias MVP (`Random`, `LowestHP`) como futuras extensiones complejas (`Focus Healer`, `Highest DPR / Threat`, `Revenge Targeting`) sin modificar el bucle de simulación principal.
3. **Métricas de Supervivencia de Grupo (Party Survival Rate):** En duelos 1v1 el resultado es binario (victoria o derrota). En encuentros grupales, una victoria con 1 solo superviviente herido es radicalmente distinta a una victoria impecable con los 4 héroes intactos. Se requería calcular la tasa media de supervivencia del equipo.

## Opciones Evaluadas

### 1. Modelado de Iniciativa
- **Opción A (Iniciativa por Equipos / Side Initiative):** Un bando actúa por completo y luego actúa el bando contrario.
  - *Pros:* Menor sobrecarga de ordenación.
  - *Contras:* Rompe las reglas canónicas de D&D 5e (2024), amplifica artificialmente el sesgo del primer turno (*alpha strike*) y desvirtúa los bonos individuales de Destreza.
- **Opción B (Cola Global Individual de Iniciativa - Seleccionada):** Todos los combatientes tiran $1d20 + \text{Modificador de Destreza}$ al inicio del combate. Se ordenan de mayor a menor con desempate por Modificador de Destreza y se ejecutan cíclicamente ronda a ronda, saltando a los combatientes incapacitados ($HP \le 0$).
  - *Pros:* Simulación canónica fiel al manual de reglas; preserva la ventaja táctica de las clases de alta Destreza (Pícaros, Exploradores).
  - *Contras:* Requiere ordenar una lista al inicio del combate ($O(K \log K)$ donde $K = N + M$), lo cual es despreciable dado que $K$ usualmente oscila entre 2 y 16 combatientes.

### 2. Diseño de la IA de Selección de Objetivos (Targeting AI)
- **Opción A (Lógica 'Hardcoded' dentro del bucle de combate):** Sentencias `if/switch` directas dentro del método `ExecuteAttack`.
  - *Pros:* Rápido de implementar inicialmente.
  - *Contras:* Código espagueti; cualquier nueva estrategia (ej. "Atacar al Clérigo") obliga a modificar el bucle crítico del motor, arriesgando regresiones.
- **Opción B (Patrón Strategy desacoplado mediante `ITargetingStrategy` - Seleccionada):** Definir una interfaz `ITargetingStrategy` con un método puro `SelectTarget(CombatFighter attacker, IReadOnlyList<CombatFighter> opponents, IRandomProvider random)` y una factoría `TargetingStrategyFactory` que resuelve implementaciones singleton libres de asignaciones.
  - *Pros:* Cumple con el Principio de Abierto/Cerrado (OCP); permite probar heurísticas de forma aislada en tests unitarios; abre la puerta a árboles de decisión de comportamiento (Behavior Trees) o modelos de utilidad en futuras fases.
  - *Contras:* Requiere una pequeña indirección virtual, fácilmente absorbida por el JIT.

## Decisión Adoptada
Se implementó la **Opción B** en ambos frentes:

1. **Agregado `CombatFighter` y `InitiativeQueue`:**
   - Cada combatiente en el cuadrilátero se encapsula en una instancia de `CombatFighter` que mantiene su estado mutable de combate (`CurrentHp`, `Initiative`, `TeamSide`), permitiendo instanciar múltiples copias idénticas de un mismo monstruo (ej. 4 Goblins con `InstanceId` independientes) sin mutar la entidad de dominio base `Creature`.
   - `InitiativeQueue.RollAndOrder` resuelve la tirada $1d20 + \text{DexMod}$ y ordena la lista de forma decreciente.

2. **Interfaz `ITargetingStrategy` y Estrategias MVP:**
   - `RandomTargetingStrategy`: Distribución uniforme entre los enemigos vivos (`CurrentHp > 0`).
   - `LowestHpTargetingStrategy`: Enfoque de daño (*Focus Fire*) sobre el enemigo vivo con menor cantidad de HP actuales, buscando sacarlo de combate y reducir la economía de acciones rival.
   - **Extensibilidad a Futuro:** Para agregar estrategias como `HighestThreatTargetingStrategy`, `FocusHealerTargetingStrategy` o `RevengeTargetingStrategy`, basta con crear una nueva clase que implemente `ITargetingStrategy` y agregar su entrada al enum `TargetingStrategy` y al switch de `TargetingStrategyFactory`.

3. **Condición de Victoria de Encuentro:**
   - La batalla finaliza en cuanto todos los miembros de un equipo alcanzan 0 HP (`team.All(f => !f.IsAlive)`).
   - El combate termina con victoria de Team A si Team B cae por completo y Team A conserva al menos 1 combatiente vivo, y viceversa.
   - Se mantiene un límite máximo de salvaguarda de 100 rondas para declarar empate y evitar bucles infinitos.

4. **Party Survival Rate:**
   - Se incorporó la métrica:
     $$\text{PartySurvivalRate} = \frac{\sum \text{Sobrevivientes de Team A}}{\text{Iteraciones} \times |Team A|} \times 100\%$$
   - Esto permite a los DMs evaluar la letalidad real del encuentro y el desgaste de recursos del grupo.

## Consecuencias

### Positivas
- **Fidelidad Táctica Máxima:** Capacidad de simular enfrentamientos reales de D&D (ej. 4 Héroes de nivel 5 contra 1 Joven Dragón Rojo o contra 8 Goblins).
- **Extensibilidad Total:** Las IAs de targeting son componentes independientes y testeables sin tocar el simulador de dados.
- **Rendimiento Ultrarrápido:** `SimulateFastEncounter` no realiza asignaciones de cadenas ni logs, completando 10,000 duelos de $4 \text{ vs. } 1$ en menos de 20 milisegundos en procesadores modernos multinúcleo.
- **Compatibilidad Hacia Atrás:** Los métodos `SimulateDuel` y `SimulateFastDuel` y los contratos DTO legacy se preservan mediante sobrecargas delegadas, garantizando que el 100% de los tests anteriores sigan pasando sin cambios.

### Negativas / Compromisos (Trade-offs)
- Mayor complejidad en las estructuras de datos de entrada (ahora se gestionan colecciones de combatientes por bando en lugar de un único par).
- La asignación de múltiples combatientes requiere validar que cada integrante cuente con un arma válida o con un `ExpectedDpr` configurado.

### Mitigaciones
- Validación defensiva centralizada en `ValidateTeam` antes de iniciar cualquier iteración.
- DTOs flexibles en `MontecarloRequestDto` que admiten tanto listas `TeamA`/`TeamB` como los campos 1v1 anteriores (`CombatantAId`/`CombatantBId`).

## Referencias
- Reglas oficiales de D&D 5e (2024 / SRD) - *Initiative, Combat Order, and Damage Resolution*.
- [ADR-0007: Motor de Combate, RNG y Dados](file:///docs/adrs/0007-combat-engine-rng-and-dice-evaluation.md).
- [ADR-0008: Concurrencia en Monte Carlo y Métricas de Combate](file:///docs/adrs/0008-montecarlo-simulation-concurrency-and-metrics.md).
- [ADR-0011: Abstracción de DPR Esperado por Arquetipo](file:///docs/adrs/0011-dpr-baselines-and-archetype-abstraction.md).
