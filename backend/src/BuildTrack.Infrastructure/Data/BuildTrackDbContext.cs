using BuildTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildTrack.Infrastructure.Data;

public sealed class BuildTrackDbContext(DbContextOptions<BuildTrackDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AttendanceEvent> AttendanceEvents => Set<AttendanceEvent>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<DeviceConnectionLog> DeviceConnectionLogs => Set<DeviceConnectionLog>();
    public DbSet<NetSdkRuntimeDiagnostics> NetSdkRuntimeDiagnostics => Set<NetSdkRuntimeDiagnostics>();
    public DbSet<DahuaActiveRegisterRawEvent> DahuaActiveRegisterRawEvents => Set<DahuaActiveRegisterRawEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(300);
            entity.Property(x => x.TimeZone).HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("workers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalWorkerCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.SiteId, x.ExternalWorkerCode }).IsUnique();
            entity.HasOne(x => x.Site).WithMany(x => x.Workers).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(x => x.Id);
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
            entity.HasIndex(x => x.RegisterDeviceId).IsUnique();
            entity.HasOne(x => x.Site).WithMany(x => x.Devices).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttendanceEvent>(entity =>
        {
            entity.ToTable("attendance_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkerExternalId).HasMaxLength(80);
            entity.Property(x => x.WorkerName).HasMaxLength(180);
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.SnapshotPath).HasMaxLength(500);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
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
            entity.Property(x => x.WorkerExternalId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.WorkerName).HasMaxLength(180);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.CloseReason).HasMaxLength(50);
            entity.Property(x => x.PresenceStatus).HasMaxLength(50);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
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
            entity.Property(x => x.RegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.RemoteIp).HasMaxLength(80);
            entity.Property(x => x.CallbackCommandName).HasMaxLength(120);
            entity.Property(x => x.PayloadFirstBytesHex).HasMaxLength(512);
            entity.Property(x => x.PayloadBase64);
            entity.Property(x => x.DecodeStatus).HasMaxLength(80).IsRequired();
            entity.Property(x => x.DecodedJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.CallbackCommand);
            entity.HasIndex(x => x.DecodeStatus);
        });
        modelBuilder.Entity<DeviceConnectionLog>(entity =>
        {
            entity.ToTable("device_connection_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RegisterDeviceId).HasMaxLength(160);
            entity.Property(x => x.RemoteIp).HasMaxLength(80);
            entity.Property(x => x.EventType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.RawPayloadJson).HasColumnType("jsonb");
            entity.HasOne(x => x.Device).WithMany(x => x.ConnectionLogs).HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}














