using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;

namespace FileServiceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;
        public FilesController(AppDbContext context, IAmazonS3 s3Client, IConfiguration config)
        {
            _context = context;
            _s3Client = s3Client;
            _config = config;
        }

        // POST: api/files/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Файл не выбран.");
            if (request.File.Length > 50 * 1024 * 1024)
                return BadRequest("Файл слишком большой. Максимум 50 МБ.");

            // 1. Генерируем уникальное имя файла
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(request.File.FileName);
            var bucketName = _config["SupabaseS3:BucketName"]!;

            // 2. Загружаем файл в Supabase (S3)
            using (var stream = request.File.OpenReadStream())
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = uniqueFileName,
                    InputStream = stream,
                    ContentType = request.File.ContentType,
                    DisablePayloadSigning = true // Отключаем подпись, чтобы Supabase нормально принял файл
                };
                await _s3Client.PutObjectAsync(putRequest);
            }

            // 3. Формируем публичную ссылку на файл
            // Извлекаем ID проекта из Endpoint (oxsbkkjswixyeiiejsrm)
            var endpoint = _config["SupabaseS3:Endpoint"]!;
            var projectId = endpoint.Replace("https://", "").Split('.')[0];
            var publicUrl = $"https://{projectId}.supabase.co/storage/v1/object/public/{bucketName}/{uniqueFileName}";

            // 4. Сохраняем информацию в базу данных (вместо пути теперь ссылка)
            var fileEntity = new FileEntity
            {
                UserId = request.UserId,
                Filename = request.File.FileName,
                FileType = request.File.ContentType,
                SizeBytes = request.File.Length,
                StoragePath = publicUrl, // <-- Теперь тут ссылка
                UploadedAt = DateTime.Now,
                ExpiresAt = request.ExpiresAt
            };

            _context.Files.Add(fileEntity);
            await _context.SaveChangesAsync();

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
            return Redirect(file.StoragePath);
        }
        // DELETE: api/files/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            var file = await _context.Files.FindAsync(id);
            if (file == null) return NotFound("Файл не найден.");

            // 1. Удаляем физический файл из Supabase S3
            var bucketName = _config["SupabaseS3:BucketName"]!;
            // Достаем имя файла из ссылки (берем всё, что идет после последнего слэша)
            var fileNameInS3 = file.StoragePath.Split('/').Last();

            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileNameInS3
                };
                await _s3Client.DeleteObjectAsync(deleteRequest);
            }
            catch (Exception ex)
            {
                // Если файла в облаке уже нет, просто выводим в лог, но базу чистим
                Console.WriteLine($"Ошибка удаления из S3: {ex.Message}");
            }

            // 2. Удаляем связанные ссылки и доступы из базы
            var links = _context.Links.Where(l => l.FileId == id);
            _context.Links.RemoveRange(links);
            var accesses = _context.Accesses.Where(a => a.FileId == id);
            _context.Accesses.RemoveRange(accesses);

            // 3. Удаляем запись из базы
            _context.Files.Remove(file);
            await _context.SaveChangesAsync();

            return Ok($"Файл '{file.Filename}' успешно удален.");
        }
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserFiles(int userId)
        {
            // Находим все ID файлов, к которым у пользователя есть доступ
            var sharedFileIds = await _context.Accesses
                .Where(a => a.UserId == userId)
                .Select(a => a.FileId)
                .ToListAsync();

            // Берем файлы, где он владелец ИЛИ где файл есть в списке расшаренных
            var files = await _context.Files
                .Where(f => f.UserId == userId || sharedFileIds.Contains(f.Id))
                .OrderByDescending(f => f.UploadedAt)
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

            var bucketName = _config["SupabaseS3:BucketName"]!;
            var fileNameInS3 = file.StoragePath.Split('/').Last();

            try
            {
                // Запрашиваем файл из S3
                var getRequest = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileNameInS3
                };

                var response = await _s3Client.GetObjectAsync(getRequest);

                // Отдаем файл прямо в поток телефона
                return File(response.ResponseStream, file.FileType, file.Filename);
            }
            catch (AmazonS3Exception)
            {
                return NotFound("Файл не найден в облачном хранилище.");
            }
        }

        // POST: api/files/share
        [HttpPost("share")]
        public async Task<IActionResult> ShareFile([FromBody] ShareRequest request)
        {
            // 1. Проверяем, существует ли файл
            var file = await _context.Files.FindAsync(request.FileId);
            if (file == null) return NotFound("Файл не найден.");

            // 2. Ищем пользователя, которому хотим дать доступ, по Email
            var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.TargetEmail);
            if (targetUser == null) return NotFound("Пользователь с таким email не найден в системе.");

            // 3. Проверяем, не выдали ли мы уже доступ этому пользователю
            var existingAccess = await _context.Accesses
                .FirstOrDefaultAsync(a => a.FileId == request.FileId && a.UserId == targetUser.Id);

            if (existingAccess != null) return BadRequest("Доступ этому пользователю уже предоставлен.");

            // 4. Создаем запись о доступе
            var access = new Access
            {
                FileId = request.FileId,
                UserId = targetUser.Id
            };

            _context.Accesses.Add(access);
            await _context.SaveChangesAsync();

            return Ok($"Доступ к файлу '{file.Filename}' предоставлен пользователю {targetUser.Email}.");
        }





    }
    public class ShareRequest
    {
        public int FileId { get; set; }
        public string TargetEmail { get; set; } = string.Empty;
    }
}