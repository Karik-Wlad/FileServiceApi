using Microsoft.EntityFrameworkCore;
using FileServiceApi;
using Amazon.S3;

var builder = WebApplication.CreateBuilder(args);

// Настройка для облака: берем порт из переменной окружения (для Render), либо 8080 по умолчанию
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// Подключаем базу данных MySQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"DEBUG_CONNECTION_STRING: {connectionString}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Настройка подключения к облачному хранилищу Supabase (S3)
var s3Config = builder.Configuration.GetSection("SupabaseS3");
var s3ClientConfig = new AmazonS3Config
{
    ServiceURL = s3Config["Endpoint"],
    ForcePathStyle = true // Это обязательно для Supabase, иначе он не поймет запросы
};
var s3Client = new AmazonS3Client(s3Config["AccessKey"], s3Config["SecretKey"], s3ClientConfig);
builder.Services.AddSingleton<IAmazonS3>(s3Client);


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
