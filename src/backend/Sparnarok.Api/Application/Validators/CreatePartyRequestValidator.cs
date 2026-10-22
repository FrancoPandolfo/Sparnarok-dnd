using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas para la creación o actualización de una Party de héroes.
/// </summary>
public class CreatePartyRequestValidator : AbstractValidator<CreatePartyRequestDto>
{
    /// <summary>
    /// Configura las validaciones para la Party.
    /// </summary>
    public CreatePartyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la Party es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre de la Party no puede exceder los 100 caracteres.");

        RuleFor(x => x.MemberCreatureIds)
            .NotNull().WithMessage("La lista de integrantes no puede ser nula.");
    }
}
