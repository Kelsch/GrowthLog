# Same Age Comparison

This is a core feature of GrowthLog.

The application should allow users to compare the growth of different people based on **age**, rather than calendar date.

This is important because family members are born in different years. A calendar-based chart makes it difficult to compare them directly.

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

The application should be able to place all of these people on the same chart.

---

## Same Age Height Comparison

Create a chart with:

**X axis:** Age
**Y axis:** Height

Each person should have their own series.

Example:

```text
Height
  |
  |                         ● Child B
  |                    ●
  |              ●
  |        ●
  |
  |                    ● Child A
  |              ●
  |        ●
  |
  |              ● Dalan
  |        ●
  +--------------------------------
       4    5    6    7    8    Age
```

Use the actual measurement age, not simply the integer birthday year.

For example, if someone was born on October 12, 2019 and measured on September 30, 2026, their age should be calculated as approximately 6.97 years rather than automatically treating it as age 7.

Do not artificially move measurements to birthdays.

The chart should plot measurements at their actual age.

---

## Same Age Weight Comparison

Create an equivalent chart:

**X axis:** Age
**Y axis:** Weight

Again, each person gets their own series.

This should allow questions like:

* How much did I weigh at age 10?
* How tall was my brother at age 12?
* How does my child's growth compare with mine at the same age?
* How do all of the cousins compare at age 8?

---

## Same Age comparison controls

Create a dedicated comparison view.

Possible controls:

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

Allow individual people to be toggled independently.

### Measurement

(•) Height
( ) Weight

### X Axis

(•) Age
( ) Calendar Date

---

## Age range

Allow the user to choose an age range.

For example:

**Age range**

[ 0 ] to [ 18 ]

Or provide convenient presets:

* 0–5
* 0–10
* 0–18
* 5–10
* 10–18
* All available

Do not require measurements to exist at every age.

Missing measurements should remain missing.

---

## Exact age calculations

Create a reusable domain/service method for calculating age at the time of a measurement.

Do not duplicate age-calculation logic throughout the application.

The calculation should account for:

* Birth date
* Measurement date
* Leap years
* Exact birthday boundaries

Where useful for charting, represent age as a decimal number.

For example:

* 5.00 years
* 5.25 years
* 5.50 years
* 5.97 years

However, the UI should display friendly values such as:

**5 years, 3 months**

rather than exposing decimal years everywhere.

---

## Same-age table

In addition to the chart, provide an optional table view.

For example:

| Age | Dalan | Child A | Child B |
| --: | ----: | ------: | ------: |
|   5 | 43.2" |   42.7" |   44.0" |
|   6 | 46.1" |   45.5" |   47.0" |
|   7 | 48.8" |   49.1" |   50.2" |
|   8 | 51.0" |       — |   52.8" |

Only show values that actually exist.

Do not interpolate missing measurements unless the user explicitly requests an interpolation feature.

---

## Same-age measurement matching

Also provide a useful option to compare measurements near the same age.

For example, if one person was measured at:

**8 years, 0 months**

and another was measured at:

**8 years, 11 months**

the application should not silently treat those as identical measurements.

Instead, provide an optional comparison tolerance such as:

**Compare around age: [8 years]**

**Tolerance: [± 6 months]**

Then show measurements that actually fall within that age window.

Clearly indicate the actual age of each measurement.

For example:

| Person  | Actual age | Height |
| ------- | ---------: | -----: |
| Dalan   |      8y 1m |  51.2" |
| Child A |     7y 11m |  50.7" |
| Child B |      8y 5m |  52.1" |

This should be an additional comparison tool and should never alter the underlying measurements.

---

# Same-age comparison and privacy

The same privacy rules apply to comparison charts.

This is critical.

If the current user is allowed to see:

* Their own family
* Brother's family

but is NOT allowed to see their parents' grandchildren,

then a same-age chart must not contain those unauthorized people.

Do not solve this by adding authorized data after retrieving all people.

The data supplied to the chart should already be authorization-filtered.

A user must never be able to discover the existence of an unauthorized person's measurements through:

* Chart tooltips
* Legends
* Axis data
* Comparison tables
* Search
* Filters
* Browser/network requests
* Export functionality

---

# Growth comparison modes

Design the charting architecture so additional comparison modes can be added later.

At minimum support:

1. **Calendar time**

   * X axis = measurement date

2. **Same age**

   * X axis = person's age at measurement

3. **Birthday year**

   * Optional future mode where measurements are grouped by the person's age/birthday year

Do not hard-code the chart implementation around calendar dates.

The underlying chart data model should support both date-based and age-based measurements.

---

# Future extensibility

Structure the comparison system so future features could include:

* Compare siblings
* Compare parent vs child
* Compare multiple generations
* "Who was tallest at age 10?"
* "Who gained the most height between ages 8 and 10?"
* Height ranking at a selected age
* Weight ranking at a selected age
* Growth velocity comparison
* Export a comparison chart/image
* Share a selected comparison with authorized family members

Do not implement all of these now unless they are straightforward, but avoid an architecture that makes them difficult to add later.
