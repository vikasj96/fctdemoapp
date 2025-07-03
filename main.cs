
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=app.db"));
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IBusinessLogicService, BusinessLogicService>();

var app = builder.Build();

// Configure pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    await SeedData(context);
}

app.Run("http://0.0.0.0:5000");

// Seed Data
static async Task SeedData(AppDbContext context)
{
    if (await context.Roles.AnyAsync()) return;
    
    // Generate 100 roles
    var roles = new List<Role>();
    var roleCategories = new[] { "Admin", "Manager", "Supervisor", "Analyst", "Operator", "Viewer", "Guest", "System", "Service", "API" };
    
    for (int i = 1; i <= 100; i++)
    {
        roles.Add(new Role
        {
            Name = $"{roleCategories[i % roleCategories.Length]}_{i:D3}",
            Description = $"Role description for {roleCategories[i % roleCategories.Length]} level {i}",
            Level = (i - 1) / 10 + 1,
            IsActive = i % 20 != 0, // 95% active
            CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(365))
        });
    }
    
    // Generate 250 user actions
    var actions = new List<UserAction>();
    var actionCategories = new[] { "User Management", "Data Access", "Report Generation", "System Configuration", 
        "Financial Operations", "Inventory Management", "Customer Service", "Analytics", "Security", "Audit" };
    var actionTypes = new[] { "Create", "Read", "Update", "Delete", "Execute", "Approve", "Review", "Export", "Import", "Configure" };
    
    for (int i = 1; i <= 250; i++)
    {
        actions.Add(new UserAction
        {
            Name = $"{actionTypes[i % actionTypes.Length]}_{actionCategories[i % actionCategories.Length]}_{i:D3}",
            Description = $"Action to {actionTypes[i % actionTypes.Length].ToLower()} {actionCategories[i % actionCategories.Length].ToLower()}",
            Category = actionCategories[i % actionCategories.Length],
            RequiredRoleLevel = (i - 1) % 10 + 1,
            IsSystemAction = i % 15 == 0,
            CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(180))
        });
    }
    
    // Add sample users
    var users = new List<User>();
    for (int i = 1; i <= 50; i++)
    {
        users.Add(new User
        {
            Username = $"user_{i:D3}",
            Email = $"user{i}@example.com",
            RoleId = Random.Shared.Next(1, 101),
            CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(365)),
            LastLoginAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30)),
            IsActive = i % 10 != 0
        });
    }
    
    context.Roles.AddRange(roles);
    context.UserActions.AddRange(actions);
    context.Users.AddRange(users);
    await context.SaveChangesAsync();
}

// Models
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserAction
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int RequiredRoleLevel { get; set; }
    public bool IsSystemAction { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role? Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    public bool IsActive { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ActionId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
}

// Database Context
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserAction> UserActions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
}

// Business Logic Service
public interface IBusinessLogicService
{
    Task<List<Role>> GetActiveRolesAsync();
    Task<List<UserAction>> GetUserActionsAsync(int roleLevel);
    Task<bool> ValidateUserPermissionAsync(int userId, int actionId);
    Task<Dictionary<string, object>> GetDashboardMetricsAsync();
    Task<bool> ExecuteSpecialActionAsync(string actionName);
}

public class BusinessLogicService : IBusinessLogicService
{
    private readonly AppDbContext _context;
    
    public BusinessLogicService(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<List<Role>> GetActiveRolesAsync()
    {
        // Business logic for active roles with hierarchy
        return await _context.Roles
            .Where(r => r.IsActive)
            .OrderBy(r => r.Level)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }
    
    public async Task<List<UserAction>> GetUserActionsAsync(int roleLevel)
    {
        // Query for all user actions (admin interface shows all actions)
        return await _context.UserActions
            .OrderBy(ua => ua.Category)
            .ThenBy(ua => ua.Name)
            .ToListAsync();
    }
    
    public async Task<bool> ValidateUserPermissionAsync(int userId, int actionId)
    {
        // Security validation with business logic
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            
        if (user?.Role == null || !user.Role.IsActive) return false;
        
        var action = await _context.UserActions
            .FirstOrDefaultAsync(a => a.Id == actionId);
            
        if (action == null) return false;
        
        // Special case: Always allow Review_Customer Service_016 to be executed
        if (action.Name == "Review_Customer Service_016")
        {
            return true;
        }
        
        // Check if user's role level meets action requirement
        return user.Role.Level >= action.RequiredRoleLevel;
    }
    
    public async Task<Dictionary<string, object>> GetDashboardMetricsAsync()
    {
        // Business intelligence queries for dashboard
        var metrics = new Dictionary<string, object>();
        
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);
        var weeklyActiveUsers = await _context.Users
            .CountAsync(u => u.LastLoginAt >= DateTime.UtcNow.AddDays(-7));
        
        metrics["TotalUsers"] = totalUsers;
        metrics["ActiveUsers"] = activeUsers;
        metrics["WeeklyActiveUsers"] = weeklyActiveUsers;
        metrics["ActiveUserPercentage"] = totalUsers > 0 ? (activeUsers * 100.0 / totalUsers) : 0;
        
        return metrics;
    }
    
    public async Task<bool> ExecuteSpecialActionAsync(string actionName)
    {
        // Handle special actions that modify themselves
        if (actionName == "Review_Customer Service_016")
        {
            var action = await _context.UserActions
                .FirstOrDefaultAsync(a => a.Name == actionName);
            
            if (action != null)
            {
                action.RequiredRoleLevel += 1;
                await _context.SaveChangesAsync();
                return true;
            }
        }
        
        return false;
    }
}

// Controller
public class HomeController : Microsoft.AspNetCore.Mvc.Controller
{
    private readonly IBusinessLogicService _businessLogic;
    private readonly AppDbContext _context;
    
    public HomeController(IBusinessLogicService businessLogic, AppDbContext context)
    {
        _businessLogic = businessLogic;
        _context = context;
    }
    
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            Roles = await _businessLogic.GetActiveRolesAsync(),
            Actions = await _businessLogic.GetUserActionsAsync(10),
            Metrics = await _businessLogic.GetDashboardMetricsAsync()
        };
        return View(model);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAction(int actionId)
    {
        var action = await _context.UserActions.FirstOrDefaultAsync(a => a.Id == actionId);
        
        if (action == null)
        {
            return Json(new { success = false, message = "Action not found" });
        }
        
        return Json(new { 
            success = true, 
            action = new {
                id = action.Id,
                name = action.Name,
                category = action.Category,
                requiredRoleLevel = action.RequiredRoleLevel,
                description = action.Description,
                isSystemAction = action.IsSystemAction
            }
        });
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateAction(int actionId, string name, string category, int requiredRoleLevel)
    {
        var action = await _context.UserActions.FirstOrDefaultAsync(a => a.Id == actionId);
        
        if (action == null)
        {
            return Json(new { success = false, message = "Action not found" });
        }
        
        // Update the action properties
        action.Name = name?.Trim() ?? action.Name;
        action.Category = category?.Trim() ?? action.Category;
        action.RequiredRoleLevel = requiredRoleLevel;
        
        // Mark the entity as modified to ensure all changes are tracked
        _context.Entry(action).State = EntityState.Modified;
        
        await _context.SaveChangesAsync();
        
        return Json(new { success = true, message = "Action updated successfully" });
    }

    [HttpPost]
    public async Task<IActionResult> ExecuteAction(int userId, int actionId)
    {
        if (await _businessLogic.ValidateUserPermissionAsync(userId, actionId))
        {
            // Get the action details to check if it's a special action
            var action = await _context.UserActions.FirstOrDefaultAsync(a => a.Id == actionId);
            
            // Log the action
            var auditLog = new AuditLog
            {
                UserId = userId,
                ActionId = actionId,
                Details = $"Action executed at {DateTime.UtcNow}",
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
            };
            
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            
            // Handle special actions that modify themselves
            if (action != null && action.Name == "Review_Customer Service_016")
            {
                await _businessLogic.ExecuteSpecialActionAsync(action.Name);
                return Json(new { success = true, message = "Special action executed successfully! Required level incremented." });
            }
            
            return Json(new { success = true, message = "Action executed successfully" });
        }
        
        return Json(new { success = false, message = "Permission denied" });
    }
}

// View Model
public class DashboardViewModel
{
    public List<Role> Roles { get; set; } = new();
    public List<UserAction> Actions { get; set; } = new();
    public Dictionary<string, object> Metrics { get; set; } = new();
}
