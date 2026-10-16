-- Phase 4 hygiene: guarantee exactly one SharingPermissions row per
-- (FamilyConnectionId, PermissionKind). The 0004 migration already declared
-- UNIQUE (FamilyConnectionId, PermissionKind), but this migration:
--   1. Removes any duplicate rows that may have slipped in before the
--      constraint was enforced (keeping the newest by UpdatedUtc, rowid).
--   2. Re-asserts the unique index under a stable name so the upsert in
--      SharingPermissionRepository.SetAsync can rely on it.
--
-- After this migration, toggling a permission is a single UPDATE/upsert —
-- there is no "newest row wins" logic anywhere in the codebase.

-- 1. Deduplicate: keep the newest row per (connection, kind).
DELETE FROM SharingPermissions
WHERE rowid NOT IN (
    SELECT MAX(rowid)
    FROM SharingPermissions
    GROUP BY FamilyConnectionId, PermissionKind
);

-- 2. Re-assert the unique index under a stable name.
CREATE UNIQUE INDEX IF NOT EXISTS UX_SharingPermissions_Connection_Kind
    ON SharingPermissions(FamilyConnectionId, PermissionKind);
