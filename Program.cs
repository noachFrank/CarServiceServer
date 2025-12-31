using Microsoft.EntityFrameworkCore;
using DispatchApp.Server.data;
using DispatchApp.Server.Hubs;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DispatchApp.Server.Services;


var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on HTTP for mobile development
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP for mobile app development (no certificate needed)
    serverOptions.ListenAnyIP(5062);
    Console.WriteLine("✅ Server listening on http://0.0.0.0:5062");

    // HTTPS for web app (optional)
    serverOptions.ListenAnyIP(7170, listenOptions =>
    {
        listenOptions.UseHttps();
    });
    Console.WriteLine("✅ Server listening on https://0.0.0.0:7170");
});

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

// Add CORS policy - Allow mobile apps and web apps
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
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
    });

    // More permissive policy for mobile development
    options.AddPolicy("AllowMobile", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
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

// Add SignalR with detailed logging
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

// Add logging for SignalR
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);


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

//app.UseHttpsRedirection();

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