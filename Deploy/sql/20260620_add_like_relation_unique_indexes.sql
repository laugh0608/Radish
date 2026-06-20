-- Content interaction like relation consistency.
-- Apply before deploying code that routes post/comment like toggles through repository-level relation guards.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

WITH ranked_post_likes AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "TenantId", "UserId", "PostId"
            ORDER BY "IsDeleted" ASC, "LikedAt" DESC, "Id" DESC
        ) AS row_no
    FROM "UserPostLike"
)
DELETE FROM "UserPostLike" AS target
USING ranked_post_likes AS ranked
WHERE target."Id" = ranked."Id"
  AND ranked.row_no > 1;

WITH post_like_counts AS (
    SELECT
        "TenantId",
        "PostId",
        COUNT(*)::INTEGER AS active_count
    FROM "UserPostLike"
    WHERE "IsDeleted" = FALSE
    GROUP BY "TenantId", "PostId"
)
UPDATE "Post" AS target
SET "LikeCount" = post_like_counts.active_count
FROM post_like_counts
WHERE target."TenantId" = post_like_counts."TenantId"
  AND target."Id" = post_like_counts."PostId"
  AND target."LikeCount" IS DISTINCT FROM post_like_counts.active_count;

UPDATE "Post" AS target
SET "LikeCount" = 0
WHERE target."LikeCount" <> 0
  AND NOT EXISTS (
      SELECT 1
      FROM "UserPostLike" AS like_relation
      WHERE like_relation."TenantId" = target."TenantId"
        AND like_relation."PostId" = target."Id"
        AND like_relation."IsDeleted" = FALSE
  );

CREATE UNIQUE INDEX IF NOT EXISTS "idx_userpostlike_tenant_user_post"
    ON "UserPostLike" ("TenantId", "UserId", "PostId");

CREATE INDEX IF NOT EXISTS "idx_userpostlike_post_active"
    ON "UserPostLike" ("TenantId", "PostId", "IsDeleted");

WITH ranked_comment_likes AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "TenantId", "UserId", "CommentId"
            ORDER BY "IsDeleted" ASC, "LikedAt" DESC, "Id" DESC
        ) AS row_no
    FROM "UserCommentLike"
)
DELETE FROM "UserCommentLike" AS target
USING ranked_comment_likes AS ranked
WHERE target."Id" = ranked."Id"
  AND ranked.row_no > 1;

WITH comment_like_counts AS (
    SELECT
        "TenantId",
        "CommentId",
        COUNT(*)::INTEGER AS active_count
    FROM "UserCommentLike"
    WHERE "IsDeleted" = FALSE
    GROUP BY "TenantId", "CommentId"
)
UPDATE "Comment" AS target
SET "LikeCount" = comment_like_counts.active_count
FROM comment_like_counts
WHERE target."TenantId" = comment_like_counts."TenantId"
  AND target."Id" = comment_like_counts."CommentId"
  AND target."LikeCount" IS DISTINCT FROM comment_like_counts.active_count;

UPDATE "Comment" AS target
SET "LikeCount" = 0
WHERE target."LikeCount" <> 0
  AND NOT EXISTS (
      SELECT 1
      FROM "UserCommentLike" AS like_relation
      WHERE like_relation."TenantId" = target."TenantId"
        AND like_relation."CommentId" = target."Id"
        AND like_relation."IsDeleted" = FALSE
  );

CREATE UNIQUE INDEX IF NOT EXISTS "idx_usercommentlike_tenant_user_comment"
    ON "UserCommentLike" ("TenantId", "UserId", "CommentId");

CREATE INDEX IF NOT EXISTS "idx_usercommentlike_comment_active"
    ON "UserCommentLike" ("TenantId", "CommentId", "IsDeleted");
