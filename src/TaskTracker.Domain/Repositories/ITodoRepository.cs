using TaskTracker.Domain.Entities;

namespace TaskTracker.Domain.Repositories;

/// <summary>
/// Repository abstraction for TodoItem operations
/// </summary>
public interface ITodoRepository
{
    Task<TodoItem?> GetByIdAsync(int id);
    Task<IEnumerable<TodoItem>> GetAllAsync();
    Task<TodoItem> AddAsync(TodoItem item);
    Task UpdateAsync(TodoItem item);
    Task DeleteAsync(int id);
}
