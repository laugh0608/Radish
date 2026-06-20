-- Inventory and benefit grant reliability guards.
-- Apply before deploying code that makes order benefit grants idempotent and inventory deduction conditional.
-- Local SQLite environments should use Radish.DbMigrate apply / doctor / verify.

UPDATE "ShopUserInventory"
SET "ItemValue" = ''
WHERE "ItemValue" IS NULL;

ALTER TABLE "ShopUserInventory"
    ALTER COLUMN "ItemValue" SET DEFAULT '';

ALTER TABLE "ShopUserInventory"
    ALTER COLUMN "ItemValue" SET NOT NULL;

WITH ranked_benefits AS (
    SELECT
        benefit."Id",
        ROW_NUMBER() OVER (
            PARTITION BY benefit."TenantId", benefit."SourceOrderId"
            ORDER BY
                CASE
                    WHEN order_snapshot."UserBenefitId" = benefit."Id" THEN 0
                    ELSE 1
                END,
                benefit."IsDeleted" ASC,
                benefit."CreateTime" ASC,
                benefit."Id" ASC
        ) AS row_no
    FROM "ShopUserBenefit" AS benefit
    LEFT JOIN "ShopOrder" AS order_snapshot
        ON order_snapshot."Id" = benefit."SourceOrderId"
    WHERE benefit."SourceOrderId" IS NOT NULL
)
DELETE FROM "ShopUserBenefit" AS target
USING ranked_benefits AS ranked
WHERE target."Id" = ranked."Id"
  AND ranked.row_no > 1;

WITH inventory_groups AS (
    SELECT
        "TenantId",
        "UserId",
        "ConsumableType",
        COALESCE("ItemValue", '') AS normalized_item_value,
        SUM(GREATEST("Quantity", 0))::INTEGER AS total_quantity
    FROM "ShopUserInventory"
    GROUP BY "TenantId", "UserId", "ConsumableType", COALESCE("ItemValue", '')
),
ranked_inventory AS (
    SELECT
        inventory."Id",
        inventory_groups.normalized_item_value,
        inventory_groups.total_quantity,
        ROW_NUMBER() OVER (
            PARTITION BY inventory."TenantId", inventory."UserId", inventory."ConsumableType", COALESCE(inventory."ItemValue", '')
            ORDER BY
                inventory."IsDeleted" ASC,
                inventory."Quantity" DESC,
                COALESCE(inventory."ModifyTime", inventory."CreateTime") DESC,
                inventory."Id" DESC
        ) AS row_no
    FROM "ShopUserInventory" AS inventory
    INNER JOIN inventory_groups
        ON inventory_groups."TenantId" = inventory."TenantId"
       AND inventory_groups."UserId" = inventory."UserId"
       AND inventory_groups."ConsumableType" = inventory."ConsumableType"
       AND inventory_groups.normalized_item_value = COALESCE(inventory."ItemValue", '')
)
UPDATE "ShopUserInventory" AS target
SET
    "ItemValue" = ranked.normalized_item_value,
    "Quantity" = ranked.total_quantity,
    "IsDeleted" = FALSE,
    "ModifyTime" = NOW()
FROM ranked_inventory AS ranked
WHERE target."Id" = ranked."Id"
  AND ranked.row_no = 1
  AND (
      target."ItemValue" IS DISTINCT FROM ranked.normalized_item_value
      OR target."Quantity" IS DISTINCT FROM ranked.total_quantity
      OR target."IsDeleted" IS DISTINCT FROM FALSE
  );

WITH ranked_inventory AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY "TenantId", "UserId", "ConsumableType", COALESCE("ItemValue", '')
            ORDER BY
                "IsDeleted" ASC,
                "Quantity" DESC,
                COALESCE("ModifyTime", "CreateTime") DESC,
                "Id" DESC
        ) AS row_no
    FROM "ShopUserInventory"
)
DELETE FROM "ShopUserInventory" AS target
USING ranked_inventory AS ranked
WHERE target."Id" = ranked."Id"
  AND ranked.row_no > 1;

CREATE TABLE IF NOT EXISTS "ShopUserInventoryGrantRecord" (
    "Id" BIGINT NOT NULL PRIMARY KEY,
    "UserId" BIGINT NOT NULL,
    "InventoryId" BIGINT NOT NULL,
    "SourceOrderId" BIGINT NOT NULL,
    "SourceProductId" BIGINT NOT NULL,
    "ConsumableType" INTEGER NOT NULL,
    "ItemValue" VARCHAR(500) NOT NULL DEFAULT '',
    "Quantity" INTEGER NOT NULL,
    "TenantId" BIGINT NOT NULL DEFAULT 0,
    "CreateTime" TIMESTAMP NOT NULL,
    "CreateBy" VARCHAR(50) NOT NULL DEFAULT 'System',
    "CreateId" BIGINT NOT NULL DEFAULT 0,
    "ModifyTime" TIMESTAMP NULL
);

INSERT INTO "ShopUserInventoryGrantRecord" (
    "Id",
    "TenantId",
    "UserId",
    "InventoryId",
    "SourceOrderId",
    "SourceProductId",
    "ConsumableType",
    "ItemValue",
    "Quantity",
    "CreateTime",
    "CreateBy",
    "CreateId"
)
SELECT
    order_snapshot."Id",
    order_snapshot."TenantId",
    order_snapshot."UserId",
    order_snapshot."UserBenefitId",
    order_snapshot."Id",
    order_snapshot."ProductId",
    order_snapshot."ConsumableType",
    COALESCE(order_snapshot."BenefitValue", ''),
    GREATEST(order_snapshot."Quantity", 1),
    COALESCE(order_snapshot."CompletedTime", order_snapshot."CreateTime"),
    'System',
    order_snapshot."UserId"
FROM "ShopOrder" AS order_snapshot
WHERE order_snapshot."ProductType" = 2
  AND order_snapshot."Status" = 2
  AND order_snapshot."UserBenefitId" IS NOT NULL
  AND order_snapshot."ConsumableType" IS NOT NULL
  AND EXISTS (
      SELECT 1
      FROM "ShopUserInventory" AS inventory
      WHERE inventory."Id" = order_snapshot."UserBenefitId"
  )
  AND NOT EXISTS (
      SELECT 1
      FROM "ShopUserInventoryGrantRecord" AS grant_record
      WHERE grant_record."SourceOrderId" = order_snapshot."Id"
  );

CREATE UNIQUE INDEX IF NOT EXISTS "idx_benefit_tenant_source_order"
    ON "ShopUserBenefit" ("TenantId", "SourceOrderId");

CREATE UNIQUE INDEX IF NOT EXISTS "idx_inventory_tenant_user_type_value"
    ON "ShopUserInventory" ("TenantId", "UserId", "ConsumableType", "ItemValue");

CREATE UNIQUE INDEX IF NOT EXISTS "idx_inventory_grant_tenant_source_order"
    ON "ShopUserInventoryGrantRecord" ("TenantId", "SourceOrderId");

CREATE INDEX IF NOT EXISTS "idx_inventory_grant_inventory"
    ON "ShopUserInventoryGrantRecord" ("TenantId", "InventoryId");
