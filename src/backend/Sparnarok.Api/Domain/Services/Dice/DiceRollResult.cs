namespace Sparnarok.Api.Domain.Services.Dice;

/// <summary>
/// Representa el resultado detallado de una tirada de dados de D&amp;D 5e.
/// </summary>
/// <param name="Total">Suma total obtenida (incluyendo modificadores, con mínimo de 0).</param>
/// <param name="DiceTotal">Suma pura de los resultados de los dados individuales.</param>
/// <param name="IndividualRolls">Lista de los resultados obtenidos en cada dado individual.</param>
/// <param name="Modifier">Modificador numérico total aplicado (característica + bonos del arma).</param>
/// <param name="IsCritical">Indica si la tirada fue duplicada por impacto crítico (Natural 20).</param>
public record DiceRollResult(
    int Total,
    int DiceTotal,
    IReadOnlyList<int> IndividualRolls,
    int Modifier,
    bool IsCritical);
