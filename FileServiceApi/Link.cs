using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileServiceApi
{
    [Table("links")]
    public class Link
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("file_id")]
        public int FileId { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}