using EkstraSim.Frontend.Components;
using EkstraSim.Frontend.Components.Services;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomLeft;

    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://jw-ekstrasim-api-gaaqaxa6azhjcke3.polandcentral-01.azurewebsites.net")
});

builder.Services.AddScoped<HttpServiceHelper>();

builder.Services.AddScoped<SeasonService>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddScoped<UpdateDatabaseService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<LeagueService>();


var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
