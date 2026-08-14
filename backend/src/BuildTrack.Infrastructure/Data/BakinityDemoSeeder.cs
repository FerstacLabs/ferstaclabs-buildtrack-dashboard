using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Security;
using BuildTrack.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BuildTrack.Infrastructure.Data;

internal static class BakinityDemoSeeder
{
    private const string WorkspaceCode = "BAK-DEMO-WORKSPACE";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static async Task SeedAsync(BuildTrackDbContext db, IConfiguration? configuration, CancellationToken ct)
    {
        if (!ParseBool(configuration?["SEED_BAKINITY_DEMO"])) return;

        if (ParseBool(configuration?["SEED_BAKINITY_DEMO_RESET"]))
        {
            var existing = await db.Tenants.FirstOrDefaultAsync(x => x.Code == DbInitializer.BakinityDemoTenantCode, ct);
            if (existing is not null)
            {
                db.Tenants.Remove(existing);
                await db.SaveChangesAsync(ct);
            }
        }

        var ownerPassword = configuration?["SEED_BAKINITY_DEMO_PASSWORD"];
        if (string.IsNullOrWhiteSpace(ownerPassword))
        {
            throw new InvalidOperationException("SEED_BAKINITY_DEMO_PASSWORD is required when SEED_BAKINITY_DEMO=true.");
        }

        var tenant = await UpsertTenantAsync(db, ct);
        await UpsertLicenseAsync(db, tenant.Id, ct);
        await UpsertUserAsync(db, tenant.Id, configuration?["SEED_BAKINITY_DEMO_EMAIL"] ?? "eldar@bakinity.az", "Eldar Məmmədov", BuildTrackUserRole.Owner, ownerPassword, true, ct);

        var siteRows = await UpsertSitesAsync(db, tenant.Id, ct);
        var primarySite = siteRows[0];
        await SeedCatalogAndWarehouseAsync(db, tenant.Id, ct);

        var supervisors = await UpsertProrabsAsync(db, tenant.Id, siteRows, configuration, ownerPassword, ct);
        var procurementUsers = await UpsertProcurementUsersAsync(db, tenant.Id, configuration, ownerPassword, ct);
        var workers = await UpsertWorkersAsync(db, tenant.Id, siteRows, ct);
        await UpsertFieldSmetaItemsAsync(db, tenant.Id, siteRows, ct);
        await UpsertAttendanceSeedAsync(db, tenant.Id, siteRows, workers, ct);
        await UpsertWarehouseWorkflowSeedAsync(db, tenant.Id, primarySite.Id, supervisors[0].Id, procurementUsers[0].Id, ct);
        await UpsertWorkspaceAsync(db, tenant.Id, siteRows, workers, ct);
        await db.SaveChangesAsync(ct);
        await UpsertSupervisorDailyReportsAsync(db, tenant.Id, siteRows, supervisors, ct);
        await db.SaveChangesAsync(ct);
        await new ProjectProgressDailyReportSyncService(db).RecalculateApprovedDailyReportProgressAsync(tenant.Id, null, ct);
    }

    private static async Task<Tenant> UpsertTenantAsync(BuildTrackDbContext db, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Code == DbInitializer.BakinityDemoTenantCode, ct);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = DbInitializer.BakinityDemoTenantId,
                Code = DbInitializer.BakinityDemoTenantCode,
                CompanyName = "BAKİNİTY MMC",
                Status = TenantStatus.Active,
            };
            db.Tenants.Add(tenant);
            return tenant;
        }

        tenant.CompanyName = "BAKİNİTY MMC";
        tenant.Status = TenantStatus.Active;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        return tenant;
    }

    private static async Task UpsertLicenseAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var hash = $"bak-demo-{tenantId:N}";
        var license = await db.Licenses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LicenseKeyHash == hash, ct);
        if (license is null)
        {
            db.Licenses.Add(new License
            {
                TenantId = tenantId,
                LicenseKeyHash = hash,
                Plan = LicensePlan.Unlimited,
                Status = LicenseStatus.Active,
                StartsAt = DateTimeOffset.UtcNow.AddDays(-30),
                ActivatedAt = DateTimeOffset.UtcNow,
            });
            return;
        }

        license.Plan = LicensePlan.Unlimited;
        license.Status = LicenseStatus.Active;
        license.ExpiresAt = null;
        license.ActivatedAt ??= DateTimeOffset.UtcNow;
    }

    private static async Task<AppUser> UpsertUserAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        string email,
        string fullName,
        BuildTrackUserRole role,
        string password,
        bool mayResetPassword,
        CancellationToken ct)
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

        user.TenantId = tenantId;
        user.FullName = fullName;
        user.Role = role;
        user.Status = BuildTrackUserStatus.Active;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        if (mayResetPassword)
        {
            user.PasswordHash = BuildTrackPasswordHasher.HashPassword(password);
        }

        return user;
    }

    private static async Task<IReadOnlyList<Site>> UpsertSitesAsync(BuildTrackDbContext db, Guid tenantId, CancellationToken ct)
    {
        var seeds = new[]
        {
            ("BAK-SITE-001", "BAKİNİTY RESIDENCE — Blok A", "Bakı şəhəri, Ağ Şəhər, 1-ci zona"),
            ("BAK-SITE-002", "BAKİNİTY RESIDENCE — Blok B", "Bakı şəhəri, Ağ Şəhər, 2-ci zona"),
            ("BAK-SITE-003", "BAKİNİTY Villa — Korpus 1", "Bakı şəhəri, Mərdəkan"),
            ("BAK-SITE-004", "BAKİNİTY Anbar və Logistika", "Bakı şəhəri, Qaradağ logistika sahəsi"),
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
            site.TimeZone = "Asia/Baku";
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

        await db.SaveChangesAsync(ct);

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsDefault, ct);
        if (warehouse is null)
        {
            warehouse = new Warehouse
            {
                Id = StableGuid("BAK-DEMO-WAREHOUSE-MAIN"),
                TenantId = tenantId,
                Name = "BAKİNİTY Mərkəzi Anbar",
                Address = "Qaradağ logistika sahəsi",
                IsDefault = true,
                IsActive = true,
            };
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync(ct);
        }

        var stock = new (string Code, decimal Quantity, decimal Minimum)[]
        {
            ("PPE-HELMET", 140, 40),
            ("PPE-GLOVE", 420, 120),
            ("PPE-VEST", 160, 50),
            ("PPE-GLASSES", 110, 30),
            ("CONS-DRILL-BIT-10", 65, 20),
            ("CONS-CUT-DISC", 180, 50),
            ("MAT-CEMENT-M400", 620, 180),
            ("MAT-CONCRETE-B25", 96, 20),
            ("MAT-REBAR-A3", 23, 6),
            ("FIN-PLASTER", 380, 120),
            ("FIN-TILE-ADHESIVE", 210, 70),
            ("ELEC-CABLE-2-5", 1850, 400),
            ("PLUMB-PPR-25", 730, 200),
            ("ROOF-BITUMEN-MEMBRANE", 86, 25),
        };

        foreach (var row in stock)
        {
            await UpsertOpeningBalanceAsync(db, tenantId, warehouse.Id, row.Code, row.Quantity, row.Minimum, ct);
        }

        var suppliers = new[]
        {
            ("BAK Beton Təchizat", "Beton və sement"),
            ("MetalPro Bakı", "Armatur və metal"),
            ("SafeWork Supply", "PPE"),
            ("Elektrik Servis", "Elektrik"),
            ("Santex Build", "Santexnika"),
        };

        foreach (var (name, categories) in suppliers)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, ct);
            if (supplier is null)
            {
                db.Suppliers.Add(new Supplier
                {
                    TenantId = tenantId,
                    Name = name,
                    Categories = categories,
                    ContactPerson = "Demo menecer",
                    Phone = "+994 12 000 00 00",
                    Status = SupplierStatus.Active,
                });
            }
        }
    }

    private static async Task UpsertOpeningBalanceAsync(BuildTrackDbContext db, Guid tenantId, Guid warehouseId, string itemCode, decimal quantity, decimal minimum, CancellationToken ct)
    {
        var item = await db.FieldWarehouseCatalogItems.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == itemCode, ct);
        if (item is null) return;

        item.MinimumStockLevel = minimum;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        var movement = await db.WarehouseStockMovements.FirstOrDefaultAsync(x =>
            x.TenantId == tenantId
            && x.WarehouseId == warehouseId
            && x.CatalogItemId == item.Id
            && x.ReferenceType == "BakinitySeedOpeningBalance",
            ct);

        if (movement is null)
        {
            db.WarehouseStockMovements.Add(new WarehouseStockMovement
            {
                TenantId = tenantId,
                WarehouseId = warehouseId,
                CatalogItemId = item.Id,
                MovementType = WarehouseStockMovementType.OpeningBalance,
                Quantity = quantity,
                ReferenceType = "BakinitySeedOpeningBalance",
                ReferenceId = item.Id,
                Note = "BAK-DEMO başlanğıc qalığı",
            });
        }
        else
        {
            movement.Quantity = quantity;
            movement.OccurredAt = DateTimeOffset.UtcNow;
        }
    }

    private static async Task<IReadOnlyList<AppUser>> UpsertProrabsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IConfiguration? configuration, string fallbackPassword, CancellationToken ct)
    {
        var password = configuration?["SEED_BAKINITY_DEMO_PRORAB_PASSWORD"] ?? fallbackPassword;
        var users = new List<AppUser>();
        for (var i = 1; i <= 10; i++)
        {
            var user = await UpsertUserAsync(db, tenantId, $"prorab{i:00}@bakinity.az", $"Prorab {i:00}", BuildTrackUserRole.Supervisor, password, false, ct);
            users.Add(user);
            var site = sites[(i - 1) % sites.Count];
            if (!await db.SupervisorSiteAssignments.AnyAsync(x => x.TenantId == tenantId && x.SupervisorUserId == user.Id && x.SiteId == site.Id && x.IsActive, ct))
            {
                db.SupervisorSiteAssignments.Add(new SupervisorSiteAssignment
                {
                    TenantId = tenantId,
                    SupervisorUserId = user.Id,
                    SiteId = site.Id,
                    IsActive = true,
                    Notes = "BAK-DEMO prorab təyinatı",
                    ValidFrom = DateTimeOffset.UtcNow.AddDays(-15),
                });
            }
        }

        return users;
    }

    private static async Task<IReadOnlyList<AppUser>> UpsertProcurementUsersAsync(BuildTrackDbContext db, Guid tenantId, IConfiguration? configuration, string fallbackPassword, CancellationToken ct)
    {
        var password = configuration?["SEED_BAKINITY_DEMO_SUPPLY_PASSWORD"] ?? fallbackPassword;
        var names = new[] { "Satınalma Operatoru 1", "Satınalma Operatoru 2", "Anbar Nəzarətçisi" };
        var users = new List<AppUser>();
        for (var i = 0; i < names.Length; i++)
        {
            users.Add(await UpsertUserAsync(db, tenantId, $"supply{i + 1:00}@bakinity.az", names[i], BuildTrackUserRole.ProcurementAgent, password, false, ct));
        }

        return users;
    }

    private static async Task<IReadOnlyList<Worker>> UpsertWorkersAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, CancellationToken ct)
    {
        var brigades = new[]
        {
            ("Monolit briqadası", "Betonçu"),
            ("Armatur briqadası", "Armaturçu"),
            ("Hörgü briqadası", "Hörgü ustası"),
            ("Suvaq briqadası", "Suvaqçı"),
            ("Elektrik briqadası", "Elektrik"),
            ("Santexnik briqadası", "Santexnik"),
        };

        var workers = new List<Worker>();
        for (var i = 1; i <= 48; i++)
        {
            var (brigade, role) = brigades[(i - 1) % brigades.Length];
            var site = sites[(i - 1) % Math.Min(3, sites.Count)];
            var code = $"BAK-W-{i:000}";
            var worker = await db.Workers.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SiteId == site.Id && x.ExternalWorkerCode == code, ct);
            if (worker is null)
            {
                worker = new Worker { TenantId = tenantId, SiteId = site.Id, ExternalWorkerCode = code };
                db.Workers.Add(worker);
            }

            worker.FullName = $"BAK işçi {i:000}";
            worker.Brigade = brigade;
            worker.Role = role;
            worker.HourlyRate = 4.5m + (i % 5);
            worker.PlannedDailyHours = 8;
            worker.AttendanceSource = "Camera";
            worker.Status = WorkerStatus.Active;
            worker.UpdatedAt = DateTimeOffset.UtcNow;
            workers.Add(worker);

            if (!await db.WorkerSiteAssignments.AnyAsync(x => x.TenantId == tenantId && x.WorkerId == worker.Id && x.SiteId == site.Id && x.Status == WorkerSiteAssignmentStatus.Active, ct))
            {
                db.WorkerSiteAssignments.Add(new WorkerSiteAssignment
                {
                    TenantId = tenantId,
                    WorkerId = worker.Id,
                    SiteId = site.Id,
                    IsPrimary = true,
                    Status = WorkerSiteAssignmentStatus.Active,
                });
            }
        }

        return workers;
    }

    private static async Task UpsertFieldSmetaItemsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, CancellationToken ct)
    {
        var rows = BuildSmetaRows();
        foreach (var site in sites.Take(3))
        {
            foreach (var (row, index) in rows.Select((row, index) => (row, index)))
            {
                var projectWorkItemId = StableGuid($"BAK-DEMO-WORK-{index + 1}").ToString();
                var item = await db.FieldSmetaItems.FirstOrDefaultAsync(x =>
                    x.TenantId == tenantId
                    && x.SiteId == site.Id
                    && (x.ProjectWorkItemId == projectWorkItemId || x.WorkName == row.Work),
                    ct);
                if (item is null)
                {
                    item = new FieldSmetaItem
                    {
                        TenantId = tenantId,
                        SiteId = site.Id,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    db.FieldSmetaItems.Add(item);
                }

                item.StageName = row.Stage;
                item.WorkName = row.Work;
                item.Unit = row.Unit;
                item.WorkCategory = row.Category;
                item.ProjectWorkItemId = projectWorkItemId;
                item.PlannedQuantity = row.Quantity;
                item.IsActive = true;
                item.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private static async Task UpsertSupervisorDailyReportsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<AppUser> supervisors, CancellationToken ct)
    {
        var constructionSites = sites.Take(3).ToArray();
        if (constructionSites.Length == 0 || supervisors.Count == 0) return;

        var ownerId = await db.Users
            .Where(x => x.TenantId == tenantId && x.Role == BuildTrackUserRole.Owner)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        var assignments = await db.SupervisorSiteAssignments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync(ct);
        var supervisorBySite = constructionSites.ToDictionary(
            site => site.Id,
            site => assignments.FirstOrDefault(x => x.SiteId == site.Id)?.SupervisorUserId ?? supervisors.First().Id);

        var smetaItems = await db.FieldSmetaItems
            .Where(x => x.TenantId == tenantId && x.ProjectWorkItemId != null)
            .ToListAsync(ct);
        var smetaBySiteAndWorkId = smetaItems.ToDictionary(
            x => $"{x.SiteId:N}|{x.ProjectWorkItemId}",
            x => x,
            StringComparer.OrdinalIgnoreCase);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4));
        var siteA = constructionSites[0];
        var siteB = constructionSites.Length > 1 ? constructionSites[1] : constructionSites[0];
        var siteC = constructionSites.Length > 2 ? constructionSites[2] : constructionSites[0];
        var seedReports = new[]
        {
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-001", siteA.Id, supervisorBySite[siteA.Id], today.AddDays(-8), FieldDailyReportStatus.Approved, "Günəşli", "Torpaq və hidroizolyasiya işləri plan üzrə tamamlandı.", "Plan üzrə qəbul edildi.", new[] { SeedLine(1, 340m, 8, 64m), SeedLine(4, 161.2m, 6, 48m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-002", siteB.Id, supervisorBySite[siteB.Id], today.AddDays(-7), FieldDailyReportStatus.Approved, "Küləkli", "Armatur karkası üzrə faktiki miqdar təsdiqləndi.", "Smeta progressinə tətbiq edildi.", new[] { SeedLine(2, 6.12m, 7, 56m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-003", siteC.Id, supervisorBySite[siteC.Id], today.AddDays(-6), FieldDailyReportStatus.Approved, "Günəşli", "Beton B25 və ikinci mərtəbə monolit işləri qəbul edildi.", "Smeta progressinə tətbiq edildi.", new[] { SeedLine(3, 60.2m, 9, 72m), SeedLine(6, 12.96m, 5, 40m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-004", siteB.Id, supervisorBySite[siteB.Id], today.AddDays(-5), FieldDailyReportStatus.Approved, "İsti", "Qəlib və hörgü miqdarları yoxlanıldı.", "Smeta progressinə tətbiq edildi.", new[] { SeedLine(5, 57.6m, 10, 80m), SeedLine(8, 431.8m, 12, 96m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-005", siteA.Id, supervisorBySite[siteA.Id], today.AddDays(-4), FieldDailyReportStatus.Approved, "Günəşli", "Suvaq işləri üzrə ilkin miqdar təsdiqləndi.", "Smeta progressinə tətbiq edildi.", new[] { SeedLine(10, 166.5m, 8, 64m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-006", siteC.Id, supervisorBySite[siteC.Id], today.AddDays(-1), FieldDailyReportStatus.Submitted, "Günəşli", "Beton B25 üçün əlavə 8 m3 iş görülüb, təsdiq gözləyir.", null, new[] { SeedLine(3, 8m, 6, 48m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-007", siteB.Id, supervisorBySite[siteB.Id], today, FieldDailyReportStatus.Submitted, "Günəşli", "Hörgü briqadası 40 m2 əlavə iş təqdim edib.", null, new[] { SeedLine(8, 40m, 7, 56m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-008", siteA.Id, supervisorBySite[siteA.Id], today.AddDays(-3), FieldDailyReportStatus.NeedsCorrection, "Yağışlı", "Suvaq miqdarında ölçü aktı əlavə olunmayıb.", "Miqdar üçün ölçü aktını əlavə edin.", new[] { SeedLine(10, 80m, 5, 35m) }),
            new SeedDailyReport("BAK-DEMO-FIELD-REPORT-009", siteB.Id, supervisorBySite[siteB.Id], today.AddDays(-2), FieldDailyReportStatus.Rejected, "Küləkli", "Qəlib miqdarı təsdiq sənədi olmadan göndərilib.", "Təsdiq sənədi olmadığı üçün rədd edildi.", new[] { SeedLine(5, 12m, 4, 28m) }),
        };

        foreach (var seed in seedReports)
        {
            var reportId = StableGuid(seed.Code);
            var report = await db.SupervisorDailyReports.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == reportId, ct);
            if (report is null)
            {
                report = new SupervisorDailyReport
                {
                    Id = reportId,
                    TenantId = tenantId,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.SupervisorDailyReports.Add(report);
            }

            report.ProjectId = StableGuid("BAK-DEMO-PROJECT");
            report.SiteId = seed.SiteId;
            report.SupervisorUserId = seed.SupervisorUserId;
            report.ReportDate = seed.ReportDate;
            report.Shift = "Gündüz";
            report.Status = seed.Status;
            report.GeneralNote = seed.Note;
            report.WeatherCondition = seed.Weather;
            report.SubmittedAt = seed.Status == FieldDailyReportStatus.Draft ? null : new DateTimeOffset(seed.ReportDate.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(18))), TimeSpan.FromHours(4)).ToUniversalTime();
            report.ReviewedAt = seed.Status is FieldDailyReportStatus.Approved or FieldDailyReportStatus.NeedsCorrection or FieldDailyReportStatus.Rejected
                ? report.SubmittedAt?.AddHours(2)
                : null;
            report.ReviewedByUserId = report.ReviewedAt is null ? null : ownerId;
            report.ReviewNote = seed.ReviewNote;

            var desiredLineIds = new HashSet<Guid>();
            foreach (var (line, lineIndex) in seed.Lines.Select((line, lineIndex) => (line, lineIndex)))
            {
                var lineId = StableGuid($"{seed.Code}-LINE-{lineIndex + 1}");
                desiredLineIds.Add(lineId);
                var projectWorkItemId = StableGuid($"BAK-DEMO-WORK-{line.WorkItemNumber}").ToString();
                if (!smetaBySiteAndWorkId.TryGetValue($"{seed.SiteId:N}|{projectWorkItemId}", out var smetaItem))
                {
                    continue;
                }

                var reportLine = report.Lines.FirstOrDefault(x => x.Id == lineId);
                if (reportLine is null)
                {
                    reportLine = new SupervisorDailyReportLine
                    {
                        Id = lineId,
                        TenantId = tenantId,
                        CreatedAt = report.CreatedAt,
                    };
                    report.Lines.Add(reportLine);
                }

                reportLine.TenantId = tenantId;
                reportLine.SmetaItemId = smetaItem.Id;
                reportLine.ProjectWorkItemId = projectWorkItemId;
                reportLine.ReportedQuantity = line.Quantity;
                reportLine.WorkerCount = line.WorkerCount;
                reportLine.WorkHours = line.WorkHours;
                reportLine.Unit = smetaItem.Unit;
                reportLine.Note = "BAK-DEMO canonical daily report line";
            }

            foreach (var staleLine in report.Lines.Where(x => !desiredLineIds.Contains(x.Id)).ToArray())
            {
                db.SupervisorDailyReportLines.Remove(staleLine);
            }
        }
    }

    private static async Task UpsertAttendanceSeedAsync(BuildTrackDbContext db, Guid tenantId, Guid siteId, IReadOnlyList<Worker> workers, CancellationToken ct)
    {
        var device = await db.Devices.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RegisterDeviceId == "BAK-DEMO-CAM-001", ct);
        if (device is null)
        {
            device = new Device
            {
                Id = StableGuid("BAK-DEMO-CAM-001"),
                TenantId = tenantId,
                SiteId = siteId,
                Name = "BAKİNİTY giriş kamerası",
                Vendor = "dahua",
                Model = "DHI-ASI6213J-MW",
                Mode = DeviceMode.ActiveRegister,
                RegisterDeviceId = "BAK-DEMO-CAM-001",
                RegisterPort = 7000,
                Status = DeviceStatus.Pending,
                Username = "admin",
                EncryptedPassword = "seed-managed-secret",
                LastRecNo = 0,
            };
            db.Devices.Add(device);
        }

        var workDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4));
        for (var i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            var eventId = StableGuid($"BAK-DEMO-ATT-{worker.ExternalWorkerCode}-{workDate:yyyyMMdd}");
            if (await db.AttendanceEvents.AnyAsync(x => x.TenantId == tenantId && x.Id == eventId, ct)) continue;
            var eventTime = DateTimeOffset.UtcNow.Date.AddHours(4 + i);
            db.AttendanceEvents.Add(new AttendanceEvent
            {
                Id = eventId,
                TenantId = tenantId,
                SiteId = siteId,
                DeviceId = device.Id,
                WorkerId = worker.Id,
                WorkerExternalId = worker.ExternalWorkerCode,
                WorkerName = worker.FullName,
                EventTime = eventTime,
                Direction = AttendanceDirection.Entry,
                Status = AttendanceEventStatus.Ok,
                Method = AttendanceMethod.Face,
                Source = "seed_bakinity_demo",
                RawPayloadJson = JsonSerializer.Serialize(new { Source = "BAK-DEMO seed", WorkerExternalId = worker.ExternalWorkerCode }, JsonOptions),
                CreatedAt = eventTime,
            });

            if (!await db.AttendanceSessions.AnyAsync(x => x.TenantId == tenantId && x.DeviceId == device.Id && x.WorkerExternalId == worker.ExternalWorkerCode && x.WorkDate == workDate, ct))
            {
                db.AttendanceSessions.Add(new AttendanceSession
                {
                    TenantId = tenantId,
                    SiteId = siteId,
                    DeviceId = device.Id,
                    WorkerId = worker.Id,
                    WorkerExternalId = worker.ExternalWorkerCode,
                    WorkerName = worker.FullName,
                    WorkDate = workDate,
                    CheckInEventId = eventId,
                    CheckInTime = eventTime,
                    LastSeenEventId = eventId,
                    LastSeenTime = eventTime.AddHours(4),
                    Status = AttendanceSessionStatus.Open,
                    Source = "seed_bakinity_demo",
                });
            }
        }
    }

    private static async Task UpsertAttendanceSeedAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers, CancellationToken ct)
    {
        const string seedSource = "seed_bakinity_demo";

        var existingSessions = await db.AttendanceSessions
            .Where(x => x.TenantId == tenantId && x.Source == seedSource)
            .ToListAsync(ct);
        var existingEvents = await db.AttendanceEvents
            .Where(x => x.TenantId == tenantId && x.Source == seedSource)
            .ToListAsync(ct);
        if (existingSessions.Count > 0 || existingEvents.Count > 0)
        {
            db.AttendanceSessions.RemoveRange(existingSessions);
            db.AttendanceEvents.RemoveRange(existingEvents);
            await db.SaveChangesAsync(ct);
        }

        var constructionSites = sites.Take(3).ToArray();
        var devicesBySite = new Dictionary<Guid, Device>();
        for (var i = 0; i < constructionSites.Length; i++)
        {
            var site = constructionSites[i];
            var registerId = $"BAK-DEMO-CAM-{i + 1:000}";
            var device = await db.Devices.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.RegisterDeviceId == registerId, ct);
            if (device is null)
            {
                device = new Device
                {
                    Id = StableGuid(registerId),
                    TenantId = tenantId,
                    RegisterDeviceId = registerId,
                    Username = "admin",
                    EncryptedPassword = "seed-managed-secret",
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                db.Devices.Add(device);
            }

            device.SiteId = site.Id;
            device.Name = $"BAKİNİTY demo kamera - {site.Name}";
            device.Vendor = "dahua";
            device.Model = "DHI-ASI6213J-MW";
            device.Mode = DeviceMode.Simulator;
            device.RegisterPort = 7000;
            device.Status = DeviceStatus.Pending;
            device.LastRecNo = 0;
            device.UpdatedAt = DateTimeOffset.UtcNow;
            devicesBySite[site.Id] = device;
        }

        var timeZone = AttendanceSchedulePolicy.ResolveTimeZone("Asia/Baku");
        var todayBaku = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
        var latestWorkDate = todayBaku.AddDays(-1);
        var workDates = Enumerable.Range(0, 14).Select(offset => latestWorkDate.AddDays(offset - 13)).ToArray();
        var orderedWorkers = workers
            .Where(worker => devicesBySite.ContainsKey(worker.SiteId))
            .OrderBy(worker => worker.ExternalWorkerCode)
            .ToArray();

        for (var dateIndex = 0; dateIndex < workDates.Length; dateIndex++)
        {
            var workDate = workDates[dateIndex];
            for (var workerIndex = 0; workerIndex < orderedWorkers.Length; workerIndex++)
            {
                var worker = orderedWorkers[workerIndex];
                if (!devicesBySite.TryGetValue(worker.SiteId, out var device)) continue;

                var isAbsent = (workerIndex + dateIndex * 3) % 7 == 0;
                if (isAbsent) continue;

                var pattern = SelectDemoAttendancePattern(workerIndex, dateIndex);
                var checkInUtc = AttendanceSchedulePolicy.ToUtc(workDate, pattern.CheckIn, timeZone);
                var checkOutUtc = AttendanceSchedulePolicy.ToUtc(workDate, pattern.CheckOut, timeZone);
                var entryEventId = StableGuid($"BAK-DEMO-ATT-IN-{worker.ExternalWorkerCode}-{workDate:yyyyMMdd}");
                var exitEventId = StableGuid($"BAK-DEMO-ATT-OUT-{worker.ExternalWorkerCode}-{workDate:yyyyMMdd}");
                var baseRecNo = dateIndex * 1000L + workerIndex * 2L + 1L;

                db.AttendanceEvents.Add(new AttendanceEvent
                {
                    Id = entryEventId,
                    TenantId = tenantId,
                    SiteId = worker.SiteId,
                    DeviceId = device.Id,
                    WorkerId = worker.Id,
                    WorkerExternalId = worker.ExternalWorkerCode,
                    WorkerName = worker.FullName,
                    EventTime = checkInUtc,
                    Direction = AttendanceDirection.Entry,
                    Status = AttendanceEventStatus.Ok,
                    Method = AttendanceMethod.Face,
                    RawRecNo = baseRecNo,
                    Source = seedSource,
                    RawPayloadJson = JsonSerializer.Serialize(new
                    {
                        Source = "BAK-DEMO seed",
                        WorkerExternalId = worker.ExternalWorkerCode,
                        WorkerName = worker.FullName,
                        SiteId = worker.SiteId,
                        WorkDate = workDate,
                        Pattern = pattern.Name,
                    }, JsonOptions),
                    CreatedAt = checkInUtc,
                });

                db.AttendanceEvents.Add(new AttendanceEvent
                {
                    Id = exitEventId,
                    TenantId = tenantId,
                    SiteId = worker.SiteId,
                    DeviceId = device.Id,
                    WorkerId = worker.Id,
                    WorkerExternalId = worker.ExternalWorkerCode,
                    WorkerName = worker.FullName,
                    EventTime = checkOutUtc,
                    Direction = AttendanceDirection.Exit,
                    Status = AttendanceEventStatus.Ok,
                    Method = AttendanceMethod.Face,
                    RawRecNo = baseRecNo + 1,
                    Source = seedSource,
                    RawPayloadJson = JsonSerializer.Serialize(new
                    {
                        Source = "BAK-DEMO seed",
                        WorkerExternalId = worker.ExternalWorkerCode,
                        WorkerName = worker.FullName,
                        SiteId = worker.SiteId,
                        WorkDate = workDate,
                        Pattern = pattern.Name,
                    }, JsonOptions),
                    CreatedAt = checkOutUtc,
                });

                db.AttendanceSessions.Add(new AttendanceSession
                {
                    Id = StableGuid($"BAK-DEMO-SESSION-{worker.ExternalWorkerCode}-{workDate:yyyyMMdd}"),
                    TenantId = tenantId,
                    SiteId = worker.SiteId,
                    DeviceId = device.Id,
                    WorkerId = worker.Id,
                    WorkerExternalId = worker.ExternalWorkerCode,
                    WorkerName = worker.FullName,
                    WorkDate = workDate,
                    CheckInEventId = entryEventId,
                    CheckInTime = checkInUtc,
                    CheckOutEventId = exitEventId,
                    CheckOutTime = checkOutUtc,
                    LastSeenEventId = exitEventId,
                    LastSeenTime = checkOutUtc,
                    CloseReason = "DeviceDirection",
                    PresenceStatus = "Closed",
                    Status = AttendanceSessionStatus.Closed,
                    Source = seedSource,
                    CreatedAt = checkInUtc,
                    UpdatedAt = checkOutUtc,
                });
            }
        }
    }

    private static DemoAttendancePattern SelectDemoAttendancePattern(int workerIndex, int dateIndex)
    {
        if ((workerIndex + dateIndex) % 23 == 0)
        {
            return new DemoAttendancePattern("late_early", new TimeOnly(8, 25), new TimeOnly(17, 10));
        }

        if ((workerIndex + dateIndex) % 17 == 0)
        {
            return new DemoAttendancePattern("early_exit", new TimeOnly(7, 58), new TimeOnly(16, 42));
        }

        if ((workerIndex + dateIndex * 2) % 8 == 0)
        {
            return (workerIndex + dateIndex) % 2 == 0
                ? new DemoAttendancePattern("late_21", new TimeOnly(8, 21), new TimeOnly(18, 5))
                : new DemoAttendancePattern("late_37", new TimeOnly(8, 37), new TimeOnly(18, 10));
        }

        if ((workerIndex + dateIndex) % 19 == 0)
        {
            return new DemoAttendancePattern("overtime", new TimeOnly(7, 55), new TimeOnly(20, 5));
        }

        return (workerIndex + dateIndex) % 2 == 0
            ? new DemoAttendancePattern("normal_early", new TimeOnly(7, 52), new TimeOnly(18, 4))
            : new DemoAttendancePattern("normal_grace", new TimeOnly(8, 2), new TimeOnly(18, 11));
    }

    private sealed record DemoAttendancePattern(string Name, TimeOnly CheckIn, TimeOnly CheckOut);

    private static async Task UpsertWarehouseWorkflowSeedAsync(BuildTrackDbContext db, Guid tenantId, Guid siteId, Guid supervisorId, Guid procurementUserId, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        var warehouse = await db.Warehouses.FirstAsync(x => x.TenantId == tenantId && x.IsDefault, ct);
        var byCode = await db.FieldWarehouseCatalogItems.Where(x => x.TenantId == tenantId && x.Code != null).ToDictionaryAsync(x => x.Code!, ct);
        var rows = new[]
        {
            ("BAK-WR-001", "PPE-HELMET", 20m, FieldWarehouseRequestStatus.PendingApproval, FieldWarehouseUrgency.Normal, "Yeni briqada üçün kaska ehtiyacı"),
            ("BAK-WR-002", "PPE-GLOVE", 150m, FieldWarehouseRequestStatus.NeedsJustification, FieldWarehouseUrgency.Urgent, "Əlcək normadan artıq istənilib"),
            ("BAK-WR-003", "FIN-TILE-ADHESIVE", 260m, FieldWarehouseRequestStatus.InFulfillment, FieldWarehouseUrgency.Critical, "Suvaq/plitka işləri dayanmasın"),
            ("BAK-WR-004", "PPE-VEST", 15m, FieldWarehouseRequestStatus.ReadyForPickup, FieldWarehouseUrgency.Normal, "Yeni işçilər üçün jilet"),
            ("BAK-WR-005", "TOOL-DRILL", 4m, FieldWarehouseRequestStatus.Rejected, FieldWarehouseUrgency.Normal, "Əlavə drel sorğusu əsaslandırılmadı"),
            ("BAK-WR-006", "CONS-CUT-DISC", 40m, FieldWarehouseRequestStatus.Issued, FieldWarehouseUrgency.Normal, "Kəsici disk gündəlik sərfiyyat"),
        };

        foreach (var row in rows)
        {
            if (!byCode.TryGetValue(row.Item2, out var catalog)) continue;
            var request = await db.FieldWarehouseRequests.Include(x => x.Lines).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == row.Item1, ct);
            if (request is null)
            {
                request = new FieldWarehouseRequest
                {
                    TenantId = tenantId,
                    ProjectId = StableGuid("BAK-DEMO-PROJECT"),
                    SiteId = siteId,
                    SupervisorUserId = supervisorId,
                    CatalogItemId = catalog.Id,
                    Code = row.Item1,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-rows.ToList().IndexOf(row) - 1),
                    SubmittedAt = DateTimeOffset.UtcNow.AddDays(-rows.ToList().IndexOf(row)),
                };
                db.FieldWarehouseRequests.Add(request);
            }

            request.RequestedQuantity = row.Item3;
            request.ApprovedQuantity = row.Item4 is FieldWarehouseRequestStatus.Rejected or FieldWarehouseRequestStatus.NeedsJustification ? 0 : row.Item3;
            request.ReservedQuantity = row.Item4 is FieldWarehouseRequestStatus.ReadyForPickup or FieldWarehouseRequestStatus.Issued ? row.Item3 : 0;
            request.IssuedQuantity = row.Item4 == FieldWarehouseRequestStatus.Issued ? row.Item3 : 0;
            request.Unit = catalog.Unit;
            request.Urgency = row.Item5;
            request.Reason = row.Item6;
            request.Status = row.Item4;
            request.AbnormalRequest = row.Item4 == FieldWarehouseRequestStatus.NeedsJustification;
            request.JustificationRequestNote = request.AbnormalRequest ? "Sorğu miqdarı norma limitindən yüksəkdir." : null;
            request.Justification = request.AbnormalRequest ? "Briqada sayı artırılıb, 2 günlük ehtiyat tələb olunur." : null;
            request.ManagerComment = row.Item4 == FieldWarehouseRequestStatus.Rejected ? "Mövcud inventar kifayətdir." : null;
            request.UpdatedAt = DateTimeOffset.UtcNow;

            var line = request.Lines.FirstOrDefault();
            if (line is null)
            {
                line = new FieldWarehouseRequestLine { TenantId = tenantId, Request = request, CatalogItemId = catalog.Id };
                request.Lines.Add(line);
            }

            line.CatalogItemId = catalog.Id;
            line.RequestedQuantity = row.Item3;
            line.ApprovedQuantity = request.ApprovedQuantity;
            line.ReservedQuantity = request.ReservedQuantity;
            line.IssuedQuantity = request.IssuedQuantity;
            line.Unit = catalog.Unit;
            line.Reason = row.Item6;
            line.Status = row.Item4 switch
            {
                FieldWarehouseRequestStatus.ReadyForPickup => FieldWarehouseRequestLineStatus.ReadyForIssue,
                FieldWarehouseRequestStatus.Issued => FieldWarehouseRequestLineStatus.Issued,
                FieldWarehouseRequestStatus.InFulfillment => FieldWarehouseRequestLineStatus.ProcurementInProgress,
                FieldWarehouseRequestStatus.Rejected => FieldWarehouseRequestLineStatus.Rejected,
                _ => FieldWarehouseRequestLineStatus.Pending,
            };

            if (row.Item4 == FieldWarehouseRequestStatus.InFulfillment && !await db.ProcurementNeeds.AnyAsync(x => x.TenantId == tenantId && x.SourceRequestId == request.Id, ct))
            {
                db.ProcurementNeeds.Add(new ProcurementNeed
                {
                    TenantId = tenantId,
                    ProjectId = request.ProjectId,
                    SiteId = siteId,
                    WarehouseId = warehouse.Id,
                    SourceRequest = request,
                    SourceRequestLine = line,
                    CatalogItemId = catalog.Id,
                    RequiredQuantity = row.Item3,
                    AlreadyAvailableQuantity = 35,
                    ShortfallQuantity = row.Item3 - 35,
                    Unit = catalog.Unit,
                    Priority = row.Item5,
                    Reason = "Anbarda qismən var, əlavə satınalma tələb olunur",
                    Status = ProcurementNeedStatus.Assigned,
                    CreatedByUserId = supervisorId,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        await RebuildWarehouseWorkflowSeedWithCanonicalServiceAsync(db, tenantId, siteId, supervisorId, procurementUserId, warehouse.Id, byCode, ct);
        var need = await db.ProcurementNeeds.Include(x => x.CatalogItem).FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Status == ProcurementNeedStatus.Assigned, ct);
        if (need is not null && !await db.ProcurementTasks.AnyAsync(x => x.TenantId == tenantId && x.Code == "BAK-PO-001", ct))
        {
            var task = new ProcurementTask
            {
                TenantId = tenantId,
                Code = "BAK-PO-001",
                AssignedProcurementUserId = procurementUserId,
                Status = ProcurementTaskStatus.Shopping,
                Priority = need.Priority,
                RequiredBy = need.RequiredBy,
                ManagerInstruction = "Qiymət və məhsul şəkli ilə təsdiqə göndərin.",
                AssignedAt = DateTimeOffset.UtcNow.AddDays(-1),
                StartedAt = DateTimeOffset.UtcNow.AddHours(-8),
            };
            task.Lines.Add(new ProcurementTaskLine
            {
                TenantId = tenantId,
                ProcurementNeedId = need.Id,
                CatalogItemId = need.CatalogItemId,
                RequestedQuantity = need.ShortfallQuantity,
                Unit = need.Unit,
                Status = ProcurementTaskLineStatus.Searching,
                Note = "Demo satınalma prosesi",
            });
            db.ProcurementTasks.Add(task);
        }
    }

    private static async Task RebuildWarehouseWorkflowSeedWithCanonicalServiceAsync(
        BuildTrackDbContext db,
        Guid tenantId,
        Guid siteId,
        Guid supervisorId,
        Guid procurementUserId,
        Guid warehouseId,
        IReadOnlyDictionary<string, FieldWarehouseCatalogItem> catalogByCode,
        CancellationToken ct)
    {
        var rows = new[]
        {
            (Code: "BAK-WR-001", ItemCode: "PPE-HELMET", Quantity: 20m, FinalStatus: FieldWarehouseRequestStatus.PendingApproval, Urgency: FieldWarehouseUrgency.Normal, Reason: "Yeni briqada üçün kaska ehtiyacı"),
            (Code: "BAK-WR-002", ItemCode: "PPE-GLOVE", Quantity: 150m, FinalStatus: FieldWarehouseRequestStatus.NeedsJustification, Urgency: FieldWarehouseUrgency.Urgent, Reason: "Əlcək normadan artıq istənilib"),
            (Code: "BAK-WR-003", ItemCode: "FIN-TILE-ADHESIVE", Quantity: 260m, FinalStatus: FieldWarehouseRequestStatus.InFulfillment, Urgency: FieldWarehouseUrgency.Critical, Reason: "Suvaq/plitka işləri dayanmasın"),
            (Code: "BAK-WR-004", ItemCode: "PPE-VEST", Quantity: 15m, FinalStatus: FieldWarehouseRequestStatus.ReadyForPickup, Urgency: FieldWarehouseUrgency.Normal, Reason: "Yeni işçilər üçün jilet"),
            (Code: "BAK-WR-005", ItemCode: "TOOL-DRILL", Quantity: 4m, FinalStatus: FieldWarehouseRequestStatus.Rejected, Urgency: FieldWarehouseUrgency.Normal, Reason: "Əlavə drel sorğusu əsaslandırılmadı"),
            (Code: "BAK-WR-006", ItemCode: "CONS-CUT-DISC", Quantity: 40m, FinalStatus: FieldWarehouseRequestStatus.Issued, Urgency: FieldWarehouseUrgency.Normal, Reason: "Kəsici disk gündəlik sərfiyyat"),
        };

        await ResetWarehouseWorkflowSeedArtifactsAsync(db, tenantId, rows.Select(x => x.Code).ToArray(), ct);

        var supplyService = new SupplyChainService(db, new WarehouseAvailabilityService(db), new WarehouseUsagePolicyService(db));
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (!catalogByCode.TryGetValue(row.ItemCode, out var catalog)) continue;

            var request = await db.FieldWarehouseRequests
                .Include(x => x.Lines)
                .FirstAsync(x => x.TenantId == tenantId && x.Code == row.Code, ct);
            request.ProjectId = StableGuid("BAK-DEMO-PROJECT");
            request.SiteId = siteId;
            request.SupervisorUserId = supervisorId;
            request.CatalogItemId = catalog.Id;
            request.RequestedQuantity = row.Quantity;
            request.ApprovedQuantity = 0;
            request.ReservedQuantity = 0;
            request.IssuedQuantity = 0;
            request.Unit = catalog.Unit;
            request.NeededBy = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(5 + index));
            request.Urgency = row.Urgency;
            request.Reason = row.Reason;
            request.Status = FieldWarehouseRequestStatus.PendingApproval;
            request.AbnormalRequest = row.FinalStatus == FieldWarehouseRequestStatus.NeedsJustification;
            request.JustificationRequestNote = request.AbnormalRequest ? "Sorğu miqdarı norma limitindən yüksəkdir." : null;
            request.Justification = request.AbnormalRequest ? "Briqada sayı artırılıb, 2 günlük ehtiyat tələb olunur." : null;
            request.ManagerComment = row.FinalStatus == FieldWarehouseRequestStatus.Rejected ? "Mövcud inventar kifayətdir." : null;
            request.ReviewedAt = null;
            request.ReviewedByUserId = null;
            request.UpdatedAt = DateTimeOffset.UtcNow;

            var line = request.Lines.Single();
            line.CatalogItemId = catalog.Id;
            line.RequestedQuantity = row.Quantity;
            line.ApprovedQuantity = 0;
            line.ReservedQuantity = 0;
            line.IssuedQuantity = 0;
            line.Unit = catalog.Unit;
            line.Reason = row.Reason;
            line.Status = row.FinalStatus == FieldWarehouseRequestStatus.Rejected
                ? FieldWarehouseRequestLineStatus.Rejected
                : FieldWarehouseRequestLineStatus.Pending;
            line.UpdatedAt = DateTimeOffset.UtcNow;

            if (row.FinalStatus == FieldWarehouseRequestStatus.NeedsJustification)
            {
                request.Status = FieldWarehouseRequestStatus.NeedsJustification;
                await db.SaveChangesAsync(ct);
                continue;
            }

            if (row.FinalStatus == FieldWarehouseRequestStatus.Rejected)
            {
                request.Status = FieldWarehouseRequestStatus.Rejected;
                await db.SaveChangesAsync(ct);
                continue;
            }

            if (row.FinalStatus == FieldWarehouseRequestStatus.PendingApproval)
            {
                request.Status = FieldWarehouseRequestStatus.PendingApproval;
                await db.SaveChangesAsync(ct);
                continue;
            }

            await db.SaveChangesAsync(ct);
            await supplyService.ApproveFieldRequestAsync(tenantId, request.Id, supervisorId, "BAK-DEMO canonical stock check", ct);

            request = await db.FieldWarehouseRequests.Include(x => x.Lines).FirstAsync(x => x.TenantId == tenantId && x.Id == request.Id, ct);
            line = request.Lines.Single();

            if (row.FinalStatus == FieldWarehouseRequestStatus.ReadyForPickup)
            {
                request.Status = FieldWarehouseRequestStatus.ReadyForPickup;
                line.Status = FieldWarehouseRequestLineStatus.ReadyForIssue;
                request.UpdatedAt = DateTimeOffset.UtcNow;
                line.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            else if (row.FinalStatus == FieldWarehouseRequestStatus.Issued)
            {
                line.Status = FieldWarehouseRequestLineStatus.ReadyForIssue;
                request.Status = FieldWarehouseRequestStatus.ReadyForPickup;
                await db.SaveChangesAsync(ct);
                await supplyService.IssueFieldRequestAsync(tenantId, request.Id, warehouseId, supervisorId, "BAK-DEMO prorab", "Demo təhvil", ct);
            }
            else if (row.FinalStatus == FieldWarehouseRequestStatus.InFulfillment)
            {
                var need = await db.ProcurementNeeds
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SourceRequestId == request.Id && x.Status != ProcurementNeedStatus.Cancelled, ct);
                if (need is null) continue;

                var task = await supplyService.CreateProcurementTaskAsync(
                    tenantId,
                    new[] { need.Id },
                    procurementUserId,
                    supervisorId,
                    "Qiymət və məhsul şəkli ilə təsdiqə göndərin.",
                    ct);
                task.Code = "BAK-PO-001";
                task.Status = ProcurementTaskStatus.Shopping;
                task.StartedAt = DateTimeOffset.UtcNow.AddHours(-8);
                foreach (var taskLine in task.Lines)
                {
                    taskLine.Status = ProcurementTaskLineStatus.Searching;
                    taskLine.Note = "Demo satınalma prosesi";
                    taskLine.UpdatedAt = DateTimeOffset.UtcNow;
                }

                need.Status = ProcurementNeedStatus.Assigned;
                need.UpdatedAt = DateTimeOffset.UtcNow;
                line.Status = FieldWarehouseRequestLineStatus.ProcurementInProgress;
                line.UpdatedAt = DateTimeOffset.UtcNow;
                request.Status = FieldWarehouseRequestStatus.InFulfillment;
                request.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static async Task ResetWarehouseWorkflowSeedArtifactsAsync(BuildTrackDbContext db, Guid tenantId, IReadOnlyCollection<string> seedRequestCodes, CancellationToken ct)
    {
        var requests = await db.FieldWarehouseRequests
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && seedRequestCodes.Contains(x.Code))
            .ToListAsync(ct);
        var requestIds = requests.Select(x => x.Id).ToArray();
        var requestLineIds = requests.SelectMany(x => x.Lines).Select(x => x.Id).ToArray();

        var oldTasks = await db.ProcurementTasks
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId && x.Code == "BAK-PO-001")
            .ToListAsync(ct);
        db.ProcurementTaskLines.RemoveRange(oldTasks.SelectMany(x => x.Lines));
        db.ProcurementTasks.RemoveRange(oldTasks);

        if (requestIds.Length > 0)
        {
            var needs = await db.ProcurementNeeds
                .Where(x => x.TenantId == tenantId && requestIds.Contains(x.SourceRequestId))
                .ToListAsync(ct);
            db.ProcurementNeeds.RemoveRange(needs);

            var issues = await db.WarehouseIssues
                .Include(x => x.Lines)
                .Where(x => x.TenantId == tenantId && requestIds.Contains(x.FieldRequestId))
                .ToListAsync(ct);
            var issueReservationIds = issues.SelectMany(x => x.Lines).Select(x => x.ReservationId).ToArray();
            var directReservationIds = requestLineIds.Length == 0
                ? Array.Empty<Guid>()
                : await db.WarehouseReservations
                    .Where(x => x.TenantId == tenantId && requestLineIds.Contains(x.RequestLineId))
                    .Select(x => x.Id)
                    .ToArrayAsync(ct);
            var reservationIds = issueReservationIds.Concat(directReservationIds).Distinct().ToArray();

            var seedIssueMovements = await db.WarehouseStockMovements
                .Where(x => x.TenantId == tenantId
                    && x.ReferenceType == "WarehouseIssueLine"
                    && x.ReferenceId.HasValue
                    && reservationIds.Contains(x.ReferenceId.Value))
                .ToListAsync(ct);
            db.WarehouseStockMovements.RemoveRange(seedIssueMovements);

            db.WarehouseIssueLines.RemoveRange(issues.SelectMany(x => x.Lines));
            db.WarehouseIssues.RemoveRange(issues);

            if (reservationIds.Length > 0)
            {
                var reservations = await db.WarehouseReservations
                    .Where(x => x.TenantId == tenantId && reservationIds.Contains(x.Id))
                    .ToListAsync(ct);
                db.WarehouseReservations.RemoveRange(reservations);
            }
        }

        await db.SaveChangesAsync(ct);
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
    }

    private static string BuildWorkspaceJson(Guid tenantId, IReadOnlyList<Site> sites, IReadOnlyList<Worker> workers)
    {
        var projectId = StableGuid("BAK-DEMO-PROJECT").ToString();
        var estimateId = StableGuid("BAK-DEMO-ESTIMATE-V1").ToString();
        var stageRows = BuildStageRows();
        var stages = stageRows.Select((row, index) => new
        {
            id = StableGuid($"BAK-DEMO-STAGE-{index + 1}").ToString(),
            objectId = sites[index % Math.Min(3, sites.Count)].Id.ToString(),
            name = row.Name,
            order = index + 1,
            totalCost = row.Total,
            laborCost = row.Labor,
            materialCost = row.Material,
            plannedStartDate = DateTime.UtcNow.Date.AddDays(index * 10 - 20).ToString("yyyy-MM-dd"),
            plannedEndDate = DateTime.UtcNow.Date.AddDays(index * 10 + 25).ToString("yyyy-MM-dd"),
            status = row.Progress >= 100 ? "Completed" : row.Progress > 0 ? "InProgress" : "NotStarted",
            progressPercent = row.Progress,
            assignedCrewId = StableGuid($"BAK-DEMO-CREW-{(index % 6) + 1}").ToString(),
            plannedHours = row.PlannedHours,
            actualHours = Math.Round(row.PlannedHours * row.Progress / 100m, 1),
            notes = "BAK-DEMO server seed",
        }).ToArray();
        var workItems = BuildSmetaRows().Select((row, index) => new
        {
            id = StableGuid($"BAK-DEMO-WORK-{index + 1}").ToString(),
            objectId = sites[index % Math.Min(3, sites.Count)].Id.ToString(),
            stageId = stages[index % stages.Length].id,
            name = row.Work,
            costCode = $"BAK-{index + 1:000}",
            unit = row.Unit,
            quantity = row.Quantity,
            unitPrice = row.Total / Math.Max(row.Quantity, 1),
            completedQuantity = Math.Round(row.Quantity * row.Progress / 100m, 2),
            laborUnitPrice = row.LaborUnit,
            laborTotal = row.LaborTotal,
            materialUnit = row.Unit,
            materialQuantity = row.Quantity,
            materialUnitPrice = row.MaterialUnit,
            materialTotal = row.MaterialTotal,
            totalCost = row.Total,
            plannedHours = row.PlannedHours,
            actualHours = Math.Round(row.PlannedHours * row.Progress / 100m, 1),
            assignedCrewId = StableGuid($"BAK-DEMO-CREW-{(index % 6) + 1}").ToString(),
            status = row.Progress >= 100 ? "Completed" : row.Progress > 0 ? "InProgress" : "NotStarted",
            progressPercent = row.Progress,
            notes = row.Category,
        }).ToArray();
        var crews = new[]
        {
            ("Monolit briqadası", "Monolit", "Prorab 01"),
            ("Armatur briqadası", "Armatur", "Prorab 02"),
            ("Hörgü briqadası", "Hörgü", "Prorab 03"),
            ("Suvaq briqadası", "Suvaq", "Prorab 04"),
            ("Elektrik/Santexnik briqadası", "MEP", "Prorab 05"),
            ("Material və logistika", "Logistika", "Prorab 06"),
        }.Select((row, index) => new
        {
            id = StableGuid($"BAK-DEMO-CREW-{index + 1}").ToString(),
            objectId = sites[index % Math.Min(3, sites.Count)].Id.ToString(),
            name = row.Item1,
            type = row.Item2,
            foremanName = row.Item3,
            workerCount = workers.Count(x => x.Brigade == row.Item1),
            activeWorkStageId = stages[index % stages.Length].id,
            activeWorkItemId = workItems[index % workItems.Length].id,
            plannedDailyHours = 8,
            status = "InProgress",
            progressPercent = stages[index % stages.Length].progressPercent,
            notes = "Serverdən idarə olunan demo briqada",
        }).ToArray();

        var data = new
        {
            workspaceTenantId = tenantId.ToString(),
            projects = new[]
            {
                new
                {
                    id = projectId,
                    name = "BAKİNİTY Residence",
                    currency = "AZN",
                    location = "Bakı",
                    clientName = "BAKİNİTY MMC",
                    createdAt = DateTime.UtcNow.AddDays(-45).ToString("O"),
                    activeEstimateVersionId = estimateId,
                },
            },
            activeProjectId = projectId,
            objects = sites.Select((site, index) => new
            {
                id = site.Id.ToString(),
                name = site.Name,
                zone = site.Address,
                address = site.Address,
                projectId,
                status = index == 3 ? "NotStarted" : "InProgress",
                plannedStartDate = DateTime.UtcNow.Date.AddDays(index * 7 - 30).ToString("yyyy-MM-dd"),
                plannedEndDate = DateTime.UtcNow.Date.AddDays(index * 25 + 120).ToString("yyyy-MM-dd"),
                clientName = "BAKİNİTY MMC",
                notes = "Server seed layihəsi",
            }).ToArray(),
            project = new
            {
                id = projectId,
                name = "BAKİNİTY Residence",
                currency = "AZN",
                location = "Bakı",
                clientName = "BAKİNİTY MMC",
                createdAt = DateTime.UtcNow.AddDays(-45).ToString("O"),
                activeEstimateVersionId = estimateId,
            },
            estimateVersions = new[]
            {
                new
                {
                    id = estimateId,
                    projectId,
                    name = "BAKİNİTY demo smeta v1",
                    createdAt = DateTime.UtcNow.AddDays(-40).ToString("O"),
                    totalAmount = 316822.70m,
                    notes = "Server-authoritative demo smeta",
                },
            },
            summary = new
            {
                totalAmount = 316822.70m,
                laborAmount = 69717.50m,
                materialAmount = 205730.50m,
                hiddenCostAmount = 41324.70m,
                currency = "AZN",
            },
            stages,
            workItems,
            crews,
            workerAssignments = workers.Select((worker, index) => new
            {
                id = worker.Id.ToString(),
                workerName = worker.FullName,
                workerExternalId = worker.ExternalWorkerCode,
                projectId,
                objectId = worker.SiteId.ToString(),
                crewId = crews[index % crews.Length].id,
                role = worker.Role,
                hourlyRate = worker.HourlyRate,
                plannedDailyHours = worker.PlannedDailyHours,
                activeStageId = stages[index % stages.Length].id,
                activeWorkItemId = workItems[index % workItems.Length].id,
                attendanceSource = worker.AttendanceSource,
                status = "active",
                riskScore = worker.RiskScore,
                notes = "Server worker seed",
            }).ToArray(),
            materials = new[]
            {
                Material("MAT-REBAR-A3", "Armatur A3", "ton", 20.75m, 8.2m, workItems[1].objectId, workItems[1].stageId, workItems[1].id, workItems[1].materialUnitPrice),
                Material("MAT-CONCRETE-B25", "Beton B25", "m3", 328.2m, 126.5m, workItems[2].objectId, workItems[2].stageId, workItems[2].id, workItems[2].materialUnitPrice),
                Material("FORM-TIMBER", "Taxta", "m3", 19m, 7.4m, workItems[4].objectId, workItems[4].stageId, workItems[4].id, workItems[4].materialUnitPrice),
                Material("PPE-HELMET", "Kaska", "ədəd", 140m, 22m, workItems[0].objectId, workItems[0].stageId, workItems[0].id, workItems[0].materialUnitPrice),
                Material("PPE-GLOVE", "İş əlcəyi", "cüt", 420m, 96m, workItems[0].objectId, workItems[0].stageId, workItems[0].id, workItems[0].materialUnitPrice),
                Material("CONS-DRILL-BIT-10", "Sverlo 10 mm", "ədəd", 65m, 17m, workItems[6].objectId, workItems[6].stageId, workItems[6].id, workItems[6].materialUnitPrice),
            },
            attendanceSessions = Array.Empty<object>(),
            workHourAllocations = Array.Empty<object>(),
            dailyReports = Array.Empty<object>(),
            issues = new[]
            {
                new { id = StableGuid("BAK-DEMO-ISSUE-1").ToString(), projectId, objectId = sites[0].Id.ToString(), stageId = stages[3].id, type = "Material", title = "Suvaq materialı üzrə tədarük nəzarətdədir", severity = "Medium", status = "Watching", dueDate = DateTime.UtcNow.Date.AddDays(5).ToString("yyyy-MM-dd"), createdAt = DateTime.UtcNow.AddDays(-2).ToString("O") },
                new { id = StableGuid("BAK-DEMO-ISSUE-2").ToString(), projectId, objectId = sites[1].Id.ToString(), stageId = stages[1].id, type = "Schedule", title = "Monolit işlərində 2 günlük gecikmə riski", severity = "High", status = "Open", dueDate = DateTime.UtcNow.Date.AddDays(3).ToString("yyyy-MM-dd"), createdAt = DateTime.UtcNow.AddDays(-1).ToString("O") },
            },
            risks = Array.Empty<object>(),
            assistantMessages = Array.Empty<object>(),
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    private static object Material(string code, string name, string unit, decimal quantity, decimal used, string objectId, string stageId, string workItemId, decimal unitPrice) => new
    {
        id = StableGuid($"BAK-DEMO-MAT-{code}").ToString(),
        objectId,
        catalogItemId = code,
        category = "Tikinti materialı",
        name,
        unit,
        quantity,
        usedQuantity = used,
        remainingQuantity = quantity - used,
        unitPrice,
        linkedStageId = stageId,
        linkedWorkItemId = workItemId,
        deliveryDate = DateTime.UtcNow.Date.AddDays(10).ToString("yyyy-MM-dd"),
        supplier = "BAKİNİTY təchizat",
        notes = "Server seed material planı",
    };

    private static SeedDailyReportLine SeedLine(int workItemNumber, decimal quantity, int workerCount, decimal workHours) =>
        new(workItemNumber, quantity, workerCount, workHours);

    private sealed record SeedDailyReport(
        string Code,
        Guid SiteId,
        Guid SupervisorUserId,
        DateOnly ReportDate,
        FieldDailyReportStatus Status,
        string Weather,
        string Note,
        string? ReviewNote,
        IReadOnlyList<SeedDailyReportLine> Lines);

    private sealed record SeedDailyReportLine(int WorkItemNumber, decimal Quantity, int WorkerCount, decimal WorkHours);

    private static (string Name, decimal Total, decimal Labor, decimal Material, int Progress, decimal PlannedHours)[] BuildStageRows() =>
    [
        ("Torpaq işləri", 6850m, 2800m, 3200m, 100, 160m),
        ("Monolit dəmir beton lentvari bünövrə / Zirzəmi", 30311.40m, 9400m, 17400m, 72, 520m),
        ("Birinci mərtəbənin monolit d/beton konstruksiyaları", 67113.80m, 14800m, 42100m, 45, 860m),
        ("İkinci mərtəbənin monolit d/beton konstruksiyaları", 26632.80m, 7200m, 15700m, 18, 430m),
        ("Dam örtüyü", 24750m, 6200m, 15200m, 0, 260m),
        ("Hörgü işləri", 11970m, 5400m, 5200m, 34, 310m),
        ("Qapı və pəncərələr", 20800m, 3100m, 16500m, 0, 120m),
        ("Suvaq işləri", 87070m, 20817.5m, 70430.5m, 9, 980m),
        ("Digər işlər", 0m, 0m, 0m, 0, 80m),
    ];

    private static (string Stage, string Work, string Unit, string Category, decimal Quantity, decimal LaborUnit, decimal LaborTotal, decimal MaterialUnit, decimal MaterialTotal, decimal Total, int Progress, decimal PlannedHours)[] BuildSmetaRows() =>
    [
        ("Torpaq işləri", "Torpaq qazıntısı və meydança hazırlığı", "m3", "Kaba işlər", 340m, 8m, 2720m, 12m, 4080m, 6800m, 100, 160m),
        ("Bünövrə / Zirzəmi", "Armatur karkasının yığılması", "ton", "Monolit", 8.5m, 520m, 4420m, 980m, 8330m, 12750m, 72, 220m),
        ("Bünövrə / Zirzəmi", "Beton B25 tökülməsi", "m3", "Monolit", 86m, 18m, 1548m, 125m, 10750m, 12298m, 70, 180m),
        ("Bünövrə / Zirzəmi", "Hidroizolyasiya", "m2", "İzolyasiya", 260m, 9m, 2340m, 22m, 5720m, 8060m, 62, 120m),
        ("1-ci mərtəbə monolit", "Qəlib və beton konstruksiyalar", "m3", "Monolit", 128m, 42m, 5376m, 310m, 39680m, 45056m, 45, 340m),
        ("2-ci mərtəbə monolit", "Monolit konstruksiya işləri", "m3", "Monolit", 72m, 38m, 2736m, 285m, 20520m, 23256m, 18, 260m),
        ("Dam örtüyü", "Dam örtüyü və membran", "m2", "Dam", 310m, 20m, 6200m, 49m, 15190m, 21390m, 0, 260m),
        ("Hörgü işləri", "Kubik/qazbeton hörgü", "m2", "Hörgü", 1270m, 4.2m, 5334m, 5.2m, 6604m, 11938m, 34, 310m),
        ("Qapı və pəncərələr", "Alüminyum pəncərə quraşdırılması", "m2", "Pəncərə", 65m, 45m, 2925m, 275m, 17875m, 20800m, 0, 120m),
        ("Suvaq işləri", "Daxili və xarici suvaq", "m2", "Suvaq", 1850m, 11m, 20350m, 35m, 64750m, 85100m, 9, 980m),
    ];

    private static Guid StableGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private static bool ParseBool(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
