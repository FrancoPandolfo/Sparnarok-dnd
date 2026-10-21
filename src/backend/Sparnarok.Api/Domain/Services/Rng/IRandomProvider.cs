namespace Sparnarok.Api.Domain.Services.Rng;

/// <summary>
/// Abstracción central para la generación de números pseudoaleatorios en el motor de combate de D&amp;D 5e.
/// Desacopla la lógica matemática estocástica de implementaciones concretas para permitir
/// simulaciones reproducibles, pruebas deterministas con semillas fijas y pruebas unitarias controladas.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Genera un entero aleatorio en el rango [minValue, maxValue).
    /// </summary>
    /// <param name="minValue">Límite inferior inclusivo.</param>
    /// <param name="maxValue">Límite superior exclusivo.</param>
    /// <returns>Número entero pseudoaleatorio.</returns>
    int Next(int minValue, int maxValue);

    /// <summary>
    /// Realiza una tirada de un dado poliédrico de <paramref name="sides"/> caras (rango [1, sides]).
    /// </summary>
    /// <param name="sides">Número de caras del dado (ej. 4, 6, 8, 10, 12, 20, 100).</param>
    /// <returns>Resultado de la tirada entre 1 y <paramref name="sides"/>.</returns>
    int RollDie(int sides);

    /// <summary>
    /// Realiza una tirada canónica de dado de veinte caras d20 (rango [1, 20]).
    /// </summary>
    /// <returns>Resultado de la tirada del d20.</returns>
    int RollD20() => RollDie(20);
}
