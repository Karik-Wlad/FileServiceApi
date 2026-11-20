using Amazon.S3;
using Microsoft.EntityFrameworkCore;

namespace FileServiceApi
{
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;

        public CleanupService(IServiceProvider serviceProvider, IAmazonS3 s3Client, IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _s3Client = s3Client;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Робот просыпается каждую минуту
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                        // Ищем файлы, у которых время жизни уже истекло
                        var expiredFiles = await context.Files
                            .Where(f => f.ExpiresAt.HasValue && f.ExpiresAt.Value < DateTime.Now)
                            .ToListAsync(stoppingToken);

                        if (expiredFiles.Any())
                        {
                            var bucketName = _config["SupabaseS3:BucketName"]!;

                            foreach (var file in expiredFiles)
                            {
                                // 1. Удаляем из облака Supabase
                                var fileNameInS3 = file.StoragePath.Split('/').Last();
                                try
                                {
                                    await _s3Client.DeleteObjectAsync(bucketName, fileNameInS3, stoppingToken);
                                }
                                catch { /* Если файла уже нет в S3, просто идем дальше */ }

                                // 2. Удаляем связи из базы (links, access)
                                var links = context.Links.Where(l => l.FileId == file.Id);
                                context.Links.RemoveRange(links);
                                var accesses = context.Accesses.Where(a => a.FileId == file.Id);
                                context.Accesses.RemoveRange(accesses);

                                // 3. Удаляем запись о файле из базы
                                context.Files.Remove(file);
                            }
                            await context.SaveChangesAsync(stoppingToken);
                            Console.WriteLine($"Робот-уборщик удалил {expiredFiles.Count} просроченных файлов.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка в роботе-уборщике: {ex.Message}");
                }

                // Спим 1 минуту перед следующей проверкой
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}