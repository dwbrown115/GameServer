using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibrary.Models
{
    [Table("Skins", Schema = "gameplay")]
    public class Skins
    {
        [Key]
        public int Id { get; set; }

        public required string UUID { get; set; }

        public required string HexValue { get; set; }

        public required int Price { get; set; }
    }
}
