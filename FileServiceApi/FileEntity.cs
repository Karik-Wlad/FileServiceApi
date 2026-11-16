using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FileServiceApi
{
    [Table("files")]
    public class FileEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("filename")]
        public string Filename { get; set; } = string.Empty;

        [Column("file_type")]
        public string FileType { get; set; } = string.Empty;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("storage_path")]
        public string StoragePath { get; set; } = string.Empty;

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }

        [Column("expires_at")]
        public DateTime? ExpiresAt { get; set; }
    }
}