using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models
{
    [Table("RefreshTokenRecord", Schema = "auth")]
    public class RefreshTokenRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } // Auto-incrementing primary key

        [Required]
        public required string UserId { get; set; }

        [Required]
        public required string DeviceId { get; set; }

        [Required]
        public required string EncryptedRefreshToken { get; set; }

        [Required]
        public required byte[] SecretKey { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;
    }
}
