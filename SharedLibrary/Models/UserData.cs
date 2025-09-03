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

        // JSON (string) representing an array of objects: { SkinId: string }
        public string? OwnedSkins { get; set; }

        // JSON (string) representing an array of objects: { PointsAtTime: int, PointsAtTimestamp: DateTime }
        public string? PointsLog { get; set; }

        // The currently selected / active skin (stores Skin UUID). Defaults to a 'white' skin value placeholder until user selects.
        [MaxLength(100)]
        public string? ActiveSkin { get; set; }
    }
}
