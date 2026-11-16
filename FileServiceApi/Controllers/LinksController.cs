using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FileServiceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LinksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LinksController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/links/generate
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateLink([FromBody] LinkRequest request)
        {
            // 1. Проверяем, существует ли такой файл в базе
            var file = await _context.Files.FindAsync(request.FileId);
            if (file == null)
                return NotFound("Файл не найден.");

            // 2. Генерируем уникальный токен (случайную строку)
            var token = Guid.NewGuid().ToString("N").Substring(0, 16);

            // 3. Создаем объект ссылки
            var link = new Link
            {
                FileId = request.FileId,
                Token = token,
                CreatedAt = DateTime.Now,
                ExpiresAt = request.ExpiresAt
            };

            // 4. Сохраняем в базу
            _context.Links.Add(link);
            await _context.SaveChangesAsync();

            // 5. Возвращаем результат (ссылку)
            // В реальном приложении тут был бы твой IP или домен, но для теста сгодится localhost
            var downloadUrl = $"http://localhost:5000/api/files/download/{token}";

            return Ok(new { Link = downloadUrl, ExpiresAt = link.ExpiresAt });
        }
    }

    // Класс для приема данных от клиента
    public class LinkRequest
    {
        public int FileId { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}