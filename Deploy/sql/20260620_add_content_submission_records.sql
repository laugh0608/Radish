-- Forum content submission records for post/comment/answer retry protection.
-- Apply before deploying code that accepts clientSubmissionId on forum content create operations.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

CREATE TABLE IF NOT EXISTS "ContentSubmissionRecord" (
    "Id" BIGINT NOT NULL PRIMARY KEY,
    "TenantId" BIGINT NOT NULL DEFAULT 0,
    "UserId" BIGINT NOT NULL,
    "OperationType" VARCHAR(40) NOT NULL,
    "ClientSubmissionId" VARCHAR(80) NOT NULL,
    "TargetType" VARCHAR(40) NULL,
    "TargetId" BIGINT NULL,
    "RequestDigest" VARCHAR(64) NOT NULL,
    "RequestSummary" VARCHAR(1000) NOT NULL,
    "ContentFingerprint" VARCHAR(64) NOT NULL,
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Pending',
    "ResultType" VARCHAR(40) NULL,
    "ResultId" BIGINT NULL,
    "ResultPublicId" VARCHAR(64) NULL,
    "ErrorCode" VARCHAR(64) NULL,
    "ErrorMessage" VARCHAR(500) NULL,
    "ExpiresAt" TIMESTAMP NOT NULL,
    "CreateTime" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreateBy" VARCHAR(50) NOT NULL DEFAULT 'System',
    "CreateId" BIGINT NOT NULL DEFAULT 0,
    "CompleteTime" TIMESTAMP NULL,
    "ModifyTime" TIMESTAMP NULL,
    "ModifyBy" VARCHAR(50) NULL,
    "ModifyId" BIGINT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "idx_content_submission_client"
    ON "ContentSubmissionRecord" ("TenantId", "UserId", "OperationType", "ClientSubmissionId");

CREATE INDEX IF NOT EXISTS "idx_content_submission_fingerprint"
    ON "ContentSubmissionRecord" ("TenantId", "UserId", "OperationType", "ContentFingerprint", "CreateTime" DESC);

CREATE INDEX IF NOT EXISTS "idx_content_submission_expires"
    ON "ContentSubmissionRecord" ("ExpiresAt");

CREATE INDEX IF NOT EXISTS "idx_content_submission_result"
    ON "ContentSubmissionRecord" ("ResultType", "ResultId");
