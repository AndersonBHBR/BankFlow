using System.Text.Json.Serialization;
using BankFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Transfers.Api.Endpoints;
using Transfers.Infrastructure;
using Transfers.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddServiceDefaults()
    .AddBankFlowJwtAuthentication();

builder.Services.AddOpenApi("v1");
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransfersInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var factory = app.Services.GetRequiredService<IDbContextFactory<TransfersDbContext>>();
    await using var dbContext = await factory.CreateDbContextAsync();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapTransferEndpoints();

app.Run();

public partial class Program
{
}
