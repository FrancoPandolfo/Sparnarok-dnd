using FluentValidation;
using Sparnarok.Api.Application.DTOs;

namespace Sparnarok.Api.Application.Validators;

/// <summary>
/// Reglas de validación para la creación de una nota de sesión.
/// </summary>
public class CreateSessionNoteRequestValidator : AbstractValidator<CreateSessionNoteRequest>
{
    public CreateSessionNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título de la nota no puede estar vacío.")
            .MaximumLength(200).WithMessage("El título no puede superar los 200 caracteres.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("El contenido de la nota no puede ser nulo.");
    }
}

/// <summary>
/// Reglas de validación para la actualización de una nota de sesión.
/// </summary>
public class UpdateSessionNoteRequestValidator : AbstractValidator<UpdateSessionNoteRequest>
{
    public UpdateSessionNoteRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título de la nota no puede estar vacío.")
            .MaximumLength(200).WithMessage("El título no puede superar los 200 caracteres.");

        RuleFor(x => x.Content)
            .NotNull().WithMessage("El contenido de la nota no puede ser nulo.");
    }
}
