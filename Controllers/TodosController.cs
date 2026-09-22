using DotNetCoreSqlDb.Data;
using DotNetCoreSqlDb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DotNetCoreSqlDb.Controllers
{
    public class TodosController : Controller
    {
        private const string TodosCacheKey = "todos-list";

        private readonly ILogger<TodosController> _logger;
        private readonly MyDatabaseContext _context;
        private readonly IDistributedCache _cache;

        public TodosController(
            MyDatabaseContext context,
            ILogger<TodosController> logger,
            IDistributedCache cache)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
        }

        // GET: Todos
        public async Task<IActionResult> Index()
        {
            return View(await BuildIndexViewModel());
        }

        // POST: Todos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Description", Prefix = "NewTodo")] Todo todo)
        {
            if (ModelState.IsValid)
            {
                _context.Add(todo);
                await _context.SaveChangesAsync();

                // После изменения SQL удаляем старые данные из Redis.
                await _cache.RemoveAsync(TodosCacheKey);

                _logger.LogInformation("Todo created. Redis cache cleared.");

                return RedirectToAction(nameof(Index));
            }

            return View(nameof(Index), await BuildIndexViewModel(todo));
        }

        // POST: Todos/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var todo = await _context.Todo.FindAsync(id);

            if (todo == null)
            {
                return NotFound();
            }

            _context.Todo.Remove(todo);
            await _context.SaveChangesAsync();

            // После удаления записи очищаем кэш.
            await _cache.RemoveAsync(TodosCacheKey);

            _logger.LogInformation("Todo deleted. Redis cache cleared.");

            return RedirectToAction(nameof(Index));
        }

        private async Task<TodosIndexViewModel> BuildIndexViewModel(
            Todo? newTodo = null)
        {
            List<Todo>? todos = null;

            // Сначала пытаемся получить список из Redis.
            var cachedTodos = await _cache.GetStringAsync(TodosCacheKey);

            if (!string.IsNullOrEmpty(cachedTodos))
            {
                todos = JsonSerializer.Deserialize<List<Todo>>(cachedTodos);

                _logger.LogInformation("Data loaded from Redis cache.");
            }

            // Если в Redis данных нет — читаем Azure SQL.
            if (todos == null)
            {
                _logger.LogInformation("Data loaded from Azure SQL database.");

                todos = await _context.Todo
                    .AsNoTracking()
                    .ToListAsync();

                // Записываем результат в Redis на 5 минут.
                var cacheOptions = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                await _cache.SetStringAsync(
                    TodosCacheKey,
                    JsonSerializer.Serialize(todos),
                    cacheOptions);

                _logger.LogInformation("Data saved to Redis cache.");
            }

            return new TodosIndexViewModel
            {
                NewTodo = newTodo ?? new Todo(),
                Todos = todos
            };
        }
    }
}
