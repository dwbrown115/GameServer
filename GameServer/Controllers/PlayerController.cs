using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using GameServer;
using SharedLibrary;

namespace GameServer.Controllers;

[ApiController]
[Route("[controller]")]
public class PlayerController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly GameDbContext _context;

    public PlayerController(IPlayerService playerService, GameDbContext context)
    {
        _playerService = playerService;
        _context = context;

        var user = new User()
        {
            Username = "Jsfdsafin67890-=",
            PasswordHash = "drtyujnfghuiop",
            Salt = "sdfghuytfghuio"
        };
        
        _context.Add(user);
        
        _context.SaveChanges(); 
        Console.WriteLine($"User created: {user.Username}");
    }

    [HttpGet("{id}")]
    public Player Get([FromRoute] string id)
    {
        var player = new Player() { Id = id };

        _playerService.DoSomething();

        return player;
    }

    [HttpPost]
    public Player Post(Player player)
    {
        Console.WriteLine("Player created");
        return player;
    }
}