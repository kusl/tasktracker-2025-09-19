using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaskTracker.Application.Services;
using TaskTracker.Domain.Entities;
using TaskTracker.Infrastructure.Data;
using Xunit;

namespace TaskTracker.Tests;

/// <summary>
/// Unit tests for TodoService using in-memory database
/// </summary>
public class TodoServiceTests : IDisposable
{
    private readonly TodoDbContext _context;
    private readonly TodoRepository _repository;
    private readonly TodoService _service;
    private readonly Mock<ILogger<TodoService>> _loggerMock;

    public TodoServiceTests()
    {
        // Setup in-memory database for testing
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TodoDbContext(options);
        _repository = new TodoRepository(_context);
        _loggerMock = new Mock<ILogger<TodoService>>();
        _service = new TodoService(_repository, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateTodoAsync_ValidTitle_ReturnsTodoItem()
    {
        // Arrange
        const string title = "Test Todo Item";

        // Act
        var result = await _service.CreateTodoAsync(title);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title, result.Title);
        Assert.False(result.IsDone);
        Assert.True(result.Id > 0);
        Assert.True(result.CreatedAtUtc > DateTime.MinValue);
    }

    [Fact]
    public async Task GetTodoByIdAsync_ExistingId_ReturnsTodoItem()
    {
        // Arrange
        var todo = await _service.CreateTodoAsync("Test Item");

        // Act
        var result = await _service.GetTodoByIdAsync(todo.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(todo.Id, result.Id);
        Assert.Equal(todo.Title, result.Title);
    }

    [Fact]
    public async Task GetTodoByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _service.GetTodoByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllTodosAsync_MultipleItems_ReturnsAll()
    {
        // Arrange
        await _service.CreateTodoAsync("Item 1");
        await _service.CreateTodoAsync("Item 2");
        await _service.CreateTodoAsync("Item 3");

        // Act
        var result = await _service.GetAllTodosAsync();
        var items = result.ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Contains(items, i => i.Title == "Item 1");
        Assert.Contains(items, i => i.Title == "Item 2");
        Assert.Contains(items, i => i.Title == "Item 3");
    }

    [Fact]
    public async Task GetAllTodosAsync_NoItems_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetAllTodosAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task MarkAsCompleteAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        var todo = await _service.CreateTodoAsync("Test Item");

        // Act
        var result = await _service.MarkAsCompleteAsync(todo.Id);

        // Assert
        Assert.True(result);

        // Verify the item is actually marked as done
        var updatedTodo = await _service.GetTodoByIdAsync(todo.Id);
        Assert.NotNull(updatedTodo);
        Assert.True(updatedTodo.IsDone);
    }

    [Fact]
    public async Task MarkAsCompleteAsync_NonExistingId_ReturnsFalse()
    {
        // Act
        var result = await _service.MarkAsCompleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteTodoAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        var todo = await _service.CreateTodoAsync("Test Item");

        // Act
        var result = await _service.DeleteTodoAsync(todo.Id);

        // Assert
        Assert.True(result);

        // Verify the item is actually deleted
        var deletedTodo = await _service.GetTodoByIdAsync(todo.Id);
        Assert.Null(deletedTodo);
    }

    [Fact]
    public async Task DeleteTodoAsync_NonExistingId_ReturnsFalse()
    {
        // Act
        var result = await _service.DeleteTodoAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CreateTodoAsync_LogsInformation()
    {
        // Arrange
        const string title = "Test Item";

        // Act
        await _service.CreateTodoAsync(title);

        // Assert - Verify logging was called
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Creating new todo item")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
