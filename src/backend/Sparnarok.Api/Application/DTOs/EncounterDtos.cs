using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud de creación de un nuevo Encuentro táctico.
/// </summary>
public record CreateEncounterRequestDto(
    string Name,
    string? DifficultyNotes,
    List<Guid> CreatureIds
);

/// <summary>
/// Solicitud de actualización de un Encuentro existente.
/// </summary>
public record UpdateEncounterRequestDto(
    string Name,
    string? DifficultyNotes,
    List<Guid> CreatureIds
);

/// <summary>
/// Representación detallada de un Encuentro y sus monstruos para respuesta de API.
/// </summary>
public record EncounterResponseDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    string? DifficultyNotes,
    DateTime CreatedAt,
    List<CreatureResponseDto> Creatures
)
{
    /// <summary>
    /// Mapea una entidad <see cref="Encounter"/> a su correspondiente DTO.
    /// </summary>
    public static EncounterResponseDto FromEntity(Encounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        return new EncounterResponseDto(
            encounter.Id,
            encounter.Name,
            encounter.OwnerUserId,
            encounter.DifficultyNotes,
            encounter.CreatedAt,
            encounter.Creatures.Select(CreatureResponseDto.FromEntity).ToList()
        );
    }
}

/// <summary>
/// Resumen ligero de un Encuentro para listas de selección.
/// </summary>
public record EncounterSummaryDto(
    Guid Id,
    string Name,
    string? DifficultyNotes,
    int CreatureCount,
    DateTime CreatedAt
)
{
    /// <summary>
    /// Mapea una entidad <see cref="Encounter"/> a su resumen ligero.
    /// </summary>
    public static EncounterSummaryDto FromEntity(Encounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        return new EncounterSummaryDto(
            encounter.Id,
            encounter.Name,
            encounter.DifficultyNotes,
            encounter.Creatures.Count,
            encounter.CreatedAt
        );
    }
}
