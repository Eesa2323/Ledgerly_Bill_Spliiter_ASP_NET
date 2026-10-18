using System.Text;
using BillSplitter;
using BillSplitter.Domain;
using BillSplitter.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// SQL Server owns the durable financial state; the connection string can be overridden by environment-specific config.
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
    // The direct-expense migration is already checked into source; do not stop startup on EF's snapshot warning.
    .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));
// Identity gives us password hashing and unique emails without exposing password handling in controllers.
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
}).AddEntityFrameworkStores<AppDbContext>();
// Access tokens are short-lived, while refresh tokens are stored as hashes by TokenService.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
// Controllers use this policy through [Authorize] on every protected resource.
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
// The currency provider is remote, so HttpClientFactory manages its connection lifetime.
builder.Services.AddHttpClient<CurrencyService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.CreateExpenseRequest>, BillSplitter.CreateExpenseValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.CreateDirectExpenseRequest>, BillSplitter.CreateDirectExpenseValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.CreateBudgetRequest>, BillSplitter.CreateBudgetValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.CreateGroupRequest>, BillSplitter.CreateGroupValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.RegisterRequest>, BillSplitter.RegisterValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<BillSplitter.Contracts.LoginRequest>, BillSplitter.LoginValidator>();

var app = builder.Build();

// Applying migrations at startup keeps a fresh local SQL Server instance easy to run.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();
    // Repair databases created before direct expenses if migration history says they are current but GroupId is still required.
    await database.Database.ExecuteSqlRawAsync("IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Expenses]') AND name = 'GroupId' AND is_nullable = 0) ALTER TABLE [dbo].[Expenses] ALTER COLUMN [GroupId] uniqueidentifier NULL;");
}

// Serve the dashboard from the same origin as the API, which keeps browser authentication simple.
app.UseDefaultFiles();
app.UseStaticFiles();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
