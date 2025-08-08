using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models;

[Table("JwtTokens", Schema = "auth")]
public class JwtToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string EncryptedToken { get; set; }

    [Required]
    [MaxLength(100)]
    public required string UserId { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; } = false;

    [Required]
    public required byte[] SecretKey { get; set; }

    [MaxLength(256)]
    public required string RefreshToken { get; set; }

    public DateTime? RefreshExpires { get; set; }
}
