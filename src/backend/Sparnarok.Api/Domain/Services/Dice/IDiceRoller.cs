namespace Sparnarok.Api.Domain.Services.Dice;

/// <summary>
/// Contrato para el parseo y evaluación de expresiones de dados de D&amp;D 5e (ej. "1d8", "2d6", "1d4+2", "1").
/// </summary>
public interface IDiceRoller
{
    /// <summary>
    /// Parsea una fórmula de dados y ejecuta la tirada aplicando modificadores y reglas de impacto crítico.
    /// </summary>
    /// <param name="formula">Fórmula en formato estándar (ej. "1d8", "2d6", "1d4+2", "1").</param>
    /// <param name="additionalModifier">Modificador adicional a sumar al daño (ej. modificador de Fuerza o Destreza).</param>
    /// <param name="isCritical">Indica si se deben duplicar los dados por impacto crítico (D&amp;D 5e 2024).</param>
    /// <returns>Resultado desglosado con total, dados individuales y modificador.</returns>
    DiceRollResult Roll(string formula, int additionalModifier = 0, bool isCritical = false);
}
