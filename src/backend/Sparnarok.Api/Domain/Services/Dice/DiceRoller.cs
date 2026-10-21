using System.Text.RegularExpressions;
using Sparnarok.Api.Domain.Services.Rng;

namespace Sparnarok.Api.Domain.Services.Dice;

/// <summary>
/// Implementación de <see cref="IDiceRoller"/> que parsea fórmulas algebraicas de dados estándar de D&amp;D
/// (ej. "1d8", "2d6", "1d4+2", "1") y calcula el resultado total e individual utilizando un <see cref="IRandomProvider"/>.
/// </summary>
public partial class DiceRoller : IDiceRoller
{
    private readonly IRandomProvider _random;

    [GeneratedRegex(@"^\s*(?:(?<count>\d+)?d(?<sides>\d+))?(?:\s*(?<mod>[+-]?\d+))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DiceFormulaRegex();

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="DiceRoller"/> con el proveedor aleatorio inyectado.
    /// </summary>
    /// <param name="random">Proveedor de números aleatorios (producción o determinista para tests).</param>
    public DiceRoller(IRandomProvider random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public DiceRollResult Roll(string formula, int additionalModifier = 0, bool isCritical = false)
    {
        if (string.IsNullOrWhiteSpace(formula))
            throw new ArgumentException("La fórmula de dados no puede estar vacía.", nameof(formula));

        var match = DiceFormulaRegex().Match(formula.Trim());
        if (!match.Success)
            throw new FormatException($"La fórmula de dados '{formula}' no tiene un formato válido de D&D (ej. '1d8', '2d6+2', '1').");

        int diceCount = 0;
        int diceSides = 0;
        int formulaMod = 0;

        var sidesGroup = match.Groups["sides"];
        if (sidesGroup.Success)
        {
            diceSides = int.Parse(sidesGroup.Value);
            var countGroup = match.Groups["count"];
            diceCount = countGroup.Success ? int.Parse(countGroup.Value) : 1;
        }

        var modGroup = match.Groups["mod"];
        if (modGroup.Success && !string.IsNullOrWhiteSpace(modGroup.Value))
        {
            formulaMod = int.Parse(modGroup.Value);
        }

        // Si es un impacto crítico (Natural 20), en D&D 5e (2024) se duplican todos los dados de daño
        var actualDiceCount = isCritical ? diceCount * 2 : diceCount;
        var individualRolls = new List<int>(actualDiceCount);
        int diceSum = 0;

        if (diceSides > 0)
        {
            for (int i = 0; i < actualDiceCount; i++)
            {
                var roll = _random.RollDie(diceSides);
                individualRolls.Add(roll);
                diceSum += roll;
            }
        }

        var totalModifier = formulaMod + additionalModifier;
        // El daño total mínimo en D&D es 0
        var total = Math.Max(0, diceSum + totalModifier);

        return new DiceRollResult(
            Total: total,
            DiceTotal: diceSum,
            IndividualRolls: individualRolls.AsReadOnly(),
            Modifier: totalModifier,
            IsCritical: isCritical);
    }
}
