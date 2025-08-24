using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models
{
    [Table("UserData", Schema = "gameplay")]
    public class UserData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string UserId { get; set; }

        public required int Points { get; set; }
    }
}
