using System.Net.WebSockets;
using System.Text;
using GameServer;
using GameServer.Handlers;
using GameServer.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharedLibrary.Requests;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration.GetSection("Settings").Get<Settings>();
if (settings == null)
{
    throw new InvalidOperationException("Settings section is missing or invalid in configuration.");
}
builder.Services.AddSingleton(settings);

// Add services to the container.
builder.Services.AddDbContext<GameDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Db"))
);

builder
    .Services.AddControllers()
    .AddNewtonsoftJson(o =>
    {
        o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        o.SerializerSettings.ContractResolver = new DefaultContractResolver(); // Explicitly use default resolver
        // Using the default settings, which serialize to camelCase JSON.
    });

builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IWebSocketService, WebSocketService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
builder.Services.AddSingleton<IWebSocketHandler, WebSocketHandler>();

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters()
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(settings.JwtSecret)
            ),
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            ValidateIssuer = false,
        };
    });

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(
        System.Net.IPAddress.Any,
        7123,
        listenOptions =>
        {
            listenOptions.UseHttps("server.pfx", "");
        }
    );
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { }

app.UseStaticFiles(); // Add this line
app.UseHttpsRedirection();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

app.Map(
    "/ws",
    async context =>
    {
        var handler = context.RequestServices.GetRequiredService<IWebSocketHandler>();
        await handler.HandleAsync(context);
    }
);

app.MapControllers();

app.Run();
