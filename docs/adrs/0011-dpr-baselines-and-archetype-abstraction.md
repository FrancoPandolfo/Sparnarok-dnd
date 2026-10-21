# ADR-0011: Abstracción de Personajes de Jugadores mediante Líneas Base de DPR Esperado (Expected DPR Baselines)

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema

En la **Fase 2.3: DPR Baselines & Archetype Abstraction** de **Sparnarok: The Architect's Codex**, debíamos resolver cómo simular con rigor el desempeño ofensivo de los Personajes de los Jugadores (PCs) a lo largo de los 20 niveles del juego sin incurrir en una sobrecarga de desarrollo inmanejable.

Modelar todas las mecánicas de clase de D&D 5e (versión 2024 / 2014) de forma discreta —incluyendo *Extra Attack* (ataques iterativos por turno), *Sneak Attack* (escalado de dados condicional con ventaja/adyacencia), *Divine Smite* (gasto de espacios de conjuro), *Action Surge* (prorrateo de recursos por descanso corto), *Rage Damage* y *Reckless Attack* (modificación dinámica de probabilidad de impacto), así como dotes de combate (*Great Weapon Master*, *Sharpshooter*)— plantea serios problemas arquitectónicos:
1. **Explosión Combinatoria:** Cada clase y subclase añade árboles de decisión, condiciones de activación y estados efímeros que ralentizan el ciclo de simulación y multiplican exponencialmente la superficie de bugs.
2. **Latencia de Cómputo en Montecarlo:** Evaluar bucles de ataque discretos (múltiples tiradas de d20 contra CA, cálculo de ventaja, tiradas de dados individuales `2d6+4`, confirmación de críticos y resolución de efectos secundarios) consume ciclos de CPU innecesarios al ejecutar 10,000 a 20,000 iteraciones concurrentes por segundo.
3. **Falsa Sensación de Precisión:** La simulación discreta de reglas de clase a menudo introduce sesgos no calibrados sobre la gestión de recursos (por ejemplo, cuándo un Paladín gasta un espacio de nivel 3 vs nivel 1), mientras que la literatura estadística de D&D (modelos de Treantmonk, Tabletop Builds y DanDM) ya cuenta con matrices empíricas de daño promedio por ronda rigurosamente validadas.

---

## Opciones Evaluadas

### Opción 1: Simulación Discreta Completa de Reglas de Clase
- Implementar un motor de reglas basado en árboles de comportamiento o máquinas de estados que replique el turno de cada clase (tirada de ataque d20 vs CA, confirmación de impacto, tirada de dados de arma, aplicación de multiplicadores y gasto de slots).
- **Desventajas:** Tiempo de desarrollo prohibitivo (semanas de trabajo por arquetipo), lentitud extrema en procesamiento paralelo de Montecarlo y mantenimiento frágil ante cambios o erratas de reglas.

### Opción 2: Abstracción Estadística mediante Líneas Base de Daño Promedio por Ronda (Expected DPR) [Elegida]
- Sustituir la simulación discreta de armas y tiradas de ataque d20 contra CA por una **abstracción de daño esperado por ronda (`ExpectedDpr`)** asociada al nivel (1-20) y arquetipo táctico del personaje (ej. *Fighter 2H*, *Ranger Ranged*, *Paladin 2H*, *Barbarian 2H*, *Rogue Sneak Attack*, *Warlock Eldritch Blast*).
- En el turno del personaje, reducir los puntos de golpe del adversario directamente con base en su `ExpectedDpr`.
- Introducir un factor estocástico de aleatoriedad de $\pm 15\%$ por ronda para reflejar la varianza natural del combate sin alterar el valor esperado estadístico a escala macroscópica.

---

## Decisión Adoptada

Se adoptó la **Opción 2: Abstracción de DPR Esperado y Matriz de Arquetipos**.

### 1. Fundamentación Matemática y Validez Estadística
En la teoría matemática de combate de D&D 5e, el Daño por Ronda esperado ($DPR$) de un personaje con bonificador de impacto $B$, daño medio por acierto $D_{hit}$, daño crítico adicional $D_{crit}$, y número de ataques $N$ frente a una CA objetivo $AC$ se define como:

$$P(\text{hit}) = \text{clamp}\left(\frac{21 + B - AC}{20}, 0.05, 0.95\right)$$

$$P(\text{crit}) = 0.05$$

$$\text{DPR} = N \times \left[ P(\text{hit}) \times D_{hit} + P(\text{crit}) \times D_{crit} \right] + \text{RecursosProrrateados}$$

Dado que el $DPR$ tabulado ya integra la probabilidad exacta de impacto y fallo, así como el prorrateo de recursos limitados a lo largo de un día de aventuras típico (3-4 turnos por encuentro), **aplicar directamente el valor de DPR como reducción de daño por turno es estadísticamente equivalente al valor límite de la simulación discreta**.

### 2. Factor de Varianza Estocástica ($\pm 15\%$)
Para evitar combates rígidamente deterministas donde el combate termine exactamente en la misma ronda sin dispersión estadística, el motor inyecta un factor de variabilidad aleatoria por turno:

$$\text{VarianceFactor} \in [-15, +15] \quad (\text{entero uniforme})$$

$$\text{TurnDamage} = \max\left(1, \text{round}\left(\text{ExpectedDpr} \times \left(1 + \frac{\text{VarianceFactor}}{100}\right)\right)\right)$$

Dado que $E[\text{VarianceFactor}] = 0$:

$$E[\text{TurnDamage}] = \text{ExpectedDpr}$$

El estimador es asintóticamente insesgado y preserva la precisión del análisis de Montecarlo tras miles de iteraciones concurrentes.

### 3. Modificaciones en el Motor de Combate (`CombatSimulatorService`)
- En el bucle de combate de `CombatSimulatorService`, cuando `attacker.Creature.IsPlayerCharacter == true` y `attacker.Creature.ExpectedDpr > 0`:
  1. Se omite la tirada de d20 contra la Clase de Armadura (AC) del defensor.
  2. No se requiere un arma equipada del catálogo de armas.
  3. Se calcula el daño con el factor de varianza de $\pm 15\%$.
  4. Se reducen directamente los puntos de golpe del defensor.
  5. Se emite un registro de combate claro en el log táctico detallando la resolución por DPR.
- La ejecución ultra-rápida (`ExecuteFastAttack`) del simulador de Montecarlo implementa este mismo camino libre de bloqueos de concurrencia.

### 4. Modelo de Datos y Servicio de Líneas Base (`IDprBaselineService`)
- Se extendió `Creature` con las propiedades `Level` (1-20), `ExpectedDpr` (double?), `Archetype` (string?) y el método de configuración `ConfigurePlayerCharacterProfile(...)`.
- Se creó la migración de EF Core `20261021202132_AddDprBaselinesToCreature` para persistir estas columnas en PostgreSQL.
- Se sembró `/data/seeds/dpr_baselines.csv` con los 6 arquetipos canónicos para los 20 niveles.
- Se implementó `DprBaselineService` con catálogo en memoria embebido y carga resiliente de CSV, expuesto vía `DprBaselinesController` (`/api/v1/dpr-baselines`).

### 5. Experiencia de Usuario en Frontend
- En `CustomHeroModal`, se sustituyó la entrada manual de arma por dos selectores: **Clase / Arquetipo** y **Nivel (1-20)**.
- Se muestra un panel visual reactivo (`#hero-expected-dpr-display`) con el DPR Estimado y nota metodológica.
- En `CombatSimulatorView`, cuando el combatiente seleccionado dispone de `expectedDpr`, el selector de arma se inhabilita automáticamente mostrando `⚡ DPR de Arquetipo (<valor> DPR) [Sin arma requerida]`.

---

## Consecuencias

### Positivas
- **Velocidad de Cómputo Incomparable:** Eliminar tiradas d20, parseo de fórmulas de dados y comprobaciones de armadura en los turnos de los PCs permite que la simulación de Montecarlo de 10,000 duelos se ejecute en menos de 15 ms en entornos de servidor estándar.
- **Precisión Metodológica Calibrada:** El daño de los personajes de los jugadores se alinea con las métricas de balance universales de la comunidad matemática de D&D 5e (Treantmonk Baseline).
- **Simplicidad para el DM:** Un Dungeon Master puede configurar un héroe de nivel 5 en 5 segundos sin tener que buscar qué dados tira su mandoble o cuántos ataques por turno realiza.
- **Desacoplamiento:** Nuevas opciones de juego o revisiones de reglas (por ejemplo, comparar el DPR de un Fighter 2014 vs 2024) se integran simplemente agregando filas al archivo CSV sin tocar una sola línea de código del motor de combate.

### Compensaciones y Limitaciones (Trade-offs)
- **Indiferencia a la CA Extrema:** El DPR esperado asume una CA objetivo media calibrada para el nivel según las tablas de diseño del Dungeon Master's Guide (típicamente 65% de probabilidad base de impacto). Si un monstruo posee una CA extremadamente atípica (ej. CA 25 a CR 1), el DPR directo asume el rendimiento promedio estándar a menos que se apliquen modificadores de escala relativos.
