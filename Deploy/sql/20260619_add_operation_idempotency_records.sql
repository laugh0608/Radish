-- Operation idempotency records for shop purchase and coin transfer.
-- Apply before deploying code that accepts idempotencyKey on asset write operations.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

CREATE TABLE IF NOT EXISTS "OperationIdempotencyRecord" (
    "Id" BIGINT NOT NULL PRIMARY KEY,
    "TenantId" BIGINT NOT NULL DEFAULT 0,
    "UserId" BIGINT NOT NULL,
    "OperationType" VARCHAR(40) NOT NULL,
    "IdempotencyKey" VARCHAR(80) NOT NULL,
    "RequestHash" VARCHAR(64) NOT NULL,
    "RequestSummary" VARCHAR(1000) NOT NULL,
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Processing',
    "ResourceType" VARCHAR(40) NULL,
    "ResourceId" BIGINT NULL,
    "ResourceNo" VARCHAR(64) NULL,
    "ResponsePayload" VARCHAR(4000) NULL,
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

CREATE UNIQUE INDEX IF NOT EXISTS "idx_operation_idempotency_unique"
    ON "OperationIdempotencyRecord" ("TenantId", "UserId", "OperationType", "IdempotencyKey");

CREATE INDEX IF NOT EXISTS "idx_operation_idempotency_user_operation_time"
    ON "OperationIdempotencyRecord" ("UserId", "OperationType", "CreateTime" DESC);

CREATE INDEX IF NOT EXISTS "idx_operation_idempotency_expires"
    ON "OperationIdempotencyRecord" ("ExpiresAt");
