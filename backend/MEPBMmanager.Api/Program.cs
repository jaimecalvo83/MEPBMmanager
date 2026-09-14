using System.Text;
using MEPBMmanager.Api.Services;
using MEPBMmanager.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<MepbmDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.MapInboundClaims = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<TurnProcessor>();
builder.Services.AddScoped<CombatResolver>();
builder.Services.AddScoped<TurnReportService>();

// CORS for React frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

for (int attempt = 1; attempt <= 10; attempt++)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MepbmDbContext>();
            await db.Database.MigrateAsync();
            
            var gameTypeCount = await db.GameTypes.CountAsync();
            Console.WriteLine($"GameTypes in DB: {gameTypeCount}");
            
            await DbInitializer.SeedAsync(db);
            Console.WriteLine("Seed completed successfully");
            
            var ntCount = await db.NationTemplates.CountAsync();
            Console.WriteLine($"NationTemplates in DB: {ntCount}");
        }
        break;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DB init attempt {attempt} failed: {ex.InnerException?.Message ?? ex.Message}");
        if (attempt == 10) throw;
        await Task.Delay(3000);
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
