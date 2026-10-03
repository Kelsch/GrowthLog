-- Add a nullable DefaultFamilyId to the Identity user table so a user can
-- star one family as the Dashboard default. Nullable: no default required.

ALTER TABLE AspNetUsers ADD COLUMN DefaultFamilyId TEXT NULL;
