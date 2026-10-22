using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Validador de reglas para encolar un objeto en la cola de fabricación (Downtime Crafting).
/// </summary>
public class QueueCraftingItemRequestValidator : AbstractValidator<QueueCraftingItemRequest>
{
    public QueueCraftingItemRequestValidator()
    {
        RuleFor(x => x.ItemName)
            .NotEmpty().WithMessage("El nombre del objeto a fabricar es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre del objeto no puede exceder los 200 caracteres.");

        RuleFor(x => x.DaysRequired)
            .GreaterThanOrEqualTo(1).WithMessage("Los días de downtime requeridos deben ser al menos 1.");

        RuleFor(x => x.GoldCost)
            .GreaterThanOrEqualTo(0m).WithMessage("El costo en oro no puede ser negativo.");

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0m).WithMessage("El peso estimado no puede ser negativo.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("La descripción no puede exceder los 2000 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Description));

        RuleFor(x => x.Rarity)
            .MaximumLength(50).WithMessage("La rareza no puede exceder los 50 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Rarity));

        RuleFor(x => x.ItemType)
            .MaximumLength(100).WithMessage("El tipo de objeto no puede exceder los 100 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.ItemType));

        RuleFor(x => x.CrafterName)
            .MaximumLength(150).WithMessage("El nombre del artesano no puede exceder los 150 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.CrafterName));
    }
}

/// <summary>
/// Validador para la actualización de fondos del Party Fund.
/// </summary>
public class UpdatePartyFundsRequestValidator : AbstractValidator<UpdatePartyFundsRequest>
{
    public UpdatePartyFundsRequestValidator()
    {
        RuleFor(x => x.CopperPieces)
            .GreaterThanOrEqualTo(0m).WithMessage("Las piezas de cobre no pueden ser negativas.");

        RuleFor(x => x.SilverPieces)
            .GreaterThanOrEqualTo(0m).WithMessage("Las piezas de plata no pueden ser negativas.");

        RuleFor(x => x.ElectrumPieces)
            .GreaterThanOrEqualTo(0m).WithMessage("Las piezas de electro no pueden ser negativas.");

        RuleFor(x => x.GoldPieces)
            .GreaterThanOrEqualTo(0m).WithMessage("Las piezas de oro no pueden ser negativas.");

        RuleFor(x => x.PlatinumPieces)
            .GreaterThanOrEqualTo(0m).WithMessage("Las piezas de platino no pueden ser negativas.");
    }
}
