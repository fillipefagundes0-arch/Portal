using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.EntityFrameworkCore;
using Portal;
using Portal.Components;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(IISDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();
builder.Services.AddDbContextFactory<PortalDb>(o => {
 if (builder.Environment.IsDevelopment() && builder.Configuration["DatabaseProvider"] == "Sqlite")
  o.UseSqlite(builder.Configuration.GetConnectionString("Portal"));
 else o.UseSqlServer(builder.Configuration.GetConnectionString("Portal"));
});
builder.Services.AddScoped<PortalService>();
var app = builder.Build();
var storage = Path.GetFullPath(builder.Configuration["StoragePath"] ?? "App_Data", app.Environment.ContentRootPath);
Directory.CreateDirectory(storage);
Directory.CreateDirectory(Path.Combine(storage, "attachments"));
using (var scope = app.Services.CreateScope()) {
 var db = scope.ServiceProvider.GetRequiredService<PortalDb>();
 await db.Database.EnsureCreatedAsync();
 var key = PortalService.Normalize(builder.Configuration["BootstrapAdmin"] ?? "");
 if (key.Length > 0 && !await db.Users.AnyAsync()) {
  db.Users.Add(new PortalUser { Key = key, Profile = AccessProfile.Automacao, Administrator = true });
  db.Audit.Add(new AuditEntry { User = "setup", Action = $"Administrador inicial: {key}" });
  await db.SaveChangesAsync();
 }
}
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/erro", createScopeForErrors: true); app.UseHsts(); }
app.UseStaticFiles();
app.UseAuthentication();
// Local test identity is unavailable in Production and cannot be selected by a request header.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("LocalDevelopmentIdentity")) {
 app.Use(async (context, next) => {
  if (context.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip))
   context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, app.Configuration["BootstrapAdmin"] ?? "")], "LocalDevelopment"));
  await next();
 });
}
app.UseAuthorization();
app.UseAntiforgery();
app.MapGet("/anexos/{id:int}", async (int id, HttpContext http, IDbContextFactory<PortalDb> factory) => {
 await using var db = await factory.CreateDbContextAsync();
 var key = PortalService.Normalize(http.User.Identity?.Name ?? "");
 if (http.User.Identity?.IsAuthenticated != true || !await db.Users.AnyAsync(x => x.Key == key && x.Active)) return Results.StatusCode(403);
 var item = await db.Attachments.FindAsync(id);
 if (item is null) return Results.NotFound();
 var path = Path.Combine(storage, "attachments", item.StoredName);
 if (!File.Exists(path)) return Results.NotFound();
 http.Response.Headers.XContentTypeOptions = "nosniff";
 return Results.File(path, "application/octet-stream", item.FileName);
});
app.MapGet("/health", () => Results.Ok(new { status = "running" }));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
