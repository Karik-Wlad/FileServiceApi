using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace FileServiceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Подключаем наш "мозг" базы данных
        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/users
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            // Идём в базу данных и берём всех пользователей
            var users = await _context.Users.ToListAsync();
            return Ok(users);
        }


        // POST: api/users/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // 1. Ищем пользователя в базе по GoogleId
            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == request.GoogleId);

            // 2. Если пользователя нет — создаем нового
            if (user == null)
            {
                user = new User
                {
                    GoogleId = request.GoogleId,
                    Email = request.Email,
                    Name = request.Name
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync(); // Сохраняем, чтобы база выдала ему ID
            }

            // 3. Возвращаем пользователя (включая его ID из базы)
            return Ok(user);
        }




    }

    // Класс для приема данных от Android
    public class LoginRequest
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

}


