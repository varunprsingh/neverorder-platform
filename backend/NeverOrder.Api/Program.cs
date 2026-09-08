using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NeverOrder.Api.Authentication;
using NeverOrder.Api.Diagnostics;
using NeverOrder.Api.Filters;
using NeverOrder.Api.Middleware;
using NeverOrder.Api.RateLimiting;
using NeverOrder.Api.RealTime;
using NeverOrder.Application;
using NeverOrder.Application.Abstractions;
using NeverOrder.Infrastructure;
using NeverOrder.Infrastructure.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "NeverOrderFrontend";

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options => options.Filters.Add<FluentValidationFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<CorrelationIdMiddleware>();

builder.Services.AddSingleton<IOrderNotifier, SignalROrderNotifier>();

builder.Services.AddNeverOrderRateLimiting(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey must be configured outside Development. Supply it via environment variables.");
    }

    // Keeps first-run friction low without committing a secret; tokens simply do not
    // survive a restart until a real key is set via user-secrets.
    jwt.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine("[NeverOrder] No Jwt:SigningKey configured. Generated an ephemeral development key.");
}

builder.Services.PostConfigure<JwtOptions>(options => options.SigningKey = jwt.SigningKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claim types stay exactly as issued rather than being rewritten to the legacy XML URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtOptions.NameClaimType,
            RoleClaimType = JwtOptions.RoleClaimType
        };

        // A browser cannot set headers on a WebSocket handshake, so SignalR passes the token in the
        // query string. Accepted only for the hub path, never for the REST surface.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments(OrderHub.Path))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options => options.AddPolicy(
    CorsPolicy,
    // AllowCredentials is what lets a browser open the hub; it forbids a wildcard origin, so the
    // allow-list above stops being optional.
    policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var signalR = builder.Services.AddSignalR();

var cacheRedis = builder.Configuration["Cache:Redis"];
if (!string.IsNullOrWhiteSpace(cacheRedis))
{
    // Without a backplane a notification only reaches clients connected to the instance that raised
    // it, which is wrong the moment there is more than one.
    signalR.AddStackExchangeRedis(cacheRedis, options => options.Configuration.ChannelPrefix =
        StackExchange.Redis.RedisChannel.Literal("neverorder:signalr"));
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NeverOrder API",
        Version = "v1",
        Description = "Virtual commerce. Shop, order and track — but never pay. No real transaction occurs."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// Ahead of request logging so the completion event carries the correlation id too.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

// After authentication so a signed-in caller is limited per account rather than per address.
app.UseRateLimiter();

app.MapControllers();
app.MapHealthEndpoints();

// A hub connection is long-lived and carries no per-request cost worth metering.
app.MapHub<OrderHub>(OrderHub.Path).DisableRateLimiting();

app.Run();

/// <summary>Exposed so the integration test host can reference the entry-point assembly.</summary>
public partial class Program;

