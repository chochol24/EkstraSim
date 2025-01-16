using EkstraSim.Frontend.Components;
using EkstraSim.Frontend.Components.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped(sp => new HttpClient
{
	BaseAddress = new Uri("https://localhost:7050/") 
});


builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddScoped<TeamService>();


var app = builder.Build();

app.UseRouting();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
