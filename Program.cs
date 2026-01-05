using Microsoft.EntityFrameworkCore;
using DispatchApp.Server.data;
using DispatchApp.Server.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DispatchApp.Server.Services;


var builder = WebApplication.CreateBuilder(args);

// Load environment-specific configuration
// Priority: appsettings.json < appsettings.{Environment}.json < appsettings.Local.json < Environment Variables
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Configure Kestrel - only override in development
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        // HTTP for mobile app development (no certificate needed)
        serverOptions.ListenAnyIP(5062);
        // HTTPS for web app (optional)
        serverOptions.ListenAnyIP(7170, listenOptions =>
        {
            listenOptions.UseHttps();
        });
    });
}
// In production, use environment variables (ASPNETCORE_URLS) or hosting configuration for URLs

// Add services to the container
builder.Services.AddControllers(options =>
     {
         // Disable automatic model validation 400 responses
         options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
     })
     .AddJsonOptions(options =>
     {
         options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
     });

// Add CORS policy - configurable via appsettings
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new string[] { };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: allow localhost and local network
            policy.WithOrigins(
                    "https://localhost:56554",
                    "http://localhost:56554",
                    "http://localhost:5173",
                    "https://localhost:5173",
                    "http://192.168.1.41:8081",  // Expo dev server
                    "exp://192.168.1.41:8081")   // Expo app
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else if (allowedOrigins.Length > 0)
        {
            // Production: use configured origins from appsettings
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            // Fallback: restrict to same origin
            policy.AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });

    // Mobile policy - more permissive but still controlled
    options.AddPolicy("AllowMobile", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Production: mobile apps don't send Origin headers, so this works
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// Add other services...
builder.Services.AddSwaggerGen();

// Register JwtService
builder.Services.AddScoped<JwtService>();

// Register SquarePaymentService
builder.Services.AddScoped<SquarePaymentService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    // Configure JWT for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            // If the request is for our SignalR hub...
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/hubs/dispatch")))
            {
                // Read the token out of the query string
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment(); // Only in development
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

// Add detailed logging for SignalR only in development
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
    builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
}


// Register the DbContext with the connection string from configuration
builder.Services.AddDbContext<DispatchDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConStr")));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Enable HTTPS redirection in production
    app.UseHttpsRedirection();
}

// IMPORTANT: Add CORS before Authorization
app.UseCors("AllowReactApp");
app.UseCors("AllowMobile");

// Serve static files from wwwroot (for Square tokenizer HTML)
app.UseStaticFiles();

// Add Authentication before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<Dispatch>("/hubs/dispatch");

app.Run();