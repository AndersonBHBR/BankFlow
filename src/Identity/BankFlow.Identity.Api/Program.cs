using System.Security.Claims;
using BankFlow.Identity.Api.Auth;
using BankFlow.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults()
    .AddBankFlowJwtAuthentication();

builder.Services.AddOpenApi("v1");
builder.Services
    .AddOptions<DemoUserOptions>()
    .Bind(builder.Configuration.GetSection(DemoUserOptions.SectionName))
    .Validate(options => options.Users.Count > 0, "Ao menos um usuário de demonstração deve ser configurado.")
    .Validate(options => options.Users.Select(user => user.Login).Distinct(StringComparer.OrdinalIgnoreCase).Count() == options.Users.Count,
        "Logins de demonstração não podem ser duplicados.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IUserCredentialStore, ConfigurationUserCredentialStore>();
builder.Services.AddSingleton<IPasswordVerifier, Pbkdf2PasswordVerifier>();
builder.Services.AddSingleton<ITokenService, TokenService>();

var app = builder.Build();

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapPost("/api/v1/auth/token", (
    LoginRequest request,
    IUserCredentialStore users,
    IPasswordVerifier passwordVerifier,
    ITokenService tokens) =>
{
    if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Credenciais incompletas",
            detail: "Login e senha são obrigatórios.");
    }

    var user = users.FindByLogin(request.Login);
    if (user is null || !passwordVerifier.Verify(request.Password, user.PasswordSalt, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    if (!user.IsActive)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    return Results.Ok(tokens.Issue(user));
})
.AllowAnonymous()
.WithName("IssueDemoToken")
.WithTags("Authentication");

app.MapGet("/api/v1/auth/me", (ClaimsPrincipal principal) => Results.Ok(new
{
    subject = principal.FindFirst("sub")?.Value,
    name = principal.Identity?.Name,
    roles = principal.FindAll("role").Select(claim => claim.Value).ToArray()
}))
.RequireAuthorization()
.WithName("GetCurrentUser")
.WithTags("Authentication");

app.Run();

public partial class Program
{
}
