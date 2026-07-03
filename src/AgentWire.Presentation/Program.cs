using AgentWire.Core.Entities;
using AgentWire.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure SQLite Database
builder.Services.AddDbContext<AgentWireDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=agentwire.db"));

// Configure CORS for Next.js Dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDashboard",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowDashboard");

// Initialize Database (Auto-Migrate for MVP)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
    db.Database.EnsureCreated(); // Creates the DB if it doesn't exist
}

// Map Endpoints
app.MapPost("/v1/traces", async (AIPacket packet, AgentWireDbContext db) =>
{
    // For MVP cost estimation
    packet.Cost = (packet.PromptTokens * 0.000001m) + (packet.CompletionTokens * 0.000002m);
    
    db.AIPackets.Add(packet);
    await db.SaveChangesAsync();
    
    return Results.Accepted("/v1/traces", packet);
})
.WithName("IngestTrace");

app.MapGet("/v1/packets", async (AgentWireDbContext db) =>
{
    var packets = await db.AIPackets
        .OrderByDescending(p => p.CreatedAt)
        .Take(100)
        .ToListAsync();
        
    return Results.Ok(packets);
})
.WithName("GetPackets");

app.MapGet("/v1/analytics/costs", async (AgentWireDbContext db) =>
{
    var packets = await db.AIPackets.ToListAsync();
    var totalCost = packets.Sum(p => p.Cost);
    var breakdown = packets
        .GroupBy(p => p.ModelName)
        .ToDictionary(g => g.Key, g => g.Sum(p => p.Cost));

    return Results.Ok(new { totalCost, breakdownByModel = breakdown });
})
.WithName("GetCostAnalytics");

app.Run();
