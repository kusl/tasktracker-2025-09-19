using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TaskTracker.Application.Services;
using TaskTracker.Domain.Entities;
using TaskTracker.Domain.Repositories;

namespace TaskTracker.Application.Services;

/// <summary>
/// Implementation of todo service with OpenTelemetry instrumentation
/// </summary>
public class TodoService : ITodoService
{
    private readonly ITodoRepository _repository;
    private readonly ILogger<TodoService> _logger;
    private static readonly ActivitySource ActivitySource = new("TaskTracker.Application");

    public TodoService(ITodoRepository repository, ILogger<TodoService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TodoItem> CreateTodoAsync(string title)
    {
        // Create a new activity (span) for tracing
        using var activity = ActivitySource.StartActivity("CreateTodo");
        activity?.SetTag("todo.title", title);

        try
        {
            _logger.LogInformation("Creating new todo item with title: {Title}", title);
            activity?.AddEvent(new ActivityEvent("CreatingTodoItem"));

            var item = new TodoItem
            {
                Title = title,
                IsDone = false,
                CreatedAtUtc = DateTime.UtcNow
            };

            var created = await _repository.AddAsync(item);

            activity?.SetTag("todo.id", created.Id);
            activity?.AddEvent(new ActivityEvent("TodoItemCreated"));
            _logger.LogInformation("Todo item created successfully with ID: {Id}", created.Id);

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating todo item");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<TodoItem?> GetTodoByIdAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetTodoById");
        activity?.SetTag("todo.id", id);

        try
        {
            _logger.LogDebug("Retrieving todo item with ID: {Id}", id);
            var item = await _repository.GetByIdAsync(id);

            if (item != null)
            {
                activity?.AddEvent(new ActivityEvent("TodoItemFound"));
                _logger.LogDebug("Todo item found with ID: {Id}", id);
            }
            else
            {
                activity?.AddEvent(new ActivityEvent("TodoItemNotFound"));
                _logger.LogWarning("Todo item not found with ID: {Id}", id);
            }

            return item;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving todo item with ID: {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TodoItem>> GetAllTodosAsync()
    {
        using var activity = ActivitySource.StartActivity("GetAllTodos");

        try
        {
            _logger.LogDebug("Retrieving all todo items");
            activity?.AddEvent(new ActivityEvent("RetrievingAllItems"));

            var items = await _repository.GetAllAsync();
            var itemsList = items.ToList();

            activity?.SetTag("todo.count", itemsList.Count);
            activity?.AddEvent(new ActivityEvent("ItemsRetrieved"));
            _logger.LogInformation("Retrieved {Count} todo items", itemsList.Count);

            return itemsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all todo items");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<bool> MarkAsCompleteAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("MarkTodoAsComplete");
        activity?.SetTag("todo.id", id);

        try
        {
            _logger.LogInformation("Marking todo item as complete with ID: {Id}", id);
            activity?.AddEvent(new ActivityEvent("MarkingAsComplete"));

            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                activity?.AddEvent(new ActivityEvent("TodoItemNotFound"));
                _logger.LogWarning("Cannot mark as complete - todo item not found with ID: {Id}", id);
                return false;
            }

            item.IsDone = true;
            await _repository.UpdateAsync(item);

            activity?.AddEvent(new ActivityEvent("MarkedAsComplete"));
            _logger.LogInformation("Todo item marked as complete with ID: {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking todo item as complete with ID: {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<bool> DeleteTodoAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("DeleteTodo");
        activity?.SetTag("todo.id", id);

        try
        {
            _logger.LogInformation("Deleting todo item with ID: {Id}", id);
            activity?.AddEvent(new ActivityEvent("DeletingTodoItem"));

            var item = await _repository.GetByIdAsync(id);
            if (item == null)
            {
                activity?.AddEvent(new ActivityEvent("TodoItemNotFound"));
                _logger.LogWarning("Cannot delete - todo item not found with ID: {Id}", id);
                return false;
            }

            await _repository.DeleteAsync(id);

            activity?.AddEvent(new ActivityEvent("TodoItemDeleted"));
            _logger.LogInformation("Todo item deleted successfully with ID: {Id}", id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting todo item with ID: {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
