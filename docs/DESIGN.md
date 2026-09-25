# GrowthLog — Architecture Design

**Tagline:** See how your family grows.

This document captures the Phase 1 design for GrowthLog: the domain model, the
authorization/visibility model, and the relational database schema. It is
intentionally implementation-free. Code will follow in later phases.

---

## 1. Guiding Principles

1. **Dapper + plain SQL is the only application data-access technology.** No
   EF Core, no ORM abstractions that hide SQL. Every query is hand-written,
   parameterized SQL executed through Dapper.
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
| `Family` | A household. Owns memberships, people, and sharing rules. |
| `FamilyMembership` | Links a `User` to a `Family` with a role. |
| `Person` | A human whose growth is tracked. May or may not have a user account. |
| `PersonFamilyMembership` | Links a `Person` to a `Family`. A person may belong to many families. |
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
- **A Person may belong to multiple Families.** Membership is modeled by a
  `PersonFamilyMembership` join table, not a `FamilyId` column on `Person`.
  This is required from v1: an adult typically belongs both to the family they
  grew up in (their parents' household) and to the family they created with
  their spouse and children. A person's measurements are a single historical
  record; the families they belong to determine who may see them.
- **Family is the unit of ownership and sharing.** Sharing is expressed
  family-to-family, not person-to-person, which keeps the permission model
  tractable. A person who belongs to two families is visible to both by
  default (subject to role), and each family independently controls what it
  shares outward.
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

### 2.4 Person–Family Membership

A `PersonFamilyMembership` row contains:

- `Id`
- `PersonId`
- `FamilyId`
- `MembershipKind` — `Adult` | `Child` (whether this person is treated as a
  dependent of this family for sharing purposes)
- `JoinedUtc`
- `IsActive` (soft removal from a family without deleting the person)

Rules:

- A person may have zero or more memberships. A person with no active
  membership is effectively orphaned and should not appear in any family view.
- `MembershipKind` is per-family. The same person can be an `Adult` in their
  parents' family and an `Adult` in their own family, or a `Child` in one and
  an `Adult` in another (e.g. a young adult still listed as a dependent).
- The `IsChild` concept used by the sharing model is derived from
  `PersonFamilyMembership.MembershipKind` **for the family being shared**, not
  from a global flag on `Person`. This is what makes multi-family people work
  cleanly: "child of the source family" is a per-family property.

### 2.5 Measurement Model

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

1. **Ownership:** U is a member of *any* family that P belongs to → yes
   (subject to role). Because P may belong to several families, this rule is
   evaluated per membership, not against a single `FamilyId`.
2. **Parental rule:** U is a parent of P (via `PersonRelationship`) → yes,
   regardless of family membership. A parent always sees their own child.
3. **Explicit sharing:** U's family has a `FamilyConnection` from a family
   that P belongs to, and a `SharingPermission` grants visibility of P (or a
   category P belongs to, e.g. "children of the source family"). The category
   check uses `PersonFamilyMembership.MembershipKind` **for the source
   family**, so a person who is a child in one family and an adult in another
   is classified correctly per connection.

### 3.2 Permission Kinds

`SharingPermission.PermissionKind` is an extensible string/enum. Initial values:

- `ViewChildren` — the receiving family may see people who are `Child`
  members of the source family.
- `ViewAdults` — the receiving family may see people who are `Adult` members
  of the source family.
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
    -- Own families: any family the user is a member of
    SELECT pfm.PersonId AS PersonId
    FROM PersonFamilyMembership pfm
    JOIN FamilyMembership fm ON fm.FamilyId = pfm.FamilyId
    WHERE fm.UserId = @UserId
      AND pfm.IsActive = 1

    UNION

    -- Own children (parental rule, independent of family membership)
    SELECT r.ToPersonId
    FROM PersonRelationship r
    JOIN People parent ON parent.Id = r.FromPersonId
    WHERE r.RelationshipType = 'Parent'
      AND parent.UserId = @UserId

    UNION

    -- Explicitly shared via family connection + permission.
    -- The category check uses the person's membership kind in the SOURCE
    -- family, so multi-family people are classified per connection.
    SELECT pfm.PersonId
    FROM PersonFamilyMembership pfm
    JOIN FamilyConnection fc ON fc.SourceFamilyId = pfm.FamilyId
    JOIN SharingPermission sp ON sp.FamilyConnectionId = fc.Id
    JOIN FamilyMembership fm ON fm.FamilyId = fc.TargetFamilyId
    WHERE fm.UserId = @UserId
      AND pfm.IsActive = 1
      AND fc.IsActive = 1
      AND sp.IsGranted = 1
      AND (
            (sp.PermissionKind = 'ViewChildren' AND pfm.MembershipKind = 'Child')
         OR (sp.PermissionKind = 'ViewAdults'   AND pfm.MembershipKind = 'Adult')
         OR (sp.PermissionKind = 'ViewAll')
      )
)
SELECT ...
FROM Measurements m
JOIN VisiblePeople vp ON vp.PersonId = m.PersonId
WHERE m.IsDeleted = 0;
```

The Blazor UI never sees rows outside `VisiblePeople`. Filters (family,
person, age range) are applied **on top of** this CTE, never instead of it.

### 3.5 Mutation Authorization

Every mutation (insert/update/delete measurement, change sharing, accept
invitation) re-checks authorization server-side:

- **Create/update/delete measurement:** user must be a member of *any* family
  the person belongs to with role `Owner` or `Adult`, **or** be the person's
  linked user. Because a person may belong to several families, the check is
  "does the user hold a mutating role in at least one of the person's active
  families?"
- **Change sharing:** user must be `Owner` of the source family.
- **Accept invitation:** token must be valid, unexpired, and unconsumed.

IDs supplied by the browser are never trusted; the service re-derives the
person's family and checks membership.

### 3.6 Roles

`FamilyMembership.Role`:

- `Owner` — full control, including sharing and membership.
- `Adult` — can manage people and measurements in the family.
- `Viewer` — read-only. Can see the family's data (subject to sharing rules)
  but cannot create, edit, or delete people or measurements, and cannot change
  sharing.

All three roles are implemented in v1. Roles are per-family. A user may be
`Owner` of one family and `Viewer` of another.

---

## 4. Database Schema

**Target database: SQLite** for local development and the default self-hosted
deployment (Docker or Linux LXC). The schema is written in portable SQL so it
also runs on **PostgreSQL** with minimal changes. No SQL Server-specific types
are used.

Portability rules:

- Use `TEXT` for identifiers (UUIDs stored as canonical lowercase strings) and
  for all string columns. SQLite has no native `UNIQUEIDENTIFIER`; PostgreSQL
  can store the same values in `uuid` or `text` without changing the
  application.
- Use `TEXT` in ISO-8601 (`YYYY-MM-DDTHH:MM:SSZ`) for timestamps and
  `YYYY-MM-DD` for dates. This is unambiguous in both engines and avoids
  SQLite's lack of a native date type.
- Use `INTEGER` for booleans (`0`/`1`). SQLite has no `BIT`; PostgreSQL can
  map the same values to `boolean` if desired.
- Use `REAL` or `NUMERIC` for measurements. Store canonical metric values
  (centimeters, kilograms) as `NUMERIC` where the engine supports it, and
  `REAL` on SQLite. The application rounds for display only.
- Use `INTEGER PRIMARY KEY AUTOINCREMENT` for the audit log's surrogate key on
  SQLite; PostgreSQL uses `BIGSERIAL`. This is the only place a surrogate
  integer key is used.
- Enforce referential integrity with `FOREIGN KEY` constraints and enable
  `PRAGMA foreign_keys = ON` on SQLite connections.
- Enforce uniqueness and `CHECK` constraints in SQL; do not rely on
  application code alone.

The schema below is an **outline**, not final DDL. Column types are described
in portable terms; the migration scripts will translate them per engine.

### 4.1 Identity Tables

Standard ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`,
`AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`,
`AspNetRoleClaims`). Created by the Identity migration script, not by hand.
Identity's own data access is the one place EF Core is permitted, because it
ships with ASP.NET Core Identity; all GrowthLog application data access uses
Dapper.

`ApplicationUser` extends `IdentityUser` with:

- `DisplayName` (text, 100)
- `PreferredUnitSystem` (text, 10; `Imperial` | `Metric`)
- `CreatedUtc` (text, ISO-8601)

### 4.2 Application Tables (outline)

Each table below lists its columns, keys, and constraints in portable terms.
The migration scripts will emit engine-specific DDL.

**Families**

- `Id` — text UUID, primary key
- `Name` — text(100), not null
- `CreatedUtc` — text ISO-8601, not null
- `IsDeleted` — integer 0/1, not null, default 0

**FamilyMemberships** (user ↔ family, with role)

- `Id` — text UUID, primary key
- `FamilyId` — text UUID, FK → Families(Id)
- `UserId` — text, FK → AspNetUsers(Id)
- `Role` — text(20), not null; `Owner` | `Adult` | `Viewer`
- `JoinedUtc` — text ISO-8601, not null
- Unique: `(FamilyId, UserId)`
- Indexes: `UserId`, `FamilyId`

**People** (a human whose growth is tracked)

- `Id` — text UUID, primary key
- `UserId` — text, nullable, FK → AspNetUsers(Id)
- `FirstName` — text(100), not null
- `LastName` — text(100), nullable
- `DateOfBirth` — text `YYYY-MM-DD`, not null
- `AvatarUrl` — text(500), nullable
- `CreatedUtc` — text ISO-8601, not null
- `IsDeleted` — integer 0/1, not null, default 0
- Index: `UserId`
- Note: **no `FamilyId` column.** Family membership lives in
  `PersonFamilyMembership`.

**PersonFamilyMembership** (person ↔ family, many-to-many)

- `Id` — text UUID, primary key
- `PersonId` — text UUID, FK → People(Id)
- `FamilyId` — text UUID, FK → Families(Id)
- `MembershipKind` — text(10), not null; `Adult` | `Child`
- `JoinedUtc` — text ISO-8601, not null
- `IsActive` — integer 0/1, not null, default 1
- Unique: `(PersonId, FamilyId)`
- Indexes: `PersonId`, `FamilyId`, `(FamilyId, MembershipKind)`
- This is the table that makes multi-family people work. The sharing model's
  "child of the source family" check reads `MembershipKind` here, scoped to the
  source family.

**PersonRelationships** (directed person ↔ person)

- `Id` — text UUID, primary key
- `FromPersonId` — text UUID, FK → People(Id)
- `ToPersonId` — text UUID, FK → People(Id)
- `RelationshipType` — text(20), not null; `Parent` | `Spouse`
- `CreatedUtc` — text ISO-8601, not null
- Check: `FromPersonId <> ToPersonId`
- Unique: `(FromPersonId, ToPersonId, RelationshipType)`
- Indexes: `FromPersonId`, `ToPersonId`

**Measurements**

- `Id` — text UUID, primary key
- `PersonId` — text UUID, FK → People(Id)
- `MeasurementDate` — text `YYYY-MM-DD`, not null
- `HeightCm` — numeric, nullable
- `WeightKg` — numeric, nullable
- `Notes` — text(1000), nullable
- `EnteredByUserId` — text, FK → AspNetUsers(Id)
- `CreatedUtc` — text ISO-8601, not null
- `ModifiedUtc` — text ISO-8601, nullable
- `IsDeleted` — integer 0/1, not null, default 0
- Check: `HeightCm IS NOT NULL OR WeightKg IS NOT NULL`
- Index: `(PersonId, MeasurementDate)`

**FamilyConnections** (directed family ↔ family)

- `Id` — text UUID, primary key
- `SourceFamilyId` — text UUID, FK → Families(Id)
- `TargetFamilyId` — text UUID, FK → Families(Id)
- `Label` — text(100), nullable; e.g. "My Parents"
- `CreatedUtc` — text ISO-8601, not null
- `IsActive` — integer 0/1, not null, default 1
- Check: `SourceFamilyId <> TargetFamilyId`
- Unique: `(SourceFamilyId, TargetFamilyId)`
- Index: `TargetFamilyId`

**SharingPermissions**

- `Id` — text UUID, primary key
- `FamilyConnectionId` — text UUID, FK → FamilyConnections(Id)
- `PermissionKind` — text(40), not null; `ViewChildren` | `ViewAdults` | `ViewAll`
- `IsGranted` — integer 0/1, not null, default 0
- `UpdatedUtc` — text ISO-8601, not null
- `UpdatedByUserId` — text, FK → AspNetUsers(Id)
- Unique: `(FamilyConnectionId, PermissionKind)`

**Invitations**

- `Id` — text UUID, primary key
- `Token` — text(128), not null, unique; cryptographically random
- `InvitedEmail` — text(256), nullable
- `InvitedByUserId` — text, FK → AspNetUsers(Id)
- `TargetFamilyId` — text UUID, FK → Families(Id)
- `IntendedRole` — text(20), not null
- `IntendedPermission` — text(40), nullable
- `Status` — text(20), not null; `Pending` | `Accepted` | `Rejected` | `Expired`
- `CreatedUtc` — text ISO-8601, not null
- `ExpiresUtc` — text ISO-8601, not null
- `AcceptedUtc` — text ISO-8601, nullable
- `AcceptedByUserId` — text, nullable, FK → AspNetUsers(Id)
- Unique: `Token`
- Index: `(Status, ExpiresUtc)`
- **v1 stores the token in plaintext.** This is a deliberate simplification.
  A future hardening pass should store only a hash of the token and compare
  hashes on redemption; the column is sized to accommodate a hash without a
  schema change.

**AuditLog** (append-only)

- `Id` — integer surrogate key, auto-increment
- `OccurredUtc` — text ISO-8601, not null
- `UserId` — text, nullable, FK → AspNetUsers(Id)
- `Action` — text(60), not null; e.g. `MeasurementCreated`, `SharingChanged`
- `EntityType` — text(60), not null
- `EntityId` — text(64), not null
- `DetailsJson` — text, nullable
- Indexes: `(EntityType, EntityId)`, `(UserId, OccurredUtc)`

### 4.3 Indexes Justified by Query Patterns

| Query | Index |
| --- | --- |
| Measurements for a person, ordered by date | `IX_Measurements_Person_Date` |
| Families a user belongs to | `IX_FamilyMemberships_UserId` |
| Members of a family | `IX_FamilyMemberships_FamilyId` |
| People in a family | `IX_PersonFamilyMembership_FamilyId` |
| Families a person belongs to | `IX_PersonFamilyMembership_PersonId` |
| Children vs adults of a family (sharing) | `IX_PersonFamilyMembership_Family_Kind` |
| Relationships from/to a person | `IX_PersonRelationships_From/To` |
| Connections targeting a family | `IX_FamilyConnections_Target` |
| Invitation lookup by token | `UQ_Invitations_Token` |
| Pending invitations | `IX_Invitations_Status_Expires` |
| Audit trail for an entity | `IX_AuditLog_Entity` |

### 4.4 Integrity Rules Enforced in SQL

- Foreign keys on every relationship.
- `CHECK` constraints preventing self-relationships and self-connections.
- `CHECK` requiring at least one of height/weight on a measurement.
- Unique constraints on `(FamilyId, UserId)`, `(PersonId, FamilyId)`,
  `(SourceFamilyId, TargetFamilyId)`, `(FamilyConnectionId, PermissionKind)`,
  and `Invitations.Token`.
- Soft-delete flags (`IsDeleted`) on `Families`, `People`, `Measurements`, and
  `IsActive` on `PersonFamilyMembership` and `FamilyConnections`, so historical
  data is not destroyed by account, membership, or sharing changes.
- `PRAGMA foreign_keys = ON` is set on every SQLite connection so FK
  constraints are actually enforced.

### 4.5 Migration Strategy

- Plain SQL scripts under `db/migrations/`, numbered (`0001_identity.sql`,
  `0002_core.sql`, ...).
- A small `MigrationRunner` (Dapper-based) applies scripts in order and records
  applied versions in a `SchemaVersions` table.
- No EF migrations. The only EF Core usage in the solution is whatever
  ASP.NET Core Identity requires internally; GrowthLog's own schema and data
  access are Dapper + SQL scripts.
- Scripts are written in portable SQL. Where an engine needs a different
  spelling (e.g. `AUTOINCREMENT` vs `BIGSERIAL`), the migration runner selects
  the appropriate variant by engine.

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

1. **SQLite is the target database** for local development and the default
   self-hosted deployment (Docker or Linux LXC). The schema is written in
   portable SQL so it also runs on **PostgreSQL** with minimal changes. No
   SQL Server-specific types are used.
2. **Blazor Web App with Interactive Server** rendering is the chosen
   architecture. No WebAssembly client is required for v1.
3. **A person may belong to multiple families from v1.** Membership is modeled
   by `PersonFamilyMembership`, not a `FamilyId` column on `Person`. A person's
   measurements are a single historical record; each family they belong to
   independently controls what it shares outward.
4. **`MembershipKind` (`Adult` | `Child`) is per-family**, set by the family
   owner, not derived from age. "Child" in the sharing model means "dependent
   whose data a parent controls," which is a social/legal concept, not a
   biological one. The same person can be a `Child` in one family and an
   `Adult` in another.
5. **Identity uses the default `IdentityUser` schema** with a small extension
   for display name and unit preference.
6. **Invitation tokens** are 256-bit random values, base64url-encoded, and
   **stored in plaintext in v1** with a unique index. Hashing the token at rest
   is a deliberate Phase 7 hardening item; the column is sized to hold a hash
   without a schema change.
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
13. **All application data access is Dapper + parameterized SQL.** No EF Core
    for GrowthLog data. The only EF Core usage permitted is whatever ASP.NET
    Core Identity requires internally.
14. **The `Viewer` role is implemented in v1.** It is read-only and cannot
    mutate people, measurements, or sharing.

---

## 7. Resolved Decisions

1. **Multi-family people are supported from v1** via
   `PersonFamilyMembership`. One-family-per-person is not acceptable.
2. **Invitation tokens are stored in plaintext in v1**, with a unique index.
   Hashing at rest is a Phase 7 hardening item.
3. **SQLite is the default database** for local development and self-hosted
   deployment. PostgreSQL is supported with minimal changes.
4. **The `Viewer` role is implemented in v1.**
5. **Measurement edits are allowed**, with soft delete and an audit trail.
   Corrections are edits, not new rows; the audit log records the change.

## 8. Remaining Open Questions

1. Should the audit log record a full before/after snapshot of edited
   measurements in `DetailsJson`, or only the changed fields?
2. Should `PersonFamilyMembership.IsActive = 0` be used to "remove" a person
   from a family, or should removal be a hard delete of the membership row?
   (Soft removal preserves history and is the current default.)
3. Should the `Viewer` role be assignable via invitation, or only by an
   `Owner` after the user has joined?
