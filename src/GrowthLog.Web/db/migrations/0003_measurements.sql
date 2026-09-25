-- Measurements: historical height/weight records.
-- Canonical storage is metric (cm / kg). Imperial is display-only.

CREATE TABLE IF NOT EXISTS Measurements (
    Id              TEXT NOT NULL PRIMARY KEY,
    PersonId        TEXT NOT NULL REFERENCES People(Id),
    MeasurementDate TEXT NOT NULL,
    HeightCm        REAL NULL,
    WeightKg        REAL NULL,
    Notes           TEXT NULL,
    EnteredByUserId TEXT NOT NULL,
    CreatedUtc      TEXT NOT NULL,
    ModifiedUtc     TEXT NULL,
    IsDeleted       INTEGER NOT NULL DEFAULT 0,
    CHECK (HeightCm IS NOT NULL OR WeightKg IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS IX_Measurements_Person_Date
    ON Measurements(PersonId, MeasurementDate);
