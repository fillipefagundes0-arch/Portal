using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Portal;
var root = Path.Combine(Path.GetTempPath(), "portal-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root, "attachments"));
try {
 var options = new DbContextOptionsBuilder<PortalDb>().UseSqlite($"Data Source={root}/test.db").Options;
 var factory = new Factory(options); var auth = new Identity();
 var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["StoragePath"] = root }).Build();
 var service = new PortalService(factory, auth, config, new Host(root));
 await using (var db = factory.CreateDbContext()) {
  await db.Database.EnsureCreatedAsync();
  db.Users.AddRange(new PortalUser { Key = "TEST\\ADMIN", Profile = AccessProfile.Automacao, Administrator = true }, new PortalUser { Key = "TEST\\PLAN", Profile = AccessProfile.Planejamento }, new PortalUser { Key = "TEST\\VIEW", Profile = AccessProfile.Visualizacao });
  await db.SaveChangesAsync();
 }
 int passed = 0;
 async Task Check(string name, Func<Task> action) { await action(); passed++; Console.WriteLine("PASS " + name); }
 async Task Denied(Func<Task> action) { try { await action(); } catch (UnauthorizedAccessException) { return; } throw new Exception("Expected authorization denial"); }
 async Task Invalid(Func<Task> action) { try { await action(); } catch (System.ComponentModel.DataAnnotations.ValidationException) { return; } throw new Exception("Expected validation error"); }
 auth.Key = "TEST\\ADMIN";
 var item = new Instrument { Tag = "pt-101", Description = "Pressão da linha", Area = "Utilidades" }; int id = 0;
 await Check("instrument creation and normalization", async () => { id = await service.Save(item); if ((await service.Get(id)).Tag != "PT-101") throw new Exception(); });
 await Check("duplicate TAG rejected", () => Invalid(async () => { await service.Save(new Instrument { Tag = "PT-101", Description = "Duplicado" }); }));
 await Check("alarm persistence and search by alarm", async () => { var i = await service.Get(id); i.Alarms.Add(new() { Code = "PAHH", Description = "Pressão muito alta", Causes = "Obstrução", Effects = "Parada", Action = "Inspecionar linha" }); await service.Save(i, true); i = await service.Get(id); i.Alarms[0].Action = "Procedimento revisado"; await service.Save(i, true); if ((await service.Get(id)).Alarms[0].Action != "Procedimento revisado") throw new Exception(); if ((await service.Search("PAHH")).Count != 1 || (await service.Get(id)).Alarms[0].Effects != "Parada") throw new Exception(); });
 await Check("alarm codes unique", async () => { var i = await service.Get(id); i.Alarms.Add(new() { Code = "pahh", Description = "Repetido" }); await Invalid(async () => { await service.Save(i, true); }); });
 await Check("stale update rejected", async () => { var a = await service.Get(id); var b = await service.Get(id); a.Description = "Atualizado"; await service.Save(a); await Invalid(async () => { await service.Save(b); }); });
 auth.Key = "TEST\\PLAN";
 await Check("planning may edit instrument", async () => { var i = await service.Get(id); i.Location = "Setor A"; await service.Save(i); });
 await Check("planning cannot edit alarms", () => Denied(async () => { await service.Save(await service.Get(id), true); }));
 await Check("planning cannot manage users", () => Denied(async () => { await service.Users(); }));
 await Check("planning may upload and bytes persist", async () => { await service.Upload(id, new TestFile("manual.txt", "manual técnico")); var a = (await service.Get(id)).Attachments.Single(); if (await File.ReadAllTextAsync(Path.Combine(root, "attachments", a.StoredName)) != "manual técnico") throw new Exception(); });
 await Check("executable upload rejected", () => Invalid(() => service.Upload(id, new TestFile("file.exe", "binary"))));
 auth.Key = "TEST\\VIEW";
 await Check("viewer may query", async () => { if ((await service.Search("PT-101")).Count != 1) throw new Exception(); });
 await Check("viewer cannot save", () => Denied(async () => { await service.Save(await service.Get(id)); }));
 await Check("viewer cannot upload", () => Denied(() => service.Upload(id, new TestFile("test.txt", "data"))));
 auth.Key = "TEST\\ADMIN";
 await Check("admin may disable user", async () => { await service.SaveUser(new() { Key = "TEST\\VIEW", Active = false }); });
 await Check("admin cannot revoke own administration", () => Invalid(() => service.SaveUser(new() { Key = "TEST\\ADMIN", Administrator = false })));
 auth.Key = "TEST\\VIEW";
 await Check("disabled user denied", () => Denied(async () => { await service.Search(""); }));
 auth.Key = "TEST\\UNKNOWN";
 await Check("unregistered identity denied", () => Denied(async () => { await service.Search(""); }));
 auth.Key = null;
 await Check("anonymous denied", () => Denied(async () => { await service.Search(""); }));
 auth.Key = "TEST\\ADMIN";
 await Check("audit recorded", async () => { if ((await service.History()).Count < 5) throw new Exception(); });
 Console.WriteLine($"{passed} passed, 0 failed");
} finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
sealed class Factory(DbContextOptions<PortalDb> options) : IDbContextFactory<PortalDb> { public PortalDb CreateDbContext() => new(options); }
sealed class Identity : AuthenticationStateProvider {
 public string? Key { get; set; }
 public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(Key is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Name, Key)], "test"))));
}
sealed class Host(string root) : IWebHostEnvironment {
 public string ApplicationName { get; set; } = "Portal"; public string EnvironmentName { get; set; } = "Development";
 public string ContentRootPath { get; set; } = root; public string WebRootPath { get; set; } = root;
 public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
sealed class TestFile(string name, string contents) : IBrowserFile {
 public string Name => name; public DateTimeOffset LastModified => DateTimeOffset.UtcNow; public long Size => System.Text.Encoding.UTF8.GetByteCount(contents); public string ContentType => "text/plain";
 public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(contents));
}
