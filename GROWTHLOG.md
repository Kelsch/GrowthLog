# Build GrowthLog

I want you to design and build a complete web application called **GrowthLog**.

GrowthLog is a private family application for recording and visualizing the height and weight of family members over the years, primarily measured on or around each person's birthday.

The application should allow multiple related families/households to participate while giving each family complete control over who can see their data.

---

# Core technology requirements

* Use **Blazor** for the application.
* Use the current stable .NET version available in this development environment.
* Prefer **Blazor Web App / Interactive Server** architecture unless there is a compelling technical reason to use something else.
* Use C# throughout the application.
* Use **ASP.NET Core Identity** or an equivalent secure authentication system.
* Use a relational SQL database.
* Use **Dapper for all application data access**.
* Do NOT use Entity Framework Core for application data access.
* Do NOT add EF Core simply because another library normally uses it.
* SQL queries should be explicit and understandable.
* Keep database access behind appropriate repository/data-access services rather than putting SQL directly inside Blazor components.
* Use parameterized SQL queries everywhere.
* Do not construct SQL using string concatenation with user input.
* Database migrations/schema creation should use an appropriate SQL migration approach rather than EF migrations.
* Structure the data-access layer so database-specific SQL is isolated and maintainable.
* Prefer straightforward Dapper code over introducing an unnecessary ORM abstraction layer.
* The application should be runnable locally with a straightforward development setup.
* Provide SQL database schema/migration scripts and sensible seed/development data where appropriate.

Before implementing the application, inspect the existing repository and determine what is already present. Do not unnecessarily replace or restructure an existing project if it can be extended cleanly.

---

# Dapper requirements

Dapper is the required persistence technology for GrowthLog.

Use Dapper for:

* SELECT queries
* INSERT operations
* UPDATE operations
* DELETE operations
* Transactions
* Multi-mapping where appropriate
* Stored procedures only where there is a compelling reason to use them

Use parameterized queries.

Example pattern:

```csharp
const string sql = """
    SELECT Id, Name, DateOfBirth
    FROM People
    WHERE FamilyId = @FamilyId
    """;

var people = await connection.QueryAsync<Person>(
    sql,
    new { FamilyId = familyId });
```

Do not use:

```csharp
dbContext.People...
```

or any Entity Framework APIs.

Avoid generic repository abstractions that hide the SQL so thoroughly that it becomes difficult to understand what database queries are actually being executed.

Prefer focused data-access classes such as:

* UserRepository
* FamilyRepository
* PersonRepository
* MeasurementRepository
* FamilySharingRepository
* InvitationRepository

The exact structure can differ if there is a better design.

Use transactions for operations that modify multiple related records.

For example, accepting a family invitation may need to update multiple records atomically.

---

# Application concept

The basic idea is:

A user belongs to a family/household.

For example:

**Dalan + Wife**

* Child 1
* Child 2
* Child 3

**Dalan's Parents**

* Parent 1
* Parent 2

**Sibling's Family**

* Sibling
* Spouse
* Their children

The families can be connected to each other.

The important distinction is:

**Family membership and data visibility are not the same thing.**

A person may be related to another family without automatically being allowed to see all of that family's measurements.

---

# Privacy model

Privacy is one of the most important requirements of GrowthLog.

I must be able to keep my immediate family's data completely private if I choose.

For example:

My family:

* Me
* My wife
* My children

My parents should NOT automatically be able to see my children's measurements merely because they are their grandparents.

I should explicitly control whether they can see them.

## Default privacy

New family data should be private by default.

Never make measurements publicly accessible.

Never expose measurements through an endpoint unless the authenticated user has permission to see them.

Do not rely solely on hiding UI elements for security.

Authorization must be enforced on the server/data-access layer.

---

# Family relationships and permissions

Create a concept of connected families.

A family can invite another family or establish a relationship with another family.

For example:

My family can be connected to:

* My parents' family
* My brother's family
* My sister's family

But each connection needs permissions.

## Important rule

A parent can always see the data of their own children.

However, a grandparent should only be able to see their grandchildren's data if the child's parent allows it.

For example:

Dalan has Child A.

Dalan's parents are Grandparent A and Grandparent B.

Grandparent A/B may be connected to Dalan's family.

But Dalan controls whether Grandparent A/B can see Child A's GrowthLog data.

This should be configurable from a settings page.

---

# Sharing settings

Create a clear **Family Sharing / Privacy Settings** page.

It should allow a parent/family owner to see connected families and choose what they can see.

For example:

## My Parents

Connected family: Dalan's Parents

[ ] Allow them to see my children's growth data

Potentially later:

[ ] Allow them to see my adult household members' data

The system should be designed so additional permissions can be added later without redesigning the entire authorization model.

Do not hard-code authorization logic directly into individual UI components.

Create a centralized authorization/visibility model.

---

# Family structure

The application should support:

* Adults
* Children
* Parents
* Grandparents
* Siblings
* Spouses/partners
* Multiple children per family
* Multiple generations
* Multiple connected family households

A person should belong to a household/family.

Relationships between people should be modeled explicitly enough that the application can determine relationships such as:

* Parent
* Child
* Grandparent
* Grandchild
* Sibling
* Spouse/partner

Do not assume that everyone in a connected family has identical visibility.

---

# Measurements

Each person can have many measurements.

A measurement should include at minimum:

* Person
* Date
* Height
* Weight
* Optional notes
* Who entered the measurement
* Created timestamp
* Last modified timestamp

Do NOT overwrite previous measurements.

Every birthday measurement should become a historical record.

The application should support entering measurements for any date, although the normal workflow is one measurement around each birthday.

---

# Birthday workflow

The main dashboard should make it obvious when someone is due for their yearly measurement.

For example:

## Upcoming Measurements

Child A
Birthday: October 12
Last measurement: October 14, 2025

[Record Measurement]

Child B
Birthday: January 4
Last measurement: January 6, 2026

[Record Measurement]

Use the person's birth date to determine their age at each measurement.

Do not calculate age merely from the calendar year.

---

# Measurement entry

Create a clean measurement entry form.

For example:

Person:
[ Child A ]

Date:
[ October 12, 2026 ]

Height:
[ 4 ft ] [ 8.5 in ]

Weight:
[ 72.4 lb ]

Notes:
[ ]

[Save Measurement]

Make the form fast to use on a phone.

Support both:

* Imperial: feet/inches and pounds
* Metric: centimeters and kilograms

Store measurements in a sensible canonical representation in the database so changing display units does not alter historical data.

---

# Dashboard

Create a polished dashboard.

The dashboard should provide:

* My family
* Connected families I am allowed to see
* Upcoming birthdays
* Upcoming annual measurements
* Recent measurements
* Quick "Record Measurement" actions
* Useful growth summaries

Do not overload the dashboard.

It should immediately answer:

**"How is everyone in my family growing?"**

---

# Family filtering

I need to be able to filter the application to specific families.

For example:

[ All Families ▼ ]

Options:

* My Family
* Mom & Dad
* Brother's Family
* Sister's Family

If I select:

**Brother's Family**

I should only see people/data that I have permission to see from that family.

I should also be able to select multiple families if practical.

The selected filter should apply consistently to:

* People lists
* Charts
* Growth comparisons
* Recent measurements
* Search/results where applicable

Do not use client-side filtering as the security mechanism.

The underlying Dapper query must only retrieve data the current user is authorized to see.

---

# Charts

Charts are one of the most important parts of GrowthLog.

Create polished, modern charts that make it easy to compare family members.

At minimum provide:

## Height over time

X axis:
Age or date

Y axis:
Height

Each person should have their own series.

Example:

* Child A
* Child B
* Child C
* Adult A

Only include people whose data the current user is authorized to see.

## Weight over time

Similar chart:

X axis:
Age or date

Y axis:
Weight

Again, each person should be separately identifiable.

## Height comparison

Create a chart/table that makes it easy to compare people's heights at approximately the same ages.

## Weight comparison

Provide a similar comparison for weight.

---

# Same Age Comparison

This is a core feature of GrowthLog.

The application should allow users to compare the growth of different people based on **age**, rather than calendar date.

This is important because family members are born in different years.

For example:

Dalan:

* Age 5: 43.2"
* Age 6: 46.1"
* Age 7: 48.8"

Child A:

* Age 5: 42.7"
* Age 6: 45.5"
* Age 7: 49.1"

Child B:

* Age 5: 44.0"
* Age 6: 47.0"
* Age 7: 50.2"

The application should be able to put all of these measurements on the same chart.

## Same Age Height Comparison

Chart:

**X axis:** Age
**Y axis:** Height

Each person gets their own series.

## Same Age Weight Comparison

Chart:

**X axis:** Age
**Y axis:** Weight

Each person gets their own series.

---

# Exact age calculations

This is important.

Do NOT store "Age 7" as the measurement's actual age.

Store:

* Date of birth
* Measurement date

Then calculate the person's age at the time of measurement.

Create a reusable domain/service method for this calculation.

It should account for:

* Birth dates
* Measurement dates
* Leap years
* Exact birthday boundaries

For charting, age may be represented as a decimal number.

Examples:

* 5.00
* 5.25
* 5.50
* 5.97

However, the UI should display friendly values such as:

**5 years, 3 months**

rather than exposing decimal years everywhere.

Do not artificially move measurements to birthdays.

If someone is measured on September 30 and their birthday is October 12, the measurement should be plotted at their actual age on September 30.

---

# Same Age Comparison Controls

Create a dedicated **Compare** view.

Controls should include:

### Families

[x] My Family
[x] Parents
[x] Brother's Family
[ ] Sister's Family

Only show families the current user is authorized to see.

### People

[x] Me
[x] Child A
[x] Child B
[x] Brother
[ ] Parent 1

Allow individuals to be toggled independently.

### Measurement

(•) Height
( ) Weight

### X Axis

(•) Age
( ) Calendar Date

### Age Range

Allow:

* 0–5
* 0–10
* 0–18
* 5–10
* 10–18
* Custom
* All available

Missing measurements must remain missing.

Do not fabricate or interpolate data.

---

# Same Age Comparison Table

Provide an optional table view.

Example:

| Age | Dalan | Child A | Child B |
| --: | ----: | ------: | ------: |
|   5 | 43.2" |   42.7" |   44.0" |
|   6 | 46.1" |   45.5" |   47.0" |
|   7 | 48.8" |   49.1" |   50.2" |
|   8 | 51.0" |       — |   52.8" |

Only show values that actually exist.

Do not interpolate missing measurements.

---

# Same Age Measurement Matching

Also provide an optional comparison around a selected age.

For example:

**Compare around age: 8 years**

**Tolerance: ±6 months**

Then show actual measurements within that window.

Example:

| Person  | Actual age | Height |
| ------- | ---------: | -----: |
| Dalan   |      8y 1m |  51.2" |
| Child A |     7y 11m |  50.7" |
| Child B |      8y 5m |  52.1" |

Never silently treat measurements at different ages as though they occurred at exactly the same age.

Clearly display the actual age of each measurement.

---

# Growth Statistics

For each person, display useful statistics such as:

* Current height
* Current weight
* Height change since previous measurement
* Weight change since previous measurement
* Height gained per year
* Weight gained per year
* Age at each measurement
* Number of measurements

For example:

**Child A**

Current:
4' 8.5"
72.4 lb

Since last birthday:
+2.7"
+6.2 lb

These should be calculated from actual measurements.

---

# Person Profile

Each person should have a profile page.

Show:

* Name
* Photo/avatar if supported
* Birthday
* Current age
* Family
* Measurement history
* Height chart
* Weight chart
* Growth statistics

Measurement history should be presented in a clean table.

---

# Family Page

Each family should have a family page.

Show:

* Family name
* Members
* Relationships
* Recent measurements
* Growth charts
* Sharing status
* Family settings

---

# Important Privacy Behavior

Every Dapper query that retrieves people or measurements must be evaluated against the current user's permissions.

Examples:

If I can see:

* My family
* Brother's family
* My parents' adults

but NOT my parents' grandchildren,

then charts must NOT contain the grandchildren.

Do not retrieve all measurements and then hide unauthorized records in Blazor.

Authorization-aware filtering should happen as close to the database query as reasonably practical.

Prefer SQL queries that include the necessary authorization joins/conditions.

The Blazor UI should only receive data the user is allowed to see.

A user must not be able to bypass authorization by:

* Changing an ID in a URL
* Calling an internal service incorrectly
* Manipulating query parameters
* Changing family filters
* Calling a data endpoint directly
* Manipulating chart filters

---

# Authentication

Require authentication for all application functionality containing family data.

Support:

* Registration
* Login
* Logout
* Password reset
* Secure password storage
* Email verification if practical
* Authorization

Do not expose family information to anonymous users.

Design the authentication system so that external login providers can be added later if desired.

---

# Invitations

Create a family invitation system.

A family administrator should be able to invite another person/family.

An invitation should have:

* Secure random token
* Expiration
* Inviting user
* Invited email/address
* Target family
* Intended permissions or relationship
* Accepted/rejected state
* Created/expiration timestamps

Do not use predictable invitation IDs/tokens.

Use Dapper transactions where accepting an invitation modifies multiple records.

---

# Family Administration

Create appropriate roles/permissions within a family.

At minimum consider:

* Family Owner/Admin
* Adult Member
* Child/dependent

Distinguish between:

1. Someone who belongs to a family
2. Someone who can administer that family
3. Someone who can view that family's data

Do not assume these are always the same.

---

# Settings

Create a settings area with:

## Account

* Name
* Email
* Password
* Display preferences

## Family

* Family name
* Members
* Family administrators

## Privacy & Sharing

* Connected families
* Who can see my family's data
* Grandparent visibility
* Sharing permissions

## Units

* Imperial
* Metric

---

# Data Model

Design a sensible relational SQL schema.

Likely entities include:

* ApplicationUser
* Family
* FamilyMembership
* Person
* PersonRelationship
* Measurement
* FamilyConnection
* SharingPermission
* Invitation

You may change these names or structure if you determine a better model.

The model needs to support:

* A user belonging to multiple families if necessary
* Multiple adults managing a family
* Children who do not necessarily have application accounts
* Family-to-family connections
* Explicit sharing permissions
* Multiple generations
* Historical measurements
* Future expansion of the sharing model

Do not make the database model dependent on the assumption that there will only ever be two generations.

---

# SQL Database Design

Create proper relational constraints and indexes.

Consider indexes for common queries such as:

* Measurements by PersonId and MeasurementDate
* Family memberships by UserId
* Family memberships by FamilyId
* People by FamilyId
* Relationships by PersonId
* Family connections
* Sharing permissions
* Invitations by token/status
* Authorization queries

Use appropriate:

* Primary keys
* Foreign keys
* Unique constraints
* Check constraints where useful
* Indexes

Do not rely exclusively on application code to maintain relational integrity.

Create SQL migration scripts for schema changes.

---

# Security Requirements

Treat children's height/weight information as private personal information.

Implement:

* Server-side authorization
* Authorization-aware Dapper queries
* CSRF protection where applicable
* Proper authentication
* Secure password handling
* Input validation
* Anti-overposting protections
* Secure invitation tokens
* Proper ownership checks
* No sensitive data in URLs where avoidable
* No logging of unnecessary personal measurement information
* Appropriate error handling without leaking information

Do not trust IDs supplied by the browser.

Every mutation must verify that the authenticated user has permission to perform that mutation.

---

# UI/UX

I want this to look like a real modern application, not a default Blazor template.

Aim for:

* Clean
* Modern
* Family-friendly
* Easy to understand
* Responsive
* Good typography
* Clear navigation
* Excellent charts
* Minimal clutter

Do not make it overly childish.

This is intended for adults to use for years.

Primary navigation should be approximately:

**Dashboard**
**People**
**Compare**
**Families**
**Settings**

The **Compare** section should contain the growth comparison tools.

Use responsive layouts.

On a phone, recording a birthday measurement should take as few steps as reasonably possible.

---

# Charting Architecture

Design chart data independently from the Blazor UI.

Create an appropriate view model/DTO specifically for chart data.

The chart layer should support:

1. Calendar date as X-axis
2. Age as X-axis
3. Multiple people/series
4. Family filtering
5. Individual person filtering
6. Height
7. Weight

The server should return only authorized chart data.

The client should not be responsible for filtering unauthorized data.

If a charting library is needed, select a well-maintained Blazor-compatible library.

Do not add a large UI framework solely to get charts.

---

# Growth Chart Behavior

Charts should support filtering.

For example:

Families:
[x] My Family
[x] Brother's Family
[ ] Parents

People:
[x] Child A
[x] Child B
[x] Nephew A

Metric:
( ) Height
( ) Weight

X-axis:
(•) Age
( ) Date

Age range:
[ All ]

The chart should update without requiring a complete page reload where practical.

Allow the user to easily toggle individuals on/off.

Charts should have readable legends and tooltips.

Do not use misleading interpolation.

If measurements are sparse, show the actual measurement points and make missing periods obvious.

---

# Percentiles

Do NOT initially implement medical growth percentiles unless you have a clearly defined source/reference dataset and an appropriate way to account for sex, age, and potentially other required information.

The first version should focus on:

**Our family's actual growth over time.**

Do not imply that GrowthLog provides medical advice.

---

# Auditability

Consider adding an audit mechanism for important actions such as:

* Measurement created
* Measurement edited
* Measurement deleted
* Sharing permission changed
* Family invitation accepted
* Family membership changed

At minimum, measurement modifications must be traceable to the user who made them.

Avoid hard deletion of historical measurements if an audit-friendly approach is more appropriate.

---

# Deletion

Provide appropriate deletion workflows.

For example:

* Delete a measurement
* Remove a person
* Remove a family member
* Disconnect a family

Be careful with cascading deletes.

Do not accidentally delete an entire family's historical data merely because a user account is removed.

---

# Seed/Demo Data

For development only, create realistic demo data representing:

Family A:

* Parent 1
* Parent 2
* Child A
* Child B

Family B:

* Grandparent 1
* Grandparent 2

Family C:

* Sibling
* Sibling's spouse
* Child C

Create several years of sample measurements so the charts and same-age comparisons can be properly tested.

Clearly identify this as development/demo data.

Do not seed fake data in production.

---

# Testing

Create automated tests for the most important business logic.

Especially test authorization.

Examples:

1. A parent can see their own child's measurements.

2. A grandparent cannot see a grandchild's measurements by default.

3. A grandparent can see a grandchild's measurements after the child's parent enables sharing.

4. Removing that permission immediately removes visibility.

5. A user cannot access another family's measurements by changing an ID in a URL.

6. A user cannot edit another family's measurements.

7. A user cannot delete another family's measurements.

8. Charts never receive unauthorized data.

9. Family filters cannot bypass authorization.

10. A family member can see the data they are explicitly permitted to see.

11. Historical measurements remain intact when a new measurement is entered.

12. Users from unrelated families cannot access each other's data.

13. Same-age comparisons only contain authorized people.

14. Same-age comparisons calculate age from birth date and measurement date.

15. Measurements are plotted at their actual age rather than being rounded to birthdays.

16. Missing measurements are not fabricated or interpolated.

17. Invitation acceptance is transactional.

18. Family-sharing changes are immediately reflected in authorization queries.

Test the data-access layer and authorization independently of the Blazor UI where practical.

Authorization tests are especially important. Do not only test that the UI hides things; test the underlying Dapper queries and services.

---

# Implementation Approach

Do this in phases.

## Phase 1 — Architecture

Before writing substantial code:

1. Inspect the repository.
2. Determine the existing .NET/Blazor structure.
3. Design the domain model.
4. Design the authorization/visibility model.
5. Design the SQL database schema.
6. Design the Dapper data-access layer.
7. Design the chart/comparison data model.
8. Explain the proposed architecture briefly.
9. Identify any important assumptions.

Then implement.

## Phase 2 — Authentication and Families

Implement:

* Authentication
* Users
* Families
* Family memberships
* People
* Relationships
* Family administration

## Phase 3 — Measurements

Implement:

* Measurement entity
* SQL schema
* Dapper queries
* Measurement entry
* Measurement history
* Editing
* Deletion
* Birthday workflow
* Unit conversion
* Exact age calculation

## Phase 4 — Sharing

Implement:

* Family connections
* Invitations
* Sharing permissions
* Grandparent visibility settings
* Server-side authorization
* Authorization-aware Dapper queries

This phase must be completed before building the charts because chart data depends on authorization.

## Phase 5 — Dashboard and Charts

Implement:

* Dashboard
* Family filters
* Height charts
* Weight charts
* Comparison views
* Person profiles
* Growth statistics

## Phase 6 — Same Age Comparison

Implement:

* Age-based charting
* Same-age height comparison
* Same-age weight comparison
* Person filtering
* Family filtering
* Age range filtering
* Same-age comparison table
* Measurement tolerance comparison
* Actual age display

## Phase 7 — Polish

Improve:

* Responsive design
* Loading states
* Validation
* Error handling
* Accessibility
* Empty states
* Navigation
* Visual consistency

## Phase 8 — Testing

Implement the authorization, Dapper data-access, domain, and comparison tests described above.

---

# Important Development Instructions

Do not generate a huge amount of speculative code all at once.

Work incrementally.

After each major phase:

1. Build the project.
2. Run the tests.
3. Fix compilation errors.
4. Fix obvious runtime problems.
5. Review the implementation against the requirements.
6. Inspect generated SQL where appropriate.
7. Verify authorization behavior.
8. Then continue.

Prefer simple, maintainable code over excessive architectural complexity.

Do not add a third-party dependency when the functionality can reasonably be implemented using built-in .NET/ASP.NET/Blazor functionality.

If you believe a third-party charting/UI library is warranted, evaluate whether it is actually necessary before adding it.

Do not introduce Entity Framework Core.

Do not replace Dapper with an ORM.

The final application should feel cohesive rather than like a collection of generated CRUD pages.

---

# Most Important Architectural Principles

The following requirements are non-negotiable:

### 1. Dapper is the application's data-access technology.

Do not use Entity Framework Core for application persistence.

### 2. Authorization must be enforced server-side.

A user's ability to see family data must be determined by explicit server-side authorization, not by the UI.

### 3. Database queries should be authorization-aware.

Do not retrieve unauthorized data and filter it out afterward.

### 4. Measurements are historical records.

Never overwrite previous measurements when a new measurement is recorded.

### 5. Same-age comparisons use actual age.

Always derive age from date of birth and measurement date.

### 6. Privacy applies everywhere.

Unauthorized people must not appear in:

* Lists
* Charts
* Search
* Tooltips
* Comparison tables
* Filters
* API/service responses
* Exports

### 7. Design for future expansion.

The application should be capable of supporting additional family relationships, permissions, comparison modes, and measurement types without requiring a complete redesign.

---

# Application Name

**GrowthLog**

Suggested tagline:

**See how your family grows.**
