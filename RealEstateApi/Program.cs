using Microsoft.EntityFrameworkCore;
using RealEstateApi.Data;
using RealEstateApi.Services;
using RealEstateApi.Endpoints;
using RealEstateApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Serilog;
using Serilog.Sinks.Elasticsearch;

var builder = WebApplication.CreateBuilder(args);

LoadDotEnv(builder.Environment.ContentRootPath);
builder.Configuration.AddEnvironmentVariables();

var elasticUri = builder.Configuration["ElasticSearch:Uri"] ?? "http://localhost:9200"; // Defaultne, protoze lokalne jiz bezi Elasticsearch!!!!

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "RealEstateApi")
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
    {
        AutoRegisterTemplate = true,
        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
        IndexFormat = $"realestate-logs-{DateTime.UtcNow:yyyy-MM}",
        NumberOfShards = 1,
        NumberOfReplicas = 0
    })
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<RealEstateDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<PropertyService>();
builder.Services.AddScoped<TenantContractService>();
builder.Services.AddScoped<PaymentService>();

var jwtSettings = builder.Configuration.GetSection("JWT");
var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]?.Trim() ?? throw new InvalidOperationException("JWT SecretKey is not configured."));
const string AuthCookieName = "realestate_auth";
var adminPassword = builder.Configuration["Auth:AdminPassword"]?.Trim();
var managerPassword = builder.Configuration["Auth:ManagerPassword"]?.Trim();

if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(managerPassword))
{
    throw new InvalidOperationException("Auth passwords are not configured. Set Auth:AdminPassword and Auth:ManagerPassword via environment variables.");
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
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token)
                && context.Request.Cookies.TryGetValue(AuthCookieName, out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("WriteAccess", policy => policy.RequireRole("admin", "manager"));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAuthenticated = context.User.Identity?.IsAuthenticated == true;

    if ((path == "/" || path == "/index.html" || path == "/create.html") && !isAuthenticated)
    {
        context.Response.Redirect("/login.html");
        return;
    }

    if (path == "/login.html" && isAuthenticated)
    {
        context.Response.Redirect("/index.html");
        return;
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/auth/login", (LoginRequest request, HttpContext httpContext) =>
{
    var username = request.Username?.Trim() ?? string.Empty;
    var password = request.Password ?? string.Empty;

    var user = username.ToLowerInvariant() switch
    {
        "admin" when password == adminPassword => (Id: "1", Username: "admin", Role: "admin"),
        "manager" when password == managerPassword => (Id: "2", Username: "manager", Role: "manager"),
        _ => ((string Id, string Username, string Role)?)null
    };

    if (!user.HasValue)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized",
            detail: "Invalid username or password.",
            extensions: new Dictionary<string, object?> { ["code"] = "invalid_credentials" });
    }

    var authenticatedUser = user.Value;

    var expirationMinutes = jwtSettings.GetValue<int?>("ExpirationMinutes") ?? 60;
    var expiresAtUtc = DateTime.UtcNow.AddMinutes(expirationMinutes);

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, authenticatedUser.Id),
        new Claim(ClaimTypes.NameIdentifier, authenticatedUser.Id),
        new Claim(ClaimTypes.Name, authenticatedUser.Username),
        new Claim(ClaimTypes.Role, authenticatedUser.Role),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
        issuer: jwtSettings["Issuer"],
        audience: jwtSettings["Audience"],
        claims: claims,
        expires: expiresAtUtc,
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));

    var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

    httpContext.Response.Cookies.Append(AuthCookieName, accessToken, new CookieOptions
    {
        Path = "/",
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Secure = httpContext.Request.IsHttps,
        Expires = expiresAtUtc
    });

    return Results.Ok(new
    {
        accessToken,
        tokenType = "Bearer",
        expiresAtUtc,
        user = new { id = authenticatedUser.Id, username = authenticatedUser.Username, role = authenticatedUser.Role }
    });
})
.AllowAnonymous();

app.MapPost("/auth/logout", (HttpContext httpContext) =>
{
    httpContext.Response.Cookies.Delete(AuthCookieName, new CookieOptions
    {
        Path = "/",
        SameSite = SameSiteMode.Strict,
        Secure = httpContext.Request.IsHttps
    });

    return Results.NoContent();
})
.AllowAnonymous();

app.MapGet("/auth/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        username = user.Identity?.Name,
        role = user.FindFirstValue(ClaimTypes.Role)
    });
})
.RequireAuthorization();

PropertyEndpoints.MapEndpoints(app);
TenantContractEndpoints.MapEndpoints(app);
PaymentEndpoints.MapEndpoints(app);

app.MapFallback(() => Results.NotFound());

app.Run();

static void LoadDotEnv(string contentRootPath)
{
    var envFilePath = Path.Combine(contentRootPath, ".env");
    if (!File.Exists(envFilePath))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envFilePath))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();

        if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
        {
            value = value[1..^1];
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

public partial class Program
{
}