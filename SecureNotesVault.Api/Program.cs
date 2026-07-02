using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecureNotesVault.Core;
using SecureNotesVault.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Register ApplicationDbContext with MySQL support using Pomelo
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // If no MySQL string is found, or if it uses the default template, fall back to SQLite
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("YOUR_LOCAL_PASSWORD"))
    {
        // Creates a lightweight database file 'secure_notes.db' right inside the container
        options.UseSqlite("Data Source=secure_notes.db",
            b => b.MigrationsAssembly("SecureNotesVault.Api"));
    }
    else
    {
        options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
            b => b.MigrationsAssembly("SecureNotesVault.Api"));
    }
});
// Register our cryptographically secure encryption service
builder.Services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();builder.Services.AddScoped<IAuthService, AuthService>();

// 3. Configure JWT Authentication Services
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtSecret = jwtSettings["Secret"] ?? "SuperSecretSecureNotesVaultKey2026!MustBeAtLeast32BytesLong"; // Default fallback secret for development
var key = Encoding.UTF8.GetBytes(jwtSecret);
if (string.IsNullOrEmpty(jwtSecret) || jwtSecret == "SuperSecretSecureNotesVaultKey2026!MustBeAtLeast32BytesLong")
{
    // In a real environment, you would throw an exception to block startup
    Console.WriteLine("⚠️ WARNING: Utilizing the default insecure fallback JWT Secret. Ensure an environment override is injected in production staging.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ClockSkew = TimeSpan.Zero // Instantly rejects expired tokens without waiting
    };
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication(); // Add this line to enable JWT authentication
app.UseAuthorization();

app.MapControllers();

// Automatically execute migrations and schema checks on container initialization
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // This safely runs the EF Core migration script onto SQLite or MySQL instantly
    await dbContext.Database.MigrateAsync();
}
app.Run();
