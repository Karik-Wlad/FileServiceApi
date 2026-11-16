using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FileServiceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FilesController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/files/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
        {
            // 1. Проверяем, пришел ли файл
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Файл не выбран.");

            // 2. Создаем папку для файлов, если ее еще нет
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // 3. Генерируем уникальное имя файла
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.File.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            // 4. Физически сохраняем файл на диск
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.File.CopyToAsync(stream);
            }

            // 5. Записываем информацию о файле в базу данных
            var fileEntity = new FileEntity
            {
                UserId = request.UserId,
                Filename = request.File.FileName,
                FileType = request.File.ContentType,
                SizeBytes = request.File.Length,
                StoragePath = filePath,
                UploadedAt = DateTime.Now,
                ExpiresAt = request.ExpiresAt
            };

            _context.Files.Add(fileEntity);
            await _context.SaveChangesAsync();

            // 6. Возвращаем успешный ответ
            return Ok(fileEntity);
        }
        // GET: api/files/download/{token}
        [HttpGet("download/{token}")]
        public async Task<IActionResult> DownloadFile(string token)
        {
            // 1. Ищем ссылку в базе по токену
            var link = await _context.Links.FirstOrDefaultAsync(l => l.Token == token);
            if (link == null)
                return NotFound("Ссылка не найдена или недействительна.");

            // 2. Проверяем, не истекла ли ссылка (если у неё есть время жизни)
            if (link.ExpiresAt.HasValue && link.ExpiresAt.Value < DateTime.Now)
                return BadRequest("Срок действия ссылки истек.");

            // 3. Ищем сам файл в базе
            var file = await _context.Files.FindAsync(link.FileId);
            if (file == null)
                return NotFound("Файл был удален из системы.");

            // 4. Проверяем, не истек ли срок хранения самого файла
            if (file.ExpiresAt.HasValue && file.ExpiresAt.Value < DateTime.Now)
                return BadRequest("Срок хранения файла истек. Файл больше недоступен.");

            // 5. Проверяем, существует ли файл физически на диске
            if (!System.IO.File.Exists(file.StoragePath))
                return NotFound("Физический файл на сервере отсутствует.");

            // 6. Отдаем файл клиенту! 
            // PhysicalFile берет файл с диска и отдает с правильным именем (file.Filename)
            return PhysicalFile(file.StoragePath, file.FileType, file.Filename);
        }
        // DELETE: api/files/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            // 1. Ищем файл в базе
            var file = await _context.Files.FindAsync(id);
            if (file == null)
                return NotFound("Файл не найден.");

            // 2. Удаляем связанные ссылки из таблицы links
            var links = _context.Links.Where(l => l.FileId == id);
            _context.Links.RemoveRange(links);

            // 3. Удаляем связанные разрешения из таблицы access
            var accesses = _context.Accesses.Where(a => a.FileId == id);
            _context.Accesses.RemoveRange(accesses);

            // 4. Удаляем физический файл с диска
            if (System.IO.File.Exists(file.StoragePath))
            {
                System.IO.File.Delete(file.StoragePath);
            }

            // 5. Удаляем запись из базы данных
            _context.Files.Remove(file);

            // 6. Сохраняем изменения в базе
            await _context.SaveChangesAsync();

            return Ok($"Файл '{file.Filename}' успешно удален.");
        }
        // GET: api/files/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserFiles(int userId)
        {
            // Ищем все файлы, которые принадлежат этому пользователю
            var files = await _context.Files
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.UploadedAt) // Сначала новые
                .ToListAsync();

            return Ok(files);
        }

        // GET: api/files/download-by-id/{id}
        [HttpGet("download-by-id/{id}")]
        public async Task<IActionResult> DownloadFileById(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null) return NotFound("Файл не найден в базе.");

            if (file.ExpiresAt.HasValue && file.ExpiresAt.Value < DateTime.Now)
                return BadRequest("Срок хранения файла истек.");

            if (!System.IO.File.Exists(file.StoragePath))
                return NotFound("Физический файл отсутствует на сервере.");

            // Отдаем файл потоком (без загрузки в память)
            return PhysicalFile(file.StoragePath, file.FileType, file.Filename);
        }


    }
}