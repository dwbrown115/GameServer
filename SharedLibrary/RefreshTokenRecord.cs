using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary
{
    [Table("RefreshTokenRecord", Schema = "auth")]
    public class RefreshTokenRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; } // Auto-incrementing primary key

        [Required]
        public string UserId { get; set; }

        [Required]
        public string DeviceId { get; set; }

        [Required]
        public string EncryptedRefreshToken { get; set; }

        [Required]
        public byte[] SecretKey { get; set; }

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;
    }
}