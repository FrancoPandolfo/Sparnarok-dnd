using FluentValidation;
using Sparnarok.Api.Application.DTOs;
using Sparnarok.Api.Domain.ValueObjects;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas de negocio para <see cref="CreateCreatureRequestDto"/>.
/// </summary>
public class CreateCreatureRequestValidator : AbstractValidator<CreateCreatureRequestDto>
{
    /// <summary>
    /// Configura las restricciones canónicas para dar de alta una criatura en D&amp;D 5e (2024).
    /// </summary>
    public CreateCreatureRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la criatura es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder los 150 caracteres.");

        RuleFor(x => x.Size)
            .IsInEnum().WithMessage("El tamaño especificado no es válido.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de criatura no es válido.");

        RuleFor(x => x.Alignment)
            .NotEmpty().WithMessage("El alineamiento es obligatorio.")
            .MaximumLength(100).WithMessage("El alineamiento no puede exceder los 100 caracteres.");

        RuleFor(x => x.ChallengeRating)
            .InclusiveBetween(0, 30).WithMessage("El Challenge Rating debe encontrarse entre 0 y 30.");

        RuleFor(x => x.SpeedFeet)
            .GreaterThanOrEqualTo(0).WithMessage("La velocidad no puede ser negativa.");

        RuleFor(x => x.Strength)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Fuerza debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Dexterity)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Destreza debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Constitution)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Constitución debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Intelligence)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Inteligencia debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Wisdom)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Sabiduría debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Charisma)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"El Carisma debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.MaxHitPoints)
            .GreaterThan(0).WithMessage("Los puntos de golpe máximos deben ser mayores a cero.");

        RuleFor(x => x.ArmorClassBase)
            .GreaterThan(0).WithMessage("La Clase de Armadura base debe ser mayor a cero.");

        RuleFor(x => x.ShieldBonus)
            .GreaterThanOrEqualTo(0).WithMessage("El bono de escudo no puede ser negativo.");
    }
}

/// <summary>
/// Validador de reglas de negocio para <see cref="UpdateCreatureRequestDto"/>.
/// </summary>
public class UpdateCreatureRequestValidator : AbstractValidator<UpdateCreatureRequestDto>
{
    /// <summary>
    /// Configura las restricciones canónicas para actualizar una criatura.
    /// </summary>
    public UpdateCreatureRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la criatura es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede exceder los 150 caracteres.");

        RuleFor(x => x.Size)
            .IsInEnum().WithMessage("El tamaño especificado no es válido.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("El tipo de criatura no es válido.");

        RuleFor(x => x.Alignment)
            .NotEmpty().WithMessage("El alineamiento es obligatorio.")
            .MaximumLength(100).WithMessage("El alineamiento no puede exceder los 100 caracteres.");

        RuleFor(x => x.ChallengeRating)
            .InclusiveBetween(0, 30).WithMessage("El Challenge Rating debe encontrarse entre 0 y 30.");

        RuleFor(x => x.SpeedFeet)
            .GreaterThanOrEqualTo(0).WithMessage("La velocidad no puede ser negativa.");

        RuleFor(x => x.Strength)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Fuerza debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Dexterity)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Destreza debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Constitution)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Constitución debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Intelligence)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Inteligencia debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Wisdom)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"La Sabiduría debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.Charisma)
            .InclusiveBetween(AbilityScores.MinScore, AbilityScores.MaxScore)
            .WithMessage($"El Carisma debe estar entre {AbilityScores.MinScore} y {AbilityScores.MaxScore}.");

        RuleFor(x => x.MaxHitPoints)
            .GreaterThan(0).WithMessage("Los puntos de golpe máximos deben ser mayores a cero.");

        RuleFor(x => x.ArmorClassBase)
            .GreaterThan(0).WithMessage("La Clase de Armadura base debe ser mayor a cero.");

        RuleFor(x => x.ShieldBonus)
            .GreaterThanOrEqualTo(0).WithMessage("El bono de escudo no puede ser negativo.");
    }
}
