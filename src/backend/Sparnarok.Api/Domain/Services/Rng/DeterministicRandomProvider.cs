namespace Sparnarok.Api.Domain.Services.Rng;

/// <summary>
/// Proveedor de números aleatorios determinista para desarrollo, depuración y pruebas unitarias.
/// Permite inicializar el generador con una semilla fija (seed) o suministrar una cola predeterminada
/// de tiradas exactas para validar escenarios matemáticos reproducibles (ej. críticos garantizados, fallos, etc.).
/// </summary>
public class DeterministicRandomProvider : IRandomProvider
{
    private readonly Random? _random;
    private readonly Queue<int>? _predefinedRolls;

    /// <summary>
    /// Inicializa una nueva instancia con una semilla fija reproducible.
    /// </summary>
    /// <param name="seed">Semilla numérica entera.</param>
    public DeterministicRandomProvider(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Inicializa una nueva instancia con una secuencia exacta de tiradas predeterminadas.
    /// Si la secuencia se agota, repite los valores en ciclo.
    /// </summary>
    /// <param name="rolls">Secuencia de tiradas a emitir en orden.</param>
    public DeterministicRandomProvider(IEnumerable<int> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);
        var rollList = rolls.ToList();
        if (rollList.Count == 0)
            throw new ArgumentException("La secuencia de tiradas predeterminadas no puede estar vacía.", nameof(rolls));

        _predefinedRolls = new Queue<int>(rollList);
    }

    /// <inheritdoc />
    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue), "minValue debe ser estrictamente menor que maxValue.");

        if (_predefinedRolls != null)
        {
            var value = _predefinedRolls.Dequeue();
            _predefinedRolls.Enqueue(value); // Re-encolar para ciclar si es necesario
            // Ajustar al rango si es necesario
            var span = maxValue - minValue;
            return minValue + (Math.Abs(value) % span);
        }

        return _random!.Next(minValue, maxValue);
    }

    /// <inheritdoc />
    public int RollDie(int sides)
    {
        if (sides <= 0)
            throw new ArgumentOutOfRangeException(nameof(sides), "El número de caras del dado debe ser mayor a cero.");

        if (_predefinedRolls != null)
        {
            var value = _predefinedRolls.Dequeue();
            _predefinedRolls.Enqueue(value); // Ciclar
            // Acotar al rango de caras [1, sides] si el valor es mayor
            return Math.Clamp(value, 1, sides);
        }

        return _random!.Next(1, sides + 1);
    }
}
