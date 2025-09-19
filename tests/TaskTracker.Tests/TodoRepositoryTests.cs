using Microsoft.EntityFrameworkCore;
using TaskTracker.Domain.Entities;
using TaskTracker.Infrastructure.Data;
using Xunit;

namespace TaskTracker.Tests;

/// <summary>
/// Integration tests for TodoRepository with in-memory database
/// </summary>
public class TodoRepositoryTests : IDisposable
{
    private readonly TodoDbContext _context;
    private readonly TodoRepository _repository;

    public TodoRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TodoDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TodoDbContext(options);
        _repository = new TodoRepository(_context);
    }

    [Fact]
    public async Task AddAsync_ValidItem_ReturnsItemWithId()
    {
        // Arrange
        var item = new TodoItem
        {
            Title = "Test Item",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = await _repository.AddAsync(item);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Test Item", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingItem_ReturnsItem()
    {
        // Arrange
        var item = new TodoItem
        {
            Title = "Test Item",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        var added = await _repository.AddAsync(item);

        // Act
        var result = await _repository.GetByIdAsync(added.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(added.Id, result.Id);
        Assert.Equal(added.Title, result.Title);
    }

    [Fact]
    public async Task UpdateAsync_ExistingItem_UpdatesSuccessfully()
    {
        // Arrange
        var item = new TodoItem
        {
            Title = "Original Title",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        var added = await _repository.AddAsync(item);

        // Act
        added.Title = "Updated Title";
        added.IsDone = true;
        await _repository.UpdateAsync(added);

        // Assert
        var updated = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(updated);
        Assert.Equal("Updated Title", updated.Title);
        Assert.True(updated.IsDone);
    }

    [Fact]
    public async Task DeleteAsync_ExistingItem_RemovesItem()
    {
        // Arrange
        var item = new TodoItem
        {
            Title = "To Delete",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        var added = await _repository.AddAsync(item);

        // Act
        await _repository.DeleteAsync(added.Id);

        // Assert
        var deleted = await _repository.GetByIdAsync(added.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllItemsOrderedByDate()
    {
        // Arrange
        var item1 = new TodoItem
        {
            Title = "First",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };
        var item2 = new TodoItem
        {
            Title = "Second",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var item3 = new TodoItem
        {
            Title = "Third",
            IsDone = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _repository.AddAsync(item1);
        await _repository.AddAsync(item2);
        await _repository.AddAsync(item3);

        // Act
        var result = await _repository.GetAllAsync();
        var items = result.ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("Third", items[0].Title); // Most recent first
        Assert.Equal("Second", items[1].Title);
        Assert.Equal("First", items[2].Title);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
