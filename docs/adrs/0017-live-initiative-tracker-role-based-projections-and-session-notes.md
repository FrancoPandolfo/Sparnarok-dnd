# ADR-0017: Tracker de Iniciativa en Vivo con Proyecciones Asimétricas por Rol en SignalR (Anti-Metagaming) y Notas de Sesión Exclusivas para el Dungeon Master

- **Fecha:** 2026-10-22
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

---

## Contexto y Problema

Durante el juego en vivo en la mesa de D&D 5e (reglas 2024), la dinámica de combate y gestión de campaña es intrínsecamente **asimétrica**:

1. **Visibilidad Táctica y Anti-Metagaming:**
   - El **Dungeon Master (DM)** requiere control y observabilidad total sobre el combate: lista completa de combatientes (héroes y monstruos), Clase de Armadura (CA) real, Puntos de Golpe (PG / HP) numéricos exactos, estado de sigilo/visibilidad de emboscada (`IsVisibleToPlayers`) y controles de orquestación (auto-roll de monstruos, avance/retroceso de turnos, alteración de vida y sobreescritura de iniciativa para jugadores AFK).
   - Los **Jugadores**, por el contrario, **jamás deben tener acceso a datos confidenciales del encuentro**. Enviar el estado completo del combate al frontend y confiar en que la interfaz oculte los monstruos o su CA es una vulnerabilidad crítica: cualquier jugador inspeccionando la pestaña de Red (*Network / WebSocket frames*) en las herramientas de desarrollo del navegador cometería *metagaming*, arruinando el factor sorpresa de emboscadas, trampas y resistencias del monstruo.

2. **Notas de Campaña y Sesión (Session Notes):**
   - El Dungeon Master necesita redactar apuntes de lore, pistas ocultas, trampas y recompensas en formato Markdown dentro del contexto de la Party.
   - Solo el usuario creador de la Party (`OwnerUserId`) tiene autorización de lectura, creación, modificación y eliminación sobre estas notas.

---

## Opciones Evaluadas

### Opción 1: Emisión Única de Estado SignalR con Censura en el Cliente (Frontend)
El servidor emite un único objeto JSON con todos los combatientes, monstruos, CA y HP reales a todos los clientes. La aplicación React filtra en render los monstruos invisibles.
- *Desventajas Críticas:* **Fuga masiva de información confidencial (Metagaming).** Los frames de WebSocket contienen los nombres, CA y HP de monstruos ocultos en texto plano. Se descarta tajantemente por violar el principio de mínimo privilegio y seguridad por diseño.

### Opción 2: Consultas REST Asimétricas por Polling HTTP
Los jugadores consultan endpoints GET cada pocos segundos con proyecciones diferenciadas por rol.
- *Desventajas:* Latencia inaceptable para combates tácticos en vivo; alta contención en la base de datos y falta de sincronismo visual entre el paso de turno y la tirada de iniciativa.

### Opción 3: Proyecciones Asimétricas de Estado en Servidor con Grupos SignalR Segregados (`LiveCombatHub`) [SELECCIONADA]
El Hub de SignalR `/hubs/live-combat` y el servicio en memoria `ILiveCombatManager` calculan dos DTOs completamente diferenciados en el backend antes de la serialización JSON. Las conexiones se adscriben a grupos independientes: `combat_{partyId}_dm` y `combat_{partyId}_player`.
- El payload del Jugador (`LiveCombatPlayerProjectionDto`) omite físicamente del array a los monstruos con `IsVisibleToPlayers == false`, omite la propiedad `ArmorClass` por completo y sustituye los PG numéricos de los monstruos por descriptores cualitativos (`"Healthy"`, `"Bloodied"`, `"Down"`).

---

## Razones de la Decisión

Se seleccionó la **Opción 3** por los siguientes principios arquitectónicos:

### 1. Modelo de Proyección en Servidor (Zero-Trust Anti-Metagaming)

La proyección se construye en memoria en `LiveCombatManager`:

```csharp
public LiveCombatPlayerProjectionDto CreatePlayerProjection(Guid partyId)
{
    var state = GetOrCreateState(partyId);
    var active = state.GetCurrentCombatant();

    // Filtro estricto en backend: SOLO héroes o monstruos con visibilidad autorizada
    var playerCombatants = state.Combatants
        .Where(c => c.IsPlayerCharacter || c.IsVisibleToPlayers)
        .Select(c =>
        {
            // Los monstruos NUNCA filtran sus HP numéricos exactos
            int? hp = c.IsPlayerCharacter ? c.CurrentHp : null;
            int? maxHp = c.IsPlayerCharacter ? c.MaxHp : null;
            string healthStatus = c.CurrentHp <= 0 ? "Down"
                : c.CurrentHp <= c.MaxHp / 2 ? "Bloodied"
                : "Healthy";

            return new LiveCombatantPlayerDto(
                c.Id,
                c.Name,
                c.Initiative,
                c.IsPlayerCharacter,
                hp,
                maxHp,
                healthStatus,
                c.Conditions
            );
        }).ToList();

    // Si el combatiente de turno es un monstruo oculto, se censura su identidad
    string? activeName = active?.Name;
    Guid? activeId = active?.Id;
    if (active != null && !active.IsPlayerCharacter && !active.IsVisibleToPlayers)
    {
        activeName = "Turno del Enemigo";
        activeId = null;
    }

    return new LiveCombatPlayerProjectionDto(
        state.PartyId,
        state.PartyName,
        state.Status.ToString(),
        state.CurrentRound,
        state.CurrentTurnIndex,
        activeId,
        activeName,
        playerCombatants
    );
}
```

### 2. Segregación de Canales SignalR por Rol

Al unirse al combate mediante `JoinCombat(partyId, role)`, la conexión se asocia al canal correspondiente:
- **Dungeon Master:** `Groups.AddToGroupAsync(connectionId, $"combat_{partyId}_dm")`
- **Jugadores:** `Groups.AddToGroupAsync(connectionId, $"combat_{partyId}_player")`

Cuando cualquier estado cambia (`SubmitInitiative`, `ModifyHp`, `NextTurn`, `ToggleVisibility`), el Hub efectúa una emisión dual diferenciada:
```csharp
public async Task BroadcastCombatStateAsync(Guid partyId)
{
    var dmProjection = _combatManager.CreateDmProjection(partyId);
    var playerProjection = _combatManager.CreatePlayerProjection(partyId);

    await Clients.Group(GetDmGroupName(partyId))
        .SendAsync("CombatStateUpdated", dmProjection);

    await Clients.Group(GetPlayerGroupName(partyId))
        .SendAsync("CombatStateUpdated", playerProjection);
}
```

### 3. Persistencia y Autorización en Session Notes

- Se crea la entidad `SessionNote` (`Id`, `PartyId`, `Title`, `Content`, `CreatedAt`, `UpdatedAt`) con índice por `PartyId`.
- En `SessionNotesController`, cada operación valida la autoría de la Party:
  ```csharp
  if (party.OwnerUserId != userId.Value)
  {
      return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
      {
          Title = "Acceso denegado",
          Status = StatusCodes.Status403Forbidden,
          Detail = "Solo el Dungeon Master tiene autorización para consultar o gestionar las notas de sesión de esta Party."
      });
  }
  ```
- En el frontend, el componente `SessionNotesEditor` ofrece edición en tiempo real de Markdown y vista previa estructurada en una pestaña que no se expone a usuarios con rol `Player`.

---

## Consecuencias y Verificación

### Consecuencias Positivas
- **Seguridad Absoluta:** Imposibilidad matemática de extraer información oculta inspeccionando el tráfico de red de un cliente jugador.
- **Flujo de Iniciativa Asincrónico:** Permite al combate permanecer en `WaitingForInitiative` mientras los jugadores tiran e ingresan sus valores, y faculta al DM para sobreescribir la iniciativa de jugadores AFK o tirar automáticamente la de todos los monstruos en 1 clic.
- **Rendimiento Óptimo:** El estado de combate táctico en vivo reside en memoria (`ConcurrentDictionary`), evitando accesos a disco por cada paso de turno o golpe de combate.

### Estrategia de Testing (Definition of Done)
1. **Tests Unitarios Backend:**
   - `LiveCombatHubTests.cs`: Verifica que la serialización JSON generada para una proyección de jugador nunca contenga combatientes invisibles ni propiedades de CA.
   - `SessionNotesTests.cs`: Pruebas de unidad con `NSubstitute` validando invariantes de dominio y retornos `403 Forbidden` ante llamadas de usuarios no autorizados.
2. **Tests de Integración Backend:**
   - `SessionNotesIntegrationTests.cs`: Ciclo CRUD persistido en PostgreSQL real con Testcontainers, confirmando rechazo de operaciones para usuarios no propietarios de la Party.
3. **Tests Frontend (Vitest):**
   - `LiveCombatTracker.test.tsx`: Aserción de ausencia de CA y de monstruos ocultos en vista de jugador; validación de controles maestros y botones rápidos de HP en vista de DM.
   - `SessionNotesEditor.test.tsx`: Carga, guardado, renderizado de Markdown y alternancia de vista previa.
4. **Test E2E de Doble Contexto (Playwright):**
   - `tests/e2e/specs/liveSession.spec.ts`: Levanta dos contextos de navegador concurrentes (`dmContext` y `playerContext`). El jugador ingresa su iniciativa y la ve reflejada, pero el DOM del jugador no contiene `"Goblin Boss"`, mientras que el DM sí lo visualiza con su CA correspondiente.
