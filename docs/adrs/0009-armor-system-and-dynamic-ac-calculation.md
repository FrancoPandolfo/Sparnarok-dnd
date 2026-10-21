# ADR-0009: Sistema de Armaduras, Ingesta CSV Heterogénea y Cálculo Dinámico de Clase de Armadura (AC)

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
En la **Fase 2.1: Armor Ingestion & Dynamic AC** de **Sparnarok: The Architect's Codex**, surge la necesidad de enriquecer el sistema de combate y el Códice con un catálogo canónico de armaduras y escudos provenientes de un archivo CSV (`/data/seeds/armors.csv`), así como actualizar el motor de combate para calcular la Clase de Armadura (CA / AC) de forma dinámica en tiempo de ejecución.

Los desafíos arquitectónicos y matemáticos clave incluyeron:
1. **Heterogeneidad en la columna `ArmorClass` del CSV:** Los valores del catálogo oficial presentan formatos textuales variados:
   - Armaduras Ligeras: `"11 + Dex modifier"`, `"12 + Dex modifier"`.
   - Armaduras Medias con tope: `"12 + Dex modifier (max 2)"`, `"15 + Dex modifier (max 2)"`.
   - Armaduras Pesadas puras: `"14"`, `"16"`, `"17"`, `"18"`.
   - Escudos (bono aditivo): `"+2"`.
2. **Presencia de guiones (`"-"`) para campos nulos:** El archivo CSV utiliza guiones para indicar ausencia de requisito de Fuerza (`Strength`) o ausencia de penalización de sigilo (`Stealth`), los cuales deben transformarse limpiamente en valores nulos (`null`) o booleanos (`false`).
3. **Cálculo Dinámico de CA en Tiempo de Ejecución (D&D 5e 2024):** La CA no puede ser un valor estático inmutable almacenado en la criatura, ya que varía dinámicamente según la armadura corporal equipada, el escudo y el modificador de Destreza:
   - **Sin armadura (Unarmored):** $10 + \text{Modificador de Destreza}$.
   - **Armadura Ligera (Light):** $\text{CA Base} + \text{Modificador de Destreza}$.
   - **Armadura Media (Medium):** $\text{CA Base} + \min(\text{Modificador de Destreza}, 2)$. Los modificadores negativos penalizan normalmente (ej. Destreza 8 (-1) con Half Plate Base 15 resulta en CA 14).
   - **Armadura Pesada (Heavy):** $\text{CA Base}$ pura (ignora completamente el modificador de Destreza).
   - **Escudos (Shields):** Suman un bono de $+2$ a la CA total, independientemente de si la criatura viste armadura o está desarmada.
4. **Separación de Ranuras de Equipo:** Una criatura puede vestir simultáneamente una armadura corporal y portar un escudo, pero no puede equipar dos armaduras corporales ni dos escudos en la misma ranura.

---

## Opciones Evaluadas

### 1. Parseo de la Fórmula de CA
- **Opción A (Parseo en tiempo de ejecución en cada turno de combate):** Evaluar las cadenas de texto ("15 + Dex modifier (max 2)") mediante expresiones regulares o intérpretes de expresiones cada vez que un ataque impacta a una criatura.
  - *Desventaja:* Impacto destructivo en el rendimiento del motor de Montecarlo, que ejecuta decenas de miles de iteraciones por segundo.
- **Opción B (Parseo en fase de Ingesta y Persistencia de Parámetros Matemáticos) [Elegida]:**
  - La entidad de dominio `Armor` analiza la cadena `RawArmorClass` durante su instanciación o actualización mediante expresiones regulares compiladas.
  - Extrae y persiste columnas numéricas normalizadas: `BaseArmorClass` (int), `ShieldBonus` (int), `AllowsDexterityModifier` (bool) y `MaxDexterityModifier` (int?).
  - Esto garantiza que en tiempo de combate el cálculo de CA se reduzca a operaciones aritméticas elementales en CPU ($O(1)$ sin asignación en memoria).

### 2. Ranuras de Equipamiento en la Entidad `Creature`
- **Opción A (Colección genérica `List<Item>`):** Mayor generalidad, pero diluye la semántica de las reglas de 5e donde el cuerpo y la mano de escudo son ranuras tácticas diferenciadas con restricciones estrictas.
- **Opción B (Propiedades explícitas `EquippedArmor` y `EquippedShield`) [Elegida]:**
  - `EquippedArmorId` / `EquippedArmor`: Referencia opcional a una armadura corporal de categoría `Light`, `Medium` o `Heavy`. El método `EquipArmor(Armor armor)` valida que la categoría no sea `Shield`.
  - `EquippedShieldId` / `EquippedShield`: Referencia opcional a un escudo de categoría `Shield`. El método `EquipShield(Armor shield)` valida que la categoría sea exclusivamente `Shield`.
  - Métodos explícitos de dominio `UnequipArmor()` y `UnequipShield()` para alterar el estado táctico con seguridad de tipos.

### 3. Servicio de Cálculo de CA
- **Opción A (Lógica dispersa dentro de `CombatSimulatorService`):** Duplicación de lógica y dificultad de reutilización en la API de Bestiario y en la UI de frontend.
- **Opción B (Servicio de Dominio Puro `ArmorClassCalculator`) [Elegida]:**
  - Servicio estático/dominio puro desacoplado que expone `CalculateAc(Creature creature)`.
  - La propiedad de lectura `Creature.EffectiveArmorClass` delega directamente en `ArmorClassCalculator.CalculateAc(this)`, manteniendo el modelo de dominio enriquecido (*Rich Domain Model*) y garantizando coherencia en controladores, simulador 1v1 y motor Montecarlo.

---

## Decisión Adoptada

1. **Entidad `Armor`:**
   - Creada en `Sparnarok.Api.Domain.Entities.Armor` con enumeración `ArmorCategory` (`Light`, `Medium`, `Heavy`, `Shield`).
   - Implementa `ParseArmorClass(string rawAc, ArmorCategory category)` mediante expresiones regulares optimizadas para clasificar formatos `"11 + Dex modifier"`, `"15 + Dex modifier (max 2)"`, `"18"` y `"+2"`.
   - Mapeada en Entity Framework Core con la tabla `"Armors"`.

2. **Pipeline de Ingesta CSV (`POST /api/ingestion/armors`):**
   - Servicio `ArmorIngestionService` que lee flujos de datos con `CsvHelper`.
   - Limpieza de datos: convierte guiones `"-"` a `null` o vacío, parsea requisitos de fuerza (`"Str 13"` $\rightarrow 13$), marcas de desventaja en sigilo (`"Disadvantage"` $\rightarrow \text{true}$) y pesos mediante `DndWeightConverter`.
   - Soporte de Bulk Upsert idempotente basado en el nombre de la pieza.

3. **Cálculo Dinámico de CA (`ArmorClassCalculator`):**
   ```csharp
   public static int CalculateAc(Creature creature)
   {
       var dexMod = creature.AbilityScores.GetModifier(AbilityScoreType.Dexterity);
       int baseAc;

       if (creature.EquippedArmor != null)
       {
           baseAc = creature.EquippedArmor.Category switch
           {
               ArmorCategory.Heavy => creature.EquippedArmor.BaseArmorClass,
               ArmorCategory.Medium => creature.EquippedArmor.BaseArmorClass + Math.Min(dexMod, creature.EquippedArmor.MaxDexterityModifier ?? 2),
               ArmorCategory.Light => creature.EquippedArmor.BaseArmorClass + dexMod,
               _ => 10 + dexMod
           };
       }
       else
       {
           baseAc = creature.ArmorClass.AllowsDexterityModifier
               ? creature.ArmorClass.Base + dexMod
               : creature.ArmorClass.Base;
       }

       if (creature.EquippedShield != null)
       {
           baseAc += creature.EquippedShield.ShieldBonus;
       }
       else if (creature.ArmorClass.ShieldBonus > 0 && creature.EquippedArmor == null)
       {
           baseAc += creature.ArmorClass.ShieldBonus;
       }

       return baseAc;
   }
   ```

4. **Integración con Repositorios y Simulador:**
   - `CreatureRepository.GetByIdAsync` y `GetAllAsync` incorporan `.Include(c => c.EquippedArmor).Include(c => c.EquippedShield)` con `AsSplitQuery()`.
   - `CombatSimulatorService` y `MontecarloSimulationService` resuelven la CA defensiva dinámicamente contra tiradas d20 en cada ataque.

---

## Consecuencias

- **Positivas:**
  - Máxima fidelidad a las reglas canónicas de D&D 5e (2024).
  - Cero penalización de rendimiento en simulaciones Montecarlo de alta concurrencia gracias a la desnormalización de los parámetros matemáticos durante la ingesta.
  - Validación matemática exhaustiva mediante 141 tests unitarios y 20 tests de integración relacional.
  - Experiencia de usuario y visualización completas con soporte en la suite E2E de Playwright.
- **Compromisos (Trade-offs):**
  - El archivo CSV requiere que valores con comas (como `"1,500 GP"`) respeten el estándar RFC 4180 mediante comillas dobles para evitar segmentación indebida de columnas.
- **Mitigaciones:**
  - `data/seeds/armors.csv` se formateó bajo RFC 4180 y se añadieron tests unitarios que verifican la lectura completa del archivo físico del repositorio.

---

## Referencias
- [Player's Handbook 2024 - Chapter 6: Equipment (Armor & Shields)](https://www.dndbeyond.com)
- [ADR-0006: Pipeline de Ingesta Masiva de Datos Mediante CSV y CsvHelper](file:///docs/adrs/0006-csv-data-ingestion-pipeline.md)
- [ADR-0007: Arquitectura de Generación Pseudoaleatoria (RNG) y Evaluación de Dados](file:///docs/adrs/0007-combat-engine-rng-and-dice-evaluation.md)
- [ADR-0008: Concurrencia en Motor de Simulación Montecarlo y Agregación Estadística](file:///docs/adrs/0008-montecarlo-simulation-concurrency-and-metrics.md)
