using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas para dar de alta una acción de combate en una criatura.
/// </summary>
public class CreateActionRequestValidator : AbstractValidator<CreateActionRequestDto>
{
    /// <summary>
    /// Configura las validaciones para la acción o ataque táctico.
    /// </summary>
    public CreateActionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la acción es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre de la acción no puede exceder los 150 caracteres.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de acción no es válido.");

        RuleFor(x => x.ReachOrRangeFeet)
            .GreaterThanOrEqualTo(0).WithMessage("El alcance no puede ser negativo.");

        RuleFor(x => x.DamageFormula)
            .MaximumLength(50).WithMessage("La fórmula de daño no puede exceder 50 caracteres.");

        RuleFor(x => x.DamageType)
            .IsInEnum().When(x => x.DamageType.HasValue)
            .WithMessage("El tipo de daño no es válido.");

        RuleFor(x => x.AttackModifierAbility)
            .IsInEnum().When(x => x.AttackModifierAbility.HasValue)
            .WithMessage("El atributo de ataque no es válido.");
    }
}
