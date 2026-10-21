using System.Globalization;
using Sparnarok.Api.Application.Interfaces;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de catálogo y consulta de matrices de daño esperado por ronda (DPR Baselines).
/// Soporta carga automática desde el archivo canónico CSV (data/seeds/dpr_baselines.csv) con respaldo
/// determinista en memoria para entornos de pruebas o despliegue aislado.
/// </summary>
public class DprBaselineService : IDprBaselineService
{
    private readonly List<DprBaselineEntry> _entries;
    private readonly Dictionary<(string Archetype, int Level), DprBaselineEntry> _lookup;

    /// <summary>
    /// Inicializa una nueva instancia cargando la matriz desde CSV o mediante la tabla de respaldo integrada.
    /// </summary>
    /// <param name="csvPath">Ruta alternativa al archivo CSV de baselines.</param>
    public DprBaselineService(string? csvPath = null)
    {
        _entries = TryLoadFromCsv(csvPath) ?? LoadDefaultBaselines();
        _lookup = _entries.ToDictionary(
            e => (e.Archetype.ToLowerInvariant(), e.Level),
            e => e);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetArchetypes()
    {
        return _entries
            .Select(e => e.Archetype)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public double? GetExpectedDpr(string archetype, int level)
    {
        if (string.IsNullOrWhiteSpace(archetype) || level is < 1 or > 20)
            return null;

        if (_lookup.TryGetValue((archetype.Trim().ToLowerInvariant(), level), out var entry))
        {
            return entry.ExpectedDpr;
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<DprBaselineEntry> GetAllBaselines() => _entries.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<DprBaselineEntry> GetBaselinesForArchetype(string archetype)
    {
        if (string.IsNullOrWhiteSpace(archetype))
            return Array.Empty<DprBaselineEntry>();

        return _entries
            .Where(e => string.Equals(e.Archetype, archetype.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Level)
            .ToList()
            .AsReadOnly();
    }

    private static List<DprBaselineEntry>? TryLoadFromCsv(string? explicitPath)
    {
        var possiblePaths = new List<string>();

        if (!string.IsNullOrWhiteSpace(explicitPath))
            possiblePaths.Add(explicitPath);

        // Rutas relativas habituales desde el ejecutable o raíz del proyecto
        possiblePaths.Add(Path.Combine(AppContext.BaseDirectory, "data", "seeds", "dpr_baselines.csv"));
        possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "data", "seeds", "dpr_baselines.csv"));
        possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "data", "seeds", "dpr_baselines.csv"));
        possiblePaths.Add(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "seeds", "dpr_baselines.csv"));

        foreach (var path in possiblePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    if (lines.Length > 1)
                    {
                        var parsed = new List<DprBaselineEntry>();
                        // Saltar cabecera
                        for (int i = 1; i < lines.Length; i++)
                        {
                            var line = lines[i].Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            var parts = line.Split(',');
                            if (parts.Length >= 3)
                            {
                                var archetype = parts[0].Trim();
                                if (int.TryParse(parts[1].Trim(), CultureInfo.InvariantCulture, out int level) &&
                                    double.TryParse(parts[2].Trim(), CultureInfo.InvariantCulture, out double dpr))
                                {
                                    var desc = parts.Length > 3 ? string.Join(",", parts.Skip(3)).Trim() : string.Empty;
                                    parsed.Add(new DprBaselineEntry(archetype, level, dpr, desc));
                                }
                            }
                        }

                        if (parsed.Count > 0)
                            return parsed;
                    }
                }
            }
            catch
            {
                // Fallback continuo
            }
        }

        return null;
    }

    private static List<DprBaselineEntry> LoadDefaultBaselines()
    {
        var list = new List<DprBaselineEntry>();

        void AddCurve(string arch, double[] dprs, string baseDesc)
        {
            for (int lvl = 1; lvl <= 20 && lvl <= dprs.Length; lvl++)
            {
                list.Add(new DprBaselineEntry(arch, lvl, dprs[lvl - 1], $"{baseDesc} (Nivel {lvl})"));
            }
        }

        // 1. Fighter 2H (Nivel 5 = 19.4 canónico)
        AddCurve("Fighter 2H",
            [9.2, 10.8, 12.5, 14.1, 19.4, 21.0, 22.8, 24.5, 25.6, 27.2, 34.8, 36.5, 38.2, 40.0, 42.1, 44.2, 48.0, 50.5, 53.2, 62.0],
            "Greatsword con Extra Attack");

        // 2. Ranger Ranged
        AddCurve("Ranger Ranged",
            [7.5, 9.8, 11.5, 13.0, 18.2, 19.5, 21.0, 22.5, 24.0, 25.5, 29.0, 30.5, 32.0, 33.5, 35.0, 37.0, 40.0, 42.0, 44.5, 48.0],
            "Arco Largo y Hunter's Mark");

        // 3. Paladin 2H
        AddCurve("Paladin 2H",
            [8.5, 11.0, 13.0, 14.5, 20.5, 21.8, 23.2, 24.8, 27.0, 28.5, 36.0, 37.8, 40.2, 42.0, 44.5, 46.8, 50.5, 53.0, 55.8, 60.0],
            "Arma Pesada y Divine Smite");

        // 4. Barbarian 2H
        AddCurve("Barbarian 2H",
            [9.8, 12.0, 14.2, 16.0, 22.0, 23.5, 25.0, 26.8, 29.0, 30.8, 34.5, 36.2, 38.5, 40.8, 43.0, 45.5, 49.0, 51.5, 54.2, 64.0],
            "Greataxe con Furia y Reckless Attack");

        // 5. Rogue Sneak Attack
        AddCurve("Rogue Sneak Attack",
            [7.2, 8.5, 11.2, 12.5, 16.8, 18.0, 21.0, 22.5, 25.5, 27.0, 30.5, 32.0, 35.5, 37.0, 40.5, 42.0, 46.0, 48.0, 51.5, 55.0],
            "Arma Sutil con Progresión de Sneak Attack");

        // 6. Warlock Eldritch Blast (Treantmonk Baseline)
        AddCurve("Warlock Eldritch Blast",
            [5.5, 8.5, 9.2, 10.5, 16.5, 17.5, 18.5, 20.0, 21.0, 22.0, 28.5, 29.8, 31.0, 32.5, 33.8, 35.0, 42.0, 43.5, 45.0, 47.0],
            "Agonizing Blast Baseline");

        return list;
    }
}
