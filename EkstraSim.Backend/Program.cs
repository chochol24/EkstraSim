using EkstraSim.Backend.Database;
using EkstraSim.Backend.Database.Services;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApiDocument();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ILeagueService, LeagueService>();
builder.Services.AddScoped<ISimulatingService, SimulatingService>();

builder.Services.AddFastEndpoints();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

builder.Services.AddDbContext<EkstraSimDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseOpenApi();
app.UseSwagger();
app.UseSwaggerUi();
app.UseCors("AllowAllOrigins");
app.UseHttpsRedirection();

app.UseFastEndpoints(c =>
{
	c.Versioning.Prefix = "v";
	c.Versioning.DefaultVersion = 1;
	c.Versioning.PrependToRoute = true;
});

app.Run();