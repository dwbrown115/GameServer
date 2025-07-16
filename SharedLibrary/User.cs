using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary;

[Table("Users", Schema = "users")]
public class User {
    public int Id { get; set; }
    public string UUID {get; set;}
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Salt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}