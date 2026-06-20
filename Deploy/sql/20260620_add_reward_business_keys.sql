-- Reward business key uniqueness guards for WOG-4.
-- Apply before deploying code that writes RewardBusinessKey for coin and experience rewards.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

ALTER TABLE "CoinTransaction"
    ADD COLUMN IF NOT EXISTS "RewardBusinessKey" VARCHAR(200) NULL;

ALTER TABLE "ExpTransaction"
    ADD COLUMN IF NOT EXISTS "RewardBusinessKey" VARCHAR(200) NULL;

WITH coin_key_candidates AS (
    SELECT
        "Id",
        "TenantId",
        CASE
            WHEN "BusinessType" = 'POST_LIKE' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:post-like:author:', "ToUserId", ':post:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
            WHEN "BusinessType" = 'POST_LIKE_ACTION' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:post-like:giver:', "ToUserId", ':post:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
            WHEN "BusinessType" = 'COMMENT_LIKE' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:comment-like:author:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
            WHEN "BusinessType" = 'COMMENT_LIKE_ACTION' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:comment-like:giver:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
            WHEN "BusinessType" = 'COMMENT_POST' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:comment-create:author:', "ToUserId", ':comment:', "BusinessId")
            WHEN "BusinessType" = 'COMMENT_REPLY' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:comment-reply:author:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
            WHEN "BusinessType" = 'GOD_COMMENT' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:highlight-base:god-comment:author:', "ToUserId", ':comment:', "BusinessId")
            WHEN "BusinessType" = 'SOFA' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT('coin:highlight-base:sofa:author:', "ToUserId", ':comment:', "BusinessId")
            WHEN "BusinessType" ~ '^(GodComment|Sofa)_RETENTION_W[1-3]$' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                THEN CONCAT(
                    'coin:highlight-retention:',
                    CASE WHEN SPLIT_PART("BusinessType", '_', 1) = 'GodComment' THEN 'god-comment' ELSE 'sofa' END,
                    ':highlight:',
                    "BusinessId",
                    ':week:',
                    SUBSTRING("BusinessType" FROM '_RETENTION_W([1-3])$'),
                    ':author:',
                    "ToUserId")
            ELSE NULL
        END AS "RewardBusinessKey",
        ROW_NUMBER() OVER (
            PARTITION BY
                "TenantId",
                CASE
                    WHEN "BusinessType" = 'POST_LIKE' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:post-like:author:', "ToUserId", ':post:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
                    WHEN "BusinessType" = 'POST_LIKE_ACTION' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:post-like:giver:', "ToUserId", ':post:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
                    WHEN "BusinessType" = 'COMMENT_LIKE' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:comment-like:author:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
                    WHEN "BusinessType" = 'COMMENT_LIKE_ACTION' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:comment-like:giver:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
                    WHEN "BusinessType" = 'COMMENT_POST' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:comment-create:author:', "ToUserId", ':comment:', "BusinessId")
                    WHEN "BusinessType" = 'COMMENT_REPLY' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:comment-reply:author:', "ToUserId", ':comment:', "BusinessId", ':day:', TO_CHAR("CreateTime", 'YYYYMMDD'))
                    WHEN "BusinessType" = 'GOD_COMMENT' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:highlight-base:god-comment:author:', "ToUserId", ':comment:', "BusinessId")
                    WHEN "BusinessType" = 'SOFA' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT('coin:highlight-base:sofa:author:', "ToUserId", ':comment:', "BusinessId")
                    WHEN "BusinessType" ~ '^(GodComment|Sofa)_RETENTION_W[1-3]$' AND "ToUserId" IS NOT NULL AND "BusinessId" IS NOT NULL
                        THEN CONCAT(
                            'coin:highlight-retention:',
                            CASE WHEN SPLIT_PART("BusinessType", '_', 1) = 'GodComment' THEN 'god-comment' ELSE 'sofa' END,
                            ':highlight:',
                            "BusinessId",
                            ':week:',
                            SUBSTRING("BusinessType" FROM '_RETENTION_W([1-3])$'),
                            ':author:',
                            "ToUserId")
                    ELSE NULL
                END
            ORDER BY "CreateTime" ASC, "Id" ASC
        ) AS row_no
    FROM "CoinTransaction"
    WHERE "Status" = 'SUCCESS'
      AND "RewardBusinessKey" IS NULL
)
UPDATE "CoinTransaction" AS target
SET "RewardBusinessKey" = candidate."RewardBusinessKey",
    "ModifyTime" = NOW()
FROM coin_key_candidates AS candidate
WHERE target."Id" = candidate."Id"
  AND candidate."RewardBusinessKey" IS NOT NULL
  AND candidate.row_no = 1
  AND NOT EXISTS (
      SELECT 1
      FROM "CoinTransaction" AS existing
      WHERE existing."TenantId" = target."TenantId"
        AND existing."RewardBusinessKey" = candidate."RewardBusinessKey"
  );

WITH exp_key_candidates AS (
    SELECT
        "Id",
        "TenantId",
        CASE
            WHEN "ExpType" = 'POST_CREATE' AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:post-create:author:', "UserId", ':post:', "BusinessId")
            WHEN "ExpType" = 'FIRST_POST'
                THEN CONCAT('exp:first-post:user:', "UserId")
            WHEN "ExpType" = 'COMMENT_CREATE' AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:comment-create:author:', "UserId", ':comment:', "BusinessId")
            WHEN "ExpType" = 'FIRST_COMMENT'
                THEN CONCAT('exp:first-comment:user:', "UserId")
            WHEN "ExpType" = 'RECEIVE_LIKE' AND LOWER("BusinessType") IN ('post', 'comment') AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:receive-like:', LOWER("BusinessType"), ':user:', "UserId", ':target:', "BusinessId", ':day:', TO_CHAR("CreatedDate", 'YYYYMMDD'))
            WHEN "ExpType" = 'GIVE_LIKE' AND LOWER("BusinessType") IN ('post', 'comment') AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:give-like:', LOWER("BusinessType"), ':user:', "UserId", ':target:', "BusinessId", ':day:', TO_CHAR("CreatedDate", 'YYYYMMDD'))
            WHEN "ExpType" = 'GOD_COMMENT' AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:highlight-base:god-comment:author:', "UserId", ':comment:', "BusinessId")
            WHEN "ExpType" = 'SOFA_COMMENT' AND "BusinessId" IS NOT NULL
                THEN CONCAT('exp:highlight-base:sofa:author:', "UserId", ':comment:', "BusinessId")
            ELSE NULL
        END AS "RewardBusinessKey",
        ROW_NUMBER() OVER (
            PARTITION BY
                "TenantId",
                CASE
                    WHEN "ExpType" = 'POST_CREATE' AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:post-create:author:', "UserId", ':post:', "BusinessId")
                    WHEN "ExpType" = 'FIRST_POST'
                        THEN CONCAT('exp:first-post:user:', "UserId")
                    WHEN "ExpType" = 'COMMENT_CREATE' AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:comment-create:author:', "UserId", ':comment:', "BusinessId")
                    WHEN "ExpType" = 'FIRST_COMMENT'
                        THEN CONCAT('exp:first-comment:user:', "UserId")
                    WHEN "ExpType" = 'RECEIVE_LIKE' AND LOWER("BusinessType") IN ('post', 'comment') AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:receive-like:', LOWER("BusinessType"), ':user:', "UserId", ':target:', "BusinessId", ':day:', TO_CHAR("CreatedDate", 'YYYYMMDD'))
                    WHEN "ExpType" = 'GIVE_LIKE' AND LOWER("BusinessType") IN ('post', 'comment') AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:give-like:', LOWER("BusinessType"), ':user:', "UserId", ':target:', "BusinessId", ':day:', TO_CHAR("CreatedDate", 'YYYYMMDD'))
                    WHEN "ExpType" = 'GOD_COMMENT' AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:highlight-base:god-comment:author:', "UserId", ':comment:', "BusinessId")
                    WHEN "ExpType" = 'SOFA_COMMENT' AND "BusinessId" IS NOT NULL
                        THEN CONCAT('exp:highlight-base:sofa:author:', "UserId", ':comment:', "BusinessId")
                    ELSE NULL
                END
            ORDER BY "CreateTime" ASC, "Id" ASC
        ) AS row_no
    FROM "ExpTransaction"
    WHERE "RewardBusinessKey" IS NULL
)
UPDATE "ExpTransaction" AS target
SET "RewardBusinessKey" = candidate."RewardBusinessKey",
    "ModifyTime" = NOW()
FROM exp_key_candidates AS candidate
WHERE target."Id" = candidate."Id"
  AND candidate."RewardBusinessKey" IS NOT NULL
  AND candidate.row_no = 1
  AND NOT EXISTS (
      SELECT 1
      FROM "ExpTransaction" AS existing
      WHERE existing."TenantId" = target."TenantId"
        AND existing."RewardBusinessKey" = candidate."RewardBusinessKey"
  );

CREATE UNIQUE INDEX IF NOT EXISTS "idx_coin_reward_business_key"
    ON "CoinTransaction" ("TenantId", "RewardBusinessKey");

CREATE UNIQUE INDEX IF NOT EXISTS "idx_exp_reward_business_key"
    ON "ExpTransaction" ("TenantId", "RewardBusinessKey");

-- Optional audit query for historical duplicates left untouched by this migration:
-- SELECT "TenantId", "BusinessType", "BusinessId", "ToUserId", COUNT(*)
-- FROM "CoinTransaction"
-- WHERE "Status" = 'SUCCESS'
--   AND "RewardBusinessKey" IS NULL
--   AND "BusinessType" IN (
--       'POST_LIKE', 'POST_LIKE_ACTION', 'COMMENT_LIKE', 'COMMENT_LIKE_ACTION',
--       'COMMENT_POST', 'COMMENT_REPLY', 'GOD_COMMENT', 'SOFA')
-- GROUP BY "TenantId", "BusinessType", "BusinessId", "ToUserId"
-- HAVING COUNT(*) > 1;

-- SELECT "TenantId", "ExpType", "BusinessType", "BusinessId", "UserId", COUNT(*)
-- FROM "ExpTransaction"
-- WHERE "RewardBusinessKey" IS NULL
--   AND "ExpType" IN (
--       'POST_CREATE', 'FIRST_POST', 'COMMENT_CREATE', 'FIRST_COMMENT',
--       'RECEIVE_LIKE', 'GIVE_LIKE', 'GOD_COMMENT', 'SOFA_COMMENT')
-- GROUP BY "TenantId", "ExpType", "BusinessType", "BusinessId", "UserId"
-- HAVING COUNT(*) > 1;
