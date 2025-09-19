using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskTracker.Application.Services;
using TaskTracker.Domain.Repositories;
using TaskTracker.Infrastructure.Data;
using TaskTracker.Infrastructure.Telemetry;

// Create Activity Source for the console app
var activitySource = new ActivitySource("TaskTracker.ConsoleApp");

// Build the host with dependency injection
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Connection string for SQLite database
        const string connectionString = "Data Source=task_tracker.db";
        
        // Configure Entity Framework with SQLite
        services.AddDbContext<TodoDbContext>(options =>
            options.UseSqlite(connectionString));
        
        // Configure OpenTelemetry with SQLite storage
        TelemetryFactory.ConfigureOpenTelemetry(services, connectionString);
        
        // Register repositories (Infrastructure layer)
        services.AddScoped<ITodoRepository, TodoRepository>();
        
        // Register services (Application layer)
        services.AddScoped<ITodoService, TodoService>();
        
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddConsole();
        });
    })
    .Build();

// Ensure database is created
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    
    var telemetryContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();
    await telemetryContext.Database.EnsureCreatedAsync();
}

// Start root activity for the entire application run
using var rootActivity = activitySource.StartActivity("ConsoleApp.Run");
rootActivity?.SetTag("app.start_time", DateTime.UtcNow.ToString("O"));

// Get logger and service
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var todoService = host.Services.GetRequiredService<ITodoService>();

logger.LogInformation("TaskTracker Console Application started");
Console.WriteLine("=== TaskTracker Console Application ===");
Console.WriteLine("Commands:");
Console.WriteLine("  add <title>  - Add a new todo item");
Console.WriteLine("  list         - List all todo items");
Console.WriteLine("  done <id>    - Mark a todo item as complete");
Console.WriteLine("  delete <id>  - Delete a todo item");
Console.WriteLine("  exit         - Exit the application");
Console.WriteLine();

var running = true;
while (running)
{
    try
    {
        Console.Write("> ");
        var input = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(input))
            continue;
        
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        
        // Create activity for each command
        using var commandActivity = activitySource.StartActivity($"Command.{command}");
        commandActivity?.SetTag("command.type", command);
        
        switch (command)
        {
            case "add" when parts.Length > 1:
                {
                    var title = parts[1];
                    commandActivity?.SetTag("command.title", title);
                    
                    var todo = await todoService.CreateTodoAsync(title);
                    Console.WriteLine($"✅ Created todo #{todo.Id}: {todo.Title}");
                    logger.LogInformation("User created todo item #{Id}", todo.Id);
                }
                break;
                
            case "list":
                {
                    var todos = await todoService.GetAllTodosAsync();
                    var todoList = todos.ToList();
                    
                    commandActivity?.SetTag("command.result_count", todoList.Count);
                    
                    if (!todoList.Any())
                    {
                        Console.WriteLine("No todo items found.");
                    }
                    else
                    {
                        Console.WriteLine("\nTodo Items:");
                        Console.WriteLine("===========");
                        foreach (var todo in todoList)
                        {
                            var status = todo.IsDone ? "✅" : "⬜";
                            Console.WriteLine($"{status} #{todo.Id}: {todo.Title} (Created: {todo.CreatedAtUtc:yyyy-MM-dd HH:mm})");
                        }
                        Console.WriteLine();
                    }
                    logger.LogInformation("User listed {Count} todo items", todoList.Count);
                }
                break;
                
            case "done" when parts.Length > 1 && int.TryParse(parts[1], out var id):
                {
                    commandActivity?.SetTag("command.id", id);
                    
                    var success = await todoService.MarkAsCompleteAsync(id);
                    if (success)
                    {
                        Console.WriteLine($"✅ Marked todo #{id} as complete");
                        logger.LogInformation("User marked todo #{Id} as complete", id);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Todo #{id} not found");
                        logger.LogWarning("User tried to mark non-existent todo #{Id} as complete", id);
                    }
                }
                break;
                
            case "delete" when parts.Length > 1 && int.TryParse(parts[1], out var id):
                {
                    commandActivity?.SetTag("command.id", id);
                    
                    var success = await todoService.DeleteTodoAsync(id);
                    if (success)
                    {
                        Console.WriteLine($"✅ Deleted todo #{id}");
                        logger.LogInformation("User deleted todo #{Id}", id);
                    }
                    else
                    {
                        Console.WriteLine($"❌ Todo #{id} not found");
                        logger.LogWarning("User tried to delete non-existent todo #{Id}", id);
                    }
                }
                break;
                
            case "exit":
                running = false;
                Console.WriteLine("Goodbye!");
                logger.LogInformation("User exited the application");
                break;
                
            default:
                Console.WriteLine("❌ Invalid command. Type 'exit' to quit.");
                commandActivity?.SetStatus(ActivityStatusCode.Error, "Invalid command");
                break;
        }
    }
    catch (Exception ex)
    {
        // Handle any unexpected exceptions gracefully
        Console.WriteLine($"❌ An error occurred: {ex.Message}");
        logger.LogError(ex, "Unhandled exception in command loop");
    }
}

rootActivity?.SetTag("app.end_time", DateTime.UtcNow.ToString("O"));
rootActivity?.AddEvent(new ActivityEvent("ApplicationShutdown"));

// Dispose of the host
await host.StopAsync();
host.Dispose();
