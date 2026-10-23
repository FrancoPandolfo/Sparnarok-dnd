using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Application.DTOs;

/// <summary>
/// Solicitud de creación de una nueva Party de héroes.
/// </summary>
public record CreatePartyRequestDto(
    string Name,
    List<Guid>? MemberCreatureIds = null,
    string? Role = "DungeonMaster",
    string? InGameName = null
);

/// <summary>
/// Solicitud para unirse a una Party existente mediante enlace o token de invitación (Fase 3.7).
/// </summary>
public record JoinPartyRequestDto(
    string Role,
    string InGameName
);

/// <summary>
/// Información de la invitación a una Party para la pantalla previa de onboarding (Fase 3.7).
/// </summary>
public record PartyInviteDetailsDto(
    Guid PartyId,
    string PartyName,
    string InviteToken,
    int MemberCount
);

/// <summary>
/// Representación DTO de un miembro registrado en la Party con su rol y nombre contextual (Fase 3.7).
/// </summary>
public record PartyMemberDto(
    Guid Id,
    Guid PartyId,
    Guid UserId,
    string Role,
    string InGameName,
    DateTime JoinedAt
)
{
    /// <summary>
    /// Mapea una entidad <see cref="PartyMember"/> a su DTO de respuesta.
    /// </summary>
    public static PartyMemberDto FromEntity(PartyMember member) =>
        new(
            member.Id,
            member.PartyId,
            member.UserId,
            member.Role,
            member.InGameName,
            member.JoinedAt
        );
}

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
    string InviteToken,
    DateTime CreatedAt,
    List<CreatureResponseDto> Members,
    List<PartyMemberDto> PartyMembers
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
            party.InviteToken,
            party.CreatedAt,
            party.Members.Select(CreatureResponseDto.FromEntity).ToList(),
            party.PartyMembers.Select(PartyMemberDto.FromEntity).ToList()
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
