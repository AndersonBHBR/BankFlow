using System.Text.Json.Serialization;
using BankFlow.ServiceDefaults;
using Accounts.Api.Endpoints;
using Accounts.Infrastructure;
using Accounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults()
    .AddBankFlowJwtAuthentication();

builder.Services.AddOpenApi("v1");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddAccountsInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<AccountsDbContext>>();
    await using var dbContext = await factory.CreateDbContextAsync();
    await dbContext.Database.EnsureCreatedAsync();
    await DemoDataSeeder.SeedAsync(dbContext);
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapAccountEndpoints();

app.Run();

public partial class Program
{
}
