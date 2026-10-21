namespace Sparnarok.Api.Domain.ValueObjects;

/// <summary>
/// Objeto de valor inmutable que encapsula la salud, daño y absorción de puntos de golpe bajo D&amp;D 5e (2024).
/// </summary>
public record HitPoints
{
    /// <summary>Puntos de golpe máximos de la criatura.</summary>
    public int Max { get; init; }

    /// <summary>Puntos de golpe actuales de la criatura.</summary>
    public int Current { get; init; }

    /// <summary>Puntos de golpe temporales activos.</summary>
    public int Temporary { get; init; }

    /// <summary>Indica si la criatura tiene al menos 1 punto de golpe actual.</summary>
    public bool IsConscious => Current > 0;

    /// <summary>
    /// Regla D&amp;D 5e (2024): Una criatura está "Ensangrentada" (Bloodied) cuando sus puntos de golpe actuales son iguales o menores a la mitad de su máximo.
    /// </summary>
    public bool IsBloodied => Current <= Max / 2;

    /// <summary>Constructor privado para deserialización y EF Core.</summary>
    private HitPoints() { }

    /// <summary>
    /// Inicializa un nuevo objeto de puntos de golpe.
    /// </summary>
    /// <param name="max">Puntos de golpe máximos (debe ser mayor a 0).</param>
    /// <param name="current">Puntos de golpe actuales (opcional, por defecto igual a max).</param>
    /// <param name="temporary">Puntos de golpe temporales iniciales (opcional, por defecto 0).</param>
    public HitPoints(int max, int? current = null, int temporary = 0)
    {
        if (max <= 0)
            throw new ArgumentOutOfRangeException(nameof(max), max, "Los puntos de golpe máximos deben ser mayores a cero.");

        var initialCurrent = current ?? max;
        if (initialCurrent < 0 || initialCurrent > max)
            throw new ArgumentOutOfRangeException(nameof(current), initialCurrent, $"Los puntos actuales deben estar entre 0 y el máximo ({max}).");

        if (temporary < 0)
            throw new ArgumentOutOfRangeException(nameof(temporary), temporary, "Los puntos temporales no pueden ser negativos.");

        Max = max;
        Current = initialCurrent;
        Temporary = temporary;
    }

    /// <summary>
    /// Aplica una cantidad de daño a la criatura siguiendo el protocolo de D&amp;D 5e (2024):
    /// Los puntos de golpe temporales absorben daño primero; el remanente reduce la salud actual hasta un mínimo de 0.
    /// </summary>
    /// <param name="amount">Cantidad de daño a infligir.</param>
    /// <returns>Nueva instancia de <see cref="HitPoints"/> con la salud calculada.</returns>
    public HitPoints ApplyDamage(int amount)
    {
        if (amount <= 0) return this;

        var absorbedByTemp = Math.Min(Temporary, amount);
        var remainingDamage = amount - absorbedByTemp;
        var newTemp = Temporary - absorbedByTemp;
        var newCurrent = Math.Max(0, Current - remainingDamage);

        return this with { Current = newCurrent, Temporary = newTemp };
    }

    /// <summary>
    /// Aplica sanación a los puntos de golpe actuales de la criatura sin exceder su máximo.
    /// No afecta ni restaura puntos de golpe temporales.
    /// </summary>
    /// <param name="amount">Cantidad de sanación recibida.</param>
    /// <returns>Nueva instancia de <see cref="HitPoints"/> con la salud restaurada.</returns>
    public HitPoints Heal(int amount)
    {
        if (amount <= 0) return this;

        var newCurrent = Math.Min(Max, Current + amount);
        return this with { Current = newCurrent };
    }

    /// <summary>
    /// Asigna puntos de golpe temporales respetando la regla de no-apilamiento de D&amp;D 5e:
    /// Se conserva el valor más alto entre los puntos temporales actuales y los nuevos.
    /// </summary>
    /// <param name="tempHp">Nueva cantidad de puntos temporales ofrecida.</param>
    /// <returns>Nueva instancia de <see cref="HitPoints"/>.</returns>
    public HitPoints ApplyTemporary(int tempHp)
    {
        if (tempHp <= 0) return this;

        return this with { Temporary = Math.Max(Temporary, tempHp) };
    }
}
