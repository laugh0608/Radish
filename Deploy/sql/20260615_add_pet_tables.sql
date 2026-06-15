-- Electronic pet Phase B first schema.
-- Apply before deploying code that exposes /api/v1/Pet/*.

CREATE TABLE IF NOT EXISTS "PetProfile" (
    "Id" BIGINT NOT NULL PRIMARY KEY,
    "PublicId" VARCHAR(36) NOT NULL,
    "UserId" BIGINT NOT NULL,
    "Name" VARCHAR(40) NOT NULL DEFAULT '小萝卜',
    "SpeciesKey" VARCHAR(40) NOT NULL DEFAULT 'radish',
    "ShapeKey" VARCHAR(40) NOT NULL DEFAULT 'sprout',
    "GrowthStage" INTEGER NOT NULL DEFAULT 1,
    "Mood" VARCHAR(20) NOT NULL DEFAULT 'calm',
    "Satiety" INTEGER NOT NULL DEFAULT 70,
    "Cleanliness" INTEGER NOT NULL DEFAULT 70,
    "Energy" INTEGER NOT NULL DEFAULT 70,
    "GrowthValue" BIGINT NOT NULL DEFAULT 0,
    "EquippedBackgroundKey" VARCHAR(60) NULL,
    "EquippedToyKey" VARCHAR(60) NULL,
    "IsPublic" BOOLEAN NOT NULL DEFAULT FALSE,
    "LastCareTime" TIMESTAMP NULL,
    "TenantId" BIGINT NOT NULL DEFAULT 0,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP NULL,
    "DeletedBy" VARCHAR(50) NULL,
    "CreateTime" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreateBy" VARCHAR(50) NOT NULL DEFAULT 'System',
    "CreateId" BIGINT NOT NULL DEFAULT 0,
    "ModifyTime" TIMESTAMP NULL,
    "ModifyBy" VARCHAR(50) NULL,
    "ModifyId" BIGINT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "idx_petprofile_public_id"
    ON "PetProfile" ("PublicId");

CREATE UNIQUE INDEX IF NOT EXISTS "idx_petprofile_user"
    ON "PetProfile" ("UserId");

CREATE TABLE IF NOT EXISTS "PetStatLog" (
    "Id" BIGINT NOT NULL PRIMARY KEY,
    "UserId" BIGINT NOT NULL,
    "PetProfileId" BIGINT NOT NULL,
    "PetPublicId" VARCHAR(36) NOT NULL,
    "ActionType" VARCHAR(20) NOT NULL,
    "Source" VARCHAR(40) NOT NULL DEFAULT 'care',
    "IdempotencyKey" VARCHAR(80) NOT NULL,
    "BeforeSatiety" INTEGER NOT NULL,
    "AfterSatiety" INTEGER NOT NULL,
    "BeforeCleanliness" INTEGER NOT NULL,
    "AfterCleanliness" INTEGER NOT NULL,
    "BeforeEnergy" INTEGER NOT NULL,
    "AfterEnergy" INTEGER NOT NULL,
    "GrowthDelta" BIGINT NOT NULL,
    "Message" VARCHAR(200) NOT NULL,
    "TenantId" BIGINT NOT NULL DEFAULT 0,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT FALSE,
    "DeletedAt" TIMESTAMP NULL,
    "DeletedBy" VARCHAR(50) NULL,
    "CreateTime" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "CreateBy" VARCHAR(50) NOT NULL DEFAULT 'System',
    "CreateId" BIGINT NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS "idx_petstatlog_user_time"
    ON "PetStatLog" ("UserId", "CreateTime" DESC);

CREATE INDEX IF NOT EXISTS "idx_petstatlog_pet_time"
    ON "PetStatLog" ("PetProfileId", "CreateTime" DESC);

CREATE UNIQUE INDEX IF NOT EXISTS "idx_petstatlog_idempotency"
    ON "PetStatLog" ("IdempotencyKey");
