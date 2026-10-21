using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Sparnarok.IntegrationTests;

public class CodexLocalizationAndSwaggerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CodexLocalizationAndSwaggerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsOpenApiSpecificationWithXmlComments()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Sparnarok: The Architect's Codex API");
        content.Should().Contain("CodexStatus");
    }

    [Fact]
    public async Task CodexStatus_WithDefaultCulture_ReturnsSpanishLocalizedMessage()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/codexstatus");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        root.GetProperty("status").GetString().Should().Be("Operational");
        root.GetProperty("systemMessage").GetString().Should().Be("El Códice de Sparnarok está inicializado y operando con precisión matemática.");
        root.GetProperty("simulationEngine").GetString().Should().Be("Motor de simulación D&D 5e activo.");
    }

    [Fact]
    public async Task CodexStatus_WithEnglishCultureHeader_ReturnsEnglishLocalizedMessage()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/codexstatus");
        request.Headers.AcceptLanguage.ParseAdd("en");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        root.GetProperty("status").GetString().Should().Be("Operational");
        root.GetProperty("systemMessage").GetString().Should().Be("The Sparnarok Codex is initialized and operating with mathematical precision.");
        root.GetProperty("simulationEngine").GetString().Should().Be("D&D 5e simulation engine active.");
    }
}
