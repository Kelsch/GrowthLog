-- GrowthLog core schema (Phase 2).
-- Portable SQL: TEXT UUIDs, ISO-8601 timestamps, INTEGER booleans.

CREATE TABLE IF NOT EXISTS Families (
    Id         TEXT NOT NULL PRIMARY KEY,
    Name       TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL,
    IsDeleted  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS FamilyMemberships (
    Id        TEXT NOT NULL PRIMARY KEY,
    FamilyId  TEXT NOT NULL REFERENCES Families(Id),
    UserId    TEXT NOT NULL,
    Role      TEXT NOT NULL CHECK (Role IN ('Owner', 'Adult', 'Viewer')),
    JoinedUtc TEXT NOT NULL,
    UNIQUE (FamilyId, UserId)
);
CREATE INDEX IF NOT EXISTS IX_FamilyMemberships_UserId   ON FamilyMemberships(UserId);
CREATE INDEX IF NOT EXISTS IX_FamilyMemberships_FamilyId ON FamilyMemberships(FamilyId);

CREATE TABLE IF NOT EXISTS People (
    Id          TEXT NOT NULL PRIMARY KEY,
    UserId      TEXT NULL,
    FirstName   TEXT NOT NULL,
    LastName    TEXT NULL,
    DateOfBirth TEXT NOT NULL,
    AvatarUrl   TEXT NULL,
    CreatedUtc  TEXT NOT NULL,
    IsDeleted   INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_People_UserId ON People(UserId);

CREATE TABLE IF NOT EXISTS PersonFamilyMembership (
    Id             TEXT NOT NULL PRIMARY KEY,
    PersonId       TEXT NOT NULL REFERENCES People(Id),
    FamilyId       TEXT NOT NULL REFERENCES Families(Id),
    MembershipKind TEXT NOT NULL CHECK (MembershipKind IN ('Adult', 'Child')),
    JoinedUtc      TEXT NOT NULL,
    IsActive       INTEGER NOT NULL DEFAULT 1,
    UNIQUE (PersonId, FamilyId)
);
CREATE INDEX IF NOT EXISTS IX_PersonFamilyMembership_PersonId ON PersonFamilyMembership(PersonId);
CREATE INDEX IF NOT EXISTS IX_PersonFamilyMembership_FamilyId ON PersonFamilyMembership(FamilyId);
CREATE INDEX IF NOT EXISTS IX_PersonFamilyMembership_Family_Kind
    ON PersonFamilyMembership(FamilyId, MembershipKind);

CREATE TABLE IF NOT EXISTS PersonRelationships (
    Id               TEXT NOT NULL PRIMARY KEY,
    FromPersonId     TEXT NOT NULL REFERENCES People(Id),
    ToPersonId       TEXT NOT NULL REFERENCES People(Id),
    RelationshipType TEXT NOT NULL CHECK (RelationshipType IN ('Parent', 'Spouse')),
    CreatedUtc       TEXT NOT NULL,
    CHECK (FromPersonId <> ToPersonId),
    UNIQUE (FromPersonId, ToPersonId, RelationshipType)
);
CREATE INDEX IF NOT EXISTS IX_PersonRelationships_From ON PersonRelationships(FromPersonId);
CREATE INDEX IF NOT EXISTS IX_PersonRelationships_To   ON PersonRelationships(ToPersonId);

CREATE TABLE IF NOT EXISTS AuditLog (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    OccurredUtc TEXT NOT NULL,
    UserId      TEXT NULL,
    Action      TEXT NOT NULL,
    EntityType  TEXT NOT NULL,
    EntityId    TEXT NOT NULL,
    DetailsJson TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_AuditLog_Entity ON AuditLog(EntityType, EntityId);
CREATE INDEX IF NOT EXISTS IX_AuditLog_User   ON AuditLog(UserId, OccurredUtc);
