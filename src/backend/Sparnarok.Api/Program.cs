using System.Reflection;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Application.Validators;
using Sparnarok.Api.Infrastructure.Persistence;
using Sparnarok.Api.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Internacionalización (i18n)
builder.Services.AddLocalization();

// 2. Persistencia relacional en PostgreSQL con EF Core
builder.Services.AddDbContext<SparnarokDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=sparnarok_dev_db;Username=sparnarok;Password=sparnarok_secret";
    options.UseNpgsql(connectionString);
});

// 3. Repositorios de Dominio
builder.Services.AddScoped<ICreatureRepository, CreatureRepository>();
builder.Services.AddScoped<IWeaponMasteryRepository, WeaponMasteryRepository>();

// 4. Servicios de Ingestión de Datos (CSV & Streaming)
builder.Services.AddScoped<ICsvParserService, Sparnarok.Api.Infrastructure.Services.CsvParserService>();
builder.Services.AddScoped<IWeaponIngestionService, Sparnarok.Api.Infrastructure.Services.WeaponIngestionService>();

// 5. Validadores de Aplicación (FluentValidation)
builder.Services.AddValidatorsFromAssemblyContaining<CreateCreatureRequestValidator>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 2. Configuración de Swagger / OpenAPI con Comentarios XML
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sparnarok: The Architect's Codex API",
        Version = "v1",
        Description = "Motor de simulación matemática y orquestación de mundo para D&D 5e."
    });

    // Inclusión de comentarios XML para documentación exhaustiva en Swagger
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// 3. Pipeline de Localización
var supportedCultures = new[] { "es", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("es")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// 4. Configuración del Entorno de Desarrollo y Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sparnarok API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
