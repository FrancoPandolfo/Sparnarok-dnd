using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud de creación de una nueva Party de héroes.
/// </summary>
public record CreatePartyRequestDto(
    string Name,
    List<Guid> MemberCreatureIds
);

/// <summary>
/// Solicitud de actualización de una Party existente.
/// </summary>
public record UpdatePartyRequestDto(
    string Name,
    List<Guid> MemberCreatureIds
);

/// <summary>
/// Representación detallada de una Party y sus miembros para respuesta de API.
/// </summary>
public record PartyResponseDto(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    DateTime CreatedAt,
    List<CreatureResponseDto> Members
)
{
    /// <summary>
    /// Mapea una entidad <see cref="Party"/> a su correspondiente DTO.
    /// </summary>
    public static PartyResponseDto FromEntity(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new PartyResponseDto(
            party.Id,
            party.Name,
            party.OwnerUserId,
            party.CreatedAt,
            party.Members.Select(CreatureResponseDto.FromEntity).ToList()
        );
    }
}

/// <summary>
/// Resumen ligero de una Party para listas de selección.
/// </summary>
public record PartySummaryDto(
    Guid Id,
    string Name,
    int MemberCount,
    DateTime CreatedAt
)
{
    /// <summary>
    /// Mapea una entidad <see cref="Party"/> a su resumen ligero.
    /// </summary>
    public static PartySummaryDto FromEntity(Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new PartySummaryDto(
            party.Id,
            party.Name,
            party.Members.Count,
            party.CreatedAt
        );
    }
}
