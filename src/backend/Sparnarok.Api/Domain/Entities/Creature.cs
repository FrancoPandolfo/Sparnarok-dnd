using Sparnarok.Api.Domain.Enums;
using Sparnarok.Api.Domain.Services.Combat;
using Sparnarok.Api.Domain.ValueObjects;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad raíz de agregado que modela una criatura o personaje de D&amp;D 5e (2024),
/// incluyendo estadísticas, salud, defensas, acciones tácticas y maestrías de armas.
/// </summary>
public class Creature
{
    private readonly List<Action> _actions = [];
    private readonly List<WeaponMastery> _knownMasteries = [];

    /// <summary>Identificador único de la criatura.</summary>
    public Guid Id { get; private set; }

    /// <summary>Nombre de la criatura o arquetipo.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Categoría de tamaño.</summary>
    public CreatureSize Size { get; private set; }

    /// <summary>Tipología biológica o mágica.</summary>
    public CreatureType Type { get; private set; }

    /// <summary>Alineamiento moral/ético (ej. 'Neutral Good', 'Unaligned').</summary>
    public string Alignment { get; private set; } = string.Empty;

    /// <summary>Valor de Desafío (Challenge Rating - CR), desde 0 hasta 30.</summary>
    public double ChallengeRating { get; private set; }

    /// <summary>Velocidad de movimiento base terrestre en pies.</summary>
    public int SpeedFeet { get; private set; }

    /// <summary>Objeto de valor con las seis puntuaciones de características.</summary>
    public AbilityScores AbilityScores { get; private set; } = AbilityScores.StandardBaseline;

    /// <summary>Objeto de valor con los puntos de golpe máximos, actuales y temporales.</summary>
    public HitPoints HitPoints { get; private set; } = new(10);

    /// <summary>Objeto de valor con la configuración de Clase de Armadura.</summary>
    public ArmorClass ArmorClass { get; private set; } = ArmorClass.Unarmored;

    /// <summary>Identificador de la armadura corporal equipada (opcional).</summary>
    public Guid? EquippedArmorId { get; private set; }

    /// <summary>Armadura corporal vestida actualmente por la criatura.</summary>
    public virtual Armor? EquippedArmor { get; private set; }

    /// <summary>Identificador del escudo equipado (opcional).</summary>
    public Guid? EquippedShieldId { get; private set; }

    /// <summary>Escudo empuñado actualmente por la criatura.</summary>
    public virtual Armor? EquippedShield { get; private set; }

    /// <summary>Colección de acciones tácticas disponibles para la criatura.</summary>
    public IReadOnlyCollection<Action> Actions => _actions.AsReadOnly();

    /// <summary>Colección de propiedades de maestría de armas dominadas por la criatura.</summary>
    public IReadOnlyCollection<WeaponMastery> KnownMasteries => _knownMasteries.AsReadOnly();

    /// <summary>
    /// Calcula la Clase de Armadura efectiva integrando dinámicamente Destreza, Armadura (Ligera/Media/Pesada) y Escudo.
    /// </summary>
    public int EffectiveArmorClass => ArmorClassCalculator.CalculateAc(this);

    /// <summary>
    /// Bono de competencia (Proficiency Bonus) derivado del Valor de Desafío (CR).
    /// </summary>
    public int ProficiencyBonus => CalculateProficiencyBonus(ChallengeRating);

    /// <summary>Indica si esta criatura representa a un personaje de jugador (Player Character - PC) o héroe custom.</summary>
    public bool IsPlayerCharacter { get; private set; }

    /// <summary>Constructor protegido para EF Core.</summary>
    protected Creature() { }

    /// <summary>
    /// Crea una nueva criatura con los atributos de D&amp;D 5e (2024).
    /// </summary>
    /// <param name="name">Nombre de la criatura.</param>
    /// <param name="size">Tamaño.</param>
    /// <param name="type">Tipo de criatura.</param>
    /// <param name="alignment">Alineamiento.</param>
    /// <param name="challengeRating">Valor de desafío (0 - 30).</param>
    /// <param name="speedFeet">Velocidad en pies (por defecto 30).</param>
    /// <param name="abilityScores">Puntuaciones de características (opcional).</param>
    /// <param name="hitPoints">Puntos de golpe (opcional).</param>
    /// <param name="armorClass">Configuración de CA (opcional).</param>
    /// <param name="id">Identificador único opcional.</param>
    /// <param name="isPlayerCharacter">Flag que indica si es un personaje de jugador / héroe custom.</param>
    public Creature(
        string name,
        CreatureSize size,
        CreatureType type,
        string alignment,
        double challengeRating,
        int speedFeet = 30,
        AbilityScores? abilityScores = null,
        HitPoints? hitPoints = null,
        ArmorClass? armorClass = null,
        Guid? id = null,
        bool isPlayerCharacter = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la criatura no puede estar vacío.", nameof(name));

        if (challengeRating is < 0 or > 30)
            throw new ArgumentOutOfRangeException(nameof(challengeRating), challengeRating, "El Challenge Rating debe estar entre 0 y 30.");

        if (speedFeet < 0)
            throw new ArgumentOutOfRangeException(nameof(speedFeet), speedFeet, "La velocidad no puede ser negativa.");

        Id = id ?? Guid.NewGuid();
        Name = name;
        Size = size;
        Type = type;
        Alignment = alignment ?? "Unaligned";
        ChallengeRating = challengeRating;
        SpeedFeet = speedFeet;
        AbilityScores = abilityScores ?? AbilityScores.StandardBaseline;
        HitPoints = hitPoints ?? new(10);
        ArmorClass = armorClass ?? ArmorClass.Unarmored;
        IsPlayerCharacter = isPlayerCharacter;
    }

    /// <summary>
    /// Marca o desmarca esta criatura como un Personaje de Jugador (Player Character).
    /// </summary>
    /// <param name="isPlayerCharacter">Si es un PJ.</param>
    public void MarkAsPlayerCharacter(bool isPlayerCharacter = true)
    {
        IsPlayerCharacter = isPlayerCharacter;
    }

    /// <summary>
    /// Calcula el Bono de Competencia canónico según el Challenge Rating de la criatura.
    /// </summary>
    /// <param name="cr">Challenge Rating (0 - 30).</param>
    /// <returns>Bono numérico (+2 a +9).</returns>
    public static int CalculateProficiencyBonus(double cr) => cr switch
    {
        < 5 => 2,
        < 9 => 3,
        < 13 => 4,
        < 17 => 5,
        < 21 => 6,
        < 25 => 7,
        < 29 => 8,
        _ => 9
    };

    /// <summary>
    /// Actualiza las estadísticas base, defensas y características de la criatura.
    /// </summary>
    public void UpdateStatistics(
        string name,
        CreatureSize size,
        CreatureType type,
        string alignment,
        double challengeRating,
        int speedFeet,
        AbilityScores abilityScores,
        HitPoints hitPoints,
        ArmorClass armorClass)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la criatura no puede estar vacío.", nameof(name));

        if (challengeRating is < 0 or > 30)
            throw new ArgumentOutOfRangeException(nameof(challengeRating), challengeRating, "El Challenge Rating debe estar entre 0 y 30.");

        if (speedFeet < 0)
            throw new ArgumentOutOfRangeException(nameof(speedFeet), speedFeet, "La velocidad no puede ser negativa.");

        Name = name;
        Size = size;
        Type = type;
        Alignment = alignment ?? "Unaligned";
        ChallengeRating = challengeRating;
        SpeedFeet = speedFeet;
        AbilityScores = abilityScores ?? AbilityScores.StandardBaseline;
        HitPoints = hitPoints ?? new(10);
        ArmorClass = armorClass ?? ArmorClass.Unarmored;
    }

    /// <summary>
    /// Obtiene el modificador numérico de una característica específica.
    /// </summary>
    /// <param name="ability">Tipo de característica.</param>
    /// <returns>Modificador con signo.</returns>
    public int GetAbilityModifier(AbilityScoreType ability) => AbilityScores.GetModifier(ability);

    /// <summary>
    /// Calcula el bono total de ataque para una acción específica (Modificador + Bono de Competencia si aplica).
    /// </summary>
    /// <param name="action">Acción a evaluar.</param>
    /// <returns>Bono numérico para la tirada de ataque.</returns>
    public int CalculateAttackBonus(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var ability = action.AttackModifierAbility ?? AbilityScoreType.Strength;
        return GetAbilityModifier(ability) + ProficiencyBonus;
    }

    /// <summary>
    /// Calcula la Clase de Dificultad (CD / DC) para tiradas de salvación provocadas por las habilidades de la criatura.
    /// Fórmula canónica D&amp;D 5e: 8 + Bono de Competencia + Modificador de Característica.
    /// </summary>
    /// <param name="ability">Característica gobernante de la salvación.</param>
    /// <returns>Valor de CD (ej. 8 + 3 + 4 = 15).</returns>
    public int CalculateSavingThrowDc(AbilityScoreType ability)
    {
        return 8 + ProficiencyBonus + GetAbilityModifier(ability);
    }

    /// <summary>
    /// Aplica daño a la criatura delegando la absorción al objeto de valor <see cref="HitPoints"/>.
    /// </summary>
    /// <param name="damageAmount">Cantidad de daño.</param>
    public void TakeDamage(int damageAmount)
    {
        HitPoints = HitPoints.ApplyDamage(damageAmount);
    }

    /// <summary>
    /// Aplica sanación a la criatura delegando en <see cref="HitPoints"/>.
    /// </summary>
    /// <param name="healAmount">Cantidad de sanación.</param>
    public void Heal(int healAmount)
    {
        HitPoints = HitPoints.Heal(healAmount);
    }

    /// <summary>
    /// Asigna puntos de golpe temporales a la criatura.
    /// </summary>
    /// <param name="tempHp">Cantidad de puntos temporales.</param>
    public void GainTemporaryHitPoints(int tempHp)
    {
        HitPoints = HitPoints.ApplyTemporary(tempHp);
    }

    /// <summary>
    /// Registra una nueva acción táctica en el repertorio de la criatura.
    /// </summary>
    /// <param name="action">Acción a agregar.</param>
    public void AddAction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_actions.Any(a => a.Id == action.Id)) return;

        _actions.Add(action);
    }

    /// <summary>
    /// Elimina una acción del repertorio de la criatura por su identificador.
    /// </summary>
    /// <param name="actionId">Identificador de la acción.</param>
    public void RemoveAction(Guid actionId)
    {
        _actions.RemoveAll(a => a.Id == actionId);
    }

    /// <summary>
    /// Remueve todas las acciones asociadas a la criatura.
    /// </summary>
    public void ClearActions()
    {
        _actions.Clear();
    }

    /// <summary>
    /// Otorga el dominio de una propiedad de maestría de armas (Weapon Mastery 2024) a la criatura.
    /// </summary>
    /// <param name="mastery">Maestría a aprender.</param>
    public void LearnWeaponMastery(WeaponMastery mastery)
    {
        ArgumentNullException.ThrowIfNull(mastery);
        if (_knownMasteries.Any(m => m.Property == mastery.Property)) return;

        _knownMasteries.Add(mastery);
    }

    /// <summary>
    /// Remueve una maestría dominada del repertorio de la criatura.
    /// </summary>
    /// <param name="masteryId">Identificador de la maestría.</param>
    public void ForgetWeaponMastery(Guid masteryId)
    {
        _knownMasteries.RemoveAll(m => m.Id == masteryId);
    }

    /// <summary>
    /// Equipa una armadura corporal en la criatura.
    /// </summary>
    /// <param name="armor">Armadura a equipar (no puede ser de categoría Shield).</param>
    public void EquipArmor(Armor? armor)
    {
        if (armor != null && armor.Category == ArmorCategory.Shield)
        {
            throw new ArgumentException("Un escudo no puede ser equipado en la ranura de armadura corporal. Use EquipShield.", nameof(armor));
        }

        EquippedArmor = armor;
        EquippedArmorId = armor?.Id;
    }

    /// <summary>
    /// Desequipa la armadura corporal de la criatura, volviendo a su defensa natural o sin armadura.
    /// </summary>
    public void UnequipArmor()
    {
        EquippedArmor = null;
        EquippedArmorId = null;
    }

    /// <summary>
    /// Equipa un escudo en la criatura.
    /// </summary>
    /// <param name="shield">Escudo a empuñar (debe pertenecer a la categoría Shield).</param>
    public void EquipShield(Armor? shield)
    {
        if (shield != null && shield.Category != ArmorCategory.Shield)
        {
            throw new ArgumentException("Solo pueden equiparse objetos de categoría Shield en la ranura de escudo.", nameof(shield));
        }

        EquippedShield = shield;
        EquippedShieldId = shield?.Id;
    }

    /// <summary>
    /// Desequipa el escudo empuñado por la criatura.
    /// </summary>
    public void UnequipShield()
    {
        EquippedShield = null;
        EquippedShieldId = null;
    }
}
