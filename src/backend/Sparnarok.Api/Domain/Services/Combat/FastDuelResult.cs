namespace Sparnarok.Api.Domain.Services.Combat;

/// <summary>
/// Resultado optimizado y de baja asignación de memoria para una simulación rápida de duelo 1v1,
/// diseñado específicamente para iteraciones masivas de Monte Carlo.
/// </summary>
/// <param name="WinnerId">Identificador de la criatura vencedora (o null en caso de empate).</param>
/// <param name="Rounds">Cantidad de rondas que duró el enfrentamiento.</param>
/// <param name="DamageDealtA">Daño total infligido por el combatiente A.</param>
/// <param name="DamageDealtB">Daño total infligido por el combatiente B.</param>
public readonly record struct FastDuelResult(
    Guid? WinnerId,
    int Rounds,
    int DamageDealtA,
    int DamageDealtB);
