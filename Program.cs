using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Authentication.Cookies;
using InteractiveMapGame.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Configure Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login.html";
        options.LogoutPath = "/api/Admin/logout";
        options.AccessDeniedPath = "/admin/login.html";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

// Add Entity Framework
builder.Services.AddDbContext<MapGameDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        // Fallback to user secrets for development
        connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
    }
    options.UseSqlServer(connectionString);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "Interactive Map Game API", 
        Version = "v1",
        Description = "API for the Interactive Map Game with LLM integration and 360 video support"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Interactive Map Game API v1");
        c.RoutePrefix = "swagger";
    });
}

// Enable CORS
app.UseCors("AllowAll");

// Enable static files
app.UseDefaultFiles();

// Serve static files including GLB/GLTF/KTX2 with proper content types
var staticFileProvider = new FileExtensionContentTypeProvider();
staticFileProvider.Mappings[".glb"] = "model/gltf-binary";
staticFileProvider.Mappings[".gltf"] = "model/gltf+json";
staticFileProvider.Mappings[".ktx2"] = "image/ktx2";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileProvider
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

// Apply database schema updates on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MapGameDbContext>();
    try
    {
        // Update InteractionLogs columns to nvarchar(max) if not already
        // nvarchar(2000) has max_length = 4000, nvarchar(max) has max_length = -1
        dbContext.Database.ExecuteSqlRaw(@"
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InteractionLogs') AND name = 'LLMPrompt' AND max_length = 4000)
            BEGIN
                ALTER TABLE [InteractionLogs] ALTER COLUMN [LLMPrompt] nvarchar(max) NULL;
            END
            IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('InteractionLogs') AND name = 'LLMResponse' AND max_length = 4000)
            BEGIN
                ALTER TABLE [InteractionLogs] ALTER COLUMN [LLMResponse] nvarchar(max) NULL;
            END
        ");
    }
    catch (Exception ex)
    {
        // Log but don't fail startup if column doesn't exist or update fails
        Console.WriteLine($"Warning: Could not update database columns: {ex.Message}");
    }
}

app.Run();
