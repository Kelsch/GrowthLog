# GrowthLog UX / Product Design Specification

## Purpose

GrowthLog is a family growth-tracking application for recording people's height and weight over time and making it easy to answer questions such as:

- Who needs to be measured next?
- What is someone's growth history?
- How tall was everyone at the same age?
- How have family members grown relative to one another?
- What were someone's height and weight on a particular date or around a particular age?

The application should feel like a **family utility first** and a database second.

The current application is functional but feels like a collection of CRUD tables and forms. The redesign should preserve the useful behavior already present—especially adding/linking people without navigating to a separate page—while substantially improving hierarchy, discoverability, speed, and visualization.

This document is a UX/product specification for generating an implementation prompt. It is intentionally more prescriptive about **behavior and user experience** than about exact HTML/CSS implementation.

---

# 1. Core UX Philosophy

## 1.1 The primary job

The primary job of GrowthLog is:

> **Record family growth with very little effort, then make that history easy and enjoyable to understand.**

The application should optimize for these recurring actions:

1. See who is due for a birthday measurement.
2. Add a measurement quickly.
3. See a person's growth history.
4. Compare people at the same age.
5. Manage family membership and sharing.

## 1.2 Contextual actions

GrowthLog should let users perform common actions **without leaving the context they are currently viewing**.

Do NOT create a new page for a simple action when an inline form, modal, popover, or side drawer is more appropriate.

Examples:

- Add person → inline section or modal/drawer.
- Add existing person → inline search/linking UI.
- Add measurement → modal/drawer.
- Edit measurement → modal/drawer.
- Rename family → inline edit or small modal.
- Remove person → confirmation dialog.
- View person → person detail page is appropriate.
- Compare people → dedicated comparison workspace is appropriate.

### Strong anti-pattern

Avoid:

`Families → Add Person page → Save → Return to Families`

Prefer:

`Families → + Add person → Inline/modal → Save → Person immediately appears in the same family`

---

# 2. Information Architecture

Keep the application navigation simple.

Primary navigation:

- Dashboard
- Families
- People
- Compare
- Settings
- Sharing
- Invitations

The exact visual treatment can change, but the conceptual structure should remain understandable.

## 2.1 Navigation rules

The current section must always be obvious.

Navigation should not become excessively nested.

Do not create separate top-level destinations for:

- Add measurement
- Add person
- Edit person
- Edit measurement

Those are actions, not destinations.

---

# 3. Global Application Shell

## 3.1 Desktop

Use a persistent left navigation rail/sidebar.

The sidebar should contain:

- GrowthLog branding
- Primary navigation
- Current-section indicator
- User/account control near the bottom

The main content area should:

- Have a comfortable maximum reading width.
- Use generous but not excessive spacing.
- Use clear page-level headings.
- Keep important actions near the content they affect.
- Avoid overly dense tables.

## 3.2 Mobile

On small screens:

- Replace the persistent sidebar with a compact navigation mechanism.
- Prioritize the current family/person context.
- Keep primary actions reachable with touch.
- Convert wide tables into stacked rows/cards or horizontally scroll only where exact tabular comparison truly requires it.
- Never require desktop-only hover behavior.

---

# 4. Visual Language

The interface should feel:

- Clean
- Modern
- Friendly
- Calm
- Data-focused without feeling clinical
- Family-oriented
- Easy for a non-technical user to understand

Avoid:

- Enterprise-admin-panel aesthetics
- Excessive borders
- Extremely dense tables
- Huge numbers of controls
- Heavy gradients
- Excessive shadows
- Tiny text
- Visually competing charts
- Making every element look like a separate card

## 4.1 Suggested visual hierarchy

Use a restrained neutral surface with:

- Dark navy/charcoal for primary text and navigation
- White or near-white content surfaces
- One primary action accent
- A small set of consistent person/category colors
- Soft status colors for due/overdue states

Person colors should remain stable throughout the application so a person is visually recognizable across charts, lists, and recent activity.

Example:

Dalan → blue  
Elowyn → teal  
Chrin → purple  
Daxon → orange  
Vincent → violet

The actual palette can differ; consistency matters more than these exact colors.

---

# 5. Dashboard

The dashboard should answer the most useful questions immediately.

## 5.1 Top section

Heading:

> Good morning, [User]!

Subheading:

> Track, compare, and celebrate your family's growth.

Primary action:

> + Add measurement

Secondary action:

> + Add person

Do not make "Add person" visually more important than measurement entry.

## 5.2 Summary area

Possible summary items:

- Families
- People
- Total measurements
- Years tracking

These should be compact and secondary to the actual growth workflow.

## 5.3 Upcoming measurements

This should be one of the most prominent sections of the dashboard.

Title:

> Upcoming measurements

Description:

> Keep track of birthdays and yearly growth measurements.

Each person card/row should show:

- Person
- Current age
- Birthday
- Next measurement date
- Relative status
- Last measurement
- Add measurement action

Example:

> Daxon Kelsch  
> Age 10 · September 29  
> Due in 12 days  
> Last measured Sep 29, 2026  
> [+ Add measurement]

Status examples:

- Due today
- Due tomorrow
- Due in 5 days
- Due in 2 months
- Overdue by 8 days
- No measurement history

## 5.4 Birthday measurement behavior

The application should treat the person's birthday as the normal recurring measurement date.

The user should not have to manually calculate when the next measurement is due.

The date should be generated from:

- Date of birth
- Current date
- Measurement/reminder settings

The UX should make it immediately apparent when a yearly measurement is approaching.

---

# 6. Families Page

The Families page should retain the current application's strongest UX characteristic:

> **People can be added or linked without leaving the family page.**

However, reorganize the page around family activity instead of database administration.

## 6.1 Family header

Example:

> Dalan's Family  
> 3 children · 2 adults · 1 family

Actions:

- Rename
- Family settings/sharing

Avoid a large standalone rename form occupying the top of the page.

## 6.2 Upcoming measurements — first

On a family page, upcoming measurements should be above the member-management tables.

Use compact horizontal cards on desktop and vertically stacked cards on mobile.

Each card should show:

- Initial/avatar
- Name
- Age
- Birthday
- Measurement due date
- Status
- Latest recorded values
- Add measurement button

The most urgent/due person should be visually easy to find.

## 6.3 People in this family

Replace the current oversized database-like table with a clearer member list.

Suggested controls:

- All
- Adults
- Children
- Search people

Suggested row:

> [Avatar] Dalan Kelsch   Adult  
> Age 27 · Birthday Mar 7  
> 168.3 cm · 62.7 kg  
> Measured Sep 28, 2026  
> [+ Add measurement] [More]

A row can open the person's profile when the name/row is selected.

The Add measurement button must remain a direct action.

## 6.4 Add/link people

Keep this workflow on the same page.

Recommended design:

### Collapsed state

> + Add a person

### Expanded state

Show two simple choices:

#### Add existing person

Search by name, then select the person.

#### Create new person

Minimal fields:

- First name
- Middle name (optional)
- Last name
- Date of birth
- Kind (Adult/Child)
- Add

Do not force users into a separate page.

If there are additional settings, collect them after the person exists rather than making initial creation unnecessarily long.

## 6.5 Shared people

Shared people should be clearly separated from people owned/managed directly by this family.

Suggested heading:

> Shared with this family

Short explanation:

> People from connected families who have granted this family access.

Use a visually different but still consistent section.

Do not mix shared records into the primary family-member list without indicating their origin.

---

# 7. People Directory

The People page is for finding and managing individual people across families.

## 7.1 Primary UI

Provide:

- Search
- Family filter
- Adult/Child filter
- Sort option
- Add person action

Person cards/rows should emphasize:

- Name
- Age
- Family membership
- Latest height
- Latest weight
- Date of latest measurement
- Quick add measurement

## 7.2 Person selection

Selecting a person should open their detailed growth view.

---

# 8. Person Detail Page

This page is where the application becomes a personal growth record rather than a CRUD interface.

## 8.1 Header

Example:

> Dalan Kelsch  
> Age 27 · Dalan's Family

Show:

- Avatar/initial
- Date of birth
- Family
- Edit person action
- Add measurement action

## 8.2 Key metric cards

Show latest:

- Height
- Weight
- Measurement date

Example:

> Height  
> 168.3 cm

> Weight  
> 62.7 kg

> Last measured  
> Sep 28, 2026

## 8.3 Growth visualization

The primary chart should be visually dominant.

Do not combine height and weight on the same confusing scale.

Use a metric selector:

`Height` | `Weight`

Optional range selector:

`All` `1Y` `2Y` `5Y`

The graph should show actual measurements as points connected chronologically.

Important interaction:

- Clicking/tapping a point selects it.
- Selected point displays exact date, age, and measurement.
- Nearby measurements should not be visually indistinguishable.
- Missing periods are not silently filled with invented values.

## 8.4 Measurement history

Below the graph, show a clean history list/table:

- Date
- Age
- Height
- Weight
- Actions

Exact values matter here.

The graph provides understanding; the history provides precision.

---

# 9. Add Measurement UX

This is one of the most important workflows in the application.

## 9.1 No navigation

Add measurement must open from the current context.

Preferred implementation pattern:

- Modal
- Side drawer
- Popover-sized form for especially small screens

Do not navigate to a new page.

## 9.2 Minimal default form

Example:

> Add measurement  
> Daxon Kelsch · Age 10

Date:

`September 29, 2026`

Height:

`137.8 cm`

Weight:

`34.6 kg`

Actions:

`Cancel` `Save measurement`

## 9.3 Smart defaults

The form should intelligently prefill:

- Today's date
- Current person
- Current age calculated from date of birth

The date can be changed for historical measurements.

## 9.4 Data validation

Validate:

- Date
- Height
- Weight

Validation must be friendly and immediate.

Do not display raw exception/error text.

Examples:

> Enter a valid height.

> Weight must be greater than 0.

> This measurement is dated before the person's birth date.

## 9.5 Save behavior

After saving:

- Close the modal/drawer.
- Update the visible person row/card.
- Update latest measurement.
- Update upcoming measurement status if applicable.
- Show a small success confirmation.

Do not reload the user to another page.

---

# 10. Comparison Experience

The current comparison screen is the area that most needs a UX redesign.

The current multi-person line graph becomes difficult to interpret when several people's lines cross and measurements are sparse.

GrowthLog should support two complementary comparison modes.

---

# 11. Comparison Mode A — Same Age

This should be the primary comparison experience.

The user should be able to answer:

> **How tall was everyone at age 10?**

## 11.1 Controls

At the top:

Metric:

`Height`

or

`Weight`

Age:

`[ 10 years ]`

The age control can be:

- Slider
- Stepper
- Number/select control

A slider is preferred for quick exploration, provided there is also an accessible keyboard/input alternative.

## 11.2 Visualization

Use a horizontal comparison visualization instead of a dense multi-line chart.

Example:

```text
Age 10

Dalan     ●─────────────── 143.5 cm
Chrin     ●────────────────── 151.1 cm
Daxon     ●────────── 120.7 cm
Elowyn    ●────────────── 132.7 cm
```

Conceptually, this can be implemented as a dot plot / horizontal measurement comparison.

The exact implementation can use another clear comparison visualization if the selected framework/library does not support a proper dot plot.

## 11.3 Handling missing values

Do not fabricate missing measurements.

If a person has no measurement close enough to the selected age, display:

> No measurement near age 10

The application can use its existing rule of finding the nearest actual measurement within a defined age tolerance, but that tolerance must be clearly communicated.

If a tolerance is used:

> Showing the nearest measurement within ±6 months.

Never interpolate values unless interpolation is explicitly added as a separate, clearly labeled feature.

---

# 12. Comparison Mode B — Growth Trajectories

This answers:

> How did everyone grow over time?

This is where a multi-person line chart belongs.

## 12.1 Controls

Provide:

- Metric: Height / Weight
- X axis: Age / Date
- Selected people
- Optional family filter

## 12.2 Interaction model

The chart should not force every person to compete equally for attention.

Interactions:

- Tap/click a person in the legend → highlight that person.
- Other people become visually muted.
- Tap again → restore all people.
- Tap a point → show exact measurement.
- Optional hover on desktop, but hover must never be the only way to access information.

## 12.3 Visual rules

Use:

- Stable person colors.
- Larger clickable measurement points.
- Clear tooltip.
- A readable legend.
- Optional subtle background reference bands.

Avoid:

- Six or more highly saturated lines with identical visual weight.
- Dual y-axes unless truly necessary.
- Decorative chart elements that compete with the data.

---

# 13. Percentile / Reference Data

If percentile information is implemented, it should be visually secondary to the actual family measurements.

Possible presentation:

- Soft reference bands behind the actual data.
- Toggle: `Percentiles`
- Clear legend.

Do not make percentile/reference information visually overpower actual family data.

---

# 14. Comparison Table

Keep the table, but demote it from being the primary visualization.

Use it for exact numbers after the graphical comparison.

Suggested:

> Comparison details

Controls should remain aligned with the selected comparison:

- Metric
- Age or date range

Rows:

Age | Person 1 | Person 2 | Person 3 | ...

Missing values:

`—`

Add explanatory text where relevant:

> Values show the nearest actual measurement within ±6 months of the selected age. Missing values are not interpolated.

The exact tolerance should remain configurable in the product logic rather than hardcoded only in the UI.

---

# 15. Upcoming Measurement System

This deserves first-class UX because it is one of GrowthLog's defining recurring workflows.

## 15.1 Due calculation

Each person has a birthday.

GrowthLog should calculate:

- Next birthday
- Next expected yearly measurement
- Days until due
- Overdue state

## 15.2 Status states

Suggested states:

### Completed

> Measured this year

### Upcoming

> Due in 24 days

### Due soon

> Due in 3 days

### Due today

> Due today

### Overdue

> 12 days overdue

### Never measured

> No measurements yet

Use subtle status styling rather than alarming colors.

## 15.3 Family-level prioritization

Within a family, sort upcoming measurement cards by urgency:

1. Due/overdue
2. Soonest upcoming
3. Later dates

However, do not hide people who have no measurement history.

---

# 16. Family Privacy / Sharing UX

GrowthLog should treat family data as private by default.

The sharing model should be understandable without requiring users to understand database relationships.

Conceptually:

- Family data is private by default.
- A family can explicitly grant access to another connected family.
- Parents can control whether another family can see their children's growth data.
- Shared people should visibly indicate their originating family.

The interface should use plain-language descriptions for permissions rather than exposing low-level access-control terminology.

Example:

> Share your family's growth information

Then show:

> Dalan's Family  
> Can view: Height and weight  
> Shared with: Chrin's Family

Avoid ambiguous settings such as:

`CanViewFamilyData = true`

The user should see human-readable consequences.

---

# 17. Empty States

Every major section needs a useful empty state.

## No people

> Your family doesn't have any people yet.  
> Add yourself, a child, or another family member to get started.  
> [+ Add person]

## No measurements

> No measurements yet.  
> Add the first height or weight measurement to start a growth history.  
> [+ Add measurement]

## No upcoming measurement

> Everyone is caught up.  
> Your next birthday measurement will appear here automatically.

## No comparison data

> There isn't enough measurement data for this comparison yet.

Do not show empty tables with blank rows.

---

# 18. Destructive Actions

For:

- Remove person from family
- Delete measurement
- Remove family member/user
- Revoke sharing

Require confirmation.

The confirmation should explain the consequence.

Example:

> Remove Daxon from Dalan's Family?

> This removes the family relationship but does not delete Daxon's person record.

The exact wording must match the application's real behavior.

---

# 19. Editing Existing Data

Editing should follow the same contextual principle as adding.

Examples:

- Edit person → modal/drawer.
- Edit measurement → modal/drawer.
- Rename family → inline/modal.

Do not send users to separate pages for tiny edits.

---

# 20. Responsive UX

The app must be designed rather than merely shrunk for mobile.

## Desktop

Use:

- Two-column sections where useful.
- Wide charts.
- Compact member rows.
- Side-by-side form fields when appropriate.

## Tablet

Reduce columns and maintain comfortable touch targets.

## Mobile

Prioritize:

1. Upcoming measurements
2. Quick add measurement
3. Family members
4. Growth charts
5. Detailed tables

Forms should stack vertically.

Wide comparison tables may use local horizontal scrolling, but the primary comparison experience should not depend on horizontal scrolling.

---

# 21. Accessibility

The implementation must support:

- Keyboard navigation.
- Visible focus states.
- Semantic buttons/links/inputs.
- Labels for all form inputs.
- Accessible modal/drawer behavior.
- Appropriate ARIA labeling where needed.
- Color plus text/icon for status; do not rely on color alone.
- Touch targets around 44 CSS px where practical.
- Readable contrast.
- Reduced-motion support.

Chart interactions must have a non-hover path.

For example, clicking/tapping a point should expose the same information that desktop hover reveals.

---

# 22. Interaction and Feedback

Every meaningful user action should have immediate feedback.

Examples:

Saving:

> Measurement saved.

Adding a person:

> Daxon was added to Dalan's Family.

Linking an existing person:

> Daxon is now part of Dalan's Family.

Removing:

> Daxon was removed from this family.

Errors:

Show the error close to the affected action and explain how to fix it.

Avoid generic messages such as:

> Operation failed.

---

# 23. Loading States

Use skeletons or local loading indicators for:

- Dashboard data
- Family members
- Charts
- Comparison results

Do not blank the entire page while one section is loading.

A working section should remain usable while another section loads when technically possible.

---

# 24. Design System Guidance

The application should use a consistent spacing scale, typography scale, border radius, button hierarchy, and input treatment.

Recommended visual hierarchy:

### Primary action

Solid accent button.

Examples:

- Add measurement
- Save measurement
- Add person

### Secondary action

Outlined or subtle button.

### Tertiary action

Text link or icon action.

Do not make every button look primary.

## Cards

Use cards only when they establish meaningful grouping.

Avoid:

Card inside card inside card.

## Tables

Use only when exact multi-column data benefits from tabular alignment.

Do not use tables as the default layout for every screen.

---

# 25. Recommended Component Patterns

A reusable GrowthLog component vocabulary should emerge from the UX.

Potential components:

- AppShell
- PageHeader
- FamilyHeader
- PersonAvatar
- PersonRow
- PersonCard
- UpcomingMeasurementCard
- MeasurementForm
- AddPersonPanel
- ConfirmDialog
- MetricCard
- GrowthChart
- SameAgeComparison
- ComparisonTable
- MeasurementHistory
- FamilyFilter
- PersonSelector
- StatusBadge
- EmptyState
- Toast/Notification

These are conceptual UI components; the actual component names can be adapted to the existing codebase.

---

# 26. Data-entry Principles

GrowthLog should minimize the number of interactions required to record a normal measurement.

For a known person, target workflow:

**Select person → enter height/weight → Save**

The normal case should not require:

- Visiting People
- Opening a profile
- Opening a Measurements tab
- Clicking Add
- Selecting the person again
- Returning to the previous page

The system already knows the context.

Use that context.

---

# 27. User Mental Model

The application should reinforce this mental model:

> **Families contain people.  
> People have measurements.  
> Birthdays create yearly measurement reminders.  
> Measurements create growth history.  
> Growth history can be visualized and compared.**

This should remain consistent everywhere.

---

# 28. What NOT to Change Just for the Sake of Redesign

Do not redesign working behavior merely because it looks old.

In particular:

- Keep inline family/person creation behavior.
- Keep the ability to link an existing person.
- Keep exact measurement values.
- Keep family sharing functionality.
- Keep the concept of separate Families and People.
- Keep the useful comparison-table data.
- Keep existing privacy rules and permissions unless the redesign is specifically changing them.

The goal is to improve **how the functionality is experienced**, not to throw away useful product behavior.

---

# 29. UX Acceptance Criteria

A redesign should be considered successful when these tasks feel straightforward:

## Task A — Record a birthday measurement

A user opens a family page, sees who is due, clicks that person's Add measurement button, enters height/weight, and saves without leaving the page.

## Task B — Add a person

A user creates a new person from the Families page without navigating to a separate Add Person page.

## Task C — Link an existing person

A user searches for an existing person directly from the family page and links them without creating a duplicate.

## Task D — Understand one person's growth

A user opens a person's profile and can immediately understand height/weight progression over time.

## Task E — Compare same-age measurements

A user selects an age and can quickly determine how different family members compared at that age.

## Task F — Compare trajectories

A user can view multiple people's growth trajectories and focus one person without the chart becoming unreadable.

## Task G — Find upcoming measurements

A user can open the dashboard or family page and immediately identify who needs to be measured next.

---

# 30. Priority Order for Implementation

The UI should be implemented in roughly this order:

### Priority 1 — Quick measurement workflow

- Upcoming measurements
- Add measurement modal/drawer
- Save/update behavior
- Due/overdue status

### Priority 2 — Family page redesign

- Family header
- Upcoming section
- Improved member rows
- Inline add/link person

### Priority 3 — Person detail

- Latest metrics
- Height chart
- Weight chart
- Measurement history

### Priority 4 — Comparison redesign

- Same-age comparison
- Growth trajectories
- Interactive person highlighting
- Improved comparison table

### Priority 5 — Refinement

- Responsive/mobile layouts
- Empty states
- Accessibility
- Loading states
- Notifications
- Visual consistency

---

# 31. Engineering Constraints for the Generated Implementation Prompt

The implementation prompt should preserve these project constraints:

- Application is written in **Blazor**.
- Data access should use **Dapper** rather than replacing it with Entity Framework.
- Prefer the existing project's architecture and dependencies.
- Do not introduce a UI framework/library unless the existing project already uses it or there is a strong project-level reason.
- Reuse existing models, services, repositories, validation, authentication, and authorization where appropriate.
- Avoid rewriting working backend functionality merely to change the UI.
- Keep changes incremental and reviewable.
- Favor reusable Blazor components over duplicated markup.
- Keep business rules out of presentation components when an existing domain/service layer is available.
- Preserve existing privacy/sharing semantics.
- Preserve existing data; migrations should be additive and deliberate.

---

# 32. Guidance for the AI Generating the Aider Prompt

The generated Aider prompt should instruct the coding model to:

1. Inspect the existing repository before changing anything.
2. Identify the current Blazor structure, models, Dapper data-access code, services, routes, and styling.
3. Preserve existing working business logic unless a change is necessary for the UX.
4. Implement the redesign incrementally.
5. Build reusable components for recurring UX patterns.
6. Avoid creating new pages for simple add/edit actions.
7. Implement the birthday measurement workflow as a first-class feature.
8. Implement same-age comparison as a distinct visualization from trajectory comparison.
9. Test existing functionality after each major change.
10. Run the application's existing build/test commands and resolve regressions.
11. Avoid inventing data or backend behavior that does not exist.
12. Prefer the simplest implementation that satisfies the UX requirements.

The coding agent should not blindly recreate the mockup pixel-for-pixel. The mockups are visual references; this document defines the desired **interaction model and product behavior**.

---

# 33. Design North Star

Every design decision should pass this test:

> **Does this make it easier for a parent/family member to record growth or understand growth?**

If the answer is no, the element should not receive prominent visual attention.

GrowthLog should ultimately feel like:

> **“I open it, I immediately see who needs to be measured, I can record it in seconds, and I can easily see how everyone has grown.”**

Not:

> **“I am managing records in a database.”**
