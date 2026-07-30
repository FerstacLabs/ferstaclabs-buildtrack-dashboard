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

## Find rows polluted by unstable Dahua UserID

Some Dahua Smart Event firmwares can send the same camera `UserID` for multiple enrolled people. After enabling `DAHUA_IDENTITY_RESOLUTION_MODE=cardname_primary`, new events store the raw camera id in `RawPayloadJson->>'UserID'` and the resolved worker identity in `WorkerExternalId` / `ResolvedWorkerExternalId`. Use this query to review older rows where the received CardName disagreed with the stored worker:

```sql
select
  ae."Id",
  ae."WorkerExternalId",
  ae."WorkerName",
  ae."CreatedAt",
  ae."SnapshotPath",
  ae."RawPayloadJson"->>'UserID' as raw_camera_user_id,
  ae."RawPayloadJson"->>'ReceivedCardName' as received_card_name,
  ae."RawPayloadJson"->>'ResolvedWorkerName' as resolved_worker_name,
  ae."RawPayloadJson"->>'ExpectedWorkerName' as expected_worker_name,
  ae."RawPayloadJson"->>'UserIdCollision' as user_id_collision
from attendance_events ae
where ae."Source" = 'dahua_active_register'
  and (
    ae."RawPayloadJson"->>'IdentityVerified' = 'false'
    or ae."RawPayloadJson"->>'IdentityRisk' = 'High'
    or (
      ae."RawPayloadJson"->>'UserIdCollision' = 'true'
      and coalesce(ae."RawPayloadJson"->>'ResolvedWorkerName', '') <> ''
      and lower(coalesce(ae."WorkerName", '')) <> lower(ae."RawPayloadJson"->>'ResolvedWorkerName')
    )
    or (
      ae."WorkerExternalId" = '1'
      and lower(coalesce(ae."RawPayloadJson"->>'ReceivedCardName', '')) = 'tahira'
      and lower(coalesce(ae."WorkerName", '')) <> 'tahira'
    )
  )
order by ae."CreatedAt" desc;
```

If these rows should be excluded from live attendance, mark them high risk with the update statement above after manual review. Do not run broad deletes against production attendance history.

## Optional review-event copy

If a reviewed audit trail is needed, copy each suspicious row into `security_events` as `IdentityMismatch` with the same snapshot path before marking it high risk. Keep this as a controlled admin operation so real attendance history is not silently rewritten.
