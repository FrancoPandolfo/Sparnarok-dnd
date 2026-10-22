using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas para la creación o agregado de un objeto al alijo de la Party (Collaborative Party Stash).
/// </summary>
public class CreateStashItemRequestValidator : AbstractValidator<CreateStashItemRequest>
{
    /// <summary>
    /// Configura las validaciones para el objeto del alijo.
    /// </summary>
    public CreateStashItemRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del objeto es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre del objeto no puede exceder los 200 caracteres.");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(1).WithMessage("La cantidad de objetos debe ser al menos 1.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0m).WithMessage("El peso unitario no puede ser negativo.");

        RuleFor(x => x.Description)
            .MaximumLength(4000).WithMessage("La descripción no puede exceder los 4000 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Rarity)
            .MaximumLength(50).WithMessage("La rareza no puede exceder los 50 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Rarity));

        RuleFor(x => x.ItemType)
            .MaximumLength(100).WithMessage("El tipo de objeto no puede exceder los 100 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.ItemType));
    }
}
