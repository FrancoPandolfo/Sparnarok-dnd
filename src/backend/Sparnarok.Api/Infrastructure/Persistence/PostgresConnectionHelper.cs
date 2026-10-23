using Npgsql;

namespace Sparnarok.Api.Infrastructure.Persistence;

/// <summary>
/// Provee utilidades para la normalización e interpretación de cadenas de conexión de PostgreSQL,
/// soportando tanto el formato nativo ADO.NET Npgsql (Host=...;Database=...) como el formato estándar
/// URI postgresql:// o postgres:// utilizado por plataformas serverless y cloud como Neon.tech y Render.
/// </summary>
public static class PostgresConnectionHelper
{
    /// <summary>
    /// Normaliza una cadena de conexión o URI de PostgreSQL al formato estándar de pares clave-valor
    /// compatible con Npgsql y Entity Framework Core.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión o URI (ej. provista por Neon.tech o Render).</param>
    /// <returns>Cadena de conexión normalizada en formato ADO.NET.</returns>
    public static string NormalizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        var trimmed = connectionString.Trim();

        // Si ya está en formato clave=valor estándar de ADO.NET (ej. Host=localhost;...)
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        try
        {
            var uri = new Uri(trimmed);
            var userInfo = uri.UserInfo.Split(':', 2);
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
            var database = uri.AbsolutePath.TrimStart('/');

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = string.IsNullOrEmpty(database) ? "postgres" : database,
                Username = username,
                Password = password,
                SslMode = SslMode.Require
            };

            return builder.ConnectionString;
        }
        catch
        {
            // En caso de que la URI no cumpla el estándar exacto de System.Uri, se retorna tal cual
            return trimmed;
        }
    }
}
