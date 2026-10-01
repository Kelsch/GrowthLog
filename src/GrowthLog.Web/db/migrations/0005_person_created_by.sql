-- Track who created each Person so the creator can always see people they
-- created, even if those people have no active family membership.

ALTER TABLE People ADD COLUMN CreatedByUserId TEXT NULL;

CREATE INDEX IF NOT EXISTS IX_People_CreatedByUserId ON People(CreatedByUserId);

-- Best-effort backfill: if a person has measurements, attribute creation to
-- the user who entered the earliest measurement. Otherwise leave NULL.
UPDATE People
SET CreatedByUserId = (
    SELECT m.EnteredByUserId
    FROM Measurements m
    WHERE m.PersonId = People.Id
    ORDER BY m.CreatedUtc ASC
    LIMIT 1
)
WHERE CreatedByUserId IS NULL
  AND EXISTS (SELECT 1 FROM Measurements m WHERE m.PersonId = People.Id);
