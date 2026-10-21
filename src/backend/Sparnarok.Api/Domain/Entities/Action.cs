using Sparnarok.Api.Domain.Enums;

namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad que representa una acción, ataque o habilidad táctica disponible para una criatura.
/// </summary>
public class Action
{
    /// <summary>Identificador único de la acción.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador de la criatura propietaria de la acción.</summary>
    public Guid CreatureId { get; private set; }

    /// <summary>Nombre descriptivo de la acción.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Descripción operativa del efecto de la acción.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Tipo de acción dentro de la economía de combate.</summary>
    public ActionType Type { get; private set; }

    /// <summary>Indica si la acción involucra una tirada de ataque contra CA.</summary>
    public bool IsAttack { get; private set; }

    /// <summary>Atributo utilizado para calcular la tirada de ataque y bonos de daño (ej. Fuerza, Destreza).</summary>
    public AbilityScoreType? AttackModifierAbility { get; private set; }

    /// <summary>Alcance o rango efectivo de la acción en pies.</summary>
    public int ReachOrRangeFeet { get; private set; }

    /// <summary>Fórmula de dados de daño base (ej. '1d8', '2d6', '1d12').</summary>
    public string DamageFormula { get; private set; } = string.Empty;

    /// <summary>Tipo de daño infligido.</summary>
    public DamageType? DamageType { get; private set; }

    /// <summary>Identificador de la maestría de armas asociada si aplica.</summary>
    public Guid? AssociatedMasteryId { get; private set; }

    /// <summary>Referencia de navegación a la maestría de armas asociada.</summary>
    public WeaponMastery? AssociatedMastery { get; private set; }

    /// <summary>Constructor protegido para EF Core.</summary>
    protected Action() { }

    /// <summary>
    /// Crea una nueva acción o ataque de criatura.
    /// </summary>
    /// <param name="creatureId">Identificador de la criatura poseedora.</param>
    /// <param name="name">Nombre de la acción.</param>
    /// <param name="type">Tipo de acción (Acción, Bonus, Reacción, etc.).</param>
    /// <param name="description">Descripción del efecto.</param>
    /// <param name="isAttack">Indica si realiza tirada de ataque.</param>
    /// <param name="reachOrRangeFeet">Alcance en pies.</param>
    /// <param name="damageFormula">Fórmula de daño.</param>
    /// <param name="damageType">Tipo de daño.</param>
    /// <param name="attackModifierAbility">Característica para el modificador de ataque.</param>
    /// <param name="associatedMastery">Maestría de armas opcional vinculada.</param>
    /// <param name="id">Identificador único opcional.</param>
    public Action(
        Guid creatureId,
        string name,
        ActionType type,
        string description = "",
        bool isAttack = false,
        int reachOrRangeFeet = 5,
        string damageFormula = "",
        DamageType? damageType = null,
        AbilityScoreType? attackModifierAbility = null,
        WeaponMastery? associatedMastery = null,
        Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        CreatureId = creatureId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("El nombre de la acción es obligatorio.", nameof(name)) : name;
        Type = type;
        Description = description ?? string.Empty;
        IsAttack = isAttack;
        ReachOrRangeFeet = reachOrRangeFeet >= 0 ? reachOrRangeFeet : throw new ArgumentOutOfRangeException(nameof(reachOrRangeFeet), "El alcance no puede ser negativo.");
        DamageFormula = damageFormula ?? string.Empty;
        DamageType = damageType;
        AttackModifierAbility = attackModifierAbility;

        if (associatedMastery is not null)
        {
            AssociateMastery(associatedMastery);
        }
    }

    /// <summary>
    /// Asocia una propiedad de maestría de armas (Weapon Mastery 2024) a esta acción de ataque.
    /// </summary>
    /// <param name="mastery">Maestría a vincular.</param>
    public void AssociateMastery(WeaponMastery mastery)
    {
        ArgumentNullException.ThrowIfNull(mastery);
        AssociatedMastery = mastery;
        AssociatedMasteryId = mastery.Id;
    }

    /// <summary>
    /// Desvincula la maestría de armas de esta acción.
    /// </summary>
    public void RemoveMastery()
    {
        AssociatedMastery = null;
        AssociatedMasteryId = null;
    }
}
