-- User identity semantics Phase B schema for PostgreSQL deployments.
-- Apply before deploying code that exposes DisplayName#PublicIndex.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

ALTER TABLE "User"
    ADD COLUMN IF NOT EXISTS "PublicIndex" BIGINT NULL;

UPDATE "User"
SET "PublicIndex" = NULL
WHERE "Id" NOT IN (20000, 20001, 20002)
  AND "PublicIndex" IN (1, 2, 3);

UPDATE "User"
SET "PublicIndex" = CASE "Id"
    WHEN 20000 THEN 1
    WHEN 20001 THEN 2
    WHEN 20002 THEN 3
    ELSE "PublicIndex"
END
WHERE "Id" IN (20000, 20001, 20002)
  AND "PublicIndex" IS DISTINCT FROM CASE "Id"
      WHEN 20000 THEN 1
      WHEN 20001 THEN 2
      WHEN 20002 THEN 3
      ELSE "PublicIndex"
  END;

WITH public_index_seed AS (
    SELECT COALESCE(MAX("PublicIndex"), 999) AS current_max
    FROM "User"
    WHERE "PublicIndex" >= 1000
),
missing_users AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (ORDER BY "Id") AS row_no
    FROM "User"
    WHERE "PublicIndex" IS NULL OR "PublicIndex" <= 0
)
UPDATE "User" AS target
SET "PublicIndex" = public_index_seed.current_max + missing_users.row_no
FROM public_index_seed, missing_users
WHERE target."Id" = missing_users."Id";

CREATE UNIQUE INDEX IF NOT EXISTS "idx_user_public_index"
    ON "User" ("PublicIndex");
