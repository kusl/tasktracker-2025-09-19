using TaskTracker.Domain.Entities;

namespace TaskTracker.Application.Services;

/// <summary>
/// Service abstraction for todo operations
/// </summary>
public interface ITodoService
{
    Task<TodoItem> CreateTodoAsync(string title);
    Task<TodoItem?> GetTodoByIdAsync(int id);
    Task<IEnumerable<TodoItem>> GetAllTodosAsync();
    Task<bool> MarkAsCompleteAsync(int id);
    Task<bool> DeleteTodoAsync(int id);
}
