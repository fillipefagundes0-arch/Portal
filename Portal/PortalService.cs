using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
namespace Portal;
public class PortalService(IDbContextFactory<PortalDb> factory, AuthenticationStateProvider authentication, IConfiguration config, IWebHostEnvironment environment) {
 public static string Normalize(string key) => key.Trim().ToUpperInvariant();
 public async Task<PortalUser?> Current() {
  var identity = (await authentication.GetAuthenticationStateAsync()).User.Identity;
  if (identity?.IsAuthenticated != true) return null;
  var key = Normalize(identity.Name ?? "");
  await using var db = await factory.CreateDbContextAsync();
  return await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Key == key && x.Active);
 }
 async Task<PortalUser> Require(bool write = false, bool alarms = false, bool admin = false) {
  var user = await Current() ?? throw new UnauthorizedAccessException("Chave não autorizada ou desativada.");
  if ((write && user.Profile == AccessProfile.Visualizacao) || (alarms && user.Profile != AccessProfile.Automacao) || (admin && !user.Administrator))
   throw new UnauthorizedAccessException("Seu perfil não permite esta operação.");
  return user;
 }
 public async Task<List<Instrument>> Search(string query) {
  await Require(); await using var db = await factory.CreateDbContextAsync();
  query = query.Trim();
  return await db.Instruments.AsNoTracking().Where(x => x.Tag.Contains(query) || x.Description.Contains(query) || x.Area.Contains(query) || x.Alarms.Any(a => a.Code.Contains(query) || a.Description.Contains(query))).OrderBy(x => x.Tag).Take(200).ToListAsync();
 }
 public async Task<Instrument> Get(int id) {
  await Require(); await using var db = await factory.CreateDbContextAsync();
  return await db.Instruments.AsNoTracking().AsSplitQuery().Include(x => x.Alarms).Include(x => x.Attachments).SingleAsync(x => x.Id == id);
 }
 static void Validate(object value) => Validator.ValidateObject(value, new ValidationContext(value), true);
 public async Task<int> Save(Instrument input, bool alarmOnly = false) {
  var user = await Require(write: true, alarms: alarmOnly); Validate(input);
  await using var db = await factory.CreateDbContextAsync();
  Instrument entity;
  if (input.Id == 0) { if (alarmOnly) throw new ValidationException("Salve o instrumento primeiro."); entity = new(); db.Instruments.Add(entity); }
  else {
   entity = await db.Instruments.Include(x => x.Alarms).SingleAsync(x => x.Id == input.Id);
   if (entity.Revision != input.Revision) throw new ValidationException("Outro usuário alterou este cadastro. Reabra a ficha antes de salvar.");
  }
  if (!alarmOnly) {
   var tag = Normalize(input.Tag);
   if (await db.Instruments.AnyAsync(x => x.Tag == tag && x.Id != input.Id)) throw new ValidationException("Esta TAG já está cadastrada.");
   entity.Tag = tag; entity.Description = input.Description.Trim(); entity.Area = input.Area;
   entity.Location = input.Location; entity.Manufacturer = input.Manufacturer; entity.Model = input.Model;
   entity.SerialNumber = input.SerialNumber; entity.Range = input.Range; entity.Unit = input.Unit;
  } else {
   foreach (var alarm in input.Alarms) { alarm.Code = Normalize(alarm.Code); Validate(alarm); }
   if (input.Alarms.Select(x => x.Code).Distinct().Count() != input.Alarms.Count) throw new ValidationException("Os códigos de alarme devem ser únicos no instrumento.");
   db.RemoveRange(entity.Alarms); entity.Alarms = input.Alarms.Select(a => new Alarm { Code = a.Code, Description = a.Description, Priority = a.Priority, Setpoint = a.Setpoint, Unit = a.Unit, Causes = a.Causes, Effects = a.Effects, Action = a.Action }).ToList();
  }
  entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = user.Key; entity.Revision = Guid.NewGuid();
  db.Audit.Add(new AuditEntry { User = user.Key, Action = $"{(alarmOnly ? "Alarmes" : "Cadastro")}: {entity.Tag}" });
  await db.SaveChangesAsync(); return entity.Id;
 }
 public async Task Upload(int instrumentId, IBrowserFile file) {
  var user = await Require(write: true);
  var extension = Path.GetExtension(file.Name).ToLowerInvariant();
  if (!new[] { ".pdf", ".png", ".jpg", ".jpeg", ".docx", ".xlsx", ".txt" }.Contains(extension)) throw new ValidationException("Formato permitido: PDF, PNG, JPG, DOCX, XLSX ou TXT.");
  if (file.Size is <= 0 or > 20 * 1024 * 1024) throw new ValidationException("O arquivo deve ter até 20 MB e não pode estar vazio.");
  await using var db = await factory.CreateDbContextAsync();
  if (!await db.Instruments.AnyAsync(x => x.Id == instrumentId)) throw new ValidationException("Instrumento não encontrado.");
  var name = Guid.NewGuid().ToString("N") + extension;
  var root = Path.GetFullPath(config["StoragePath"] ?? "App_Data", environment.ContentRootPath);
  var path = Path.Combine(root, "attachments", name);
  try {
   await using (var output = new FileStream(path, FileMode.CreateNew)) await file.OpenReadStream(20 * 1024 * 1024).CopyToAsync(output);
   db.Attachments.Add(new Attachment { InstrumentId = instrumentId, FileName = Path.GetFileName(file.Name), StoredName = name, Size = file.Size });
   db.Audit.Add(new AuditEntry { User = user.Key, Action = $"Anexo no instrumento {instrumentId}: {Path.GetFileName(file.Name)}" });
   await db.SaveChangesAsync();
  } catch { if (File.Exists(path)) File.Delete(path); throw; }
 }
 public async Task<List<PortalUser>> Users() { await Require(admin: true); await using var db = await factory.CreateDbContextAsync(); return await db.Users.AsNoTracking().OrderBy(x => x.Key).ToListAsync(); }
 public async Task SaveUser(PortalUser input) {
  var actor = await Require(admin: true); Validate(input);
  if (!Enum.IsDefined(input.Profile)) throw new ValidationException("Perfil inválido.");
  input.Key = Normalize(input.Key);
  if (!input.Key.Contains('\\') || input.Key.Length > 150) throw new ValidationException("Informe a identidade completa: DOMÍNIO\\chave.");
  await using var db = await factory.CreateDbContextAsync();
  var current = await db.Users.SingleOrDefaultAsync(x => x.Key == input.Key);
  if (current?.Id == actor.Id && (!input.Active || !input.Administrator)) throw new ValidationException("Você não pode remover seu próprio acesso administrativo.");
  if (current is null) { current = new PortalUser { Key = input.Key }; db.Users.Add(current); }
  current.Active = input.Active; current.Profile = input.Profile; current.Administrator = input.Administrator;
  db.Audit.Add(new AuditEntry { User = actor.Key, Action = $"Permissões: {input.Key}; perfil={input.Profile}; ativo={input.Active}; administrador={input.Administrator}" });
  await db.SaveChangesAsync();
 }
 public async Task<List<AuditEntry>> History() { await Require(admin: true); await using var db = await factory.CreateDbContextAsync(); return await db.Audit.AsNoTracking().OrderByDescending(x => x.Id).Take(100).ToListAsync(); }
}
