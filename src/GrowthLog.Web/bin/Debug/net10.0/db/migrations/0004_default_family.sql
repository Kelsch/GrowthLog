-- DefaultFamilyId on AspNetUsers is owned by EF Core Identity (it is a
-- property on ApplicationUser), so EF creates the column. This Dapper
-- migration is intentionally a no-op to avoid a duplicate ADD COLUMN.
--
-- Kept as a recorded version so existing databases that already applied it
-- stay in sync and fresh databases simply record it as applied.
SELECT 1;
