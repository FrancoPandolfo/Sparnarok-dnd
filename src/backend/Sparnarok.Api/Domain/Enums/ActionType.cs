namespace Sparnarok.Api.Domain.Enums;

/// <summary>
/// Economía de acciones y categorías de activación en D&amp;D 5e (2024).
/// </summary>
public enum ActionType
{
    /// <summary>Acción principal del turno.</summary>
    Action = 1,

    /// <summary>Acción adicional rápida (Bonus Action).</summary>
    BonusAction = 2,

    /// <summary>Reacción en respuesta a un desencadenante específico.</summary>
    Reaction = 3,

    /// <summary>Acción libre que no consume presupuesto de acción.</summary>
    FreeAction = 4,

    /// <summary>Acción legendaria ejecutada al final del turno de otra criatura.</summary>
    LegendaryAction = 5
}
