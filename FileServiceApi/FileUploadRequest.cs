using Microsoft.AspNetCore.Http;
using System;

namespace FileServiceApi
{
    public class FileUploadRequest
    {
        public int UserId { get; set; }
        public IFormFile File { get; set; } = null!;
        public DateTime? ExpiresAt { get; set; }
    }
}