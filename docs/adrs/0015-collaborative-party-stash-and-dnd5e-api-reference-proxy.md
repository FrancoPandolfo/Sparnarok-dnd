# ADR-0015: Alijo Colaborativo de la Party (Collaborative Party Stash) y Proxy de Referencia D&D 5e API con Caché en Memoria

- **Fecha:** 2026-10-22
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema

Con la implementación de la persistencia de grupos (Partys y Encuentros en Fase 3.5), los aventureros y Dungeon Masters requieren gestionar el inventario colectivo acumulado a lo largo de las sesiones de juego. En D&D 5e (reglas 2024), la capacidad de carga y el seguimiento de tesoros, pociones, objetos mágicos y equipo de supervivencia son esenciales para el balance táctico y la logística del grupo.

El sistema de **Collaborative Party Stash** introduce los siguientes requerimientos arquitectónicos:

1. **Gestión de Inventario de la Party:**
   - La entidad `Party` debe almacenar una colección de objetos (`StashItems`), calculando en tiempo real el peso total acumulado (`TotalStashWeight`).
   - Cada objeto almacena propiedades tácticas: Nombre, Cantidad, Peso Unitario, Rareza, Categoría/Tipo, Descripción y procedencia (SRD Oficial vs. Homebrew).
2. **Experiencia de Usuario e Integración con D&D 5e API Oficial:**
   - Cargar manualmente descripciones extensas y pesos de armas, armaduras y objetos mágicos oficiales resulta tedioso y propenso a errores.
   - Es necesario conectar con la API pública oficial de D&D 5e (`https://www.dnd5eapi.co/api/2024/`) para autocompletar instantáneamente datos de catálogo.
3. **Manejo Robusto de Datos Externos Heterogéneos:**
   - La API externa divide los ítems entre dos endpoints distintos: `/equipment` y `/magic-items`.
   - Muchos objetos mágicos y artefactos devuelven `weight: null` o carecen del campo `weight` o devuelven arrays de descripción (`desc: []`). El backend debe deserializar y mapear estos valores de forma defensiva sin arrojar excepciones (`NullReferenceException` o deserialización fallida).
4. **Protección contra Rate-Limiting y Latencia:**
   - Las consultas frecuentes de autocompletado en el frontend pueden saturar la API externa o sufrir demoras de red.
   - Se debe implementar almacenamiento en caché en el backend y debounce en el frontend.
5. **Flexibilidad para Homebrew y Edición Libre:**
   - Aunque un ítem sea importado del SRD oficial, el usuario debe tener libertad total para editar su nombre (ej. "Daga +1 bendecida"), peso, rareza o descripción antes de guardarlo.
6. **Seguridad y Autorización a Nivel de Recurso:**
   - El alijo pertenece a la Party. Solo el usuario propietario (`OwnerUserId`) puede consultar, añadir o eliminar ítems del alijo (retornando `403 Forbidden` si un usuario ajeno lo intenta).

---

## Opciones Evaluadas

### Opción 1: Consulta Directa desde el Navegador (Frontend) a `dnd5eapi.co`
El cliente React consume directamente la API externa mediante llamadas `fetch` desde el navegador.
- *Ventajas:* No requiere código proxy en el backend de .NET.
- *Desventajas:* Expone al frontend a problemas de CORS y bloqueos de red; genera llamadas duplicadas sin control de caché centralizada; traslada la lógica compleja de unificación de endpoints (`/equipment` + `/magic-items`) y saneamiento de nulos al cliente.

### Opción 2: Ingesta Masiva y Duplicación Completa en PostgreSQL
Descargar e insertar todo el compendio de equipo y objetos mágicos en la base de datos local de PostgreSQL de Sparnarok.
- *Ventajas:* Consultas 100% locales con SQL relacional.
- *Desventajas:* Incremento innecesario del tamaño de la base de datos con miles de registros de solo lectura; desactualización frente a erratas y versiones del SRD 2024; mantenimiento de migraciones y pipelines pesados de sincronización.

### Opción 3: Backend Gateway Proxy con `HttpClient` Tipado, `IMemoryCache`, Mapeo Defensivo y Autocomplete con Debounce [SELECCIONADA]
La API de .NET 10 actúa como Gateway Proxy mediante una interfaz tipada `IDnd5eApiClient`, consultando bajo demanda los endpoints de `equipment` y `magic-items`, almacenando catálogos y detalles en `IMemoryCache`, y sanitizando los datos a DTOs normalizados. El frontend utiliza un componente Typeahead con debounce de 250ms.
- *Ventajas:*
  - Cero problemas de CORS o saturación externa.
  - Tiempos de respuesta inferiores a 1ms para consultas en caché.
  - Normalización unificada de tipos y nulos en el backend C#.
  - Almacenamiento persistente en base de datos reservado exclusivamente para los objetos reales de cada Party (`StashItem`).

---

## Razones de la Decisión

Se seleccionó la **Opción 3** por los siguientes fundamentos arquitectónicos:

### 1. Invariantes de Dominio en `Party` y `StashItem`
La entidad `StashItem` encapsula las reglas de negocio asegurando integridad de datos:
```csharp
public class StashItem
{
    public Guid Id { get; private set; }
    public Guid PartyId { get; private set; }
    public string Name { get; private set; }
    public int Quantity { get; private set; }
    public decimal Weight { get; private set; }
    public string Description { get; private set; }
    public string Rarity { get; private set; }
    public string ItemType { get; private set; }
    public bool IsOfficialSrd { get; private set; }
    public string? ReferenceIndex { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public StashItem(Guid partyId, string name, int quantity, decimal weight, string description, string rarity, string itemType, bool isOfficialSrd, string? referenceIndex)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del objeto no puede estar vacío.");
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser al menos 1.");
        if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight), "El peso no puede ser negativo.");
        // Asignación de propiedades...
    }
}
```
La entidad `Party` computa dinámicamente el peso acumulado:
```csharp
public decimal TotalStashWeight => _stashItems.Sum(i => i.Weight * i.Quantity);
```

### 2. Mapeo Defensivo de Datos Heterogéneos y Nulos
El cliente `Dnd5eApiClient` procesa `JsonDocument` de forma resiliente ante propiedades ausentes o `null`:
```csharp
decimal weight = 0m;
if (root.TryGetProperty("weight", out var weightProp) && weightProp.ValueKind == JsonValueKind.Number)
{
    weight = weightProp.GetDecimal();
}

string description = "";
if (root.TryGetProperty("desc", out var descProp) && descProp.ValueKind == JsonValueKind.Array)
{
    description = string.Join("\n\n", descProp.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)));
}
```

### 3. Estrategia de Caché Multinivel en Memoria (`IMemoryCache`)
- **Catálogo Unificado:** Cacheado por 1 hora (`equipment` + `magic-items`).
- **Búsquedas Filtradas:** Cacheadas por 30 minutos.
- **Detalle de Ítem:** Cacheado por 2 horas mediante clave `dnd5e_detail_{index}`.

### 4. Componente Frontend Autocomplete con Debounce
En `PartyStashModal.tsx`, las pulsaciones de teclado están sujetas a un temporizador de 250ms antes de disparar la consulta al proxy. Al seleccionar una sugerencia, los campos de peso, rareza, tipo y descripción se autocompletan pero se mantienen 100% editables por el usuario.

### 5. Autorización a Nivel de Recurso (403 Forbidden)
Los endpoints `/api/v1/parties/{id}/stash` validan la correspondencia entre el `OwnerUserId` de la Party y el identificador del usuario autenticado en el token JWT, retornando `Forbid()` cuando no coinciden.

---

## Consecuencias

### Positivas
- Experiencia de usuario ágil y fluida para equipar al grupo con objetos oficiales del SRD 2024 en un solo clic.
- Resiliencia absoluta ante caídas o respuestas incompletas de la API externa gracias al caché local y valores por defecto seguros.
- Separación limpia entre datos de referencia externa de solo lectura y entidades de persistencia transaccionales de usuario.
- Aislamiento seguro y cumplimiento de privacidad por usuario en entornos multi-inquilino.

### Verificación y Calidad
- **Pruebas Unitarias Backend:** 180 tests aprobados, incluyendo suite específica con simulación de `HttpMessageHandler`, comprobación de caché y control de pesos nulos.
- **Pruebas de Integración Backend:** 32 tests aprobados, validando endpoints proxy y respuestas 403 Forbidden ante intentos de acceso no autorizados.
- **Pruebas Unitarias Frontend (Vitest):** 36 tests aprobados (7 suites) cubriendo debounce, autocompletado y gestión de estado.
- **Pruebas E2E (Playwright):** 12 tests automatizados 100% verdes simulando el flujo completo de autocompletado, edición y persistencia del alijo.
