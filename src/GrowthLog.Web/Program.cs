using GrowthLog.Web.Components;
using GrowthLog.Web.Data;
using GrowthLog.Web.Data.Authorization;
using GrowthLog.Web.Data.Repositories;
using GrowthLog.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("GrowthLog")
    ?? "Data Source=growthlog.db";

// Identity uses EF Core internally (permitted by the design for Identity only).
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/access-denied";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Dapper-based data access for all GrowthLog application data.
builder.Services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
builder.Services.AddSingleton<MigrationRunner>();
builder.Services.AddScoped<FamilyRepository>();
builder.Services.AddScoped<PersonRepository>();
builder.Services.AddScoped<RelationshipRepository>();
builder.Services.AddScoped<FamilyMembershipRepository>();
builder.Services.AddScoped<PersonFamilyMembershipRepository>();
builder.Services.AddScoped<AuditRepository>();
builder.Services.AddScoped<MeasurementRepository>();
builder.Services.AddScoped<FamilyConnectionRepository>();
builder.Services.AddScoped<SharingPermissionRepository>();
builder.Services.AddScoped<InvitationRepository>();
builder.Services.AddScoped<FamilyAuthorizationService>();

var app = builder.Build();

// Apply EF Core Identity migrations and Dapper SQL migrations at startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();

    var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await runner.RunAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();
