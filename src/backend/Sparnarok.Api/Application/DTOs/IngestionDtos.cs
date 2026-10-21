using CsvHelper.Configuration;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Modelo de datos que refleja una fila del archivo CSV de armas importado.
/// </summary>
public class WeaponCsvRecord
{
    /// <summary>Nombre del arma (ej. "Longsword", "Dagger").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Fórmula de daño en dados (ej. "1d8", "1d4").</summary>
    public string Damage { get; set; } = string.Empty;

    /// <summary>Tipo de daño como cadena (ej. "Slashing", "Piercing", "Bludgeoning").</summary>
    public string DamageType { get; set; } = string.Empty;

    /// <summary>Propiedades tácticas (ej. "Versatile (1d10)", "Finesse, Light").</summary>
    public string Properties { get; set; } = string.Empty;

    /// <summary>Peso del arma en libras.</summary>
    public double? Weight { get; set; }

    /// <summary>Costo comercial del arma (ej. "15 gp", "2 gp").</summary>
    public string Cost { get; set; } = string.Empty;
}

/// <summary>
/// Mapeo declarativo para CsvHelper que vincula las cabeceras del CSV con las propiedades del DTO.
/// </summary>
public sealed class WeaponCsvClassMap : ClassMap<WeaponCsvRecord>
{
    /// <summary>
    /// Configura el mapeo tolerante a mayúsculas y espacios en las cabeceras del CSV.
    /// </summary>
    public WeaponCsvClassMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.Damage).Name("Damage");
        Map(m => m.DamageType).Name("DamageType");
        Map(m => m.Properties).Name("Properties").Optional();
        Map(m => m.Weight).Name("Weight").Optional();
        Map(m => m.Cost).Name("Cost").Optional();
    }
}

/// <summary>
/// Resultado del proceso de ingesta masiva (Bulk Ingestion / Upsert).
/// </summary>
/// <param name="TotalProcessed">Cantidad total de filas procesadas del archivo.</param>
/// <param name="Inserted">Cantidad de nuevos registros dados de alta.</param>
/// <param name="Updated">Cantidad de registros existentes actualizados.</param>
/// <param name="Errors">Lista de mensajes de error o filas omitidas por inconsistencias.</param>
public record IngestionResultDto(
    int TotalProcessed,
    int Inserted,
    int Updated,
    IReadOnlyList<string> Errors);
