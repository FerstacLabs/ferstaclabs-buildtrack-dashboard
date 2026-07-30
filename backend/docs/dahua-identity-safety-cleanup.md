# Dahua identity safety cleanup

This is a manual/admin-only guide for cleaning up historical `dahua_active_register` attendance events that were created before identity verification was enforced.

Do not delete production rows automatically. Review the candidate rows first.

## Find suspicious attendance rows

```sql
select
  ae."Id",
  ae."SiteId",
  ae."DeviceId",
  ae."WorkerExternalId",
  ae."WorkerName",
  ae."CreatedAt",
  ae."SnapshotPath",
  ae."RawPayloadJson"->>'ReceivedCardName' as received_card_name,
  ae."RawPayloadJson"->>'ExpectedWorkerName' as expected_worker_name,
  ae."RawPayloadJson"->>'CardNameMismatch' as card_name_mismatch,
  ae."RawPayloadJson"->>'IdentityRisk' as identity_risk
from attendance_events ae
where ae."Source" = 'dahua_active_register'
  and (
    ae."RawPayloadJson"->>'CardNameMismatch' = 'true'
    or ae."RawPayloadJson"->>'IdentityRisk' = 'High'
    or (
      coalesce(ae."RawPayloadJson"->>'ReceivedCardName', '') <> ''
      and coalesce(ae."RawPayloadJson"->>'ExpectedWorkerName', '') <> ''
      and lower(ae."RawPayloadJson"->>'ReceivedCardName') <> lower(ae."RawPayloadJson"->>'ExpectedWorkerName')
    )
  )
order by ae."CreatedAt" desc;
```

## Mark historical rows as unverified/high risk

```sql
update attendance_events ae
set "RawPayloadJson" =
  jsonb_set(
    jsonb_set(
      jsonb_set(coalesce(ae."RawPayloadJson", '{}'::jsonb), '{IdentityVerified}', '"false"', true),
      '{IdentityRisk}', '"High"', true
    ),
    '{ClassificationReason}', '"Historical identity mismatch cleanup marker"', true
  )
where ae."Source" = 'dahua_active_register'
  and (
    ae."RawPayloadJson"->>'CardNameMismatch' = 'true'
    or ae."RawPayloadJson"->>'IdentityRisk' = 'High'
    or (
      coalesce(ae."RawPayloadJson"->>'ReceivedCardName', '') <> ''
      and coalesce(ae."RawPayloadJson"->>'ExpectedWorkerName', '') <> ''
      and lower(ae."RawPayloadJson"->>'ReceivedCardName') <> lower(ae."RawPayloadJson"->>'ExpectedWorkerName')
    )
  );
```

After this marker is applied, default live attendance and snapshot gallery filters exclude those rows.

## Optional review-event copy

If a reviewed audit trail is needed, copy each suspicious row into `security_events` as `IdentityMismatch` with the same snapshot path before marking it high risk. Keep this as a controlled admin operation so real attendance history is not silently rewritten.
