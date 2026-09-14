using MEPBMmanager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MEPBMmanager.Infrastructure.Data;

public class MepbmDbContext : DbContext
{
    public MepbmDbContext(DbContextOptions<MepbmDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Turn> Turns => Set<Turn>();
    public DbSet<Nation> Nations => Set<Nation>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Army> Armies => Set<Army>();
    public DbSet<Navy> Navies => Set<Navy>();
    public DbSet<PopulationCentre> PopulationCentres => Set<PopulationCentre>();
    public DbSet<HexTile> HexTiles => Set<HexTile>();
    public DbSet<NationRelation> NationRelations => Set<NationRelation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Spell> Spells => Set<Spell>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Guard> Guards => Set<Guard>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<GameEvent> GameEvents => Set<GameEvent>();
    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<TurnResult> TurnResults => Set<TurnResult>();
    public DbSet<GameType> GameTypes => Set<GameType>();
    public DbSet<NationTemplate> NationTemplates => Set<NationTemplate>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<GameAdmin> GameAdmins => Set<GameAdmin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
        });

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.Username).IsUnique();
            e.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        });

        // Game
        modelBuilder.Entity<Game>(e =>
        {
            e.HasKey(g => g.Id);
            e.HasOne(g => g.GameType).WithMany(gt => gt.Games).HasForeignKey(g => g.GameTypeId);
        });

        // Player
        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.UserId, p.GameId }).IsUnique();
            e.HasOne(p => p.User).WithMany(u => u.Players).HasForeignKey(p => p.UserId);
            e.HasOne(p => p.Game).WithMany(g => g.Players).HasForeignKey(p => p.GameId);
            e.HasOne(p => p.Nation).WithMany(n => n.Players).HasForeignKey(p => p.NationId);
            e.HasOne(p => p.WantsToPlayWith).WithMany().HasForeignKey(p => p.WantsToPlayWithUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Turn
        modelBuilder.Entity<Turn>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.GameId, t.Number }).IsUnique();
            e.HasOne(t => t.Game).WithMany(g => g.Turns).HasForeignKey(t => t.GameId);
        });

        // Nation
        modelBuilder.Entity<Nation>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasIndex(n => new { n.GameId, n.Name }).IsUnique();
            e.HasOne(n => n.Game).WithMany(g => g.Nations).HasForeignKey(n => n.GameId);
        });

        // Character
        modelBuilder.Entity<Character>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Nation).WithMany(n => n.Characters).HasForeignKey(c => c.NationId);
            e.HasOne(c => c.Company).WithMany(co => co.Characters).HasForeignKey(c => c.CompanyId);
            e.HasOne(c => c.Army).WithMany(a => a.Characters).HasForeignKey(c => c.ArmyId);
        });

        // Company
        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasOne(c => c.Nation).WithMany(n => n.Companies).HasForeignKey(c => c.NationId);
        });

        // Army
        modelBuilder.Entity<Army>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasOne(a => a.Nation).WithMany(n => n.Armies).HasForeignKey(a => a.NationId);
        });

        // Navy
        modelBuilder.Entity<Navy>(e =>
        {
            e.HasKey(n => n.Id);
            e.HasOne(n => n.Nation).WithMany(na => na.Navies).HasForeignKey(n => n.NationId);
        });

        // PopulationCentre
        modelBuilder.Entity<PopulationCentre>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasOne(p => p.Nation).WithMany(n => n.PopulationCentres).HasForeignKey(p => p.NationId);
        });

        // HexTile
        modelBuilder.Entity<HexTile>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.HasBridge).HasDefaultValue(false);
            e.Property(h => h.HasFord).HasDefaultValue(false);
            e.Property(h => h.HasMajorRiver).HasDefaultValue(false);
            e.Property(h => h.HasMinorRiver).HasDefaultValue(false);
            e.Property(h => h.HasRoad).HasDefaultValue(false);
            e.HasIndex(h => new { h.GameId, h.Q, h.R }).IsUnique();
            e.HasOne(h => h.Game).WithMany(g => g.HexTiles).HasForeignKey(h => h.GameId);
            e.HasOne(h => h.GameType).WithMany(gt => gt.HexTiles).HasForeignKey(h => h.GameTypeId);
        });

        // GameType
        modelBuilder.Entity<GameType>(e =>
        {
            e.HasKey(gt => gt.Id);
            e.HasIndex(gt => gt.Code).IsUnique();
        });

        // NationTemplate
        modelBuilder.Entity<NationTemplate>(e =>
        {
            e.HasKey(nt => nt.Id);
            e.HasIndex(nt => new { nt.GameTypeId, nt.Name }).IsUnique();
            e.HasOne(nt => nt.GameType).WithMany(gt => gt.Templates).HasForeignKey(nt => nt.GameTypeId);
        });

        // NationRelation
        modelBuilder.Entity<NationRelation>(e =>
        {
            e.HasKey(nr => nr.Id);
            e.HasIndex(nr => new { nr.NationId, nr.TargetNationId }).IsUnique();
            e.HasOne(nr => nr.Nation).WithMany(n => n.Relations).HasForeignKey(nr => nr.NationId);
            e.HasOne(nr => nr.TargetNation).WithMany(n => n.RelationsTo).HasForeignKey(nr => nr.TargetNationId);
        });

        // Order
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasOne(o => o.Game).WithMany(g => g.Orders).HasForeignKey(o => o.GameId);
            e.HasOne(o => o.Turn).WithMany(t => t.Orders).HasForeignKey(o => o.TurnId);
            e.HasOne(o => o.Nation).WithMany(n => n.Orders).HasForeignKey(o => o.NationId);
            e.HasOne(o => o.Character).WithMany(c => c.Orders).HasForeignKey(o => o.CharacterId);
            e.HasOne(o => o.Army).WithMany(a => a.Orders).HasForeignKey(o => o.ArmyId);
        });

        // Spell
        modelBuilder.Entity<Spell>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.Character).WithMany(c => c.Spells).HasForeignKey(s => s.CharacterId);
        });

        // Artifact
        modelBuilder.Entity<Artifact>(e =>
        {
            e.HasKey(a => a.Id);
        });

        // Guard
        modelBuilder.Entity<Guard>(e =>
        {
            e.HasKey(g => g.Id);
            e.HasOne(g => g.Character).WithMany(c => c.GuardedBy).HasForeignKey(g => g.CharacterId);
        });

        // Message
        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.Game).WithMany(g => g.Messages).HasForeignKey(m => m.GameId);
            e.HasOne(m => m.Sender).WithMany(n => n.Messages).HasForeignKey(m => m.SenderId);
        });

        // GameEvent
        modelBuilder.Entity<GameEvent>(e =>
        {
            e.HasKey(ge => ge.Id);
            e.HasOne(ge => ge.Game).WithMany(g => g.Events).HasForeignKey(ge => ge.GameId);
            e.HasOne(ge => ge.Turn).WithMany(t => t.Events).HasForeignKey(ge => ge.TurnId);
        });

        // MarketPrice
        modelBuilder.Entity<MarketPrice>(e =>
        {
            e.HasKey(mp => mp.Id);
            e.HasIndex(mp => new { mp.GameId, mp.Good }).IsUnique();
            e.HasOne(mp => mp.Game).WithMany(g => g.MarketPrices).HasForeignKey(mp => mp.GameId);
        });

        // TurnResult
        modelBuilder.Entity<TurnResult>(e =>
        {
            e.HasKey(tr => tr.Id);
            e.HasOne(tr => tr.Turn).WithMany(t => t.Results).HasForeignKey(tr => tr.TurnId);
        });

        // Encounter
        modelBuilder.Entity<Encounter>(e =>
        {
            e.HasKey(en => en.Id);
            e.HasOne(en => en.Game).WithMany(g => g.Encounters).HasForeignKey(en => en.GameId);
            e.HasOne(en => en.Character).WithMany(c => c.Encounters).HasForeignKey(en => en.CharacterId);
            e.HasOne(en => en.Army).WithMany(a => a.Encounters).HasForeignKey(en => en.ArmyId);
        });

        // GameAdmin
        modelBuilder.Entity<GameAdmin>(e =>
        {
            e.HasKey(ga => ga.Id);
            e.HasIndex(ga => new { ga.GameId, ga.UserId }).IsUnique();
            e.HasOne(ga => ga.Game).WithMany(g => g.GameAdmins).HasForeignKey(ga => ga.GameId);
            e.HasOne(ga => ga.User).WithMany().HasForeignKey(ga => ga.UserId);
        });
    }
}
