using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models;

[Table("Users", Schema = "users")]
public class User
{
    public int Id { get; set; }
    public required string UUID { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Salt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
