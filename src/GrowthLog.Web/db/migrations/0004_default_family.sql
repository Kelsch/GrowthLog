-- Add a nullable DefaultFamilyId to the Identity user table so a user can
-- star one family as the Dashboard default. Nullable: no default required.
--
-- Idempotent: SQLite has no "ADD COLUMN IF NOT EXISTS", so guard the ALTER
-- with a pragma_table_info check. This makes the migration safe to run on
-- databases where the column already exists (e.g. history out of sync or a
-- previously partially-applied deploy) while still creating it on fresh DBs.

ALTER TABLE AspNetUsers ADD COLUMN DefaultFamilyId TEXT NULL
WHERE NOT EXISTS (
    SELECT 1 FROM pragma_table_info('AspNetUsers') WHERE name = 'DefaultFamilyId'
);
