# GrowthLog — Architecture Design

**Tagline:** See how your family grows.

This document captures the Phase 1 design for GrowthLog: the domain model, the
authorization/visibility model, and the relational database schema. It is
intentionally implementation-free. Code will follow in later phases.

---

## 1. Guiding Principles

1. **Dapper is the only application data-access technology.** No EF Core, no
   ORM abstractions that hide SQL.
2. **Authorization is enforced server-side**, in the data-access layer, not in
   Blazor components.
3. **Queries are authorization-aware.** We never fetch unauthorized rows and
   filter them in the UI.
4. **Measurements are immutable historical records.** New measurements are
   inserted; edits are tracked; deletes are soft where practical.
5. **Age is always derived** from `DateOfBirth` and `MeasurementDate`. It is
   never stored as a literal "Age 7".
6. **Privacy is the default.** New families and new people are private until
   explicitly shared.
7. **Design for expansion.** Additional relationship types, permission kinds,
   and measurement types must be addable without schema redesign.

---

## 2. Domain Model

### 2.1 Core Entities

| Entity | Purpose |
| --- | --- |
| `ApplicationUser` | An authenticated account (ASP.NET Core Identity). |
| `Family` | A household. Owns people, memberships, and sharing rules. |
| `FamilyMembership` | Links a `User` to a `Family` with a role. |
| `Person` | A human whose growth is tracked. May or may not have a user account. |
| `PersonRelationship` | Directed relationship between two `Person` rows (parent/child/spouse/sibling). |
| `Measurement` | A dated height/weight record for a `Person`. |
| `FamilyConnection` | A directed link between two `Family` rows (e.g. "my parents"). |
| `SharingPermission` | A grant from a source family to a connected family describing *what* may be seen. |
| `Invitation` | A tokenized invitation to join a family or establish a connection. |
| `AuditLog` | Append-only record of sensitive actions. |

### 2.2 Key Modeling Decisions

- **User ≠ Person.** A `Person` is a subject of measurement. A `User` is an
  account. They are linked optionally via `Person.UserId` (nullable). This lets
  us track children who have no account, and lets an adult user be linked to
  their own `Person` row.
- **Family is the unit of ownership and sharing.** People belong to exactly one
  family (their household). Sharing is expressed family-to-family, not
  person-to-person, which keeps the permission model tractable.
- **Relationships are explicit and directed.** `PersonRelationship` stores
  `(FromPersonId, ToPersonId, RelationshipType)`. We store the minimal set
  (parent→child, spouse↔spouse) and derive grandparents, grandchildren, and
  siblings via queries. This avoids combinatorial duplication.
- **Family connections are directed.** `FamilyConnection` has a
  `SourceFamilyId` and `TargetFamilyId`. The source family is the one that
  *grants* visibility; the target family is the one that *receives* it. This
  matches the requirement that "Dalan controls whether his parents can see his
  children."
- **Permissions are data, not code.** `SharingPermission` rows describe what a
  connected family may see. New permission kinds are added as new rows, not new
  code branches.

### 2.3 Relationship Types

Initial enum values (stored as a lookup table or constrained string):

- `Parent` (FromPerson is parent of ToPerson)
- `Spouse` (symmetric; stored once with a canonical ordering)
- `Sibling` (derived from shared parents; not stored directly)

Grandparent / grandchild / aunt / uncle / cousin are **derived** by traversing
`Parent` edges. This keeps the schema small and future-proof.

### 2.4 Measurement Model

A `Measurement` row contains:

- `Id`
- `PersonId`
- `MeasurementDate` (date only)
- `HeightCm` (decimal, canonical metric)
- `WeightKg` (decimal, canonical metric)
- `Notes` (nullable)
- `EnteredByUserId`
- `CreatedUtc`, `ModifiedUtc`
- `IsDeleted` (soft delete flag)

**Canonical units are metric.** Imperial is a display concern only. This
guarantees that switching display units never alters historical data.

Age at measurement is **never stored**. It is computed by a domain service
(`AgeCalculator`) from `Person.DateOfBirth` and `Measurement.MeasurementDate`,
handling leap years and exact birthday boundaries.

---

## 3. Authorization / Visibility Model

### 3.1 The Central Question

> "Can user U see person P's measurements?"

This is answered by a single authorization service backed by SQL. The answer
is derived from:

1. **Ownership:** U is a member of P's family → yes (subject to role).
2. **Parental rule:** U is a parent of P (via `PersonRelationship`) → yes.
3. **Explicit sharing:** U's family has a `FamilyConnection` from P's family,
   and a `SharingPermission` grants visibility of P (or a category P belongs
   to, e.g. "children of the source family").

### 3.2 Permission Kinds

`SharingPermission.PermissionKind` is an extensible string/enum. Initial values:

- `ViewChildren` — the receiving family may see the source family's children.
- `ViewAdults` — the receiving family may see the source family's adult members.
- `ViewAll` — shorthand for both (future convenience).

The schema stores one row per granted kind, so adding `ViewPhotos` or
`ViewMedicalNotes` later requires no schema change.

### 3.3 The Grandparent Rule

The requirement is explicit:

- A **parent** can always see their own child's data.
- A **grandparent** can see a grandchild's data **only if** the child's parent
  (the grandparent's child) has granted `ViewChildren` on the connection from
  the parent's family to the grandparent's family.

This falls out naturally from the model: the grandparent's family receives a
`FamilyConnection` from the parent's family, and the parent's family controls
the `SharingPermission` rows on that connection.

### 3.4 Authorization-Aware Queries

Every query that returns people or measurements joins against a
**visibility CTE** that encodes the rules above. Example shape (illustrative,
not final SQL):

```sql
WITH VisiblePeople AS (
    -- Own family
    SELECT p.Id
    FROM People p
    JOIN FamilyMemberships fm ON fm.FamilyId = p.FamilyId
    WHERE fm.UserId = @UserId

    UNION

    -- Own children (parental rule)
    SELECT r.ToPersonId
    FROM PersonRelationship r
    JOIN People parent ON parent.Id = r.FromPersonId
    WHERE r.RelationshipType = 'Parent'
      AND parent.UserId = @UserId

    UNION

    -- Explicitly shared via family connection + permission
    SELECT p.Id
    FROM People p
    JOIN FamilyConnection fc ON fc.SourceFamilyId = p.FamilyId
    JOIN SharingPermission sp ON sp.FamilyConnectionId = fc.Id
    JOIN FamilyMemberships fm ON fm.FamilyId = fc.TargetFamilyId
    WHERE fm.UserId = @UserId
      AND sp.IsGranted = 1
      AND (
            (sp.PermissionKind = 'ViewChildren' AND p.IsChild = 1)
         OR (sp.PermissionKind = 'ViewAdults'   AND p.IsChild = 0)
         OR (sp.PermissionKind = 'ViewAll')
      )
)
SELECT ...
FROM Measurements m
JOIN VisiblePeople vp ON vp.Id = m.PersonId
WHERE m.IsDeleted = 0;
```

The Blazor UI never sees rows outside `VisiblePeople`. Filters (family,
person, age range) are applied **on top of** this CTE, never instead of it.

### 3.5 Mutation Authorization

Every mutation (insert/update/delete measurement, change sharing, accept
invitation) re-checks authorization server-side:

- **Create/update/delete measurement:** user must be a member of the person's
  family with role `Owner` or `Adult`, **or** be the person's linked user.
- **Change sharing:** user must be `Owner` of the source family.
- **Accept invitation:** token must be valid, unexpired, and unconsumed.

IDs supplied by the browser are never trusted; the service re-derives the
person's family and checks membership.

### 3.6 Roles

`FamilyMembership.Role`:

- `Owner` — full control, including sharing and membership.
- `Adult` — can manage people and measurements in the family.
- `Viewer` — read-only (future; useful for e.g. a grandparent account that is
  a member of the grandparent family but only views).

Roles are per-family. A user may be `Owner` of one family and `Viewer` of
another.

---

## 4. Database Schema

Target: SQL Server (default for ASP.NET Core Identity on Windows). The schema
is written in portable SQL where practical so it can be adapted to PostgreSQL
or SQLite for tests.

### 4.1 Identity Tables

Standard ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`,
`AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`,
`AspNetRoleClaims`). Created by the Identity migration script, not by hand.

`ApplicationUser` extends `IdentityUser` with:

- `DisplayName` (nvarchar(100))
- `PreferredUnitSystem` (nvarchar(10), `Imperial` | `Metric`)
- `CreatedUtc`

### 4.2 Application Tables

```sql
CREATE TABLE Families (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Name          NVARCHAR(100)    NOT NULL,
    CreatedUtc    DATETIME2        NOT NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0
);

CREATE TABLE FamilyMemberships (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FamilyId      UNIQUEIDENTIFIER NOT NULL REFERENCES Families(Id),
    UserId        NVARCHAR(450)    NOT NULL REFERENCES AspNetUsers(Id),
    Role          NVARCHAR(20)     NOT NULL,  -- Owner | Adult | Viewer
    JoinedUtc     DATETIME2        NOT NULL,
    CONSTRAINT UQ_FamilyMemberships_Family_User UNIQUE (FamilyId, UserId)
);
CREATE INDEX IX_FamilyMemberships_UserId ON FamilyMemberships(UserId);
CREATE INDEX IX_FamilyMemberships_FamilyId ON FamilyMemberships(FamilyId);

CREATE TABLE People (
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FamilyId      UNIQUEIDENTIFIER NOT NULL REFERENCES Families(Id),
    UserId        NVARCHAR(450)    NULL REFERENCES AspNetUsers(Id),
    FirstName     NVARCHAR(100)    NOT NULL,
    LastName      NVARCHAR(100)    NULL,
    DateOfBirth   DATE             NOT NULL,
    IsChild       BIT              NOT NULL DEFAULT 1,
    AvatarUrl     NVARCHAR(500)    NULL,
    CreatedUtc    DATETIME2        NOT NULL,
    IsDeleted     BIT              NOT NULL DEFAULT 0
);
CREATE INDEX IX_People_FamilyId ON People(FamilyId);
CREATE INDEX IX_People_UserId   ON People(UserId);

CREATE TABLE PersonRelationships (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FromPersonId      UNIQUEIDENTIFIER NOT NULL REFERENCES People(Id),
    ToPersonId        UNIQUEIDENTIFIER NOT NULL REFERENCES People(Id),
    RelationshipType  NVARCHAR(20)     NOT NULL,  -- Parent | Spouse
    CreatedUtc        DATETIME2        NOT NULL,
    CONSTRAINT CK_PersonRelationships_NoSelf CHECK (FromPersonId <> ToPersonId),
    CONSTRAINT UQ_PersonRelationships UNIQUE (FromPersonId, ToPersonId, RelationshipType)
);
CREATE INDEX IX_PersonRelationships_From ON PersonRelationships(FromPersonId);
CREATE INDEX IX_PersonRelationships_To   ON PersonRelationships(ToPersonId);

CREATE TABLE Measurements (
    Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    PersonId         UNIQUEIDENTIFIER NOT NULL REFERENCES People(Id),
    MeasurementDate  DATE             NOT NULL,
    HeightCm         DECIMAL(6,2)     NULL,
    WeightKg         DECIMAL(6,3)     NULL,
    Notes            NVARCHAR(1000)   NULL,
    EnteredByUserId  NVARCHAR(450)    NOT NULL REFERENCES AspNetUsers(Id),
    CreatedUtc       DATETIME2        NOT NULL,
    ModifiedUtc      DATETIME2        NULL,
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT CK_Measurements_HasValue CHECK (HeightCm IS NOT NULL OR WeightKg IS NOT NULL)
);
CREATE INDEX IX_Measurements_Person_Date ON Measurements(PersonId, MeasurementDate);

CREATE TABLE FamilyConnections (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    SourceFamilyId  UNIQUEIDENTIFIER NOT NULL REFERENCES Families(Id),
    TargetFamilyId  UNIQUEIDENTIFIER NOT NULL REFERENCES Families(Id),
    Label           NVARCHAR(100)    NULL,  -- e.g. "My Parents"
    CreatedUtc      DATETIME2        NOT NULL,
    IsActive        BIT              NOT NULL DEFAULT 1,
    CONSTRAINT CK_FamilyConnections_NoSelf CHECK (SourceFamilyId <> TargetFamilyId),
    CONSTRAINT UQ_FamilyConnections UNIQUE (SourceFamilyId, TargetFamilyId)
);
CREATE INDEX IX_FamilyConnections_Target ON FamilyConnections(TargetFamilyId);

CREATE TABLE SharingPermissions (
    Id                  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FamilyConnectionId  UNIQUEIDENTIFIER NOT NULL REFERENCES FamilyConnections(Id),
    PermissionKind      NVARCHAR(40)     NOT NULL,  -- ViewChildren | ViewAdults | ViewAll
    IsGranted           BIT              NOT NULL DEFAULT 0,
    UpdatedUtc          DATETIME2        NOT NULL,
    UpdatedByUserId     NVARCHAR(450)    NOT NULL REFERENCES AspNetUsers(Id),
    CONSTRAINT UQ_SharingPermissions UNIQUE (FamilyConnectionId, PermissionKind)
);

CREATE TABLE Invitations (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Token             NVARCHAR(128)    NOT NULL,  -- cryptographically random
    InvitedEmail      NVARCHAR(256)    NULL,
    InvitedByUserId   NVARCHAR(450)    NOT NULL REFERENCES AspNetUsers(Id),
    TargetFamilyId    UNIQUEIDENTIFIER NOT NULL REFERENCES Families(Id),
    IntendedRole      NVARCHAR(20)     NOT NULL,
    IntendedPermission NVARCHAR(40)    NULL,
    Status            NVARCHAR(20)     NOT NULL,  -- Pending | Accepted | Rejected | Expired
    CreatedUtc        DATETIME2        NOT NULL,
    ExpiresUtc        DATETIME2        NOT NULL,
    AcceptedUtc       DATETIME2        NULL,
    AcceptedByUserId  NVARCHAR(450)    NULL REFERENCES AspNetUsers(Id),
    CONSTRAINT UQ_Invitations_Token UNIQUE (Token)
);
CREATE INDEX IX_Invitations_Status_Expires ON Invitations(Status, ExpiresUtc);

CREATE TABLE AuditLog (
    Id            BIGINT IDENTITY PRIMARY KEY,
    OccurredUtc   DATETIME2        NOT NULL,
    UserId        NVARCHAR(450)    NULL REFERENCES AspNetUsers(Id),
    Action        NVARCHAR(60)     NOT NULL,  -- MeasurementCreated, SharingChanged, ...
    EntityType    NVARCHAR(60)     NOT NULL,
    EntityId      NVARCHAR(64)     NOT NULL,
    DetailsJson   NVARCHAR(MAX)    NULL
);
CREATE INDEX IX_AuditLog_Entity ON AuditLog(EntityType, EntityId);
CREATE INDEX IX_AuditLog_User   ON AuditLog(UserId, OccurredUtc);
```

### 4.3 Indexes Justified by Query Patterns

| Query | Index |
| --- | --- |
| Measurements for a person, ordered by date | `IX_Measurements_Person_Date` |
| Families a user belongs to | `IX_FamilyMemberships_UserId` |
| Members of a family | `IX_FamilyMemberships_FamilyId` |
| People in a family | `IX_People_FamilyId` |
| Relationships from/to a person | `IX_PersonRelationships_From/To` |
| Connections targeting a family | `IX_FamilyConnections_Target` |
| Invitation lookup by token | `UQ_Invitations_Token` |
| Pending invitations | `IX_Invitations_Status_Expires` |
| Audit trail for an entity | `IX_AuditLog_Entity` |

### 4.4 Integrity Rules Enforced in SQL

- Foreign keys on every relationship.
- `CHECK` constraints preventing self-relationships and self-connections.
- `CHECK` requiring at least one of height/weight on a measurement.
- Unique constraints on `(FamilyId, UserId)`, `(SourceFamilyId, TargetFamilyId)`,
  `(FamilyConnectionId, PermissionKind)`, and `Invitations.Token`.
- Soft-delete flags (`IsDeleted`) on `Families`, `People`, `Measurements` so
  historical data is not destroyed by account or membership changes.

### 4.5 Migration Strategy

- Plain SQL scripts under `db/migrations/`, numbered (`0001_identity.sql`,
  `0002_core.sql`, ...).
- A small `MigrationRunner` (Dapper-based) applies scripts in order and records
  applied versions in a `SchemaVersions` table.
- No EF migrations.

---

## 5. Chart / Comparison Data Model

Charts are fed by DTOs, not entities:

- `ChartSeriesDto { PersonId, PersonName, Points: List<ChartPointDto> }`
- `ChartPointDto { MeasurementDate, AgeYears (decimal), HeightCm?, WeightKg? }`
- `ComparisonTableDto { Ages: int[], Rows: List<ComparisonRowDto> }`
- `ComparisonRowDto { Age, Values: Dictionary<PersonId, decimal?> }`

The server builds these DTOs from authorization-aware queries. The client
receives only authorized series. Missing measurements remain `null`; no
interpolation is performed.

Age is computed once, server-side, by `AgeCalculator.GetAgeYears(dob, date)`
returning a decimal (e.g. `5.25`). The UI formats it as "5 years, 3 months".

---

## 6. Key Assumptions

1. **SQL Server** is the target database for development and production.
   Tests may use SQLite or a SQL Server LocalDB instance; the schema is written
   to be portable enough for either.
2. **Blazor Web App with Interactive Server** rendering is the chosen
   architecture. No WebAssembly client is required for v1.
3. **A person belongs to exactly one family.** Cross-family people (e.g. a
   spouse who is in two households) are modeled as two `Person` rows linked by
   a `Spouse` relationship, or as one person with a membership in both
   families. The simpler v1 choice is one family per person; this can be
   relaxed later by adding a `PersonFamilyMembership` join table.
4. **`IsChild` is a stored flag**, set by the family owner, not derived from
   age. This is because "child" in the sharing model means "dependent whose
   data a parent controls," which is a social/legal concept, not a biological
   one. It can be recomputed later if desired.
5. **Identity uses the default `IdentityUser` schema** with a small extension
   for display name and unit preference.
6. **Invitation tokens** are 256-bit random values, base64url-encoded, stored
   hashed if practical (v1 may store plaintext with a unique index; hashing is
   a Phase 7 hardening item).
7. **No medical percentiles** in v1. The schema leaves room for a future
   `ReferenceDataset` table but none is created now.
8. **Audit log is append-only** and written in the same transaction as the
   mutation it records.
9. **Soft delete** is used for `People` and `Measurements`. Hard delete is
   reserved for administrative cleanup and is not exposed in the UI.
10. **Unit conversion** is a pure display concern. All storage is metric.
11. **Age range filters** are applied in SQL against a computed age expression,
    not in the client.
12. **The `VisiblePeople` CTE is the single source of truth** for read
    authorization. Any new query that returns people or measurements must join
    against it (or an equivalent view). This is enforced by code review and by
    tests that assert unauthorized rows never appear.

---

## 7. Open Questions for the User

1. Should a `Person` be allowed to belong to more than one family in v1, or is
   one-family-per-person acceptable?
2. Should invitation tokens be stored hashed from the start, or is plaintext
   with a unique index acceptable for v1?
3. Is SQL Server LocalDB acceptable for local development, or do you prefer
   SQLite for zero-install development?
4. Should the `Viewer` role be implemented in v1, or deferred?
5. Should measurement edits be allowed at all, or should corrections be new
   rows with a "supersedes" link? (The current design allows edits with an
   audit trail; the alternative is more audit-friendly but more complex.)
