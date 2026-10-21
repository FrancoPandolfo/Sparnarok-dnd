namespace Sparnarok.Api.Domain.Services.Rng;

/// <summary>
/// Proveedor de números aleatorios para producción en tiempo de ejecución.
/// Utiliza la instancia de alto rendimiento y segura para hilos <see cref="Random.Shared"/>.
/// </summary>
public class DefaultRandomProvider : IRandomProvider
{
    /// <inheritdoc />
    public int Next(int minValue, int maxValue)
    {
        return Random.Shared.Next(minValue, maxValue);
    }

    /// <inheritdoc />
    public int RollDie(int sides)
    {
        if (sides <= 0)
            throw new ArgumentOutOfRangeException(nameof(sides), "El número de caras del dado debe ser mayor a cero.");

        return Random.Shared.Next(1, sides + 1);
    }
}
