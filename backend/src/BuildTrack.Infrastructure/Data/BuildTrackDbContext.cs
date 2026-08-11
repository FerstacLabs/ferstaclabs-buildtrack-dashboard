using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Data;

public sealed class BuildTrackDbContext : DbContext
{
    private readonly ITenantContext? tenantContext;

    public BuildTrackDbContext(DbContextOptions<BuildTrackDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        this.tenantContext = tenantContext;
    }

    private Guid? CurrentTenantId => tenantContext?.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<WorkerCameraIdentity> WorkerCameraIdentities => Set<WorkerCameraIdentity>();
    public DbSet<WorkerSiteAssignment> WorkerSiteAssignments => Set<WorkerSiteAssignment>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<DeviceConnectionLog> DeviceConnectionLogs => Set<DeviceConnectionLog>();
    public DbSet<NetSdkRuntimeDiagnostics> NetSdkRuntimeDiagnostics => Set<NetSdkRuntimeDiagnostics>();
    public DbSet<DahuaActiveRegisterRawEvent> DahuaActiveRegisterRawEvents => Set<DahuaActiveRegisterRawEvent>();
    public DbSet<SupervisorSiteAssignment> SupervisorSiteAssignments => Set<SupervisorSiteAssignment>();
    public DbSet<FieldSmetaItem> FieldSmetaItems => Set<FieldSmetaItem>();
    public DbSet<SupervisorDailyReport> SupervisorDailyReports => Set<SupervisorDailyReport>();
    public DbSet<SupervisorDailyReportLine> SupervisorDailyReportLines => Set<SupervisorDailyReportLine>();
    public DbSet<SupervisorSiteNote> SupervisorSiteNotes => Set<SupervisorSiteNote>();
    public DbSet<SupervisorWorkerEvent> SupervisorWorkerEvents => Set<SupervisorWorkerEvent>();
    public DbSet<FieldWarehouseCatalogItem> FieldWarehouseCatalogItems => Set<FieldWarehouseCatalogItem>();
    public DbSet<FieldWarehouseRequest> FieldWarehouseRequests => Set<FieldWarehouseRequest>();
    public DbSet<SupervisorAuditEvent> SupervisorAuditEvents => Set<SupervisorAuditEvent>();
    public DbSet<SupplyUnit> SupplyUnits => Set<SupplyUnit>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<FieldWarehouseRequestLine> FieldWarehouseRequestLines => Set<FieldWarehouseRequestLine>();
    public DbSet<WarehouseReservation> WarehouseReservations => Set<WarehouseReservation>();
    public DbSet<WarehouseStockMovement> WarehouseStockMovements => Set<WarehouseStockMovement>();
    public DbSet<WarehouseUsagePolicy> WarehouseUsagePolicies => Set<WarehouseUsagePolicy>();
    public DbSet<ProcurementNeed> ProcurementNeeds => Set<ProcurementNeed>();
    public DbSet<ProcurementTask> ProcurementTasks => Set<ProcurementTask>();
    public DbSet<ProcurementTaskLine> ProcurementTaskLines => Set<ProcurementTaskLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ProcurementAttachment> ProcurementAttachments => Set<ProcurementAttachment>();
    public DbSet<ProcurementReceipt> ProcurementReceipts => Set<ProcurementReceipt>();
    public DbSet<ProcurementReceiptLine> ProcurementReceiptLines => Set<ProcurementReceiptLine>();
    public DbSet<CatalogItemPurchasePrice> CatalogItemPurchasePrices => Set<CatalogItemPurchasePrice>();
    public DbSet<WarehouseGoodsReceipt> WarehouseGoodsReceipts => Set<WarehouseGoodsReceipt>();
    public DbSet<WarehouseGoodsReceiptLine> WarehouseGoodsReceiptLines => Set<WarehouseGoodsReceiptLine>();
    public DbSet<WarehouseIssue> WarehouseIssues => Set<WarehouseIssue>();
    public DbSet<WarehouseIssueLine> WarehouseIssueLines => Set<WarehouseIssueLine>();
    public DbSet<SupplyNotification> SupplyNotifications => Set<SupplyNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(60);
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany(x => x.Users).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LicenseKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Plan).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.LicenseKeyHash).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany(x => x.Licenses).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("sites");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("workers");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.ExternalWorkerCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Brigade).HasMaxLength(120);
            entity.Property(x => x.Role).HasMaxLength(120);
            entity.Property(x => x.HourlyRate).HasPrecision(18, 2);
            entity.Property(x => x.PlannedDailyHours).HasPrecision(8, 2);
            entity.Property(x => x.AttendanceSource).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.SiteId, x.ExternalWorkerCode }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany(x => x.Workers).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkerCameraIdentity>(entity =>
        {
            entity.ToTable("worker_camera_identities");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Vendor).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ExternalUserId).HasMaxLength(80);
            entity.Property(x => x.CardName).HasMaxLength(180);
            entity.Property(x => x.NormalizedCardName).HasMaxLength(180);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.WorkerId);
            entity.HasIndex(x => x.DeviceId);
            entity.HasIndex(x => new { x.TenantId, x.DeviceId, x.NormalizedCardName })
                .IsUnique()
                .HasFilter("\"NormalizedCardName\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.DeviceId, x.ExternalUserId })
                .IsUnique()
                .HasFilter("\"ExternalUserId\" IS NOT NULL");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany(x => x.CameraIdentities).HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkerSiteAssignment>(entity =>
        {
            entity.ToTable("worker_site_assignments");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.WorkerId);
            entity.HasIndex(x => x.SiteId);
            entity.HasIndex(x => new { x.TenantId, x.WorkerId, x.SiteId, x.Status })
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany(x => x.SiteAssignments).HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Vendor).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Mode).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.RegisterDeviceId).HasMaxLength(160).IsRequired();
            entity.Property(x => x.LastKnownIp).HasMaxLength(80);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EncryptedPassword).HasMaxLength(700).IsRequired();
            entity.Property(x => x.CgiLastRecNo);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.RegisterDeviceId).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany(x => x.Devices).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttendanceEvent>(entity =>
        {
            entity.ToTable("attendance_events");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.WorkerExternalId).HasMaxLength(80);
            entity.Property(x => x.WorkerName).HasMaxLength(180);
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.SnapshotPath).HasMaxLength(500);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany(x => x.AttendanceEvents).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.DeviceId, x.RawRecNo }).IsUnique().HasFilter("\"RawRecNo\" IS NOT NULL");
            entity.HasIndex(x => new { x.DeviceId, x.WorkerExternalId, x.EventTime, x.Method }).IsUnique().HasFilter("\"WorkerExternalId\" IS NOT NULL");
        });



        modelBuilder.Entity<AttendanceSession>(entity =>
        {
            entity.ToTable("attendance_sessions");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.WorkerExternalId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.WorkerName).HasMaxLength(180);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.CloseReason).HasMaxLength(50);
            entity.Property(x => x.PresenceStatus).HasMaxLength(50);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CheckInEvent).WithMany().HasForeignKey(x => x.CheckInEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CheckOutEvent).WithMany().HasForeignKey(x => x.CheckOutEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.LastSeenEvent).WithMany().HasForeignKey(x => x.LastSeenEventId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.SiteId, x.WorkDate });
            entity.HasIndex(x => new { x.DeviceId, x.WorkerExternalId, x.WorkDate });
            entity.HasIndex(x => new { x.SiteId, x.DeviceId, x.WorkerExternalId, x.WorkDate })
                .IsUnique()
                .HasDatabaseName("IX_AttendanceSessions_DailyUnique");
            entity.HasIndex(x => new { x.WorkerExternalId, x.WorkDate });
            entity.HasIndex(x => new { x.DeviceId, x.WorkerExternalId, x.WorkDate, x.Status })
                .IsUnique()
                .HasFilter("\"Status\" = 'Open'");
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("security_events");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(40);
            entity.Property(x => x.Direction).HasMaxLength(30);
            entity.Property(x => x.SnapshotPath).HasMaxLength(500);
            entity.Property(x => x.SnapshotUrl).HasMaxLength(1000);
            entity.Property(x => x.StoredSnapshotPath).HasMaxLength(500);
            entity.Property(x => x.StoredSnapshotContentType).HasMaxLength(80);
            entity.Property(x => x.SnapshotDownloadStatus).HasMaxLength(40);
            entity.Property(x => x.SnapshotDownloadError).HasMaxLength(500);
            entity.Property(x => x.SnapshotSource).HasMaxLength(80);
            entity.Property(x => x.ErrorCode).HasMaxLength(50);
            entity.Property(x => x.Message).HasMaxLength(300);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.ReviewNote).HasMaxLength(500);
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.SiteId, x.EventDate });
            entity.HasIndex(x => new { x.DeviceId, x.RawRecNo }).IsUnique().HasFilter("\"RawRecNo\" IS NOT NULL");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.EventType);
        });
        modelBuilder.Entity<NetSdkRuntimeDiagnostics>(entity =>
        {
            entity.ToTable("netsdk_runtime_diagnostics");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ListenerPortsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.LastServiceEventType).HasMaxLength(120);
            entity.Property(x => x.ActiveRegisterServiceMode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ExperimentalStartServiceLastDecodeStatus).HasMaxLength(1000);
            entity.Property(x => x.ExperimentalStartServiceErrorHex).HasMaxLength(40);
                        entity.Property(x => x.LastServicePayloadFirst256Hex).HasMaxLength(512);
            entity.Property(x => x.LastRegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.LastParsedRegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.LastParsedSerial).HasMaxLength(160);
            entity.Property(x => x.LastParsedRemoteIp).HasMaxLength(80);
            entity.Property(x => x.LastPossibleSessionHandlesJson).HasColumnType("jsonb");
            entity.Property(x => x.LastPayloadStructLayout).HasMaxLength(1000);
            entity.Property(x => x.LastExperimentalSubscribeJson).HasColumnType("jsonb");
            entity.Property(x => x.ResponseDevRegErrorHex).HasMaxLength(40);
            entity.Property(x => x.ResponseDevRegDevSerial).HasMaxLength(160);
            entity.Property(x => x.ResponseDevRegIp).HasMaxLength(80);
            entity.Property(x => x.ResponseDevRegCommandSource).HasMaxLength(120);
            entity.Property(x => x.ActiveRegisterSessionHandleSource).HasMaxLength(120);
            entity.Property(x => x.ActiveRegisterSessionHandleStrategyResult).HasMaxLength(80);
            entity.Property(x => x.LoginStrategy).HasMaxLength(120);
            entity.Property(x => x.LoginErrorHex).HasMaxLength(40);
            entity.Property(x => x.LoginNativeErrorHex).HasMaxLength(40);
            entity.Property(x => x.StartListenExErrorHex).HasMaxLength(40);
            entity.Property(x => x.LastAlarmCommandName).HasMaxLength(120);
            entity.Property(x => x.LastAlarmPayloadFirst256Hex).HasMaxLength(512);
            entity.Property(x => x.LastAlarmDecodeStatus).HasMaxLength(120);
            entity.Property(x => x.LastDecodedAlarmJson).HasColumnType("jsonb");
            entity.Property(x => x.SmartEventErrorHex).HasMaxLength(40);
            entity.Property(x => x.SmartEventRemoteIp).HasMaxLength(80);
            entity.Property(x => x.LastSmartEventResubscribeReason).HasMaxLength(160);
            entity.Property(x => x.LastSmartEventResubscribeError).HasMaxLength(500);
            entity.Property(x => x.LastSmartEventName).HasMaxLength(120);
            entity.Property(x => x.LastSmartEventParseStatus).HasMaxLength(120);
            entity.Property(x => x.LastSmartEventUserId).HasMaxLength(80);
            entity.Property(x => x.LastSmartEventCardName).HasMaxLength(180);
            entity.Property(x => x.LastSmartEventRawStructSummaryJson).HasColumnType("jsonb");
            entity.Property(x => x.LastRecordQueryError).HasMaxLength(1000);
            entity.Property(x => x.LastDecodeError).HasMaxLength(1000);
            entity.Property(x => x.NetSdkDecodeStatus).HasMaxLength(80).IsRequired();
        });
        modelBuilder.Entity<DahuaActiveRegisterRawEvent>(entity =>
        {
            entity.ToTable("dahua_active_register_raw_events");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.RegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.RemoteIp).HasMaxLength(80);
            entity.Property(x => x.CallbackCommandName).HasMaxLength(120);
            entity.Property(x => x.PayloadFirstBytesHex).HasMaxLength(512);
            entity.Property(x => x.PayloadBase64);
            entity.Property(x => x.DecodeStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DecodedJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.CallbackCommand);
            entity.HasIndex(x => x.DecodeStatus);
        });

        modelBuilder.Entity<SupervisorSiteAssignment>(entity =>
        {
            entity.ToTable("supervisor_site_assignments");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.SupervisorUserId);
            entity.HasIndex(x => x.SiteId);
            entity.HasIndex(x => new { x.TenantId, x.SupervisorUserId, x.SiteId, x.IsActive });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldSmetaItem>(entity =>
        {
            entity.ToTable("field_smeta_items");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.StageName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.WorkName).HasMaxLength(220).IsRequired();
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.WorkCategory).HasMaxLength(100);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.SiteId);
            entity.HasIndex(x => new { x.TenantId, x.SiteId, x.WorkName }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupervisorDailyReport>(entity =>
        {
            entity.ToTable("supervisor_daily_reports");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Shift).HasMaxLength(80);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.GeneralNote).HasMaxLength(2000);
            entity.Property(x => x.WeatherCondition).HasMaxLength(120);
            entity.Property(x => x.ReviewNote).HasMaxLength(1000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.SupervisorUserId, x.SiteId, x.ReportDate }).IsUnique();
            entity.HasIndex(x => new { x.SiteId, x.ReportDate });
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupervisorDailyReportLine>(entity =>
        {
            entity.ToTable("supervisor_daily_report_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.ReportedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.WorkHours).HasPrecision(18, 2);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ReportId);
            entity.HasIndex(x => x.SmetaItemId);
            entity.HasOne(x => x.Report).WithMany(x => x.Lines).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SmetaItem).WithMany().HasForeignKey(x => x.SmetaItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupervisorSiteNote>(entity =>
        {
            entity.ToTable("supervisor_site_notes");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.AttachmentPath).HasMaxLength(500);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.SiteId, x.EventDateTime });
            entity.HasIndex(x => x.SupervisorUserId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupervisorWorkerEvent>(entity =>
        {
            entity.ToTable("supervisor_worker_events");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.SiteId, x.EventDateTime });
            entity.HasIndex(x => x.WorkerId);
            entity.HasIndex(x => x.SupervisorUserId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Worker).WithMany().HasForeignKey(x => x.WorkerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldWarehouseCatalogItem>(entity =>
        {
            entity.ToTable("field_warehouse_catalog_items");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.NameAz).HasMaxLength(180);
            entity.Property(x => x.NameRu).HasMaxLength(180);
            entity.Property(x => x.NameEn).HasMaxLength(180);
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Subcategory).HasMaxLength(120);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(80);
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.SearchAliases).HasMaxLength(1000);
            entity.Property(x => x.SpecificationSchemaJson).HasColumnType("jsonb");
            entity.Property(x => x.MinimumStockLevel).HasPrecision(18, 3);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => new { x.TenantId, x.Category, x.Subcategory });
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldWarehouseRequest>(entity =>
        {
            entity.ToTable("field_warehouse_requests");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ApprovedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReservedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.IssuedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Urgency).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.GeneralNote).HasMaxLength(1200);
            entity.Property(x => x.JustificationRequestNote).HasMaxLength(1200);
            entity.Property(x => x.Justification).HasMaxLength(1200);
            entity.Property(x => x.ManagerComment).HasMaxLength(1200);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.SiteId, x.CreatedAt });
            entity.HasIndex(x => x.SupervisorUserId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SupervisorUser).WithMany().HasForeignKey(x => x.SupervisorUserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupervisorAuditEvent>(entity =>
        {
            entity.ToTable("supervisor_audit_events");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.SupervisorNameSnapshot).HasMaxLength(180);
            entity.Property(x => x.Action).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.SiteId, x.Timestamp });
            entity.HasIndex(x => x.SupervisorUserId);
            entity.HasIndex(x => x.Action);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplyUnit>(entity =>
        {
            entity.ToTable("supply_units");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.NameAz).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NameEn).HasMaxLength(80).IsRequired();
            entity.Property(x => x.NameRu).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.ToTable("warehouses");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.IsDefault });
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FieldWarehouseRequestLine>(entity =>
        {
            entity.ToTable("field_warehouse_request_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ApprovedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReservedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.IssuedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1200);
            entity.Property(x => x.SpecificationJson).HasColumnType("jsonb");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.RequestId);
            entity.HasIndex(x => x.CatalogItemId);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasOne(x => x.Request).WithMany(x => x.Lines).HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseReservation>(entity =>
        {
            entity.ToTable("warehouse_reservations");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.WarehouseId, x.CatalogItemId, x.Status });
            entity.HasIndex(x => x.RequestLineId);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequestLine).WithMany().HasForeignKey(x => x.RequestLineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WarehouseStockMovement>(entity =>
        {
            entity.ToTable("warehouse_stock_movements");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.ReferenceType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.WarehouseId, x.CatalogItemId });
            entity.HasIndex(x => new { x.TenantId, x.ReferenceType, x.ReferenceId, x.CatalogItemId, x.MovementType }).IsUnique().HasFilter("\"ReferenceId\" IS NOT NULL");
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseUsagePolicy>(entity =>
        {
            entity.ToTable("warehouse_usage_policies");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.DefaultMaximumPerRequest).HasPrecision(18, 3);
            entity.Property(x => x.DefaultMaximumPerWorker).HasPrecision(18, 3);
            entity.Property(x => x.DefaultMaximumPerSitePeriod).HasPrecision(18, 3);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.CatalogItemId });
            entity.HasIndex(x => new { x.TenantId, x.Category });
        });

        modelBuilder.Entity<ProcurementNeed>(entity =>
        {
            entity.ToTable("procurement_needs");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.RequiredQuantity).HasPrecision(18, 3);
            entity.Property(x => x.AlreadyAvailableQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ShortfallQuantity).HasPrecision(18, 3);
            entity.Property(x => x.PurchasedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(1200).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.CatalogItemId });
            entity.HasIndex(x => new { x.TenantId, x.SourceRequestId });
            entity.HasIndex(x => x.SourceRequestLineId);
            entity.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SourceRequest).WithMany().HasForeignKey(x => x.SourceRequestId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SourceRequestLine).WithMany().HasForeignKey(x => x.SourceRequestLineId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcurementTask>(entity =>
        {
            entity.ToTable("procurement_tasks");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Code).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.ManagerInstruction).HasMaxLength(1200);
            entity.Property(x => x.VerificationNote).HasMaxLength(1200);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.AssignedProcurementUserId, x.Status });
            entity.HasOne(x => x.AssignedProcurementUser).WithMany().HasForeignKey(x => x.AssignedProcurementUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProcurementTaskLine>(entity =>
        {
            entity.ToTable("procurement_task_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.RequestedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.PurchasedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.AcceptedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SpecificationJson).HasColumnType("jsonb");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1200);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.TaskId);
            entity.HasIndex(x => x.ProcurementNeedId);
            entity.HasIndex(x => x.CatalogItemId);
            entity.HasOne(x => x.Task).WithMany(x => x.Lines).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProcurementNeed).WithMany().HasForeignKey(x => x.ProcurementNeedId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.TaxId).HasMaxLength(60);
            entity.Property(x => x.Phone).HasMaxLength(80);
            entity.Property(x => x.Email).HasMaxLength(180);
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.ContactPerson).HasMaxLength(180);
            entity.Property(x => x.Categories).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<ProcurementAttachment>(entity =>
        {
            entity.ToTable("procurement_attachments");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.AttachmentType).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(700).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.MimeType).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.TaskId);
            entity.HasIndex(x => x.TaskLineId);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TaskLine).WithMany().HasForeignKey(x => x.TaskLineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcurementReceipt>(entity =>
        {
            entity.ToTable("procurement_receipts");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.ReceiptNumber).HasMaxLength(120);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.TaskId);
            entity.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.StorageAttachment).WithMany().HasForeignKey(x => x.StorageAttachmentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProcurementReceiptLine>(entity =>
        {
            entity.ToTable("procurement_receipt_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ReceiptId);
            entity.HasIndex(x => x.TaskLineId);
            entity.HasOne(x => x.Receipt).WithMany(x => x.Lines).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TaskLine).WithMany().HasForeignKey(x => x.TaskLineId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CatalogItemPurchasePrice>(entity =>
        {
            entity.ToTable("catalog_item_purchase_prices");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.CatalogItemId, x.PurchasedAt });
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WarehouseGoodsReceipt>(entity =>
        {
            entity.ToTable("warehouse_goods_receipts");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Note).HasMaxLength(1200);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ProcurementTaskId);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProcurementTask).WithMany().HasForeignKey(x => x.ProcurementTaskId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WarehouseGoodsReceiptLine>(entity =>
        {
            entity.ToTable("warehouse_goods_receipt_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.ExpectedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.RejectedQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Condition).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Note).HasMaxLength(1200);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.ReceiptId);
            entity.HasOne(x => x.Receipt).WithMany(x => x.Lines).HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProcurementTaskLine).WithMany().HasForeignKey(x => x.ProcurementTaskLineId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WarehouseIssue>(entity =>
        {
            entity.ToTable("warehouse_issues");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.RecipientName).HasMaxLength(180);
            entity.Property(x => x.HandoverNote).HasMaxLength(1200);
            entity.Property(x => x.HandoverAttachmentPath).HasMaxLength(700);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.FieldRequestId);
            entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FieldRequest).WithMany().HasForeignKey(x => x.FieldRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WarehouseIssueLine>(entity =>
        {
            entity.ToTable("warehouse_issue_lines");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.Unit).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.IssueId);
            entity.HasOne(x => x.Issue).WithMany(x => x.Lines).HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.CatalogItem).WithMany().HasForeignKey(x => x.CatalogItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplyNotification>(entity =>
        {
            entity.ToTable("supply_notifications");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.Audience).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.Audience, x.Status });
        });
        modelBuilder.Entity<DeviceConnectionLog>(entity =>
        {
            entity.ToTable("device_connection_logs");
            entity.HasKey(x => x.Id);
            entity.HasQueryFilter(x => CurrentTenantId == null || x.TenantId == CurrentTenantId);
            entity.Property(x => x.RegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.RemoteIp).HasMaxLength(80);
            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Device).WithMany(x => x.ConnectionLogs).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}














