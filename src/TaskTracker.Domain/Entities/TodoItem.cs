namespace TaskTracker.Domain.Entities;

/// <summary>
/// Represents a todo item entity in the domain model
/// </summary>
public class TodoItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public bool IsDone { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
