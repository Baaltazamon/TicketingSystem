using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SupportPilot.Application;
using SupportPilot.Application.Abstractions;
using SupportPilot.Api.Auth;
using SupportPilot.Api.Endpoints;
using SupportPilot.Api.Realtime;
using SupportPilot.Infrastructure;
using SupportPilot.Infrastructure.Auth;
using SupportPilot.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<ITicketRealtimeNotifier, SignalRTicketRealtimeNotifier>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT settings are missing.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/tickets"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SupportStaff", policy => policy.RequireRole("Admin", "Agent"));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SupportPilot API",
        Version = "v1",
        Description = "Support ticketing API with SLA tracking, comments, attachments, notifications and knowledge base workflows."
    });

    foreach (var xmlFile in new[]
    {
        "SupportPilot.Api.xml",
        "SupportPilot.Contracts.xml",
        "SupportPilot.Domain.xml",
        "SupportPilot.Application.xml"
    })
    {
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseStatusCodePages(async statusCodeContext =>
{
    var httpContext = statusCodeContext.HttpContext;
    var statusCode = httpContext.Response.StatusCode;
    await Results.Problem(
            statusCode: statusCode,
            title: GetProblemTitle(statusCode),
            detail: GetProblemTitle(statusCode),
            type: $"https://httpstatuses.com/{statusCode}")
        .ExecuteAsync(httpContext);
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "SupportPilot API",
    status = "running",
    docs = "/swagger"
}));

app.MapAuthEndpoints();
app.MapTicketEndpoints();
app.MapAdminEndpoints();
app.MapKnowledgeBaseEndpoints();
app.MapReportEndpoints();
app.MapNotificationEndpoints();
app.MapHub<TicketHub>("/hubs/tickets");
app.MapHealthChecks("/api/health");
app.MapHealthChecks("/api/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/api/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

static string GetProblemTitle(int statusCode) =>
    statusCode switch
    {
        StatusCodes.Status400BadRequest => "Validation failed",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Resource not found",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "Request failed"
    };

app.Run();

/// <summary>
/// API entry point type exposed for integration tests and WebApplicationFactory.
/// </summary>
public partial class Program;
