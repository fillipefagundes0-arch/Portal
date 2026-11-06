using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
namespace Portal;
public enum AccessProfile { Visualizacao, Planejamento, Automacao }
public class PortalUser {
 public int Id { get; set; }
 [Required, MaxLength(150)] public string Key { get; set; } = "";
 public AccessProfile Profile { get; set; }
 public bool Active { get; set; } = true;
 public bool Administrator { get; set; }
}
public class Instrument {
 public int Id { get; set; }
 [Required, MaxLength(80)] public string Tag { get; set; } = "";
 [Required, MaxLength(300)] public string Description { get; set; } = "";
 [MaxLength(150)] public string Area { get; set; } = "";
 [MaxLength(200)] public string Location { get; set; } = "";
 [MaxLength(100)] public string Manufacturer { get; set; } = "";
 [MaxLength(100)] public string Model { get; set; } = "";
 [MaxLength(100)] public string SerialNumber { get; set; } = "";
 [MaxLength(100)] public string Range { get; set; } = "";
 [MaxLength(40)] public string Unit { get; set; } = "";
 public List<Alarm> Alarms { get; set; } = [];
 public List<Attachment> Attachments { get; set; } = [];
 public DateTime UpdatedAt { get; set; }
 public string UpdatedBy { get; set; } = "";
 [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
}
public class Alarm {
 public int Id { get; set; }
 public int InstrumentId { get; set; }
 [Required, MaxLength(80)] public string Code { get; set; } = "";
 [Required, MaxLength(300)] public string Description { get; set; } = "";
 public string Priority { get; set; } = "Média";
 [MaxLength(80)] public string Setpoint { get; set; } = "";
 [MaxLength(40)] public string Unit { get; set; } = "";
 [MaxLength(4000)] public string Causes { get; set; } = "";
 [MaxLength(4000)] public string Effects { get; set; } = "";
 [MaxLength(4000)] public string Action { get; set; } = "";
}
public class Attachment {
 public int Id { get; set; }
 public int InstrumentId { get; set; }
 public string FileName { get; set; } = "";
 public string StoredName { get; set; } = "";
 public long Size { get; set; }
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class AuditEntry {
 public int Id { get; set; }
 public DateTime At { get; set; } = DateTime.UtcNow;
 public string User { get; set; } = "";
 public string Action { get; set; } = "";
}
public class PortalDb(DbContextOptions<PortalDb> options) : DbContext(options) {
 public DbSet<Instrument> Instruments => Set<Instrument>();
 public DbSet<PortalUser> Users => Set<PortalUser>();
 public DbSet<Attachment> Attachments => Set<Attachment>();
 public DbSet<AuditEntry> Audit => Set<AuditEntry>();
 protected override void OnModelCreating(ModelBuilder b) {
  b.Entity<Instrument>().HasIndex(x => x.Tag).IsUnique();
  b.Entity<PortalUser>().HasIndex(x => x.Key).IsUnique();
  b.Entity<Alarm>().HasIndex(x => new { x.InstrumentId, x.Code }).IsUnique();
 }
}
