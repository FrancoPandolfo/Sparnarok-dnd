namespace Sparnarok.Api.Domain.Entities;

/// <summary>
/// Entidad de usuario persistida con soporte para autenticación federada (Google OAuth2)
/// y autenticación convencional con contraseña, con vinculación bidireccional.
/// </summary>
public class ApplicationUser
{
    /// <summary>Identificador único del usuario en el sistema Sparnarok.</summary>
    public Guid Id { get; private set; }

    /// <summary>Identificador 'sub' (Subject) emitido por el proveedor Google OAuth2 (nulo si solo usa contraseña).</summary>
    public string? GoogleSubjectId { get; private set; }

    /// <summary>Correo electrónico verificado del usuario.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Nombre completo o de pantalla del usuario.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Apodo o alias global del usuario en el ecosistema Sparnarok (Fase 3.7).</summary>
    public string? GlobalNickname { get; private set; }

    /// <summary>Hash criptográfico de la contraseña para inicio de sesión convencional (nulo si solo usa Google).</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>URL del avatar o foto de perfil del usuario (opcional).</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>Fecha y hora en UTC del registro inicial del usuario.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Fecha y hora en UTC del último inicio de sesión exitoso.</summary>
    public DateTime LastLoginAtUtc { get; private set; }

    /// <summary>Indica si el usuario tiene una contraseña convencional establecida.</summary>
    public bool HasPassword => !string.IsNullOrWhiteSpace(PasswordHash);

    /// <summary>Indica si el usuario tiene su cuenta de Google vinculada.</summary>
    public bool HasGoogleLinked => !string.IsNullOrWhiteSpace(GoogleSubjectId);

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
    /// Crea una nueva instancia de <see cref="ApplicationUser"/> mediante registro convencional con email y contraseña.
    /// </summary>
    /// <param name="email">Email del usuario.</param>
    /// <param name="passwordHash">Hash criptográfico de la contraseña.</param>
    /// <param name="displayName">Nombre a mostrar.</param>
    /// <param name="id">Identificador opcional (por defecto genera uno nuevo).</param>
    /// <returns>Nueva entidad de usuario inicializada.</returns>
    public static ApplicationUser CreateConventional(
        string email,
        string passwordHash,
        string displayName,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email no puede estar vacío.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("El hash de contraseña no puede estar vacío.", nameof(passwordHash));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("El nombre del usuario no puede estar vacío.", nameof(displayName));

        var now = DateTime.UtcNow;
        return new ApplicationUser
        {
            Id = id ?? Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            CreatedAtUtc = now,
            LastLoginAtUtc = now
        };
    }

    /// <summary>
    /// Vincula una cuenta de Google a un usuario existente.
    /// </summary>
    /// <param name="googleSubjectId">Subject ID único de Google.</param>
    /// <param name="avatarUrl">URL del avatar (opcional).</param>
    public void LinkGoogle(string googleSubjectId, string? avatarUrl = null)
    {
        if (string.IsNullOrWhiteSpace(googleSubjectId))
            throw new ArgumentException("El Google Subject Id no puede estar vacío.", nameof(googleSubjectId));

        GoogleSubjectId = googleSubjectId.Trim();
        if (!string.IsNullOrWhiteSpace(avatarUrl) && string.IsNullOrWhiteSpace(AvatarUrl))
        {
            AvatarUrl = avatarUrl.Trim();
        }
        LastLoginAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Establece o actualiza la contraseña convencional para el usuario.
    /// </summary>
    /// <param name="passwordHash">Hash criptográfico de la contraseña.</param>
    public void SetPassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("El hash de contraseña no puede estar vacío.", nameof(passwordHash));

        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Actualiza los metadatos del usuario en inicios de sesión posteriores de Google.
    /// </summary>
    /// <param name="displayName">Nombre actualizado.</param>
    /// <param name="avatarUrl">Avatar actualizado.</param>
    /// <param name="googleSubjectId">Subject ID opcional para autovinculación.</param>
    public void UpdateFromGoogle(string displayName, string? avatarUrl, string? googleSubjectId = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarUrl = avatarUrl.Trim();
        }

        if (string.IsNullOrWhiteSpace(GoogleSubjectId) && !string.IsNullOrWhiteSpace(googleSubjectId))
        {
            GoogleSubjectId = googleSubjectId.Trim();
        }

        LastLoginAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Establece o actualiza el apodo global del usuario (Fase 3.7).
    /// </summary>
    /// <param name="nickname">Nuevo apodo o null para desvincular.</param>
    public void SetGlobalNickname(string? nickname)
    {
        GlobalNickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
    }

    /// <summary>
    /// Registra un nuevo inicio de sesión exitoso.
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }
}
