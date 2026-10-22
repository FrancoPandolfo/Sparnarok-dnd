# ADR-0016: Reactividad en Tiempo Real con SignalR (WebSockets), Cola de Fabricación (Downtime Crafting) e Historial de Auditoría en el Alijo Colaborativo

- **Fecha:** 2026-10-22
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

---

## Contexto y Problema

Durante las sesiones de rol en vivo en la mesa de juego, múltiples jugadores y el Dungeon Master (DM) interactúan simultáneamente con el tesoro común y el inventario del grupo. La implementación inicial del Collaborative Party Stash requería recargas manuales del navegador o polling para observar cambios realizados por otros integrantes.

Asimismo, surgen tres requerimientos tácticos esenciales para la experiencia de usuario (Quality of Life) en D&D 5e (reglas 2024):

1. **Sincronización en Tiempo Real (SignalR / WebSockets):**
   - Cuando un jugador añade o elimina un objeto del alijo, o modifica las monedas del grupo, todos los clientes conectados a esa Party deben recibir los cambios instantáneamente sin recargar la página.
2. **Historial de Transacciones (Audit Log):**
   - Para evitar disputas o pérdidas de trazabilidad en la mesa, el sistema debe auditar automáticamente cualquier movimiento de monedas, adición o eliminación de objetos y progreso de fabricación.
3. **Módulo de Fabricación en Descanso (Downtime Crafting):**
   - Los personajes invierten tiempo entre aventuras para forjar equipo, transcribir pergaminos o destilar pociones. El coste en oro (PO) debe descontarse inmediatamente del tesoro común (*Party Fund*), asignando un contador de "Días Restantes".
   - El Dungeon Master dispone de la acción exclusiva: *"Avanzar 1 día de Downtime"*, reduciendo el contador de todos los proyectos activos. Al llegar a 0 días restantes, el objeto se transfiere de manera atómica al alijo principal.

---

## Opciones Evaluadas

### Opción 1: Short Polling / Long Polling HTTP
El cliente React realiza peticiones periódicas `GET /parties/{id}/stash` cada 3 a 5 segundos.
- *Desventajas:* Sobrecarga innecesaria en la base de datos y en el pipeline de la API; consumo de ancho de banda y latencia perceptible de varios segundos entre acciones de los jugadores.

### Opción 2: Server-Sent Events (SSE) Unidireccional
El servidor mantiene una conexión HTTP abierta para enviar eventos hacia el navegador.
- *Desventajas:* SSE es unidireccional y carece de soporte nativo para agrupación dinámica de clientes en canales temáticos (*groups*) como ofrece SignalR; requiere gestión manual de reconexión y tokens JWT.

### Opción 3: ASP.NET Core SignalR con Hub Tipado, Grupos por Party y WebSockets [SELECCIONADA]
Se incorpora SignalR en el backend de .NET 10 mediante un `StashHub` mapeado en `/hubs/stash`. Los clientes se suscriben exclusivamente a grupos aislados `$"party_{partyId}"`. El transporte primario es WebSockets bidireccional con reconexión automática (`withAutomaticReconnect()`) y autenticación JWT vía query parameter `?access_token=...`.

---

## Razones de la Decisión

Se seleccionó la **Opción 3** por los siguientes fundamentos arquitectónicos y técnicos:

### 1. Hub de SignalR y Aislamiento por Grupos (`StashHub`)
Para garantizar la privacidad y prevenir fugas de información entre grupos distintos, las conexiones se aíslan estrictamente por el identificador de la Party:
```csharp
public class StashHub : Hub
{
    public async Task JoinPartyGroup(string partyId)
    {
        if (Guid.TryParse(partyId, out var id))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(id));
        }
    }

    public static string GetGroupName(Guid partyId) => $"party_{partyId}";
}
```

### 2. Autenticación JWT en Conexiones WebSockets
Dado que el protocolo WebSocket estándar en los navegadores web no permite enviar cabeceras personalizadas (`Authorization: Bearer ...`), SignalR transporta el token en la cadena de consulta. El pipeline de ASP.NET Core intercepta este token en `OnMessageReceived`:
```csharp
options.Events = new JwtBearerEvents
{
    OnMessageReceived = context =>
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.HttpContext.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        {
            context.Token = accessToken;
        }
        return Task.CompletedTask;
    }
};
```

### 3. Servicio de Notificación Desacoplado (`IStashNotificationService`)
Los controladores delegan la emisión de eventos en `IStashNotificationService`, desacoplando la lógica HTTP de la infraestructura de SignalR:
- `ItemAdded(StashItemResponseDto item)`
- `ItemDeleted(Guid itemId)`
- `FundsUpdated(PartyFundDto funds)`
- `CraftingQueueUpdated(IReadOnlyList<CraftingQueueItemDto> queue)`
- `ActivityLogged(StashActivityLogDto log)`
- `DowntimeAdvanced(DowntimeAdvanceResultDto result)`
- `StashUpdated(PartyStashResponseDto stash)`

### 4. Modelo de Monedas (Party Fund) con Invariantes de Dominio
Se implementó en la entidad `Party` el almacenamiento de las 5 monedas canónicas de D&D 5e:
- Piezas de Cobre (PC / cp): 1/100 PO
- Piezas de Plata (PP / sp): 1/10 PO
- Piezas de Electro (PE / ep): 1/2 PO
- Piezas de Oro (PO / gp): 1 PO
- Piezas de Platino (PL / pp): 10 PO

El cálculo del equivalente total se evalúa mediante la propiedad de solo lectura:
```csharp
public decimal TotalGoldEquivalent =>
    (CopperPieces / 100m) +
    (SilverPieces / 10m) +
    (ElectrumPieces / 2m) +
    GoldPieces +
    (PlatinumPieces * 10m);
```

### 5. Fabricación en Descanso (Downtime Crafting) y Transición Atómica
La entidad `CraftingQueueItem` encapsula el ciclo de vida de la fabricación (`InProgress` -> `Completed`). Al avanzar el día con `AdvanceDay()`:
- Si `DaysRemaining == 0`, el estado cambia a `Completed`.
- El método `ToStashItem()` proyecta el trabajo terminado en una nueva entidad `StashItem` que se persiste de inmediato en el alijo de la Party y emite el evento en vivo `StashUpdated`.

### 6. Historial de Auditoría (StashActivityLog)
Cada mutación de recursos genera un registro inmutable con el usuario actor, tipo de acción (`ItemAdded`, `ItemDeleted`, `FundsUpdated`, `CraftingStarted`, `DowntimeAdvanced`, `CraftingCompleted`) y marca de tiempo UTC.

---

## Consecuencias

### Positivas
- **Reactividad Instantánea:** Todos los miembros de la mesa observan los cambios en el alijo, monedas y cola de fabricación en menos de 50ms sin refrescar la ventana.
- **Trazabilidad Total:** Registro permanente de quién gastó oro o tomó objetos del alijo.
- **Mecánica Oficial de Downtime:** Automatización de las reglas de descanso y fabricación de D&D 5e 2024 con validación estricta de balance de fondos.
- **Resiliencia Frontend:** El cliente SignalR gestiona reconexión automática y degradación elegante a llamadas REST en caso de desconexión.

### Verificación y Calidad
- **Pruebas Unitarias Backend:** 193 tests aprobados (100% verdes), incluyendo cobertura exhaustiva para `StashHub`, `StashNotificationService` y reglas de dominio de fondos y fabricación.
- **Pruebas de Integración Backend:** 33 tests aprobados con PostgreSQL Testcontainers, validando el flujo completo de fondos, encolado, deducción de oro, avance de downtime y consulta de auditoría.
- **Pruebas Frontend (Vitest):** 37 tests aprobados (7 suites) cubriendo renderizado de pestañas, modificación de monedas, encolado de fabricación y logs.
