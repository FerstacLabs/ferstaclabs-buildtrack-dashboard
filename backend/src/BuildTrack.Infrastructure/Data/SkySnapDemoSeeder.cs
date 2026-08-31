using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Data;

internal static class SkySnapDemoSeeder
{
    public static readonly Guid TenantId = Guid.Parse("5ec5ca9a-0000-4000-9000-000000000001");
    public const string TenantCode = "SKYSNAP-DEMO";
    public const string DefaultOwnerEmail = "tomasz.odrobinski@skysnap.pl";
    public const string DefaultOwnerName = "Tomasz Odrobiński";
    public const string DemoPasswordDocumentationValue = "SkySnapDemo!2026";
    private const string ProjectId = "skysnap-demo-project";
    private const string EstimateId = "skysnap-demo-estimate-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static async Task SeedAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken ct)
    {
        if (!ParseBool(configuration?["SEED_SKYSNAP_DEMO"])) return;

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await SeedInternalAsync(db, configuration, ct);
            await transaction.CommitAsync(ct);
            return;
        }

        await SeedInternalAsync(db, configuration, ct);
    }

    private static async Task SeedInternalAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken ct)
    {
        if (ParseBool(configuration?["SEED_SKYSNAP_DEMO_RESET"]))
        {
            var existing = await db.Tenants.FirstOrDefaultAsync(x => x.Code == TenantCode, ct);
            if (existing is not null) db.Tenants.Remove(existing);
            await db.SaveChangesAsync(ct);
        }

        var password = configuration?["SEED_SKYSNAP_DEMO_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = DemoPasswordDocumentationValue;
        }

        var tenant = await UpsertTenantAsync(db, ct);
        await UpsertLicenseAsync(db, tenant.Id, ct);
        await UpsertUserAsync(db, tenant.Id, configuration?["SEED_SKYSNAP_DEMO_EMAIL"] ?? DefaultOwnerEmail, DefaultOwnerName, BuildTrackUserRole.Owner, password, true, ct);
        var sites = await UpsertSitesAsync(db, tenant.Id, ct);
        await db.SaveChangesAsync(ct);

        await SeedCatalogAndWarehouseAsync(db, tenant.Id, ct);
        var supervisors = await UpsertSupervisorsAsync(db, tenant.Id, sites, password, ct);
        await UpsertProcurementUsersAsync(db, tenant.Id, password, ct);
        var workers = await UpsertWorkersAsync(db, tenant.Id, sites, ct);
        await db.SaveChangesAsync(ct);

        await UpsertFieldSmetaAndCanonicalProgressAsync(db, tenant.Id, sites, workers, ct);
        await UpsertAttendanceSeedAsync(db, tenant.Id, sites, workers, ct);
        await UpsertWarehouseWorkflowSeedAsync(db, tenant.Id, sites[0], supervisors[0].Id, ct);
        await UpsertDailyReportsAsync(db, tenant.Id, sites, supervisors, ct);
        await UpsertDevicesAsync(db, tenant.Id, sites, ct);
        await db.SaveChangesAsync(ct);
        await new ProjectProgressDailyReportSyncService(db).RecalculateApprovedDailyReportProgressAsync(tenant.Id, null, ct);
    }

    private static async Task<Tenant> UpsertTenantAsync(BuildTrackDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Code == TenantCode, ct);
        if (tenant is null)
        {
            tenant = new Tenant { Id = TenantId, Code = TenantCode, CompanyName = "SkySnap Construction Demo", Status = TenantStatus.Active };
            db.Tenants.Add(tenant);
            return tenant;
        }

        tenant.CompanyName = "SkySnap Construction Demo";
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        return tenant;
    }

    private static async Task UpsertLicenseAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var hash = $"skysnap-demo-{tenantId:N}";
        var license = await db.Licenses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LicenseKeyHash == hash, ct);
        if (license is null)
        {
            db.Licenses.Add(new License
            {
                TenantId = tenantId,
                LicenseKeyHash = hash,
                Plan = LicensePlan.Unlimited,
                Status = LicenseStatus.Active,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-14),
                ActivatedAt = DateTimeOffset.UtcNow,
                MaxProjects = null,
                MaxUsers = null,
                MaxCameras = null,
            });
            return;
        }

        license.Plan = LicensePlan.Unlimited;
        license.Status = LicenseStatus.Active;
        license.ExpiresAt = null;
        license.ActivatedAt ??= DateTimeOffset.UtcNow;
    }

    private static async Task<AppUser> UpsertUserAsync(BuildTrackDbContext db, Guid tenantId, string email, string fullName, BuildTrackUserRole role, string password, bool resetPassword, CancellationToken ct)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, ct);
        if (user is null)
        {
            user = new AppUser
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FullName = fullName,
                PasswordHash = BuildTrackPasswordHasher.HashPassword(password),
                Role = role,
                Status = BuildTrackUserStatus.Active,
            };
            db.Users.Add(user);
            return user;
        }

        if (user.TenantId != tenantId)
        {
            throw new InvalidOperationException($"Cannot seed SkySnap demo user '{normalizedEmail}' because that email already belongs to another tenant.");
        }

        user.FullName = fullName;
        user.Role = role;
        user.Status = BuildTrackUserStatus.Active;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (resetPassword) user.PasswordHash = BuildTrackPasswordHasher.HashPassword(password);
        return user;
    }

    private static async Task<IReadOnlyList<Site>> UpsertSitesAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var seeds = new[]
        {
            ("SKY-SITE-001", "Riverside Business Center - Phase 1", "Warsaw, Vistula Riverside"),
            ("SKY-SITE-002", "SkyView Residential Towers - Block A", "Krakow, Podgorze District"),
            ("SKY-SITE-003", "North Logistics Hub", "Gdansk, Port Logistics Zone"),
            ("SKY-SITE-004", "City Hospital Extension", "Poznan, Medical Campus"),
        };

        var result = new List<Site>();
        foreach (var (key, name, address) in seeds)
        {
            var id = StableGuid(key);
            var site = await db.Sites.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (site is null)
            {
                site = new Site { Id = id, TenantId = tenantId };
                db.Sites.Add(site);
            }

            site.Name = name;
            site.Address = address;
            site.TimeZone = "Europe/Warsaw";
            result.Add(site);
        }

        return result;
    }

    private static async Task SeedCatalogAndWarehouseAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var index = new DbInitializer.CatalogSeedIndex();
        foreach (var item in await db.FieldWarehouseCatalogItems.Where(x => x.TenantId == tenantId).ToListAsync(ct))
        {
            index.Track(item);
        }

        foreach (var seed in DbInitializer.SupplyCatalogSeedItems)
        {
            DbInitializer.UpsertCatalog(db, index, tenantId, seed);
        }

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault, ct);
        if (warehouse is null)
        {
            warehouse = new Warehouse
            {
                Id = StableGuid("SKYSNAP-DEMO-WAREHOUSE"),
                TenantId = tenantId,
                Name = "SkySnap Central Warehouse",
                Address = "Warsaw logistics support center",
                IsDefault = true,
                IsActive = true,
            };
            db.Warehouses.Add(warehouse);
        }

        foreach (var row in new (string Code, decimal Quantity, decimal Minimum)[]
        {
            ("MAT-CONCRETE-B25", 180, 40),
            ("MAT-CEMENT-M400", 720, 160),
            ("STEEL-REBAR-12", 26, 8),
            ("STEEL-REBAR-16", 18, 6),
            ("MASON-BRICK", 24000, 5000),
            ("ROOF-BITUMEN-MEMBRANE", 120, 25),
            ("FIN-TILE-ADHESIVE", 90, 120),
            ("PPE-HELMET", 88, 30),
            ("PPE-GLOVE", 240, 100),
            ("PPE-VEST", 95, 40),
            ("ELEC-CABLE-2-5", 1400, 350),
            ("CONS-DRILL-BIT-10", 18, 25),
        })
        {
            await SeedOpeningBalanceAsync(db, tenantId, warehouse.Id, row.Code, row.Quantity, row.Minimum, ct);
        }

        foreach (var (name, categories, email) in new[]
        {
            ("PolBuild Materials", "Concrete, cement, masonry", "orders@polbuild.example"),
            ("SteelWorks Warsaw", "Rebar and metal", "sales@steelworks.example"),
            ("SafeSite Supply", "PPE and consumables", "hello@safesite.example"),
            ("MEP Partner Poland", "Electrical and MEP", "supply@mep-partner.example"),
        })
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, ct);
            if (supplier is null)
            {
                supplier = new Supplier { TenantId = tenantId, Name = name };
                db.Suppliers.Add(supplier);
            }

            supplier.Categories = categories;
            supplier.Email = email;
            supplier.ContactPerson = "Demo account manager";
            supplier.Phone = "+48 22 000 000";
            supplier.Status = SupplierStatus.Active;
        }
    }

    private static async Task SeedOpeningBalanceAsync(BuildTrackDbContext db, Guid tenantId, Guid warehouseId, string itemCode, decimal quantity, decimal minimum, CancellationToken ct)
    {
        var item = await db.FieldWarehouseCatalogItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == itemCode, ct);
        if (item is null) return;
        item.MinimumStockLevel = minimum;
        if (await db.WarehouseStockMovements.AnyAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouseId && x.CatalogItemId == item.Id && x.ReferenceType == "SkySnapSeedOpeningBalance", ct)) return;

        db.WarehouseStockMovements.Add(new WarehouseStockMovement
        {
            TenantId = tenantId,
            WarehouseId = warehouseId,
            CatalogItemId = item.Id,
            MovementType = WarehouseStockMovementType.OpeningBalance,
            Quantity = quantity,
            ReferenceType = "SkySnapSeedOpeningBalance",
            ReferenceId = item.Id,
            Note = "SkySnap demo opening stock",
        });
    }

    private static async Task<IReadOnlyList<AppUser>> UpsertSupervisorsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, string password, CancellationToken ct)
    {
        var names = new[]
        {
            "Anna Kowalska", "Marek Nowak", "Piotr Zielinski", "Katarzyna Wisniewska", "Lukasz Kaminski",
            "Magdalena Lewandowska", "Tomasz Wozniak", "Karol Dabrowski", "Ewa Mazur", "Pawel Kaczmarek",
        };
        var result = new List<AppUser>();
        for (var i = 0; i < names.Length; i++)
        {
            var email = $"{names[i].ToLowerInvariant().Replace(" ", ".")}@skysnap-demo.pl";
            var user = await UpsertUserAsync(db, tenantId, email, names[i], BuildTrackUserRole.Supervisor, password, false, ct);
            result.Add(user);
            var site = sites[i % sites.Count];
            if (!await db.SupervisorSiteAssignments.AnyAsync(x => x.TenantId == tenantId && x.SupervisorUserId == user.Id && x.SiteId == site.Id && x.IsActive, ct))
            {
                db.SupervisorSiteAssignments.Add(new SupervisorSiteAssignment
                {
                    TenantId = tenantId,
                    SupervisorUserId = user.Id,
                    SiteId = site.Id,
                    IsActive = true,
                    Notes = "SkySnap demo field manager assignment",
                });
            }
        }

        return result;
    }

    private static async Task UpsertProcurementUsersAsync(BuildTrackDbContext db, Guid tenantId, string password, CancellationToken ct)
    {
        foreach (var (name, email) in new[] { ("Olivia Procurement", "procurement@skysnap-demo.pl"), ("Daniel Warehouse", "warehouse@skysnap-demo.pl") })
        {
            await UpsertUserAsync(db, tenantId, email, name, BuildTrackUserRole.ProcurementAgent, password, false, ct);
        }
    }

    private static async Task<IReadOnlyList<Worker>> UpsertWorkersAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, CancellationToken ct)
    {
        var crewNames = new[] { "Concrete Crew", "Rebar Crew", "Masonry Crew", "Finishing Crew", "Electrical & MEP Crew", "Logistics Crew" };
        var roles = new[] { "Concrete worker", "Steel fixer", "Mason", "Painter", "Electrician", "Logistics operator" };
        var firstNames = new[] { "Adam", "Bartosz", "Cezary", "Damian", "Emil", "Filip", "Grzegorz", "Hubert", "Igor", "Jan", "Kamil", "Leon", "Michal", "Norbert", "Oskar", "Patryk", "Robert", "Sebastian", "Wiktor", "Zbigniew", "Aleksandra", "Beata", "Celina", "Dorota" };
        var lastNames = new[] { "Kowal", "Nowak", "Lis", "Zielinski", "Mazur", "Lewandowski", "Kaminski", "Wojcik", "Sikora", "Baran", "Duda", "Krol" };
        var workers = new List<Worker>();
        for (var i = 0; i < 48; i++)
        {
            var site = sites[i % sites.Count];
            var code = $"SKY-W-{i + 1:000}";
            var worker = await db.Workers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ExternalWorkerCode == code, ct);
            if (worker is null)
            {
                worker = new Worker { TenantId = tenantId, SiteId = site.Id, ExternalWorkerCode = code };
                db.Workers.Add(worker);
            }

            worker.SiteId = site.Id;
            worker.FullName = $"{firstNames[i % firstNames.Length]} {lastNames[i % lastNames.Length]}";
            worker.Brigade = crewNames[i % crewNames.Length];
            worker.Role = roles[i % roles.Length];
            worker.HourlyRate = 16 + (i % 8) * 1.75m;
            worker.PlannedDailyHours = 8;
            worker.AttendanceSource = i % 4 == 0 ? "Camera" : "ForemanTablet";
            worker.RiskScore = (i * 7) % 35;
            worker.Status = WorkerStatus.Active;
            worker.Notes = "SkySnap English demo worker";
            worker.UpdatedAt = DateTimeOffset.UtcNow;
            workers.Add(worker);

            if (!await db.WorkerSiteAssignments.AnyAsync(x => x.TenantId == tenantId && x.WorkerId == worker.Id && x.SiteId == site.Id && x.Status == WorkerSiteAssignmentStatus.Active, ct))
            {
                db.WorkerSiteAssignments.Add(new WorkerSiteAssignment { TenantId = tenantId, WorkerId = worker.Id, SiteId = site.Id, Status = WorkerSiteAssignmentStatus.Active, IsPrimary = true });
            }
        }

        return workers;
    }

    private static async Task UpsertFieldSmetaAndCanonicalProgressAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == ProjectId, ct);
        if (project is null)
        {
            project = new ProjectRecord { Id = ProjectId, TenantId = tenantId };
            db.Projects.Add(project);
        }

        project.Name = "SkySnap Construction Demo Portfolio";
        project.Currency = "AZN";
        project.Location = "Poland";
        project.ClientName = "SkySnap Demo";
        project.ActiveEstimateVersionId = EstimateId;
        project.Status = ProjectEntityStatus.InProgress;
        project.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-35));
        project.EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(180));
        project.UpdatedAt = DateTimeOffset.UtcNow;

        var estimate = await db.ProjectEstimateVersions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == EstimateId, ct);
        if (estimate is null)
        {
            estimate = new ProjectEstimateVersionRecord { Id = EstimateId, TenantId = tenantId, CreatedAt = DateTimeOffset.UtcNow.AddDays(-30) };
            db.ProjectEstimateVersions.Add(estimate);
        }

        estimate.ProjectId = ProjectId;
        estimate.Name = "SkySnap English demo estimate v1";
        estimate.TotalAmount = StageSeeds().Sum(x => x.TotalCost);
        estimate.Notes = "Presentation-ready English estimate seeded for SkySnap partner demo";
        estimate.UpdatedAt = DateTimeOffset.UtcNow;

        for (var i = 0; i < sites.Count; i++)
        {
            var link = await db.ProjectSites.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ProjectId == ProjectId && x.SiteId == sites[i].Id, ct);
            if (link is null)
            {
                link = new ProjectSiteRecord { Id = sites[i].Id.ToString(), TenantId = tenantId, ProjectId = ProjectId, SiteId = sites[i].Id };
                db.ProjectSites.Add(link);
            }

            link.Zone = sites[i].Address;
            link.Status = ProjectEntityStatus.InProgress;
            link.PlannedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30 + i * 8));
            link.PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(120 + i * 25));
            link.Notes = "SkySnap partner demo project site";
        }

        var crewSeeds = CrewSeeds();
        for (var i = 0; i < crewSeeds.Length; i++)
        {
            var seed = crewSeeds[i];
            var crew = await db.ProjectCrews.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == seed.Id, ct);
            if (crew is null)
            {
                crew = new ProjectCrewRecord { Id = seed.Id, TenantId = tenantId };
                db.ProjectCrews.Add(crew);
            }

            crew.ProjectId = ProjectId;
            crew.SiteId = sites[i % sites.Count].Id;
            crew.Name = seed.Name;
            crew.Type = seed.Type;
            crew.ForemanName = seed.Foreman;
            crew.WorkerCount = workers.Count(x => x.Brigade == seed.Name);
            crew.PlannedDailyHours = 8;
            crew.Status = ProjectEntityStatus.InProgress;
            crew.ProgressPercent = 45 + i * 6;
            crew.Notes = "SkySnap demo crew";
        }

        var stageSeeds = StageSeeds();
        for (var i = 0; i < stageSeeds.Length; i++)
        {
            var seed = stageSeeds[i];
            var stage = await db.ProjectStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == seed.Id, ct);
            if (stage is null)
            {
                stage = new ProjectStageRecord { Id = seed.Id, TenantId = tenantId };
                db.ProjectStages.Add(stage);
            }

            stage.ProjectId = ProjectId;
            stage.EstimateVersionId = EstimateId;
            stage.SiteId = sites[i % sites.Count].Id;
            stage.Name = seed.Name;
            stage.Code = $"SKY-STG-{i + 1:00}";
            stage.Order = i + 1;
            stage.TotalCost = seed.TotalCost;
            stage.LaborCost = seed.LaborCost;
            stage.MaterialCost = seed.MaterialCost;
            stage.ProgressPercent = seed.Progress;
            stage.PlannedHours = seed.PlannedHours;
            stage.ActualHours = Math.Round(seed.PlannedHours * seed.Progress / 100m, 1);
            stage.AssignedCrewId = crewSeeds[i % crewSeeds.Length].Id;
            stage.Status = seed.Progress >= 100 ? ProjectEntityStatus.Completed : seed.Delayed ? ProjectEntityStatus.Delayed : ProjectEntityStatus.InProgress;
            stage.PlannedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-24 + i * 12));
            stage.PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(28 + i * 14));
            stage.Notes = "SkySnap drone visual progress can be linked to this stage";
        }

        await db.SaveChangesAsync(ct);

        var workSeeds = WorkItemSeeds();
        for (var i = 0; i < workSeeds.Length; i++)
        {
            var seed = workSeeds[i];
            var stage = stageSeeds[i % stageSeeds.Length];
            var site = sites[i % sites.Count];
            var item = await db.ProjectWorkItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == seed.Id, ct);
            if (item is null)
            {
                item = new ProjectWorkItemRecord { Id = seed.Id, TenantId = tenantId };
                db.ProjectWorkItems.Add(item);
            }

            item.ProjectId = ProjectId;
            item.SiteId = site.Id;
            item.StageId = stage.Id;
            item.EstimateVersionId = EstimateId;
            item.Code = $"SKY-WI-{i + 1:000}";
            item.Name = seed.Name;
            item.Unit = seed.Unit;
            item.Quantity = seed.Quantity;
            item.CompletedQuantity = Math.Round(seed.Quantity * seed.Progress / 100m, 2);
            item.LaborUnitPrice = seed.LaborUnitPrice;
            item.LaborTotal = seed.LaborTotal;
            item.MaterialUnit = seed.Unit;
            item.MaterialQuantity = seed.Quantity;
            item.MaterialUnitPrice = seed.MaterialUnitPrice;
            item.MaterialTotal = seed.MaterialTotal;
            item.TotalCost = seed.LaborTotal + seed.MaterialTotal;
            item.PlannedHours = seed.PlannedHours;
            item.ActualHours = Math.Round(seed.PlannedHours * seed.Progress / 100m, 1);
            item.AssignedCrewId = crewSeeds[i % crewSeeds.Length].Id;
            item.Status = seed.Progress >= 100 ? ProjectEntityStatus.Completed : seed.Delayed ? ProjectEntityStatus.Delayed : ProjectEntityStatus.InProgress;
            item.ProgressPercent = seed.Progress;
            item.PlannedStartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20 + i * 5));
            item.PlannedEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20 + i * 7));
            item.Notes = seed.Note;

            var smeta = await db.FieldSmetaItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == site.Id && x.ProjectWorkItemId == item.Id, ct);
            if (smeta is null)
            {
                smeta = new FieldSmetaItem { TenantId = tenantId, SiteId = site.Id, ProjectWorkItemId = item.Id };
                db.FieldSmetaItems.Add(smeta);
            }

            smeta.StageName = stage.Name;
            smeta.WorkName = item.Name;
            smeta.Unit = item.Unit;
            smeta.WorkCategory = "SkySnap English estimate";
            smeta.PlannedQuantity = item.Quantity;
            smeta.IsActive = true;
        }

        await db.SaveChangesAsync(ct);

        await UpsertProjectMaterialsAsync(db, tenantId, sites, stageSeeds, workSeeds, ct);
        await db.SaveChangesAsync(ct);

        await UpsertWorkspaceAsync(db, tenantId, sites, workers, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task UpsertProjectMaterialsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<StageSeed> stages, IReadOnlyList<WorkItemSeed> workItems, CancellationToken ct)
    {
        var rows = new[]
        {
            ("SKY-MAT-001", "Concrete B25", "m3", 360m, 140m, "MAT-CONCRETE-B25", 92m),
            ("SKY-MAT-002", "Rebar 16 mm", "ton", 24m, 9m, "STEEL-REBAR-16", 930m),
            ("SKY-MAT-003", "Bricks", "pcs", 52000m, 18000m, "MASON-BRICK", 0.45m),
            ("SKY-MAT-004", "Waterproofing membrane", "roll", 95m, 38m, "ROOF-BITUMEN-MEMBRANE", 42m),
            ("SKY-MAT-005", "Safety helmets", "pcs", 120m, 32m, "PPE-HELMET", 11m),
            ("SKY-MAT-006", "Reflective vests", "pcs", 140m, 40m, "PPE-VEST", 7m),
            ("SKY-MAT-007", "Tile adhesive", "bag", 260m, 60m, "FIN-TILE-ADHESIVE", 9m),
            ("SKY-MAT-008", "Cable 2.5 mm2", "m", 2400m, 720m, "ELEC-CABLE-2-5", 1.2m),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var material = await db.ProjectWorkItemMaterials.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == row.Item1, ct);
            if (material is null)
            {
                material = new ProjectWorkItemMaterialRecord { Id = row.Item1, TenantId = tenantId };
                db.ProjectWorkItemMaterials.Add(material);
            }

            material.ProjectId = ProjectId;
            material.SiteId = sites[i % sites.Count].Id;
            material.StageId = stages[i % stages.Count].Id;
            material.WorkItemId = workItems[i % workItems.Count].Id;
            material.CatalogItemId = row.Item6;
            material.Category = "Construction material";
            material.Name = row.Item2;
            material.Unit = row.Item3;
            material.Quantity = row.Item4;
            material.UsedQuantity = row.Item5;
            material.RemainingQuantity = row.Item4 - row.Item5;
            material.UnitPrice = row.Item7;
            material.Supplier = "SkySnap demo supply network";
            material.DeliveryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7 + i * 2));
            material.Notes = "English demo material plan";
            material.IsActive = true;
        }
    }

    private static async Task UpsertWorkspaceAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers, CancellationToken ct)
    {
        var workspace = await db.ProjectProgressWorkspaces.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (workspace is null)
        {
            workspace = new ProjectProgressWorkspace { TenantId = tenantId };
            db.ProjectProgressWorkspaces.Add(workspace);
        }

        workspace.WorkspaceJson = BuildWorkspaceJson(tenantId, sites, workers);
        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        workspace.NormalizedMigrationVersion = 3;
        workspace.NormalizedMigrationStatus = "SkySnapDemoSeed";
        workspace.NormalizedMigratedAt = DateTimeOffset.UtcNow;
        workspace.NormalizedMigrationError = null;
    }

    private static string BuildWorkspaceJson(Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers)
    {
        var stages = StageSeeds();
        var workItems = WorkItemSeeds();
        var crews = CrewSeeds();
        var objects = sites.Select((site, index) => new
        {
            id = site.Id.ToString(),
            name = site.Name,
            zone = site.Address,
            address = site.Address,
            projectId = ProjectId,
            status = "InProgress",
            plannedStartDate = DateTime.UtcNow.Date.AddDays(-30 + index * 8).ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.Date.AddDays(120 + index * 25).ToString("yyyy-MM-dd"),
            clientName = "SkySnap Demo",
            notes = "English SkySnap partner demo site",
        }).ToArray();

        var data = new
        {
            workspaceTenantId = tenantId.ToString(),
            projects = new[] { ProjectJson() },
            activeProjectId = ProjectId,
            objects,
            project = ProjectJson(),
            estimateVersions = new[] { new { id = EstimateId, projectId = ProjectId, name = "SkySnap English demo estimate v1", createdAt = DateTimeOffset.UtcNow.AddDays(-30).ToString("O"), totalAmount = stages.Sum(x => x.TotalCost), notes = "FerstacLabs + 1Muhasib + SkySnap presentation estimate" } },
            summary = new { totalAmount = 1_245_600m, laborAmount = 318_400m, materialAmount = 781_500m, hiddenCostAmount = 145_700m, currency = "AZN" },
            stages = stages.Select((stage, index) => new
            {
                id = stage.Id,
                objectId = sites[index % sites.Count].Id.ToString(),
                name = stage.Name,
                order = index + 1,
                totalCost = stage.TotalCost,
                laborCost = stage.LaborCost,
                materialCost = stage.MaterialCost,
                plannedStartDate = DateTime.UtcNow.Date.AddDays(-24 + index * 12).ToString("yyyy-MM-dd"),
                plannedEndDate = DateTime.UtcNow.Date.AddDays(28 + index * 14).ToString("yyyy-MM-dd"),
                status = stage.Progress >= 100 ? "Completed" : stage.Delayed ? "Delayed" : "InProgress",
                progressPercent = stage.Progress,
                assignedCrewId = crews[index % crews.Length].Id,
                plannedHours = stage.PlannedHours,
                actualHours = Math.Round(stage.PlannedHours * stage.Progress / 100m, 1),
                notes = "SkySnap drone capture can verify this stage visually",
            }).ToArray(),
            workItems = workItems.Select((item, index) => new
            {
                id = item.Id,
                objectId = sites[index % sites.Count].Id.ToString(),
                stageId = stages[index % stages.Length].Id,
                name = item.Name,
                costCode = $"SKY-WI-{index + 1:000}",
                unit = item.Unit,
                quantity = item.Quantity,
                unitPrice = (item.LaborTotal + item.MaterialTotal) / Math.Max(item.Quantity, 1m),
                completedQuantity = Math.Round(item.Quantity * item.Progress / 100m, 2),
                laborUnitPrice = item.LaborUnitPrice,
                laborTotal = item.LaborTotal,
                materialUnit = item.Unit,
                materialQuantity = item.Quantity,
                materialUnitPrice = item.MaterialUnitPrice,
                materialTotal = item.MaterialTotal,
                totalCost = item.LaborTotal + item.MaterialTotal,
                plannedHours = item.PlannedHours,
                actualHours = Math.Round(item.PlannedHours * item.Progress / 100m, 1),
                assignedCrewId = crews[index % crews.Length].Id,
                status = item.Progress >= 100 ? "Completed" : item.Delayed ? "Delayed" : "InProgress",
                progressPercent = item.Progress,
                notes = item.Note,
            }).ToArray(),
            crews = crews.Select((crew, index) => new
            {
                id = crew.Id,
                objectId = sites[index % sites.Count].Id.ToString(),
                name = crew.Name,
                type = crew.Type,
                foremanName = crew.Foreman,
                workerCount = workers.Count(x => x.Brigade == crew.Name),
                activeWorkStageId = stages[index % stages.Length].Id,
                activeWorkItemId = workItems[index % workItems.Length].Id,
                plannedDailyHours = 8,
                status = "InProgress",
                progressPercent = 45 + index * 6,
                notes = "SkySnap English demo crew",
            }).ToArray(),
            workerAssignments = workers.Select((worker, index) => new
            {
                id = worker.Id.ToString(),
                workerName = worker.FullName,
                workerExternalId = worker.ExternalWorkerCode,
                projectId = ProjectId,
                objectId = worker.SiteId.ToString(),
                crewId = crews[index % crews.Length].Id,
                role = worker.Role,
                hourlyRate = worker.HourlyRate,
                plannedDailyHours = worker.PlannedDailyHours,
                activeStageId = stages[index % stages.Length].Id,
                activeWorkItemId = workItems[index % workItems.Length].Id,
                attendanceSource = worker.AttendanceSource,
                status = "active",
                riskScore = worker.RiskScore,
                notes = "SkySnap demo worker",
            }).ToArray(),
            materials = new[] { "SKY-MAT-001", "SKY-MAT-002", "SKY-MAT-003", "SKY-MAT-004", "SKY-MAT-005", "SKY-MAT-006", "SKY-MAT-007", "SKY-MAT-008" }
                .Select((id, index) => new
                {
                    id,
                    objectId = sites[index % sites.Count].Id.ToString(),
                    catalogItemId = index switch { 0 => "MAT-CONCRETE-B25", 1 => "STEEL-REBAR-16", 2 => "MASON-BRICK", 3 => "ROOF-BITUMEN-MEMBRANE", 4 => "PPE-HELMET", 5 => "PPE-VEST", 6 => "FIN-TILE-ADHESIVE", _ => "ELEC-CABLE-2-5" },
                    category = "Construction material",
                    name = new[] { "Concrete B25", "Rebar 16 mm", "Bricks", "Waterproofing membrane", "Safety helmets", "Reflective vests", "Tile adhesive", "Cable 2.5 mm2" }[index],
                    unit = new[] { "m3", "ton", "pcs", "roll", "pcs", "pcs", "bag", "m" }[index],
                    quantity = new[] { 360m, 24m, 52000m, 95m, 120m, 140m, 260m, 2400m }[index],
                    usedQuantity = new[] { 140m, 9m, 18000m, 38m, 32m, 40m, 60m, 720m }[index],
                    remainingQuantity = new[] { 220m, 15m, 34000m, 57m, 88m, 100m, 200m, 1680m }[index],
                    unitPrice = new[] { 92m, 930m, 0.45m, 42m, 11m, 7m, 9m, 1.2m }[index],
                    linkedStageId = stages[index % stages.Length].Id,
                    linkedWorkItemId = workItems[index % workItems.Length].Id,
                    deliveryDate = DateTime.UtcNow.Date.AddDays(7 + index * 2).ToString("yyyy-MM-dd"),
                    supplier = "SkySnap demo supply network",
                    notes = "English demo material plan",
                }).ToArray(),
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = new[]
            {
                new { id = "sky-issue-001", projectId = ProjectId, objectId = sites[1].Id.ToString(), stageId = "sky-stage-004", type = "Delay", title = "Facade works require drone verification before scaffold removal", severity = "Medium", status = "Watching", dueDate = DateTime.UtcNow.Date.AddDays(4).ToString("yyyy-MM-dd"), createdAt = DateTimeOffset.UtcNow.AddDays(-2).ToString("O") },
                new { id = "sky-issue-002", projectId = ProjectId, objectId = sites[2].Id.ToString(), stageId = "sky-stage-006", type = "Material", title = "Tile adhesive stock is below approved demand", severity = "High", status = "Open", dueDate = DateTime.UtcNow.Date.AddDays(2).ToString("yyyy-MM-dd"), createdAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("O") },
            },
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private static object ProjectJson() => new
    {
        id = ProjectId,
        name = "SkySnap Construction Demo Portfolio",
        currency = "AZN",
        location = "Poland",
        clientName = "SkySnap Demo",
        createdAt = DateTimeOffset.UtcNow.AddDays(-35).ToString("O"),
        activeEstimateVersionId = EstimateId,
    };

    private static async Task UpsertAttendanceSeedAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers, CancellationToken ct)
    {
        var deviceId = StableGuid("SKYSNAP-DEMO-ATTENDANCE-SEED-DEVICE");
        var seedDevice = await db.Devices.FirstOrDefaultAsync(x => x.Id == deviceId, ct);
        if (seedDevice is null)
        {
            seedDevice = new Device
            {
                Id = deviceId,
                TenantId = tenantId,
                SiteId = sites[0].Id,
                Name = "SkySnap Demo Attendance Seed Device",
                RegisterDeviceId = "SKYSNAP-DEMO-SEED-ATTENDANCE",
                Mode = DeviceMode.Simulator,
                RegisterPort = 0,
                Username = "seed",
                EncryptedPassword = "demo-not-configured",
                Status = DeviceStatus.Offline,
            };
            db.Devices.Add(seedDevice);
        }
        else if (seedDevice.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cannot seed SkySnap attendance events because seed device id belongs to another tenant.");
        }

        var day = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var selected = workers.Take(36).ToArray();
        for (var i = 0; i < selected.Length; i++)
        {
            var worker = selected[i];
            var checkIn = new DateTimeOffset(day.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(7.5 + (i % 5) * 0.15))), TimeSpan.Zero);
            var checkOut = checkIn.AddHours(8).AddMinutes((i % 4) * 10);
            var rawBase = 20000 + i * 2;
            var checkInEventId = StableGuid($"SKY-SEED-CHECKIN-{i}");
            var checkOutEventId = StableGuid($"SKY-SEED-CHECKOUT-{i}");
            if (!await db.AttendanceEvents.AnyAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.RawRecNo == rawBase, ct))
            {
                db.AttendanceEvents.Add(new AttendanceEvent { Id = checkInEventId, TenantId = tenantId, SiteId = worker.SiteId, DeviceId = deviceId, WorkerId = worker.Id, WorkerExternalId = worker.ExternalWorkerCode, WorkerName = worker.FullName, EventTime = checkIn, Direction = AttendanceDirection.Entry, Status = AttendanceEventStatus.Ok, Method = AttendanceMethod.Face, RawRecNo = rawBase, Source = "seed_skysnap_demo", RawPayloadJson = "{\"Source\":\"SkySnapSeed\"}", CreatedAt = checkIn });
            }
            if (!await db.AttendanceEvents.AnyAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.RawRecNo == rawBase + 1, ct))
            {
                db.AttendanceEvents.Add(new AttendanceEvent { Id = checkOutEventId, TenantId = tenantId, SiteId = worker.SiteId, DeviceId = deviceId, WorkerId = worker.Id, WorkerExternalId = worker.ExternalWorkerCode, WorkerName = worker.FullName, EventTime = checkOut, Direction = AttendanceDirection.Exit, Status = AttendanceEventStatus.Ok, Method = AttendanceMethod.Face, RawRecNo = rawBase + 1, Source = "seed_skysnap_demo", RawPayloadJson = "{\"Source\":\"SkySnapSeed\"}", CreatedAt = checkOut });
            }
            if (!await db.AttendanceSessions.AnyAsync(x => x.TenantId == tenantId && x.DeviceId == deviceId && x.WorkerExternalId == worker.ExternalWorkerCode && x.WorkDate == day, ct))
            {
                db.AttendanceSessions.Add(new AttendanceSession
                {
                    TenantId = tenantId,
                    SiteId = worker.SiteId,
                    DeviceId = deviceId,
                    WorkerId = worker.Id,
                    WorkerExternalId = worker.ExternalWorkerCode,
                    WorkerName = worker.FullName,
                    WorkDate = day,
                    CheckInEventId = checkInEventId,
                    CheckInTime = checkIn,
                    CheckOutEventId = checkOutEventId,
                    CheckOutTime = checkOut,
                    LastSeenTime = checkOut,
                    Status = AttendanceSessionStatus.Closed,
                    Source = "seed_skysnap_demo",
                });
            }
        }
    }

    private static async Task UpsertWarehouseWorkflowSeedAsync(BuildTrackDbContext db, Guid tenantId, Site site, Guid supervisorId, CancellationToken ct)
    {
        var item = await db.FieldWarehouseCatalogItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == "FIN-TILE-ADHESIVE", ct);
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault, ct);
        if (item is null || warehouse is null) return;
        var request = await db.FieldWarehouseRequests.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == "SKY-WR-001", ct);
        if (request is null)
        {
            request = new FieldWarehouseRequest { TenantId = tenantId, Code = "SKY-WR-001", CatalogItemId = item.Id, SiteId = site.Id, SupervisorUserId = supervisorId };
            db.FieldWarehouseRequests.Add(request);
        }

        request.RequestedQuantity = 160;
        request.ApprovedQuantity = 130;
        request.ReservedQuantity = 90;
        request.IssuedQuantity = 0;
        request.Unit = item.Unit;
        request.NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        request.Urgency = FieldWarehouseUrgency.Urgent;
        request.Reason = "Tile adhesive required for finishing crew on Block A";
        request.GeneralNote = "Warehouse has partial stock; procurement shortfall is visible for presentation.";
        request.Status = FieldWarehouseRequestStatus.InFulfillment;
        request.SubmittedAt ??= DateTimeOffset.UtcNow.AddDays(-1);

        if (request.Lines.Count == 0)
        {
            request.Lines.Add(new FieldWarehouseRequestLine { TenantId = tenantId, CatalogItemId = item.Id, RequestedQuantity = 160, ApprovedQuantity = 130, ReservedQuantity = 90, Unit = item.Unit, Status = FieldWarehouseRequestLineStatus.ProcurementInProgress });
        }

        if (!await db.ProcurementNeeds.AnyAsync(x => x.TenantId == tenantId && x.SourceRequest == request, ct))
        {
            db.ProcurementNeeds.Add(new ProcurementNeed
            {
                TenantId = tenantId,
                SiteId = site.Id,
                WarehouseId = warehouse.Id,
                SourceRequest = request,
                SourceRequestLine = request.Lines.First(),
                CatalogItemId = item.Id,
                RequiredQuantity = 130,
                AlreadyAvailableQuantity = 90,
                ShortfallQuantity = 40,
                Unit = item.Unit,
                Priority = FieldWarehouseUrgency.Urgent,
                RequiredBy = request.NeededBy,
                Reason = "SkySnap demo procurement shortfall",
                Status = ProcurementNeedStatus.Approved,
                CreatedByUserId = supervisorId,
            });
        }
    }

    private static async Task UpsertDailyReportsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<AppUser> supervisors, CancellationToken ct)
    {
        var smetaItems = await db.FieldSmetaItems.Where(x => x.TenantId == tenantId).OrderBy(x => x.WorkName).Take(8).ToArrayAsync(ct);
        if (smetaItems.Length == 0) return;
        for (var i = 0; i < 8; i++)
        {
            var code = $"SKY-DR-{i + 1:000}";
            var site = sites[i % sites.Count];
            var supervisor = supervisors[i % supervisors.Count];
            var date = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-i));
            var report = await db.SupervisorDailyReports.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.GeneralNote != null && x.GeneralNote.Contains(code), ct);
            if (report is null)
            {
                report = new SupervisorDailyReport { TenantId = tenantId, SiteId = site.Id, SupervisorUserId = supervisor.Id, ReportDate = date };
                db.SupervisorDailyReports.Add(report);
            }

            report.SiteId = site.Id;
            report.SupervisorUserId = supervisor.Id;
            report.ReportDate = date;
            report.Status = i switch { 0 or 1 => FieldDailyReportStatus.Submitted, 2 or 3 or 4 or 5 => FieldDailyReportStatus.Approved, 6 => FieldDailyReportStatus.NeedsCorrection, _ => FieldDailyReportStatus.Rejected };
            report.WeatherCondition = i % 3 == 0 ? "Windy, clear visibility for drone flight" : "Cloudy, normal site conditions";
            report.GeneralNote = $"{code}: Daily report includes workforce, materials, photo evidence and management review.";
            report.SubmittedAt ??= DateTimeOffset.UtcNow.AddDays(-i).AddHours(10);
            if (report.Status == FieldDailyReportStatus.Approved) report.ReviewNote = "Reviewed by management; progress accepted.";

            if (report.Lines.Count == 0)
            {
                var smeta = smetaItems[i % smetaItems.Length];
                report.Lines.Add(new SupervisorDailyReportLine { TenantId = tenantId, SmetaItemId = smeta.Id, ProjectWorkItemId = smeta.ProjectWorkItemId, ReportedQuantity = 12 + i * 3, WorkerCount = 8 + i % 5, WorkHours = 64 + i * 4, Unit = smeta.Unit, Note = "Completed quantity verified against estimate plan." });
            }
        }
    }

    private static async Task UpsertDevicesAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, CancellationToken ct)
    {
        for (var i = 0; i < sites.Count; i++)
        {
            var registerId = $"SKYSNAP-DEMO-SITE-{i + 1:00}";
            var device = await db.Devices.FirstOrDefaultAsync(x => x.RegisterDeviceId == registerId, ct);
            if (device is null)
            {
                device = new Device { Id = StableGuid(registerId), TenantId = tenantId, RegisterDeviceId = registerId };
                db.Devices.Add(device);
            }
            else if (device.TenantId != tenantId)
            {
                throw new InvalidOperationException($"Cannot seed SkySnap device '{registerId}' because it belongs to another tenant.");
            }

            device.SiteId = sites[i].Id;
            device.Name = $"{sites[i].Name} - Active Register Terminal";
            device.Vendor = "dahua";
            device.Model = "DHI-ASI6213J-MW";
            device.Mode = DeviceMode.ActiveRegister;
            device.RegisterPort = i % 2 == 0 ? 7000 : 9500;
            device.Status = DeviceStatus.Pending;
            device.Username = "admin";
            device.EncryptedPassword = "demo-not-configured";
            device.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static CrewSeed[] CrewSeeds() =>
    [
        new("sky-crew-concrete", "Concrete Crew", "Concrete works", "Anna Kowalska"),
        new("sky-crew-rebar", "Rebar Crew", "Rebar and steel fixing", "Marek Nowak"),
        new("sky-crew-masonry", "Masonry Crew", "Masonry", "Piotr Zielinski"),
        new("sky-crew-finishing", "Finishing Crew", "Finishing", "Katarzyna Wisniewska"),
        new("sky-crew-mep", "Electrical & MEP Crew", "Electrical and MEP", "Lukasz Kaminski"),
        new("sky-crew-logistics", "Logistics Crew", "Warehouse and logistics", "Magdalena Lewandowska"),
    ];

    private static StageSeed[] StageSeeds() =>
    [
        new("sky-stage-001", "Site preparation and earthworks", 82_000m, 32_000m, 39_000m, 100, 620m, false),
        new("sky-stage-002", "Reinforced concrete foundations", 184_500m, 54_000m, 118_000m, 78, 980m, false),
        new("sky-stage-003", "Structural frame and slabs", 318_000m, 84_000m, 212_000m, 52, 1460m, false),
        new("sky-stage-004", "Masonry and facade envelope", 206_000m, 58_000m, 126_000m, 36, 900m, true),
        new("sky-stage-005", "Roofing and waterproofing", 129_600m, 31_400m, 82_500m, 24, 540m, false),
        new("sky-stage-006", "Interior finishing and MEP", 325_500m, 59_000m, 204_000m, 18, 1100m, true),
    ];

    private static WorkItemSeed[] WorkItemSeeds() =>
    [
        new("sky-work-001", "Excavation and soil removal", "m3", 1450m, 8m, 11_600m, 18m, 26_100m, 220m, 100, "Earthworks completed; drone orthophoto ready."),
        new("sky-work-002", "Lean concrete B15 under foundations", "m3", 140m, 22m, 3_080m, 75m, 10_500m, 90m, 100, "Foundation base accepted."),
        new("sky-work-003", "Rebar installation for foundations", "ton", 22m, 430m, 9_460m, 920m, 20_240m, 210m, 82, "Steel fixing under QA review."),
        new("sky-work-004", "Concrete B25 foundation pour", "m3", 360m, 18m, 6_480m, 92m, 33_120m, 180m, 76, "Pump schedule coordinated."),
        new("sky-work-005", "Monolithic columns and slabs", "m3", 620m, 38m, 23_560m, 120m, 74_400m, 480m, 54, "Progress checked weekly."),
        new("sky-work-006", "External masonry walls", "m2", 4200m, 9m, 37_800m, 15m, 63_000m, 430m, 38, "Facade drone comparison pending.", true),
        new("sky-work-007", "Roof waterproofing membrane", "m2", 3100m, 5m, 15_500m, 11m, 34_100m, 190m, 24, "Weather-sensitive activity."),
        new("sky-work-008", "Electrical cable rough-in", "m", 4800m, 1.8m, 8_640m, 2.4m, 11_520m, 260m, 22, "MEP crew starts after masonry zones."),
        new("sky-work-009", "Interior plaster and putty", "m2", 6500m, 6m, 39_000m, 7.5m, 48_750m, 360m, 16, "Material shortfall tracked."),
        new("sky-work-010", "Tile adhesive and floor tile works", "m2", 2200m, 8m, 17_600m, 19m, 41_800m, 240m, 8, "Procurement example linked.", true),
        new("sky-work-011", "Safety equipment issue for crews", "set", 85m, 4m, 340m, 18m, 1_530m, 24m, 75, "Warehouse request demo."),
        new("sky-work-012", "Drone progress capture and evidence pack", "flight", 18m, 120m, 2_160m, 0m, 0m, 36m, 45, "SkySnap embedded panel story."),
    ];

    private static Guid StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private static bool ParseBool(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private sealed record CrewSeed(string Id, string Name, string Type, string Foreman);
    private sealed record StageSeed(string Id, string Name, decimal TotalCost, decimal LaborCost, decimal MaterialCost, decimal Progress, decimal PlannedHours, bool Delayed);
    private sealed record WorkItemSeed(string Id, string Name, string Unit, decimal Quantity, decimal LaborUnitPrice, decimal LaborTotal, decimal MaterialUnitPrice, decimal MaterialTotal, decimal PlannedHours, decimal Progress, string Note, bool Delayed = false);
}
