using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TaskTracker.Infrastructure.Telemetry;

/// <summary>
/// Factory for configuring OpenTelemetry with SQLite storage
/// </summary>
public static class TelemetryFactory
{
    public static void ConfigureOpenTelemetry(IServiceCollection services, string connectionString)
    {
        // Configure resource attributes for all telemetry
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: "TaskTracker",
                serviceVersion: "1.0.0",
                serviceInstanceId: Environment.MachineName);

        // Add custom SQLite exporter service
        services.AddSingleton<SqliteTelemetryExporter>();
        services.AddDbContext<TelemetryDbContext>(options =>
            options.UseSqlite(connectionString));

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("TaskTracker"))
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource("TaskTracker.Application")
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                    })
                    .AddConsoleExporter()
                    .AddProcessor(new SqliteBatchActivityProcessor(services.BuildServiceProvider()));
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter("TaskTracker")
                    .AddConsoleExporter();
            });

        // Configure logging to use OpenTelemetry
        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.AddConsoleExporter();
                options.AddProcessor(new SqliteBatchLogProcessor(services.BuildServiceProvider()));
            });
        });
    }
}

/// <summary>
/// DbContext for storing telemetry data
/// </summary>
public class TelemetryDbContext : DbContext
{
    public TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : base(options) { }

    public DbSet<LogEntry> Logs { get; set; }
    public DbSet<SpanEntry> Spans { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Level).IsRequired();
            entity.Property(e => e.Message).IsRequired();
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<SpanEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TraceId).IsRequired();
            entity.Property(e => e.SpanId).IsRequired();
            entity.Property(e => e.OperationName).IsRequired();
            entity.Property(e => e.StartTime).IsRequired();
            entity.HasIndex(e => e.StartTime);
        });
    }
}

/// <summary>
/// Entity for storing log entries
/// </summary>
public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
}

/// <summary>
/// Entity for storing span entries
/// </summary>
public class SpanEntry
{
    public int Id { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string SpanId { get; set; } = string.Empty;
    public string? ParentSpanId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long DurationMs { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Attributes { get; set; }
    public string? Events { get; set; }
}

/// <summary>
/// Custom SQLite exporter for telemetry data
/// </summary>
public class SqliteTelemetryExporter
{
    private readonly TelemetryDbContext _context;

    public SqliteTelemetryExporter(TelemetryDbContext context)
    {
        _context = context;
        _context.Database.EnsureCreated();
    }

    public async Task ExportLogsAsync(IEnumerable<LogRecord> logs)
    {
        foreach (var log in logs)
        {
            var entry = new LogEntry
            {
                Timestamp = log.Timestamp,
                Level = log.LogLevel.ToString(),
                Message = log.FormattedMessage ?? string.Empty,
                Category = log.CategoryName,
                Exception = log.Exception?.ToString(),
                Properties = System.Text.Json.JsonSerializer.Serialize(log.Attributes)
            };
            _context.Logs.Add(entry);
        }
        await _context.SaveChangesAsync();
    }

    public async Task ExportSpansAsync(IEnumerable<Activity> spans)
    {
        foreach (var span in spans)
        {
            var entry = new SpanEntry
            {
                TraceId = span.TraceId.ToString(),
                SpanId = span.SpanId.ToString(),
                ParentSpanId = span.ParentSpanId.ToString(),
                OperationName = span.DisplayName,
                StartTime = span.StartTimeUtc,
                EndTime = span.StartTimeUtc.Add(span.Duration),
                DurationMs = (long)span.Duration.TotalMilliseconds,
                Status = span.Status.ToString(),
                Attributes = System.Text.Json.JsonSerializer.Serialize(span.Tags),
                Events = System.Text.Json.JsonSerializer.Serialize(span.Events.Select(e => new { e.Name, e.Timestamp }))
            };
            _context.Spans.Add(entry);
        }
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Custom batch processor for activities
/// </summary>
public class SqliteBatchActivityProcessor : BaseProcessor<Activity>
{
    private readonly SqliteTelemetryExporter _exporter;

    public SqliteBatchActivityProcessor(IServiceProvider serviceProvider) : base()
    {
        var scope = serviceProvider.CreateScope();
        _exporter = scope.ServiceProvider.GetRequiredService<SqliteTelemetryExporter>();
    }

    public override void OnEnd(Activity data)
    {
        if (data != null)
        {
            Task.Run(async () => await _exporter.ExportSpansAsync(new[] { data }));
        }
    }
}

/// <summary>
/// Custom batch processor for logs
/// </summary>
public class SqliteBatchLogProcessor : BaseProcessor<LogRecord>
{
    private readonly SqliteTelemetryExporter _exporter;

    public SqliteBatchLogProcessor(IServiceProvider serviceProvider) : base()
    {
        var scope = serviceProvider.CreateScope();
        _exporter = scope.ServiceProvider.GetRequiredService<SqliteTelemetryExporter>();
    }

    public override void OnEnd(LogRecord data)
    {
        if (data != null)
        {
            Task.Run(async () => await _exporter.ExportLogsAsync(new[] { data }));
        }
    }
}
