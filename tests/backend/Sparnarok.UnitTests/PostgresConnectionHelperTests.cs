using FluentAssertions;
using Sparnarok.Api.Infrastructure.Persistence;
using Xunit;

namespace Sparnarok.UnitTests;

/// <summary>
/// Pruebas unitarias para la normalización de cadenas de conexión de PostgreSQL (PostgresConnectionHelper).
/// Valida soporte para URIs de Neon.tech (postgresql:// / postgres://), codificación de contraseñas y passthrough ADO.NET.
/// </summary>
public class PostgresConnectionHelperTests
{
    [Fact]
    public void NormalizeConnectionString_WithNeonUri_ParsesCorrectlyToAdoNet()
    {
        // Arrange
        var neonUri = "postgresql://neondb_owner:secretpass123@ep-cool-fog-123456.us-east-2.aws.neon.tech/neondb?sslmode=require";

        // Act
        var result = PostgresConnectionHelper.NormalizeConnectionString(neonUri);

        // Assert
        result.Should().Contain("Host=ep-cool-fog-123456.us-east-2.aws.neon.tech");
        result.Should().Contain("Database=neondb");
        result.Should().Contain("Username=neondb_owner");
        result.Should().Contain("Password=secretpass123");
        result.Should().Contain("SSL Mode=Require");
    }

    [Fact]
    public void NormalizeConnectionString_WithPostgresSchemeAndSpecialCharacters_UnescapesPassword()
    {
        // Arrange
        var uriWithEncodedPass = "postgres://admin:p%40ssw%3Ard%21@localhost:5433/custom_db";

        // Act
        var result = PostgresConnectionHelper.NormalizeConnectionString(uriWithEncodedPass);

        // Assert
        result.Should().Contain("Host=localhost");
        result.Should().Contain("Port=5433");
        result.Should().Contain("Database=custom_db");
        result.Should().Contain("Username=admin");
        result.Should().Contain("Password=p@ssw:rd!");
    }

    [Fact]
    public void NormalizeConnectionString_WithStandardAdoNetFormat_ReturnsOriginalTrimmedString()
    {
        // Arrange
        var standardAdoNet = "Host=myhost;Database=mydb;Username=myuser;Password=mypass;SSL Mode=Require;";

        // Act
        var result = PostgresConnectionHelper.NormalizeConnectionString(standardAdoNet);

        // Assert
        result.Should().Be(standardAdoNet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeConnectionString_WithNullOrWhitespace_ReturnsEmptyString(string? input)
    {
        // Act
        var result = PostgresConnectionHelper.NormalizeConnectionString(input);

        // Assert
        result.Should().BeEmpty();
    }
}
