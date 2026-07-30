using BuildTrack.Domain.Dahua;
using BuildTrack.Domain.Entities;
using BuildTrack.Infrastructure.Dahua;

namespace BuildTrack.Tests;

public sealed class DahuaNetSdkSmartEventDecoderTests
{
    [Fact]
    public void ResolveEventName_MapsAccessControlSmartEvent()
    {
        Assert.Equal("EVENT_IVS_ACCESS_CTL", DahuaNetSdkSmartEventDecoder.ResolveEventName(0x204));
    }

    [Fact]
    public void Decode_SkipsUnsupportedSmartEvent()
    {
        var buffer = new byte[8192];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x9999, handle.AddrOfPinnedObject(), IntPtr.Zero, 0, 7);

            Assert.Equal("UnsupportedSmartEvent", result.ParseStatus);
            Assert.Null(result.Record);
            Assert.Equal(7, result.Sequence);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void Decode_AccessEventWithoutPersonFields_ReturnsDiagnosticOnlyResult()
    {
        var buffer = new byte[8192];
        WriteInt(buffer, 0, 1);
        WriteAscii(buffer, 4, 128, "AccessControl");
        WriteInt(buffer, 144, 2026);
        WriteInt(buffer, 148, 7);
        WriteInt(buffer, 152, 24);
        WriteInt(buffer, 156, 9);
        WriteInt(buffer, 160, 35);
        WriteInt(buffer, 164, 12);
        WriteInt(buffer, 180, 1234);

        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var result = DahuaNetSdkSmartEventDecoder.Decode(0x204, handle.AddrOfPinnedObject(), IntPtr.Zero, 4096, 11);

            Assert.Equal("EVENT_IVS_ACCESS_CTL", result.EventName);
            Assert.Equal("DEV_EVENT_ACCESS_CTL_INFO", result.StructName);
            Assert.Equal("DecodedAccessSmartEventNoPersonFields", result.ParseStatus);
            Assert.NotNull(result.Record);
            Assert.Equal(1234, result.Record!.RecNo);
            Assert.Equal("15", result.Record.MethodRaw);
            Assert.Equal("Entry", result.Record.Type);
            Assert.Contains("ImageBytesLength", result.RawStructSummaryJson);
        }
        finally
        {
            handle.Free();
        }
    }

    [Fact]
    public void SmartEventClassification_TrustedSummaryOverridesBrokenTopLevelRecord()
    {
        var brokenTopLevel = UnknownTopLevelRecord();
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(brokenTopLevel, TrustedSummary("1", "ilham", "1", confidence: "High", source: "FixedStructField"), worker);
        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker);

        Assert.True(recognized);
        Assert.Equal("1", trusted.StatusRaw);
        Assert.Equal("1", trusted.UserId);
        Assert.Equal("Ilham", trusted.CardName);
        Assert.Equal("1", trusted.RawFields["Status"]);
        Assert.Equal("1", trusted.RawFields["UserID"]);
        Assert.Equal("Ilham", trusted.RawFields["CardName"]);
    }

    [Fact]
    public void SmartEventClassification_MappedWorkerNameRejectsRandomCandidate()
    {
        var record = KnownFaceRecord("1", "pp");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "pp", "1", confidence: "High", source: "FixedStructField"), worker);
        var recognized = DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker);

        Assert.False(recognized);
        Assert.Equal("Ilham", trusted.CardName);
        Assert.Equal("Ilham", trusted.RawFields["CardName"]);
        Assert.Equal("pp", trusted.RawFields["ReceivedCardName"]);
        Assert.Equal("Ilham", trusted.RawFields["ExpectedWorkerName"]);
        Assert.Equal("true", trusted.RawFields["CardNameMismatch"]);
    }

    [Fact]
    public void SmartEventClassification_UserIdPrimaryBlocksCardNameMismatchByDefault()
    {
        var record = KnownFaceRecord("1", "fj");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "fj", "1", confidence: "High", source: "CanonicalSmartEventParser"), worker);

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker, DahuaIdentityMatchPolicy.UserIdPrimary));

        var mismatch = DahuaSmartEventClassification.BuildIdentityMismatchRecord(trusted, TrustedSummary("1", "fj", "1", confidence: "High", source: "CanonicalSmartEventParser"));

        Assert.Equal("IdentityMismatch", mismatch.RawFields["Classification"]);
        Assert.Equal("true", mismatch.RawFields["CardNameMismatch"]);
        Assert.Equal("fj", mismatch.RawFields["ReceivedCardName"]);
        Assert.Equal("Ilham", mismatch.RawFields["ExpectedWorkerName"]);
        Assert.Equal("false", mismatch.RawFields["IdentityVerified"]);
        Assert.Equal("High", mismatch.RawFields["IdentityRisk"]);
        Assert.Null(mismatch.UserId);
        Assert.Null(mismatch.CardName);
        Assert.True(DahuaSecurityReviewEventPolicy.IsFaceReviewEvent(mismatch));
    }

    [Fact]
    public void SmartEventClassification_UserIdPrimaryUnsafeOverrideMarksHighRiskAttendance()
    {
        var record = KnownFaceRecord("1", "fj");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "fj", "1", confidence: "High", source: "CanonicalSmartEventParser"), worker);

        Assert.True(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker, DahuaIdentityMatchPolicy.UserIdPrimary, allowCardNameMismatchAttendance: true));

        DahuaSmartEventClassification.MarkRecognizedAttendance(trusted, worker, DahuaIdentityMatchPolicy.UserIdPrimary, allowCardNameMismatchAttendance: true);

        Assert.Equal("RecognizedAttendance", trusted.RawFields["Classification"]);
        Assert.Equal("true", trusted.RawFields["CardNameMismatch"]);
        Assert.Equal("false", trusted.RawFields["IdentityVerified"]);
        Assert.Equal("High", trusted.RawFields["IdentityRisk"]);
        Assert.Equal("true", trusted.RawFields["UnsafeMismatchAttendanceAllowed"]);
        Assert.False(DahuaVerifiedAttendancePayload.IsVerifiedActiveRegisterPayload(System.Text.Json.JsonSerializer.Serialize(trusted.RawFields)));
    }

    [Fact]
    public void SmartEventClassification_MappedWorkerNameRejectsAnotherRandomCandidate()
    {
        var record = KnownFaceRecord("1", "cj");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "cj", "1", confidence: "High", source: "FixedStructField"), worker);

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.Equal("Ilham", trusted.CardName);
    }

    [Fact]
    public void SmartEventClassification_ParserUncertainKeepsMismatchDiagnosticsButPreventsAttendance()
    {
        var record = KnownFaceRecord("1", "fj");
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "fj", "1", confidence: "High", source: "CanonicalSmartEventParser"), worker);
        var uncertain = DahuaSmartEventClassification.BuildParserUncertainRecord(trusted, TrustedSummary("1", "fj", "1", confidence: "High", source: "CanonicalSmartEventParser"));

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.Equal("ParserUncertainSmartEvent", uncertain.RawFields["Classification"]);
        Assert.Equal("1", uncertain.RawFields["Status"]);
        Assert.Equal("1", uncertain.RawFields["UserID"]);
        Assert.Equal("fj", uncertain.RawFields["CardName"]);
        Assert.Equal("Ilham", uncertain.RawFields["ExpectedWorkerName"]);
        Assert.Equal("true", uncertain.RawFields["WorkerResolved"]);
        Assert.Null(uncertain.UserId);
        Assert.Null(uncertain.CardName);
        Assert.True(DahuaSecurityReviewEventPolicy.IsFaceReviewEvent(uncertain));
        Assert.False(DahuaSdkAccessEventNormalizer.ShouldInsertPayrollAttendance(uncertain));
    }

    [Fact]
    public void SmartEventClassification_UnknownFaceRequiresFailedOrMissingTrustedPersonFields()
    {
        var unknown = DahuaSmartEventClassification.BuildUnknownFaceRecord(
            UnknownTopLevelRecord(),
            TrustedSummary(null, null, "0", confidence: "High", source: "FixedStructField", errorCode: "16"));

        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(unknown));
        Assert.True(DahuaSmartEventClassification.IsConfirmedUnknownFace(unknown));
    }

    [Fact]
    public void SmartEventClassification_NoPersonFieldsWithoutFailureIsParserUncertain()
    {
        var uncertain = DahuaSmartEventClassification.BuildParserUncertainRecord(
            UnknownTopLevelRecord(),
            TrustedSummary(null, null, "0", confidence: "Low", source: "DecodedStringCandidates"));

        Assert.False(DahuaUnknownFacePolicy.IsUnknownFace(uncertain));
        Assert.False(DahuaSmartEventClassification.IsConfirmedUnknownFace(uncertain));
        Assert.Equal("ParserUncertainSmartEvent", uncertain.RawFields["Classification"]);
    }

    [Fact]
    public void SmartEventClassification_UnresolvedTrustedWorkerDoesNotCreateAttendance()
    {
        var record = KnownFaceRecord("2", "Tahira");
        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("2", "Tahira", "1", confidence: "High", source: "FixedStructField"), null);

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, null));
        Assert.Equal("UnresolvedExternalWorker", trusted.RawFields["WorkerResolutionStatus"]);
        Assert.Equal("2", trusted.UserId);
        Assert.Equal("Tahira", trusted.CardName);
    }

    [Fact]
    public void SmartEventClassification_CardNamePrimaryKeepsTahiraSeparateFromRawUserIdCollision()
    {
        var record = KnownFaceRecord("1", "tahira");
        var tahira = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "W-0002",
            FullName = "tahira",
            Status = WorkerStatus.Active,
        };

        var resolved = DahuaSmartEventClassification.BuildCardNamePrimaryRecognizedRecord(
            record,
            TrustedSummary("1", "tahira", "1", confidence: "High", source: "CanonicalSmartEventParser"),
            tahira,
            rawCameraUserId: "1",
            userIdCollision: true,
            originalUserIdMappedWorkerName: "ilham",
            autoProvisioned: true);
        DahuaSmartEventClassification.MarkRecognizedAttendance(resolved, tahira);

        Assert.Equal("W-0002", resolved.UserId);
        Assert.Equal("tahira", resolved.CardName);
        Assert.Equal("1", resolved.RawFields["UserID"]);
        Assert.Equal("1", resolved.RawFields["CameraUserID"]);
        Assert.Equal("W-0002", resolved.RawFields["WorkerExternalId"]);
        Assert.Equal("tahira", resolved.RawFields["ReceivedCardName"]);
        Assert.Equal("tahira", resolved.RawFields["ResolvedWorkerName"]);
        Assert.Equal("CardName", resolved.RawFields["IdentityResolvedBy"]);
        Assert.Equal("true", resolved.RawFields["UserIdCollision"]);
        Assert.Equal("ilham", resolved.RawFields["OriginalUserIdMappedWorkerName"]);
        Assert.Equal("true", resolved.RawFields["AutoProvisionedWorker"]);
        Assert.Equal("false", resolved.RawFields["CardNameMismatch"]);
        Assert.Equal("true", resolved.RawFields["IdentityVerified"]);
        Assert.True(DahuaVerifiedAttendancePayload.IsVerifiedActiveRegisterPayload(System.Text.Json.JsonSerializer.Serialize(resolved.RawFields)));
    }

    [Theory]
    [InlineData("ilham", true)]
    [InlineData("tahira", true)]
    [InlineData("J4myH", false)]
    [InlineData("uiryH", false)]
    [InlineData("Bx", false)]
    [InlineData("fj", false)]
    [InlineData("p1x", false)]
    public void CardNamePolicy_SeparatesHumanNamesFromCorruptedCandidates(string cardName, bool expectedValid)
    {
        var valid = DahuaCameraCardNamePolicy.TryValidate(cardName, 3, out _, out _, out _);

        Assert.Equal(expectedValid, valid);
    }

    [Fact]
    public void CardNamePolicy_AllowlistLimitsAutoProvisionNames()
    {
        Assert.True(DahuaCameraCardNamePolicy.TryValidate("tahira", 3, ["Bx", "uiryH"], ["ilham", "tahira"], out _, out _, out _));
        Assert.False(DahuaCameraCardNamePolicy.TryValidate("rauf", 3, ["Bx", "uiryH"], ["ilham", "tahira"], out _, out _, out var reason));
        Assert.Equal("card name is not in auto-provision allowlist", reason);
    }

    [Fact]
    public void WorkerCodeGenerator_UsesNextSystemWorkerCode()
    {
        var workers = new[]
        {
            new Worker { ExternalWorkerCode = "1", FullName = "ilham" },
            new Worker { ExternalWorkerCode = "W-0001", FullName = "existing" },
            new Worker { ExternalWorkerCode = "tahira", FullName = "old tahira" },
        };

        Assert.Equal("W-0002", DahuaWorkerCodeGenerator.NextWorkerCode(workers));
    }

    [Fact]
    public void IdentityResolutionModeParser_DefaultsToStrictAndSupportsCardNamePrimary()
    {
        Assert.Equal(DahuaIdentityResolutionMode.StrictUserId, DahuaIdentityResolutionModeParser.Parse(null));
        Assert.Equal(DahuaIdentityResolutionMode.StrictUserId, DahuaIdentityResolutionModeParser.Parse("strict_userid"));
        Assert.Equal(DahuaIdentityResolutionMode.CardNamePrimary, DahuaIdentityResolutionModeParser.Parse("cardname_primary"));
        Assert.Equal(DahuaIdentityResolutionMode.Hybrid, DahuaIdentityResolutionModeParser.Parse("hybrid"));
    }

    [Fact]
    public void SmartEventClassification_CanonicalDecodedRecordKeepsDiagnosticUserAndName()
    {
        var decodedRecord = KnownFaceRecord("1", "ilham");
        decodedRecord.RawFields["UserIdSource"] = "CanonicalSmartEventParser";
        decodedRecord.RawFields["CardNameSource"] = "CanonicalSmartEventParser";
        decodedRecord.RawFields["StatusSource"] = "CanonicalSmartEventParser";
        decodedRecord.RawFields["UserIdConfidence"] = "High";
        decodedRecord.RawFields["CardNameConfidence"] = "High";
        decodedRecord.RawFields["StatusConfidence"] = "High";
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(decodedRecord, TrustedSummary("1", "ilham", "1", confidence: "High", source: "CanonicalSmartEventParser"), worker);
        DahuaSmartEventClassification.MarkRecognizedAttendance(trusted, worker);

        Assert.True(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.Equal("1", trusted.UserId);
        Assert.Equal("Ilham", trusted.CardName);
        Assert.Equal("RecognizedAttendance", trusted.RawFields["Classification"]);
        Assert.Equal("true", trusted.RawFields["IdentityVerified"]);
        Assert.Equal("Normal", trusted.RawFields["IdentityRisk"]);
        Assert.Equal("CanonicalSmartEventParser", trusted.RawFields["UserIdSource"]);
        Assert.Equal("CanonicalSmartEventParser", trusted.RawFields["CardNameSource"]);
        Assert.True(DahuaVerifiedAttendancePayload.IsVerifiedActiveRegisterPayload(System.Text.Json.JsonSerializer.Serialize(trusted.RawFields)));
    }

    [Fact]
    public void SmartEventClassification_DuplicateExternalIdCreatesIdentityMappingConflictReview()
    {
        var record = KnownFaceRecord("1", "ilham");
        var workers = new[]
        {
            new Worker { SiteId = Guid.NewGuid(), ExternalWorkerCode = "1", FullName = "Ilham", Status = WorkerStatus.Active },
            new Worker { SiteId = Guid.NewGuid(), ExternalWorkerCode = "1", FullName = "Tahira", Status = WorkerStatus.Active },
        };
        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "ilham", "1", confidence: "High", source: "CanonicalSmartEventParser"), null);

        var conflict = DahuaSmartEventClassification.BuildIdentityMappingConflictRecord(trusted, TrustedSummary("1", "ilham", "1", confidence: "High", source: "CanonicalSmartEventParser"), workers);

        Assert.Equal("IdentityMappingConflict", conflict.RawFields["Classification"]);
        Assert.Equal("2", conflict.RawFields["MappedWorkerCount"]);
        Assert.Equal("false", conflict.RawFields["IdentityVerified"]);
        Assert.Null(conflict.UserId);
        Assert.True(DahuaSecurityReviewEventPolicy.IsFaceReviewEvent(conflict));
        Assert.Equal(SecurityEventType.IdentityMappingConflict, DahuaSecurityReviewEventPolicy.ResolveEventType(conflict));
    }

    [Fact]
    public void SmartEventClassification_DuplicateCardNameCreatesIdentityMappingConflictReview()
    {
        var record = KnownFaceRecord("1", "tahira");
        var workers = new[]
        {
            new Worker { SiteId = Guid.NewGuid(), ExternalWorkerCode = "tahira", FullName = "Tahira", Status = WorkerStatus.Active },
            new Worker { SiteId = Guid.NewGuid(), ExternalWorkerCode = "camera-tahira", FullName = "tahira", Status = WorkerStatus.Active },
        };
        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", "tahira", "1", confidence: "High", source: "CanonicalSmartEventParser"), null);

        var conflict = DahuaSmartEventClassification.BuildIdentityMappingConflictRecord(trusted, TrustedSummary("1", "tahira", "1", confidence: "High", source: "CanonicalSmartEventParser"), workers);

        Assert.Equal("IdentityMappingConflict", conflict.RawFields["Classification"]);
        Assert.Equal("tahira", conflict.RawFields["ReceivedCardName"]);
        Assert.Equal("2", conflict.RawFields["MappedWorkerCount"]);
        Assert.Null(conflict.UserId);
        Assert.True(DahuaSecurityReviewEventPolicy.IsFaceReviewEvent(conflict));
    }

    [Theory]
    [InlineData("""{"Classification":"RecognizedAttendance","IdentityVerified":"true","CardNameMismatch":"true","IdentityRisk":"Normal"}""")]
    [InlineData("""{"Classification":"RecognizedAttendance","IdentityVerified":"false","CardNameMismatch":"false","IdentityRisk":"High"}""")]
    [InlineData("""{"Classification":"ParserUncertainSmartEvent","IdentityVerified":"false"}""")]
    public void VerifiedPayload_ExcludesPollutedOrSuspiciousActiveRegisterRows(string rawPayloadJson)
    {
        Assert.False(DahuaVerifiedAttendancePayload.IsVerifiedActiveRegisterPayload(rawPayloadJson));
    }

    [Fact]
    public void SmartEventClassification_LowConfidenceCandidateIlhamStaysUnknown()
    {
        var brokenTopLevel = UnknownTopLevelRecord();
        var worker = new Worker
        {
            SiteId = Guid.NewGuid(),
            ExternalWorkerCode = "1",
            FullName = "Ilham",
            Status = WorkerStatus.Active,
        };

        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(brokenTopLevel, TrustedSummary("1", "ilham", "1", confidence: "Low", source: "DecodedStringCandidates"), worker);
        var unknown = DahuaSmartEventClassification.BuildUnknownFaceRecord(trusted, TrustedSummary("1", "ilham", "1", confidence: "Low", source: "DecodedStringCandidates"));

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, worker));
        Assert.True(DahuaUnknownFacePolicy.IsUnknownFace(unknown));
    }

    [Theory]
    [InlineData("cj")]
    [InlineData("pp")]
    [InlineData("*cj")]
    [InlineData("KF")]
    public void SmartEventClassification_StringCandidatesNeverCreateAttendance(string candidate)
    {
        var record = KnownFaceRecord("1", candidate);
        var trusted = DahuaSmartEventClassification.BuildTrustedRecord(record, TrustedSummary("1", candidate, "1", confidence: "Low", source: "DecodedStringCandidates"), null);

        Assert.False(DahuaSmartEventClassification.IsRecognizedAttendance(trusted, null));
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        BitConverter.GetBytes(value).CopyTo(buffer, offset);
    }

    private static void WriteAscii(byte[] buffer, int offset, int length, string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(length, bytes.Length));
    }

    private static DahuaAccessRecord KnownFaceRecord(string userId, string cardName) => new()
    {
        RecNo = 701,
        CreateTime = DateTimeOffset.Parse("2026-07-24T06:15:00+00:00"),
        UserId = userId,
        CardName = cardName,
        StatusRaw = "1",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/app/data/security-snapshots/smart-events/known.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Status"] = "1",
            ["Method"] = "15",
            ["ErrorCode"] = "0",
        },
    };

    private static DahuaAccessRecord UnknownTopLevelRecord() => new()
    {
        RecNo = 702,
        CreateTime = DateTimeOffset.Parse("2026-07-24T06:16:00+00:00"),
        UserId = null,
        CardName = null,
        StatusRaw = "0",
        MethodRaw = "15",
        Type = "Entry",
        Url = "/app/data/security-snapshots/smart-events/unknown.jpg",
        RawFields = new Dictionary<string, string?>
        {
            ["Status"] = "0",
            ["Method"] = "15",
        },
    };

    private static string TrustedSummary(string? userId, string? cardName, string status, string confidence, string source, string errorCode = "0") =>
        $$"""
          {
            "SmartEventName": "EVENT_IVS_ACCESS_CTL",
            "SmartEventType": "0x204",
            "Status": "{{status}}",
            "UserId": {{JsonValue(userId)}},
            "CardName": {{JsonValue(cardName)}},
            "StatusSource": "{{source}}",
            "UserIdSource": "{{source}}",
            "CardNameSource": "{{source}}",
            "StatusConfidence": "{{confidence}}",
            "UserIdConfidence": "{{confidence}}",
            "CardNameConfidence": "{{confidence}}",
            "UsedDecodedStringCandidatesForClassification": false,
            "Method": "face",
            "Direction": "Entry",
            "EventTime": "2026-07-24T06:15:00+00:00",
            "ImageBytesLength": 47701,
            "ErrorCode": "{{errorCode}}"
          }
          """;

    private static string JsonValue(string? value) => value is null ? "null" : $"\"{value}\"";
}
