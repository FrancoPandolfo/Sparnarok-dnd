namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de usuario persistida tras la autenticación federada con Google OAuth2.
/// </summary>
public class ApplicationUser
{
    /// <summary>Identificador único del usuario en el sistema Sparnarok.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador 'sub' (Subject) emitido por el proveedor Google OAuth2.</summary>
    public string GoogleSubjectId { get; private set; } = string.Empty;

    /// <summary>Correo electrónico verificado del usuario.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Nombre completo o de pantalla del usuario.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>URL del avatar o foto de perfil provista por Google (opcional).</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>Fecha y hora en UTC del registro inicial del usuario.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Fecha y hora en UTC del último inicio de sesión exitoso.</summary>
    public DateTime LastLoginAtUtc { get; private set; }

    /// <summary>Colección de héroes o criaturas personalizadas pertenecientes a este usuario.</summary>
    public virtual ICollection<Creature> CustomHeroes { get; private set; } = new List<Creature>();

    /// <summary>Constructor protegido requerido por Entity Framework Core.</summary>
    protected ApplicationUser() { }

    /// <summary>
    /// Crea una nueva instancia de <see cref="ApplicationUser"/> derivada de una autenticación válida de Google.
    /// </summary>
    /// <param name="googleSubjectId">Subject ID único de Google.</param>
    /// <param name="email">Email del usuario.</param>
    /// <param name="displayName">Nombre a mostrar.</param>
    /// <param name="avatarUrl">URL de foto de perfil (opcional).</param>
    /// <param name="id">Identificador opcional (por defecto genera uno nuevo).</param>
    /// <returns>Nueva entidad de usuario inicializada.</returns>
    public static ApplicationUser CreateFromGoogle(
        string googleSubjectId,
        string email,
        string displayName,
        string? avatarUrl = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(googleSubjectId))
            throw new ArgumentException("El Google Subject Id no puede estar vacío.", nameof(googleSubjectId));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email no puede estar vacío.", nameof(email));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("El nombre del usuario no puede estar vacío.", nameof(displayName));

        var now = DateTime.UtcNow;
        return new ApplicationUser
        {
            Id = id ?? Guid.NewGuid(),
            GoogleSubjectId = googleSubjectId.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
            CreatedAtUtc = now,
            LastLoginAtUtc = now
        };
    }

    /// <summary>
    /// Actualiza los metadatos del usuario en inicios de sesión posteriores.
    /// </summary>
    /// <param name="displayName">Nombre actualizado.</param>
    /// <param name="avatarUrl">Avatar actualizado.</param>
    public void UpdateFromGoogle(string displayName, string? avatarUrl)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarUrl = avatarUrl.Trim();
        }

        LastLoginAtUtc = DateTime.UtcNow;
    }
}
