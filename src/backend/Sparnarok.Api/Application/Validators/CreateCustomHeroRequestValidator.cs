using FluentValidation;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.ValueObjects;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas de negocio para <see cref="CreateCustomHeroRequestDto"/>.
/// </summary>
public class CreateCustomHeroRequestValidator : AbstractValidator<CreateCustomHeroRequestDto>
{
    public CreateCustomHeroRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del héroe es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder los 150 caracteres.");

        RuleFor(x => x.MaxHitPoints)
            .GreaterThan(0).WithMessage("Los puntos de golpe deben ser mayores a 0.")
            .LessThanOrEqualTo(1000).WithMessage("Los puntos de golpe no pueden exceder 1,000.");

        RuleFor(x => x.ArmorClass)
            .GreaterThan(0).WithMessage("La Clase de Armadura debe ser mayor a 0.")
            .LessThanOrEqualTo(40).WithMessage("La Clase de Armadura no puede exceder 40.");

        RuleFor(x => x.Strength)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Fuerza debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Dexterity)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Destreza debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Constitution)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Constitución debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Intelligence)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Inteligencia debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Wisdom)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Sabiduría debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Charisma)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"El Carisma debe encontrarse entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.SpeedFeet)
            .GreaterThanOrEqualTo(0).WithMessage("La velocidad no puede ser negativa.");

        RuleFor(x => x.Level)
            .InclusiveBetween(1, 20).WithMessage("El nivel del héroe debe encontrarse entre 1 y 20.");

        RuleFor(x => x.ExpectedDpr)
            .GreaterThanOrEqualTo(0).When(x => x.ExpectedDpr.HasValue)
            .WithMessage("El DPR esperado debe ser mayor o igual a 0.");

        RuleFor(x => x.AttackName)
            .MaximumLength(100).WithMessage("El nombre del ataque no puede exceder los 100 caracteres.");

        RuleFor(x => x.DamageFormula)
            .MaximumLength(50).WithMessage("La fórmula de daño no puede exceder los 50 caracteres.");

        RuleFor(x => x.AttackModifierAbility)
            .IsInEnum().WithMessage("La característica de modificador de ataque no es válida.");
    }
}

