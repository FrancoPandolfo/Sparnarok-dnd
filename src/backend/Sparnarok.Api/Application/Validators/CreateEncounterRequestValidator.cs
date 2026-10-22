using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas para la creación o actualización de un Encuentro táctico.
/// </summary>
public class CreateEncounterRequestValidator : AbstractValidator<CreateEncounterRequestDto>
{
    /// <summary>
    /// Configura las validaciones para el Encuentro.
    /// </summary>
    public CreateEncounterRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del Encuentro es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre del Encuentro no puede exceder los 100 caracteres.");

        RuleFor(x => x.DifficultyNotes)
            .MaximumLength(500).When(x => x.DifficultyNotes != null)
            .WithMessage("Las notas de dificultad no pueden exceder los 500 caracteres.");

        RuleFor(x => x.CreatureIds)
            .NotNull().WithMessage("La lista de criaturas no puede ser nula.");
    }
}
