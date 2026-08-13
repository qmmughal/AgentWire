using System;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Application.Replay;
using AgentWire.Application.Security;
using AgentWire.Infrastructure.Auditing;
using AgentWire.Infrastructure.Auth;
using AgentWire.Infrastructure.Data;
using AgentWire.Infrastructure.Replay;
using AgentWire.Presentation.Auth;
using AgentWire.Presentation.Endpoints;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Configure SQLite Database
builder.Services.AddDbContext<AgentWireDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection") ?? "Data Source=agentwire.db"));

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

// --- Application services ---
builder.Services.AddScoped<IAuditLogWriter, AuditLogWriter>();
builder.Services.AddScoped<IUserProvisioningService, UserProvisioningService>();
builder.Services.AddSingleton<IJwtIssuer, JwtIssuer>();
builder.Services.AddSingleton<IPacketScanner, PacketScanner>();
builder.Services.AddScoped<ICurrentOrgAccessor, CurrentOrgAccessor>();
builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>();

// --- Auth ---
var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
});

authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

// Deferred to run against the fully-merged, DI-resolved IConfiguration (not the
// early `builder.Configuration` snapshot) - important for WebApplicationFactory-based
// tests, whose config overrides only land by the time the DI container is built.
// Also the reason JwtIssuer (which signs tokens) and this (which validates them) must
// resolve the signing key the same lazy way: eagerly resolving it here as a plain
// local variable would have baked in stale config and silently signed/validated with
// two different keys.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, ILoggerFactory>((options, config, loggerFactory) =>
    {
        // MapInboundClaims=false + explicit RoleClaimType/NameClaimType avoids the
        // well-known ASP.NET Core gotcha where the default inbound claim-type mapping
        // silently renames "role"/"sub" to legacy XML-schema URIs, breaking
        // [Authorize(Roles=...)] without any obvious error.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = JwtIssuer.Issuer(config),
            ValidateAudience = true,
            ValidAudience = JwtIssuer.Audience(config),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtIssuer.ResolveSecurityKey(config, loggerFactory.CreateLogger("AgentWire.Startup")),
            ValidateLifetime = true,
            RoleClaimType = JwtIssuer.RoleClaimType,
            NameClaimType = "sub"
        };
    });

authBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationOptions.SchemeName, _ => { });

authBuilder.AddCookie(AuthEndpoints.CookieSsoSchemeName, options =>
{
    options.Cookie.Name = "agentwire.sso";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

if (builder.Configuration.GetValue<bool>("Oidc:Enabled"))
{
    authBuilder.AddOpenIdConnect(AuthEndpoints.OidcSchemeName, options =>
    {
        options.SignInScheme = AuthEndpoints.CookieSsoSchemeName;
        options.Authority = builder.Configuration["Oidc:Authority"];
        options.ClientId = builder.Configuration["Oidc:ClientId"];
        options.ClientSecret = builder.Configuration["Oidc:ClientSecret"];
        options.CallbackPath = builder.Configuration["Oidc:CallbackPath"] ?? "/signin-oidc";
        options.ResponseType = "code";
        options.SaveTokens = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Registered unconditionally (like the JWT signing key) - SP metadata is always
// servable so an admin can hand it to their IdP before "enabling" anything; Login()
// itself checks whether an IdP has actually been configured and 404s cleanly if not.
// Built via a DI factory delegate (not eagerly here) for the same reason as the JWT
// signing key above - deferred until the fully-merged IConfiguration is available.
builder.Services.AddSingleton(sp => AgentWire.Presentation.Auth.Saml2ConfigurationFactory.Build(
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILoggerFactory>().CreateLogger("AgentWire.Startup")));

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowDashboard");
app.UseAuthentication();
app.UseAuthorization();

// Apply pending EF Core migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
    db.Database.Migrate();
}

app.MapSetupEndpoints();
app.MapAuthEndpoints();
app.MapPacketEndpoints();
app.MapAdminEndpoints();
app.MapControllers();

app.Run();

public partial class Program
{
}
