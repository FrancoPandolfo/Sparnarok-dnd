using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.ValueObjects;

/// <summary>
/// Objeto de valor inmutable que encapsula las seis puntuaciones de características de D&amp;D 5e (2024) y su cálculo matemático de modificadores.
/// </summary>
public record AbilityScores
{
    /// <summary>Puntaje mínimo canónico para una característica en D&amp;D 5e.</summary>
    public const int MinScore = 1;

    /// <summary>Puntaje máximo canónico para una característica en D&amp;D 5e.</summary>
    public const int MaxScore = 30;

    /// <summary>Puntuación de Fuerza.</summary>
    public int Strength { get; init; }

    /// <summary>Puntuación de Destreza.</summary>
    public int Dexterity { get; init; }

    /// <summary>Puntuación de Constitución.</summary>
    public int Constitution { get; init; }

    /// <summary>Puntuación de Inteligencia.</summary>
    public int Intelligence { get; init; }

    /// <summary>Puntuación de Sabiduría.</summary>
    public int Wisdom { get; init; }

    /// <summary>Puntuación de Carisma.</summary>
    public int Charisma { get; init; }

    /// <summary>
    /// Constructor privado para uso de deserializadores o EF Core.
    /// </summary>
    private AbilityScores() { }

    /// <summary>
    /// Crea un conjunto de puntuaciones de características validando el rango canónico [1, 30].
    /// </summary>
    /// <param name="strength">Puntuación de Fuerza (1-30).</param>
    /// <param name="dexterity">Puntuación de Destreza (1-30).</param>
    /// <param name="constitution">Puntuación de Constitución (1-30).</param>
    /// <param name="intelligence">Puntuación de Inteligencia (1-30).</param>
    /// <param name="wisdom">Puntuación de Sabiduría (1-30).</param>
    /// <param name="charisma">Puntuación de Carisma (1-30).</param>
    /// <exception cref="ArgumentOutOfRangeException">Si alguna puntuación se encuentra fuera del rango permitido.</exception>
    public AbilityScores(
        int strength,
        int dexterity,
        int constitution,
        int intelligence,
        int wisdom,
        int charisma)
    {
        ValidateScore(strength, nameof(strength));
        ValidateScore(dexterity, nameof(dexterity));
        ValidateScore(constitution, nameof(constitution));
        ValidateScore(intelligence, nameof(intelligence));
        ValidateScore(wisdom, nameof(wisdom));
        ValidateScore(charisma, nameof(charisma));

        Strength = strength;
        Dexterity = dexterity;
        Constitution = constitution;
        Intelligence = intelligence;
        Wisdom = wisdom;
        Charisma = charisma;
    }

    /// <summary>
    /// Retorna una instancia con el conjunto estándar de atributos (10 en cada uno).
    /// </summary>
    public static AbilityScores StandardBaseline => new(10, 10, 10, 10, 10, 10);

    /// <summary>
    /// Calcula el modificador de característica canónico de D&amp;D 5e: floor((score - 10) / 2).
    /// </summary>
    /// <param name="score">Puntuación numérica (1-30).</param>
    /// <returns>Modificador con signo (ej. 10 -> 0, 12 -> +1, 9 -> -1, 1 -> -5, 30 -> +10).</returns>
    public static int CalculateModifier(int score)
    {
        ValidateScore(score, nameof(score));
        return (int)Math.Floor((score - 10) / 2.0);
    }

    /// <summary>
    /// Obtiene la puntuación correspondiente al tipo de característica especificado.
    /// </summary>
    /// <param name="type">Tipo de característica.</param>
    /// <returns>Puntuación numérica.</returns>
    public int GetScore(AbilityScoreType type) => type switch
    {
        AbilityScoreType.Strength => Strength,
        AbilityScoreType.Dexterity => Dexterity,
        AbilityScoreType.Constitution => Constitution,
        AbilityScoreType.Intelligence => Intelligence,
        AbilityScoreType.Wisdom => Wisdom,
        AbilityScoreType.Charisma => Charisma,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de característica desconocido.")
    };

    /// <summary>
    /// Obtiene el modificador numérico calculado para una característica específica.
    /// </summary>
    /// <param name="type">Tipo de característica.</param>
    /// <returns>Modificador entero.</returns>
    public int GetModifier(AbilityScoreType type) => CalculateModifier(GetScore(type));

    private static void ValidateScore(int score, string paramName)
    {
        if (score is < MinScore or > MaxScore)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                score,
                $"La puntuación de característica debe encontrarse entre {MinScore} y {MaxScore}.");
        }
    }
}
