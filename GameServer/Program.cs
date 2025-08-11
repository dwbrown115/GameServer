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

var settings = new Settings();
builder.Configuration.Bind("Settings", settings);
builder.Services.AddSingleton(settings);

// Add services to the container.
builder.Services.AddDbContext<GameDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Db"))
);

// Migration-only context for database migrations
builder.Services.AddDbContext<MigrationOnlyContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Db"))
);

builder
    .Services.AddControllers()
    .AddNewtonsoftJson(o =>
    {
        o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        // Using the default settings, which serialize to camelCase JSON.
    });

builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IWebSocketService, WebSocketService>();
builder.Services.AddSingleton<IWebSocketConnectionManager, WebSocketConnectionManager>();
builder.Services.AddSingleton<IWebSocketHandler, WebSocketHandler>();

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters()
        {
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(settings.BearerKey)
            ),
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            ValidateIssuer = false,
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) { }

// app.UseHttpsRedirection();
app.UseWebSockets();
app.UseAuthentication();

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
