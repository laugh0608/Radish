using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;

namespace Radish.DbMigrate;

internal static partial class InitialDataSeeder
{
    /// <summary>初始化商城分类数据</summary>
    private static async Task SeedShopCategoriesAsync(ISqlSugarClient db)
    {
        var categories = new[]
        {
            new ProductCategory
            {
                Id = "badge",
                Name = "徽章收藏",
                Icon = "badge",
                Description = "专属徽章，展示你的独特身份",
                SortOrder = 0,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "frame",
                Name = "头像框",
                Icon = "frame",
                Description = "精美头像框，装点个人形象",
                SortOrder = 1,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "title",
                Name = "称号",
                Icon = "title",
                Description = "独特称号，彰显个性",
                SortOrder = 2,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "signature",
                Name = "签名档",
                Icon = "signature",
                Description = "个性签名，留下你的专属印记",
                SortOrder = 3,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "effect",
                Name = "特效装扮",
                Icon = "sparkles",
                Description = "点赞特效、用户名特效等",
                SortOrder = 4,
                IsEnabled = true,
                CreateTime = DateTime.Now
            },
            new ProductCategory
            {
                Id = "theme",
                Name = "主题皮肤",
                Icon = "palette",
                Description = "个性化界面主题",
                SortOrder = 5,
                IsEnabled = true,
                CreateTime = DateTime.Now
            }
        };

        foreach (var category in categories)
        {
            var exists = await db.Queryable<ProductCategory>().AnyAsync(c => c.Id == category.Id);
            if (!exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 创建商品分类 Id={category.Id}, Name={category.Name}...");
                await db.Insertable(category).ExecuteCommandAsync();
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={category.Id} 的商品分类，跳过创建。");
            }
        }
    }

    /// <summary>初始化商城商品数据</summary>
    private static async Task SeedShopProductsAsync(ISqlSugarClient db)
    {
        // 商品 ID 从 100000 开始
        var products = new[]
        {
            // ========== 徽章类 ==========
            new Product
            {
                Id = 100001,
                Name = "元老徽章",
                Description = "社区元老专属徽章，见证社区成长",
                Icon = "badge-veteran",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-veteran",
                Price = 500,
                StockType = StockType.Limited,
                Stock = 100,
                LimitPerUser = 1,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100002,
                Name = "萝卜爱好者",
                Description = "热爱萝卜的小伙伴专属徽章",
                Icon = "badge-carrot-lover",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-carrot-lover",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100003,
                Name = "夜猫子",
                Description = "深夜活跃用户专属徽章",
                Icon = "badge-night-owl",
                CategoryId = "badge",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Badge,
                BenefitValue = "badge-night-owl",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 头像框类 ==========
            new Product
            {
                Id = 100011,
                Name = "星光头像框",
                Description = "闪烁的星光环绕，璀璨夺目",
                Icon = "frame-star",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-star",
                Price = 300,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100012,
                Name = "萝卜花环",
                Description = "可爱的萝卜花环头像框",
                Icon = "frame-carrot-wreath",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-carrot-wreath",
                Price = 250,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100013,
                Name = "极简线条框",
                Description = "简约而不简单的线条设计",
                Icon = "frame-minimal",
                CategoryId = "frame",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.AvatarFrame,
                BenefitValue = "frame-minimal",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 称号类 ==========
            new Product
            {
                Id = 100021,
                Name = "萝卜达人",
                Description = "专属称号「萝卜达人」，显示在用户名旁",
                Icon = "title-expert",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "萝卜达人",
                Price = 200,
                StockType = StockType.Limited,
                Stock = 50,
                LimitPerUser = 1,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100022,
                Name = "用爱发电",
                Description = "专属称号「用爱发电」，致敬每一位贡献者",
                Icon = "title-love-power",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "用爱发电",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100023,
                Name = "快乐水源",
                Description = "专属称号「快乐水源」，传递快乐的人",
                Icon = "title-joy",
                CategoryId = "title",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Title,
                BenefitValue = "快乐水源",
                Price = 150,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 签名档类 ==========
            new Product
            {
                Id = 100031,
                Name = "来自萝卜星球",
                Description = "评论签名「来自萝卜星球」",
                Icon = "signature-planet",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自萝卜星球 🥕",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100032,
                Name = "深夜食堂",
                Description = "评论签名「来自深夜食堂」",
                Icon = "signature-midnight",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自深夜食堂 🌙",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100033,
                Name = "代码世界",
                Description = "评论签名「来自代码世界」",
                Icon = "signature-code",
                CategoryId = "signature",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Signature,
                BenefitValue = "来自代码世界 💻",
                Price = 100,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 特效类 ==========
            new Product
            {
                Id = 100041,
                Name = "彩虹用户名",
                Description = "用户名显示为彩虹渐变色",
                Icon = "effect-rainbow",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.NameColor,
                BenefitValue = "rainbow",
                Price = 400,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100042,
                Name = "金色用户名",
                Description = "用户名显示为尊贵金色",
                Icon = "effect-gold",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.NameColor,
                BenefitValue = "gold",
                Price = 300,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100043,
                Name = "爱心点赞特效",
                Description = "点赞时显示爱心飘散动画",
                Icon = "effect-heart-like",
                CategoryId = "effect",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.LikeEffect,
                BenefitValue = "heart-burst",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Days,
                DurationDays = 30,
                SortOrder = 2,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 主题类 ==========
            new Product
            {
                Id = 100051,
                Name = "暗夜主题",
                Description = "深邃的暗夜配色主题",
                Icon = "theme-dark",
                CategoryId = "theme",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Theme,
                BenefitValue = "theme-dark-night",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 0,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },
            new Product
            {
                Id = 100052,
                Name = "樱花主题",
                Description = "浪漫的樱花粉配色主题",
                Icon = "theme-sakura",
                CategoryId = "theme",
                ProductType = ProductType.Benefit,
                BenefitType = BenefitType.Theme,
                BenefitValue = "theme-sakura",
                Price = 200,
                StockType = StockType.Unlimited,
                DurationType = DurationType.Permanent,
                SortOrder = 1,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            },

            // ========== 实用类 ==========
            new Product
            {
                Id = 100061,
                Name = "改名卡",
                Description = "使用后可修改一次用户名",
                Icon = "card-rename",
                CategoryId = "effect",
                ProductType = ProductType.Consumable,
                ConsumableType = ConsumableType.RenameCard,
                Price = 100,
                StockType = StockType.Unlimited,
                SortOrder = 10,
                IsOnSale = true,
                IsEnabled = true,
                CreateTime = DateTime.Now,
                CreateBy = "System"
            }
        };

        foreach (var product in products)
        {
            var exists = await db.Queryable<Product>().AnyAsync(p => p.Id == product.Id);
            if (!exists)
            {
                Console.WriteLine($"[Radish.DbMigrate] 创建商品 Id={product.Id}, Name={product.Name}...");
                try
                {
                    await db.Insertable(product).ExecuteCommandAsync();
                }
                catch (Exception ex) when (IsUniqueConstraintViolation(ex, "ShopProduct.Id") || IsUniqueConstraintViolation(ex, "Product.Id"))
                {
                    product.ModifyTime = DateTime.Now;
                    product.ModifyBy = "System";

                    var updated = await db.Updateable(product)
                        .IgnoreColumns(p => new { p.CreateTime, p.CreateBy, p.CreateId })
                        .ExecuteCommandAsync();

                    if (updated <= 0)
                    {
                        throw;
                    }

                    Console.WriteLine($"[Radish.DbMigrate] 检测到商品 Id={product.Id} 的旧记录，已自动纠正商品配置。");
                }
            }
            else
            {
                Console.WriteLine($"[Radish.DbMigrate] 已存在 Id={product.Id} 的商品，跳过创建。");
            }
        }
    }
}
