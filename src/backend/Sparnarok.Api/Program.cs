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

// Soporte para credenciales locales no versionadas (appsettings.private.json)
builder.Configuration.AddJsonFile("appsettings.private.json", optional: true, reloadOnChange: true);

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
builder.Services.AddScoped<IArmorIngestionService, Sparnarok.Api.Infrastructure.Services.ArmorIngestionService>();

// 4.1 Cliente Open5e Bestiary e Ingesta de Monstruos (Fase 2.2)
builder.Services.AddHttpClient<IOpen5eBestiaryClient, Sparnarok.Api.Infrastructure.Services.Open5eBestiaryClient>(client =>
{
    client.BaseAddress = new Uri("https://api.open5e.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IOpen5eSyncService, Sparnarok.Api.Infrastructure.Services.Open5eSyncService>();

// 5. Motor de Simulación de Combate y RNG (Fase 2)
builder.Services.AddSingleton<Sparnarok.Api.Domain.Services.Rng.IRandomProvider, Sparnarok.Api.Domain.Services.Rng.DefaultRandomProvider>();
builder.Services.AddSingleton<Sparnarok.Api.Domain.Services.Dice.IDiceRoller, Sparnarok.Api.Domain.Services.Dice.DiceRoller>();
builder.Services.AddSingleton<IDprBaselineService, Sparnarok.Api.Infrastructure.Services.DprBaselineService>();
builder.Services.AddScoped<ICombatSimulatorService, Sparnarok.Api.Infrastructure.Services.CombatSimulatorService>();
builder.Services.AddScoped<IMontecarloSimulationService, Sparnarok.Api.Infrastructure.Services.MontecarloSimulationService>();

// 5.1 Identidad y Autenticación Federada (Google OAuth2 & JWT)
builder.Services.AddSingleton<IJwtTokenService, Sparnarok.Api.Infrastructure.Services.JwtTokenService>();
builder.Services.AddScoped<IGoogleAuthService, Sparnarok.Api.Infrastructure.Services.GoogleAuthService>();

var jwtKey = builder.Configuration["Authentication:Jwt:Key"]
    ?? Sparnarok.Api.Infrastructure.Services.JwtTokenService.DefaultDevKey;
var jwtIssuer = builder.Configuration["Authentication:Jwt:Issuer"]
    ?? Sparnarok.Api.Infrastructure.Services.JwtTokenService.DefaultIssuer;
var jwtAudience = builder.Configuration["Authentication:Jwt:Audience"]
    ?? Sparnarok.Api.Infrastructure.Services.JwtTokenService.DefaultAudience;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});

builder.Services.AddAuthorization();

// 6. Validadores de Aplicación (FluentValidation)
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

    // Configuración de Seguridad JWT Bearer para Swagger UI
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT en el formato: Bearer {tu_token}"
    };
    options.AddSecurityDefinition("Bearer", securityScheme);
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
