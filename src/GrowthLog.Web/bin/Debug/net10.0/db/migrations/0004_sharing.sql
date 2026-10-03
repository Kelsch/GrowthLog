-- Phase 4: Family connections, sharing permissions, and invitations.

CREATE TABLE IF NOT EXISTS FamilyConnections (
    Id             TEXT NOT NULL PRIMARY KEY,
    SourceFamilyId TEXT NOT NULL REFERENCES Families(Id),
    TargetFamilyId TEXT NOT NULL REFERENCES Families(Id),
    Label          TEXT NULL,
    IsActive       INTEGER NOT NULL DEFAULT 1,
    CreatedByUserId TEXT NOT NULL,
    CreatedUtc     TEXT NOT NULL,
    CHECK (SourceFamilyId <> TargetFamilyId),
    UNIQUE (SourceFamilyId, TargetFamilyId)
);
CREATE INDEX IF NOT EXISTS IX_FamilyConnections_Source ON FamilyConnections(SourceFamilyId);
CREATE INDEX IF NOT EXISTS IX_FamilyConnections_Target ON FamilyConnections(TargetFamilyId);

-- A permission row grants a connected family (TargetFamilyId) visibility
-- into the SourceFamilyId's data. PermissionKind controls what is visible.
CREATE TABLE IF NOT EXISTS SharingPermissions (
    Id                 TEXT NOT NULL PRIMARY KEY,
    FamilyConnectionId TEXT NOT NULL REFERENCES FamilyConnections(Id) ON DELETE CASCADE,
    PermissionKind     TEXT NOT NULL CHECK (PermissionKind IN ('ViewChildren', 'ViewAdults', 'ViewAll')),
    IsGranted          INTEGER NOT NULL DEFAULT 0,
    UpdatedByUserId    TEXT NOT NULL,
    UpdatedUtc         TEXT NOT NULL,
    UNIQUE (FamilyConnectionId, PermissionKind)
);
CREATE INDEX IF NOT EXISTS IX_SharingPermissions_Connection ON SharingPermissions(FamilyConnectionId);

CREATE TABLE IF NOT EXISTS Invitations (
    Id              TEXT NOT NULL PRIMARY KEY,
    Token           TEXT NOT NULL UNIQUE,
    FamilyId        TEXT NOT NULL REFERENCES Families(Id),
    InvitedEmail    TEXT NULL,
    IntendedRole    TEXT NOT NULL CHECK (IntendedRole IN ('Owner', 'Adult', 'Viewer')),
    InvitedByUserId TEXT NOT NULL,
    Status          TEXT NOT NULL CHECK (Status IN ('Pending', 'Accepted', 'Rejected', 'Expired')),
    CreatedUtc      TEXT NOT NULL,
    ExpiresUtc      TEXT NOT NULL,
    AcceptedUtc     TEXT NULL,
    AcceptedByUserId TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_Invitations_Token  ON Invitations(Token);
CREATE INDEX IF NOT EXISTS IX_Invitations_Family ON Invitations(FamilyId, Status);
CREATE INDEX IF NOT EXISTS IX_Invitations_Email  ON Invitations(InvitedEmail, Status);
