# ADR-0010: Integración de Bestiario Open5e y Perfiles Estáticos de Combatientes Custom (PCs)

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
En la **Fase 2.2: Bestiary API Integration & Custom Combatant Profiles** de **Sparnarok: The Architect's Codex**, el simulador táctico de combate 1v1 y el motor probabilístico de Montecarlo requerían datos de combate reales y rápidos para validar encuentros y medir Damage Per Round (DPR) y probabilidades de victoria.

Sin embargo, el proyecto enfrentaba dos cuellos de botella críticos de *Data Entry*:
1. **Modelado y tipeo manual de monstruos:** Introducir manualmente decenas de monstruos del Monster Manual o SRD era lento, propenso a errores tipográficos y no escalable.
2. **Complejidad de modelado de Personajes de Jugadores (PCs):** Intentar modelar un motor completo de creación de personajes de D&D (con razas, linajes, clases, subclases, trasfondos de 2024, árboles de dotes, progresión de nivel 1-20 y cálculo de espacios de conjuro) representaba semanas o meses de esfuerzo de ingeniería antes de poder probar un solo combate.

Era imperativo resolver la disponibilidad de datos de combate reales de forma inmediata y desacoplada del motor de creación de personajes.

---

## Opciones Evaluadas

### 1. Modelado de Combatientes de Jugadores (PCs)
- **Opción A (Construir un PC Builder completo):** Modelar entidades `CharacterClass`, `Race`, `Feat`, `ProgressionTable`, etc.
  - *Desventaja:* Sobrecarga arquitectónica masiva (*scope creep*), desvía el foco del valor nuclear de Sparnarok (el motor de simulación táctica y análisis de balance) y bloquea la experimentación inmediata.
- **Opción B (Perfiles Estáticos con Estadísticas Finales) [Elegida]:**
  - Implementar un endpoint dedicado `POST /api/v1/creatures/custom` que recibe los *Stats Finales* de un héroe (Nombre, CA final, HP máximo, Atributos STR/DEX/CON/INT/WIS/CHA, nombre y fórmula de ataque).
  - La criatura se persiste con el indicador `IsPlayerCharacter = true`.
  - Para respetar la CA final suministrada por el usuario, se configura la armadura base con `AllowsDexterityModifier = false`, garantizando que la CA efectiva coincida exactamente con el valor introducido sin doble suma de Destreza.
  - En la interfaz web de React, se dispone de un modal accesible ("Añadir Héroe Custom") en la vista del simulador, agregando al héroe directamente a los selectores de combate.

### 2. Fuente e Ingesta de Monstruos del Bestiario
- **Opción A (Archivos CSV locales de monstruos):** Cargar archivos CSV locales como se hizo con armas y armaduras.
  - *Desventaja:* Requiere mantener y versionar archivos gigantescos de datos de monstruos con descripciones y acciones multilínea complejas dentro del repositorio Git.
- **Opción B (Integración con API Externa Abierta: Open5e v1) [Elegida]:**
  - Consumir el endpoint público y canónico `https://api.open5e.com/v1/monsters/` mediante un cliente HTTP tipado (`Open5eBestiaryClient`) registrado a través de `IHttpClientFactory`.
  - Diseñar el endpoint `POST /api/ingestion/open5e/monsters/sync` para sincronizar lotes de monstruos en PostgreSQL con estrategia de upsert idempotente por nombre.

---

## Decisión Adoptada

### 1. Arquitectura de Ingesta Open5e
- **Cliente HTTP Tipado:** Se registró `Open5eBestiaryClient` en `Program.cs` usando `builder.Services.AddHttpClient<IOpen5eBestiaryClient, Open5eBestiaryClient>` con `BaseAddress = new Uri("https://api.open5e.com/v1/")`.
- **Mapeo Robusto de Datos de Monstruos (`Open5eSyncService`):**
  - **Clase de Armadura:** Open5e expone la CA final ya calculada (`armor_class`, ej. 14, 16). Se mapea con `allowsDexterityModifier: false` para evitar que el modificador de destreza de la entidad altere la CA canónica del monstruo.
  - **Puntos de Golpe:** Se inicializa el Value Object `HitPoints` con `current = max = monster.hit_points` y `temporary = 0`.
  - **Challenge Rating (CR):** Se implementó un parser de fracciones de D&D para soportar tanto números estándar como representaciones textuales de fracciones (`"1/8"`, `"1/4"`, `"1/2"`).
  - **Velocidad:** Parseo seguro del objeto flexible `speed` (admitiendo diccionarios con clave `walk` o enteros directos).
  - **Acciones Tácticas:** Se mapea cada acción de la colección `actions` a una entidad `CreatureAction`, extrayendo mediante expresiones regulares el bono de ataque (`attackBonus`) y la fórmula de dados de daño (`damageFormula`, ej. `1d8+3`, `2d6+4`).
  - **Idempotencia:** Si el monstruo ya existe en la base de datos (búsqueda por nombre sin distinción de mayúsculas/minúsculas), se actualizan sus atributos y se reemplazan sus acciones; si no existe, se inserta una nueva criatura.

### 2. Perfiles Estáticos de Combatientes (PCs)
- **Endpoint:** `POST /api/v1/creatures/custom`.
- **DTO:** `CreateCustomHeroRequestDto` con validaciones FluentValidation (`CreateCustomHeroRequestValidator`):
  - Nombre requerido y longitud acotada.
  - Puntos de golpe y CA mayores a 0.
  - Atributos entre 1 y 30.
- **Persistencia de Dominio:** Se crea una `Creature` con `MarkAsPlayerCharacter()`, permitiendo a la UI distinguir héroes con insignias visuales (`🛡️ [Héroe]`).

---

## Consecuencias

### Positivas
- **Velocidad de Puesta en Marcha:** Disponibilidad instantánea de más de 50 monstruos de SRD en la base de datos local y en pruebas automatizadas.
- **Flexibilidad Táctica Total:** Los directores de juego e investigadores pueden simular cualquier personaje de su mesa en menos de 10 segundos ingresando su ficha simplificada.
- **Rendimiento Máximo en Montecarlo:** Al disponer de combatientes con CA efectiva precalculada y acciones estructuradas, el motor de combate 1v1 y Montecarlo operan a máxima velocidad en memoria sin latencia externa.
- **Seguridad en Pruebas:** El cliente Open5e y la sincronización se prueban con `HttpMessageHandler` mockeado en tests de integración y JSON fixtures en tests unitarios, sin depender de la conectividad a internet en CI/CD.

### Compensaciones y Limitaciones (Trade-offs)
- **Granularidad de Equipo en Monstruos:** Los monstruos de Open5e no detallan qué tipo específico de armadura o escudo llevan equipado en campos separados; la CA se preserva como valor estático efectivo.
- **Sin Automatización de Reglas de Nivel para PCs:** Los combatientes custom no calculan de forma dinámica sus espacios de conjuro ni dotes por nivel; cualquier bonificación debe reflejarse en los valores finales introducidos en el formulario.
