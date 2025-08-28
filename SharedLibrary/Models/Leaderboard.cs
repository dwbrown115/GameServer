using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models
{
    [Table("Leaderboard", Schema = "gameplay")]
    public class Leaderboard
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public required string UserId { get; set; }

        public required string Username { get; set; }

        public required int PlayerHighestScore { get; set; }

        public DateTime ScoreTimestamp { get; set; }

        public DateTime PreviousScoreTimestamp { get; set; }

        // JSON (string) representing an array of objects: { HighScoreAtTime: int, HighScoreAtTimestamp: DateTime }
        public string? HighScoreLog { get; set; }
    }
}
