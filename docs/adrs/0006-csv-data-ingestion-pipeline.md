# ADR-0006: Pipeline de Ingesta Masiva de Datos Mediante CSV y CsvHelper

- **Fecha:** 2026-10-21
- **Estado:** Aceptado
- **Decisores:** Equipo de Arquitectura de Sparnarok

## Contexto y Problema
Para simular matemáticamente el combate de D&D 5e (2024), el motor "Sparnarok" requiere un volumen exhaustivo de datos canónicos: armas, reglas, hechizos, monstruos y propiedades de maestría.

Cargar estas entidades de forma individual mediante endpoints REST manuales (`POST /creatures`, `POST /weapons`) resulta inviable para catálogos extensos y tareas de seeding o migración. Además, el equipo y la comunidad de D&D manejan comúnmente tablas de referencia tabulares exportadas desde hojas de cálculo o bases de datos de referencia (como 5e SRD).

Necesitamos una arquitectura de ingesta de datos masiva que cumpla con:
1. Simplicidad y portabilidad en el formato de intercambio.
2. Alto rendimiento y seguridad de memoria al parsear archivos grandes mediante streaming (evitando cargar archivos gigantescos en RAM como cadenas monolíticas).
3. Mapeo fuertemente tipado con tolerancia a mayúsculas/minúsculas y validación de tipos numéricos y delimitaciones de campos complejos (como cadenas con comas escapadas).
4. Persistencia idempotente (Bulk Upsert) en PostgreSQL vía EF Core para evitar duplicados si un lote se procesa múltiples veces.

## Opciones Evaluadas

### 1. Formato de Ingesta
- **Opción A (JSON Monolítico):** Fácil de serializar en APIs, pero verboso para tablas tabulares uniformes (armas, monstruos, hechizos) y propenso a saturar la memoria si se reciben arrays masivos en payloads HTTP.
- **Opción B (Archivos SQLite / Dump SQL):** Máximo rendimiento para migraciones completas, pero introduce acoplamiento estricto al motor de persistencia y no es fácilmente editable por diseñadores de juego o game masters.
- **Opción C (CSV - Comma-Separated Values) [Elegida]:** Estándar de facto para datos tabulares masivos. Extremadamente compacto, legible por humanos, integrable con herramientas estándar (Excel, Google Sheets, scripts Python) y procesable línea a línea mediante streams.

### 2. Librería de Parseo en el Ecosistema .NET
- **Opción A (Implementación propia `string.Split(',')`):** Frágil ante campos entre comillas que contienen comas internas (por ejemplo `Properties = "Versatile (1d10), Light"`), saltos de línea incrustados y variantes de encoding. Alto riesgo de errores sutiles de parsing.
- **Opción B (Librería `CsvHelper` de Josh Close) [Elegida]:** Estándar de la industria en .NET. Soporta lectura en streaming mediante `IAsyncEnumerable<T>`, mapeos declarativos (`ClassMap<T>`), manejo nativo de comillas RFC 4180, validación tipada y bajo footprint de asignación de memoria.
- **Opción C (OLEDB / Microsoft.VisualBasic.FileIO.TextFieldParser):** APIs legadas dependientes de plataformas o componentes COM/Windows, no aptas para microservicios modernos en contenedores Linux.

## Decisión Adoptada
1. **Adopción de CSV para Ingesta y Seeding:** Se adopta el formato CSV con encabezados estándar (`Name,Damage,DamageType,Properties,Weight,Cost` para armas) como canal oficial de ingesta masiva.
2. **Uso de CsvHelper en .NET 10:** Se integra `CsvHelper` (versión 33.x) dentro de `Sparnarok.Api` mediante un servicio genérico en streaming (`ICsvParserService`).
3. **Mapeo Tipado con `ClassMap<T>`:** Se configuran mapas explícitos (`WeaponCsvClassMap`) que ignoran diferencias de mayúsculas/minúsculas en los encabezados (`HeaderValidated = null`, `MissingFieldFound = null`).
4. **Estrategia de Idempotencia (Upsert EF Core):** El servicio `WeaponIngestionService` consulta las entidades existentes por su identificador natural (`Name` en minúsculas), actualizando las propiedades existentes e insertando las nuevas entidades en lotes únicos con `SaveChangesAsync()`.
5. **Streaming HTTP Multipart:** El endpoint expone `POST /api/ingestion/weapons` aceptando `IFormFile`, consumiendo el stream subyacente sin necesidad de escribir archivos temporales a disco.

## Consecuencias
- **Positivas:**
  - Ingesta rápida de catálogos oficiales y homebrew de armas en cuestión de milisegundos.
  - Cero dependencias nativas del sistema operativo; compatibilidad 100% con Linux y Docker.
  - Los archivos CSV son controlables en Git y fácilmente modificables por analistas de reglas.
  - Idempotencia garantizada: re-ejecutar la ingesta actualiza los registros sin duplicación ni fallos por clave primaria/índice único.
- **Negativas / Compromisos (Trade-offs):**
  - El formato CSV es plano; para relaciones complejas o grafos de objetos anidados (como criaturas con múltiples acciones y condiciones) se requerirán convenciones adicionales o pipelines dedicados por entidad.
  - Las comas internas en descripciones o propiedades deben estar obligatoriamente encomilladas según RFC 4180.
- **Mitigaciones:**
  - La interfaz de usuario proporciona una guía explícita del esquema CSV requerido con ejemplos sintácticos.
  - El servicio recopila y devuelve métricas detalladas (`totalProcessed`, `inserted`, `updated`) y una lista de advertencias descriptivas (`errors`) para cualquier registro que no cumpla las validaciones de dominio.

## Referencias
- [Documentación Oficial de CsvHelper](https://joshclose.github.io/CsvHelper/)
- [RFC 4180: Common Format and MIME Type for Comma-Separated Values (CSV) Files](https://www.ietf.org/rfc/rfc4180.txt)
- [Reglas D&D 5e (2024): Weapons & Equipment Table](https://www.dndbeyond.com)
