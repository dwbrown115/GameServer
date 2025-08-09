using System.Text;
using GameServer;
using GameServer.Models;
using GameServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

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
    });

builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IWebSocketService, WebSocketService>();

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
app.UseAuthentication();
app.UseAuthorization();
app.UseWebSockets();

// app.Map("/ws", async context =>
// {
//     await WebSocketHandler.HandleWebSocket(
//         context,
//         token => IsTokenValid(token),           // Your JWT validator
//         token => GetPlayerIdFromToken(token)    // Your claim extractor
//     );
// });

app.MapControllers();

app.Run();

// using System.Text;
// using GameServer;
// using GameServer.Services;
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.IdentityModel.Tokens;
// using Newtonsoft.Json;
// using Newtonsoft.Json.Serialization;
// using Server;
// using Server.Models;
// using Server.Services;
//
// var builder = WebApplication.CreateBuilder(args);
//
// var settings = new Settings();
// builder.Configuration.Bind("Settings", settings);
// builder.Services.AddSingleton(settings);
//
// // Add services to the container.
//
// builder.Services.AddDbContext<GameDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Db")));
//
// builder.Services.AddControllers().AddNewtonsoftJson(o =>
// {
//     // o.SerializerSettings.ContractResolver = new DefaultContractResolver();
//     o.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
// });
//
// builder.Services.AddScoped<IPlayerService, MockPlayerService>();
// builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
//
// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
//     // o.TokenValidationParameters = new TokenValidationParameters() {
//     //     IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(settings.BearerKey)),
//     //     ValidateIssuerSigningKey = true,
//     //     ValidateAudience = false,
//     //     ValidateIssuer = false
//     // };
//
// });
//
// var app = builder.Build();
//
// // Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
// }
//
// // app.UseHttpsRedirection();
//
// app.UseAuthorization();
// app.UseAuthentication();
//
// app.MapControllers();
//
// app.Run();
