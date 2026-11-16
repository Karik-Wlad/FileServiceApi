using System.ComponentModel.DataAnnotations.Schema;

namespace FileServiceApi
{
    [Table("access")]
    public class Access
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("file_id")]
        public int FileId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }
    }
}