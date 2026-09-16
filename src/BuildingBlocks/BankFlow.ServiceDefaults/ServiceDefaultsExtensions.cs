using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BankFlow.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static WebApplicationBuilder AddServiceDefaults(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 1_048_576;
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            options.UseUtcTimestamp = true;
        });

        builder.Services.AddProblemDetails();
        builder.Services
            .AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: builder.Environment.ApplicationName,
                    serviceNamespace: "bankflow")
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
                ]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health/live"))
                .AddHttpClientInstrumentation()
                .AddSource("BankFlow.Transfers.Messaging", "BankFlow.Accounts.Messaging")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("BankFlow.Transfers.Messaging", "BankFlow.Accounts.Messaging")
                .AddOtlpExporter());

        return builder;
    }

    public static WebApplicationBuilder AddBankFlowJwtAuthentication(this WebApplicationBuilder builder)
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);

        builder.Services
            .AddOptions<JwtOptions>()
            .Bind(jwtSection)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer é obrigatório.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience é obrigatório.")
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt:SigningKey deve possuir ao menos 32 bytes.")
            .Validate(options => options.ExpirationMinutes is >= 5 and <= 120, "Jwt:ExpirationMinutes deve ficar entre 5 e 120.")
            .ValidateOnStart();

        var jwt = jwtSection.Get<JwtOptions>() ?? throw new InvalidOperationException("Configuração JWT ausente.");

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(SecurityPolicies.Authenticated, policy =>
                policy.RequireAuthenticatedUser())
            .AddPolicy(SecurityPolicies.Transfers, policy =>
                policy.RequireAuthenticatedUser().RequireRole(SecurityRoles.TransfersUser, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.Accounts, policy =>
                policy.RequireAuthenticatedUser().RequireRole(SecurityRoles.AccountsManager, SecurityRoles.Admin))
            .AddPolicy(SecurityPolicies.Administrator, policy =>
                policy.RequireAuthenticatedUser().RequireRole(SecurityRoles.Admin));

        return builder;
    }

    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true
        });
        return app;
    }
}
