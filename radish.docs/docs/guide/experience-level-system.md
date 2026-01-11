# 经验值与等级系统设计方案

## 1. 系统概述

经验值系统是 Radish 社区的用户成长激励体系,通过社区互动积累经验值,提升用户等级,并获得对应的身份标识和特权。系统设计遵循"易上手、难精通"的成长曲线,让新用户快速获得成就感,同时为活跃用户提供长期目标。

**核心设计原则**:
- **公平性**:经验值获取透明,规则明确,防刷机制健全
- **成长性**:等级曲线合理,既有短期激励,也有长期目标
- **可扩展性**:等级配置化,支持动态调整和扩展
- **文化认同**:修仙体系等级昵称,增强社区文化氛围

---

## 2. 核心概念

### 2.1 等级体系(修仙主题)

| 等级 | 昵称 | 所需累计经验值 | 升级所需经验值 | 主题色 | 说明 |
|------|------|---------------|--------------|--------|------|
| **Lv.0** | 凡人 | 0 | 100 | #9E9E9E | 新用户初始等级 |
| **Lv.1** | 练气 | 100 | 200 | #4CAF50 | 开始修炼之路 |
| **Lv.2** | 筑基 | 300 | 500 | #2196F3 | 打下坚实基础 |
| **Lv.3** | 金丹 | 800 | 1000 | #FFC107 | 凝聚金丹,小有所成 |
| **Lv.4** | 元婴 | 1800 | 2000 | #FF9800 | 修成元婴,中流砥柱 |
| **Lv.5** | 化神 | 3800 | 4000 | #FF5722 | 化神境界,神通初显 |
| **Lv.6** | 炼虚 | 7800 | 8000 | #9C27B0 | 炼虚合道,高手境界 |
| **Lv.7** | 合体 | 15800 | 16000 | #673AB7 | 合体大能,社区精英 |
| **Lv.8** | 大乘 | 31800 | 32000 | #3F51B5 | 大乘期修士,德高望重 |
| **Lv.9** | 渡劫 | 63800 | 64000 | #E91E63 | 渡劫飞升,传说人物 |
| **Lv.10** | 飞升 | 127800 | - | #FFD700 | 羽化飞升,至高荣耀 |

**升级曲线规律**:
- **指数增长**:每级所需经验值约为上一级的 2 倍
- **累计经验值**:`sum = 100 × (2^level - 1)`
- **从 Lv.n 到 Lv.(n+1)**:`exp_required = 100 × 2^n`

**示例计算**:
- Lv.0 → Lv.1:需要 100 经验值
- Lv.1 → Lv.2:需要 200 经验值(累计 300)
- Lv.2 → Lv.3:需要 500 经验值(累计 800)
- Lv.9 → Lv.10:需要 64000 经验值(累计 127800)

### 2.2 等级配置化设计

**配置表结构**(`LevelConfig` 表):
```sql
CREATE TABLE level_config (
    level INT PRIMARY KEY,                        -- 等级(0-10)
    level_name VARCHAR(50) NOT NULL,              -- 等级昵称(凡人/练气/筑基...)
    exp_required BIGINT NOT NULL,                 -- 升到下一级所需经验值
    exp_cumulative BIGINT NOT NULL,               -- 累计经验值(达到此等级的总经验)
    theme_color VARCHAR(20),                      -- 主题色(十六进制)
    icon_url VARCHAR(500),                        -- 等级图标URL
    badge_url VARCHAR(500),                       -- 等级徽章URL
    description VARCHAR(500),                     -- 等级描述
    privileges TEXT,                              -- 特权列表(JSON格式)
    is_enabled BOOLEAN DEFAULT TRUE,              -- 是否启用
    sort_order INT NOT NULL,                      -- 排序
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    INDEX idx_level (level)
);
```

**动态扩展示例**:
```json
// 未来可扩展到 Lv.11-15
{
  "level": 11,
  "level_name": "仙人",
  "exp_required": 128000,
  "exp_cumulative": 255800,
  "theme_color": "#FF1493",
  "privileges": ["自定义主题", "专属勋章", "社区决策权"]
}
```

### 2.3 经验值计算公式配置化

**设计理念**:
- **灵活调整**:通过配置文件动态调整经验值曲线,无需修改代码
- **公式多样**:支持多种数学模型(指数/多项式/混合/分段)
- **热更新**:管理员可通过 API 重新计算等级配置
- **可追溯**:配置变更记录操作日志

#### 2.3.1 支持的计算公式

**1. 混合公式（Hybrid，默认推荐）**
```
ExpRequired(level) = BaseExp × (level + 1)^Exponent × ScaleFactor
```

**特点**:
- 灵活性高,易于调优
- 平滑增长曲线
- 适合中长期运营

**参数**:
- `BaseExp`: 基础经验值（建议 50-200）
- `Exponent`: 指数幂次（建议 1.5-2.5，控制曲线陡度）
- `ScaleFactor`: 缩放因子（1.0 = 正常难度，>1 增加难度，<1 降低难度）

**示例**:
```json
{
  "FormulaType": "Hybrid",
  "BaseExp": 100.0,
  "Exponent": 2.0,
  "ScaleFactor": 1.0
}
```

**经验值预览**（默认配置）:
- Lv.0 → Lv.1: 100 经验（100 × 1² × 1.0）
- Lv.1 → Lv.2: 400 经验（100 × 2² × 1.0）
- Lv.2 → Lv.3: 900 经验（100 × 3² × 1.0）
- Lv.9 → Lv.10: 10000 经验（100 × 10² × 1.0）

---

**2. 指数增长公式（Exponential）**
```
ExpRequired(level) = BaseExp × Multiplier^level × ScaleFactor
```

**特点**:
- 经典游戏公式
- 增长速度快
- 后期等级难度陡增

**参数**:
- `BaseExp`: 基础经验值
- `Multiplier`: 增长倍数（建议 1.5-2.5）
- `ScaleFactor`: 缩放因子

**示例**:
```json
{
  "FormulaType": "Exponential",
  "BaseExp": 100.0,
  "Multiplier": 2.0,
  "ScaleFactor": 1.0
}
```

**经验值预览**:
- Lv.0 → Lv.1: 100（100 × 2⁰）
- Lv.1 → Lv.2: 200（100 × 2¹）
- Lv.2 → Lv.3: 400（100 × 2²）
- Lv.9 → Lv.10: 51200（100 × 2⁹）

---

**3. 多项式公式（Polynomial）**
```
ExpRequired(level) = A×level³ + B×level² + C×level + D
```

**特点**:
- 精细控制曲线形状
- 可调整前期/中期/后期难度比例
- 数学模型灵活

**参数**:
- `PolynomialA`: 三次项系数
- `PolynomialB`: 二次项系数
- `PolynomialC`: 一次项系数
- `PolynomialD`: 常数项

**示例**:
```json
{
  "FormulaType": "Polynomial",
  "PolynomialA": 10.0,
  "PolynomialB": 50.0,
  "PolynomialC": 100.0,
  "PolynomialD": 0.0,
  "ScaleFactor": 1.0
}
```

---

**4. 分段公式（Segmented）**
```
不同等级区间使用不同的指数:
level ≤ Threshold1: BaseExp × (level+1)^Exponent1
level ≤ Threshold2: BaseExp × (level+1)^Exponent2
level > Threshold2: BaseExp × (level+1)^Exponent3
```

**特点**:
- 符合修仙阶段特点
- 每个境界难度可独立调整
- 阶段性成长曲线

**参数**:
- `SegmentThreshold1`: 低级到中级分界线（默认 3）
- `SegmentThreshold2`: 中级到高级分界线（默认 7）
- `SegmentExponent1/2/3`: 各阶段的指数

**示例**:
```json
{
  "FormulaType": "Segmented",
  "BaseExp": 100.0,
  "SegmentThreshold1": 3,
  "SegmentThreshold2": 7,
  "SegmentExponent1": 1.0,
  "SegmentExponent2": 1.5,
  "SegmentExponent3": 2.0,
  "ScaleFactor": 1.0
}
```

**经验值预览**（3个阶段难度递增）:
- Lv.0-3: 平缓增长（凡人→金丹）
- Lv.4-7: 中速增长（元婴→合体）
- Lv.8-10: 高速增长（大乘→飞升）

#### 2.3.2 配置文件示例

**位置**: `Radish.Api/appsettings.json`

```json
{
  "ExperienceCalculator": {
    // 计算公式类型: Exponential / Polynomial / Hybrid / Segmented
    "FormulaType": "Hybrid",

    // === 基础参数（所有公式通用） ===
    "BaseExp": 100.0,              // 基础经验值
    "ScaleFactor": 1.0,            // 缩放因子（调整整体难度）
    "MinExpRequired": 10,          // 最小经验值要求
    "MaxLevel": 10,                // 最大等级

    // === 缓存配置 ===
    "EnableCache": true,           // 是否启用缓存
    "CacheExpirationMinutes": 60,  // 缓存过期时间

    // === Hybrid/Exponential 公式参数 ===
    "Exponent": 2.0,               // 指数幂次（控制曲线陡度）
    "Multiplier": 2.0,             // 指数增长倍数

    // === Polynomial 公式参数 ===
    "PolynomialA": 10.0,
    "PolynomialB": 50.0,
    "PolynomialC": 100.0,
    "PolynomialD": 0.0,

    // === Segmented 公式参数 ===
    "SegmentThreshold1": 3,        // 低级到中级分界线
    "SegmentThreshold2": 7,        // 中级到高级分界线
    "SegmentExponent1": 1.0,       // 低级段指数
    "SegmentExponent2": 1.5,       // 中级段指数
    "SegmentExponent3": 2.0        // 高级段指数
  }
}
```

#### 2.3.3 推荐配置方案

**1. 新社区快速成长模式**（推荐用于早期运营）
```json
{
  "FormulaType": "Hybrid",
  "BaseExp": 50.0,
  "Exponent": 1.8,
  "ScaleFactor": 1.0
}
```
**特点**: 升级较快,用户容易获得成就感

---

**2. 长期运营平衡模式**（默认配置）
```json
{
  "FormulaType": "Hybrid",
  "BaseExp": 100.0,
  "Exponent": 2.0,
  "ScaleFactor": 1.0
}
```
**特点**: 中等难度,平衡短期激励和长期目标

---

**3. 硬核修仙模式**（推荐用于成熟社区）
```json
{
  "FormulaType": "Exponential",
  "BaseExp": 100.0,
  "Multiplier": 2.2,
  "ScaleFactor": 1.0
}
```
**特点**: 后期难度高,顶级玩家稀缺性强

---

**4. 修仙阶段递进模式**（主题化推荐）
```json
{
  "FormulaType": "Segmented",
  "BaseExp": 100.0,
  "SegmentThreshold1": 3,
  "SegmentThreshold2": 7,
  "SegmentExponent1": 1.5,
  "SegmentExponent2": 2.0,
  "SegmentExponent3": 2.5,
  "ScaleFactor": 1.0
}
```
**特点**: 符合修仙境界突破感,每个大境界难度递增

#### 2.3.4 动态调整流程

**1. 修改配置文件**
```bash
# 编辑 Radish.Api/appsettings.json
vim Radish.Api/appsettings.json
```

**2. 调用管理员 API 重新计算**
```http
POST /api/v1/Experience/RecalculateLevelConfigs
Authorization: Bearer {admin_token}

Response:
{
  "isSuccess": true,
  "messageInfo": "成功重新计算 11 个等级配置",
  "responseData": [
    {
      "level": 0,
      "levelName": "凡人",
      "expRequired": 100,
      "expCumulative": 0,
      "themeColor": "#9E9E9E"
    },
    // ... 其他等级
  ]
}
```

**3. 系统自动更新**
- 更新数据库中的 `LevelConfig` 表
- 清除计算器缓存
- 记录操作日志

**注意事项**:
- ⚠️ **现有用户经验值不会变动**,只影响未来的升级计算
- ⚠️ **建议在低峰期操作**,避免影响用户体验
- ⚠️ **提前通知用户**,说明等级曲线调整

#### 2.3.5 技术实现

**计算器接口**:
```csharp
public interface IExperienceCalculator
{
    /// <summary>计算指定等级升到下一级所需的经验值</summary>
    long CalculateExpRequired(int level);

    /// <summary>计算达到指定等级所需的累计经验值</summary>
    long CalculateExpCumulative(int level);

    /// <summary>批量计算所有等级的经验值配置</summary>
    Dictionary<int, (long ExpRequired, long ExpCumulative)> CalculateAllLevels();

    /// <summary>获取当前使用的公式类型</summary>
    string GetFormulaType();

    /// <summary>获取当前配置的摘要信息</summary>
    string GetConfigSummary();
}
```

**数据库初始化**（使用动态计算）:
```csharp
// Radish.DbMigrate/InitialDataSeeder.cs
private static async Task SeedLevelConfigsAsync(ISqlSugarClient db, IServiceProvider services)
{
    var calculator = services.GetRequiredService<IExperienceCalculator>();
    var levelExpData = calculator.CalculateAllLevels();

    Console.WriteLine($"使用 {calculator.GetFormulaType()} 公式计算经验值");
    Console.WriteLine($"配置摘要: {calculator.GetConfigSummary()}");

    foreach (var (level, (expRequired, expCumulative)) in levelExpData)
    {
        // 创建等级配置...
    }
}
```

**管理员 API**:
```csharp
[HttpPost]
[Authorize(Policy = "SystemOrAdmin")]
public async Task<MessageModel<List<LevelConfigVo>>> RecalculateLevelConfigs()
{
    var operatorId = GetCurrentUserId();
    var operatorName = GetCurrentUserName();

    var result = await _experienceService.RecalculateLevelConfigsAsync(
        operatorId,
        operatorName ?? "Admin"
    );

    return MessageModel<List<LevelConfigVo>>.Success(
        $"成功重新计算 {result.Count} 个等级配置",
        result
    );
}
```

### 2.4 经验值特性

- **存储单位**:整数(BIGINT),避免浮点计算误差
- **不可转让**:经验值仅个人所有,不可转账或交易
- **不可兑换**:经验值与萝卜币独立,互不兑换
- **可追溯**:每笔经验值变动记录完整日志
- **防刷机制**:同一行为有每日上限,异常检测自动封禁

---

## 3. 经验值获取机制

### 3.1 互动奖励规则

| 行为 | 获得方 | 奖励经验值 | 每日上限 | 说明 |
|------|--------|-----------|---------|------|
| **发布帖子** | 作者 | +10 经验值 | 50 经验值(5 篇) | 鼓励内容创作 |
| **发布评论** | 作者 | +2 经验值 | 20 经验值(10 条) | 促进讨论互动 |
| **被点赞(帖子)** | 作者 | +5 经验值 | 100 经验值(20 次) | 内容质量认可 |
| **被点赞(评论)** | 作者 | +3 经验值 | 60 经验值(20 次) | 优质评论奖励 |
| **给他人点赞** | 点赞者 | +1 经验值 | 10 经验值(10 次) | 鼓励良性互动 |
| **成为神评** | 评论者 | +50 经验值 | 200 经验值(4 次) | 神评成就奖励 |
| **成为沙发** | 评论者 | +20 经验值 | 80 经验值(4 次) | 沙发成就奖励 |
| **被回复(评论)** | 评论者 | +3 经验值 | 30 经验值(10 次) | 引发讨论奖励 |
| **首次登录(日)** | 用户 | +5 经验值 | 5 经验值(1 次) | 签到奖励 |
| **连续登录(周)** | 用户 | +20 经验值 | 20 经验值(1 次) | 连续 7 天登录 |

**经验值计算原则**:
- **高质量内容优先**:被点赞奖励 > 发布奖励
- **互动优先于创作**:评论被回复 > 单纯发评论
- **成就奖励丰厚**:神评/沙发一次性奖励高于日常互动
- **每日上限防刷**:避免用户通过大量低质量内容刷经验

### 3.2 防刷机制

#### 3.2.1 每日上限控制

**实现方式**:
```
缓存 Key: exp:daily:{userId}:{action_type}:{date}
Value: 今日该行为已获得的经验值
TTL: 次日凌晨
```

**示例**:
```csharp
// 检查每日上限
var todayExpKey = $"exp:daily:{userId}:post_like:{DateTime.Today:yyyyMMdd}";
var todayExp = await _cache.GetAsync<int>(todayExpKey);

if (todayExp >= 100) // 被点赞每日上限 100
{
    return; // 不再奖励
}

// 增加经验值并更新缓存
await GrantExperienceAsync(userId, 5, "被点赞");
await _cache.IncrementAsync(todayExpKey, 5, TimeSpan.FromHours(24));
```

#### 3.2.2 去重规则

**同一行为不重复奖励**:
- 同一用户对同一帖子的点赞,仅奖励作者一次(取消点赞不扣除)
- 同一用户对同一评论的点赞,仅奖励作者一次
- 同一评论成为神评/沙发,仅奖励一次(失去地位不扣除)

**实现方式**:
```sql
-- 在 exp_transaction 表中使用唯一键防止重复
UNIQUE INDEX idx_dedup (user_id, exp_type, business_type, business_id, created_date)
```

#### 3.2.3 异常检测

**检测规则**:
- 短时间内大量发帖(1 小时内超过 20 篇)
- 短时间内大量点赞(1 分钟内超过 50 次)
- 互刷行为检测(两个账号频繁互相点赞/评论)

**处理策略**:
- 第一次:警告并冻结经验值获取 24 小时
- 第二次:冻结经验值获取 7 天
- 第三次:永久禁止经验值获取,等级降至 Lv.0

### 3.3 初始经验值

- **新用户注册**:初始 0 经验值,等级 Lv.0
- **完善资料**:+10 经验值(头像、简介、兴趣标签)
- **首次发帖**:+20 经验值(鼓励新用户参与)
- **首次评论**:+10 经验值

---

## 4. 数据库设计

### 4.1 用户经验值表

```sql
CREATE TABLE user_experience (
    user_id BIGINT PRIMARY KEY,                   -- 用户ID
    current_level INT NOT NULL DEFAULT 0,         -- 当前等级(0-10)
    current_exp BIGINT NOT NULL DEFAULT 0,        -- 当前经验值
    total_exp BIGINT NOT NULL DEFAULT 0,          -- 累计总经验值(含已用于升级的)
    level_up_at TIMESTAMP,                        -- 最近升级时间
    exp_frozen BOOLEAN DEFAULT FALSE,             -- 经验值是否冻结(作弊惩罚)
    frozen_until TIMESTAMP,                       -- 冻结到期时间
    frozen_reason VARCHAR(500),                   -- 冻结原因
    version INT NOT NULL DEFAULT 0,               -- 乐观锁版本号
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL,
    INDEX idx_level (current_level),
    INDEX idx_total_exp (total_exp DESC)          -- 用于排行榜
);
```

### 4.2 经验值交易记录表

```sql
CREATE TABLE exp_transaction (
    id BIGINT PRIMARY KEY,                        -- 雪花ID
    user_id BIGINT NOT NULL,                      -- 用户ID
    exp_type VARCHAR(50) NOT NULL,                -- 经验值类型(见 4.3)
    exp_amount INT NOT NULL,                      -- 经验值变动量(正数=增加)
    business_type VARCHAR(50),                    -- 业务类型(Post/Comment/User)
    business_id BIGINT,                           -- 业务ID
    remark VARCHAR(500),                          -- 备注
    exp_before BIGINT NOT NULL,                   -- 变动前经验值
    exp_after BIGINT NOT NULL,                    -- 变动后经验值
    level_before INT NOT NULL,                    -- 变动前等级
    level_after INT NOT NULL,                     -- 变动后等级
    created_date DATE NOT NULL,                   -- 创建日期(用于去重)
    created_at TIMESTAMP NOT NULL,
    INDEX idx_user_time (user_id, created_at DESC),
    INDEX idx_type (exp_type),
    UNIQUE INDEX idx_dedup (user_id, exp_type, business_type, business_id, created_date)
);
```

### 4.3 经验值类型枚举

| 类型代码 | 说明 | business_type | business_id |
|---------|------|--------------|-------------|
| `POST_CREATE` | 发布帖子 | Post | 帖子ID |
| `POST_LIKED` | 帖子被点赞 | Post | 帖子ID |
| `COMMENT_CREATE` | 发布评论 | Comment | 评论ID |
| `COMMENT_LIKED` | 评论被点赞 | Comment | 评论ID |
| `COMMENT_REPLIED` | 评论被回复 | Comment | 评论ID |
| `LIKE_OTHERS` | 给他人点赞 | Post/Comment | 目标ID |
| `GOD_COMMENT` | 成为神评 | Comment | 评论ID |
| `SOFA_COMMENT` | 成为沙发 | Comment | 评论ID |
| `DAILY_LOGIN` | 每日登录 | User | 用户ID |
| `WEEKLY_LOGIN` | 连续登录(周) | User | 用户ID |
| `PROFILE_COMPLETE` | 完善资料 | User | 用户ID |
| `FIRST_POST` | 首次发帖 | Post | 帖子ID |
| `FIRST_COMMENT` | 首次评论 | Comment | 评论ID |
| `ADMIN_ADJUST` | 管理员调整 | - | - |
| `PENALTY` | 惩罚扣除 | - | - |

### 4.4 等级配置表

见 **2.2 等级配置化设计** 中的 `level_config` 表。

### 4.5 每日统计表

```sql
CREATE TABLE user_exp_daily_stats (
    id BIGINT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    stat_date DATE NOT NULL,                      -- 统计日期
    exp_earned BIGINT NOT NULL DEFAULT 0,         -- 当日获得经验值
    exp_from_post INT NOT NULL DEFAULT 0,         -- 来自发帖
    exp_from_comment INT NOT NULL DEFAULT 0,      -- 来自评论
    exp_from_like INT NOT NULL DEFAULT 0,         -- 来自点赞
    exp_from_highlight INT NOT NULL DEFAULT 0,    -- 来自神评/沙发
    exp_from_login INT NOT NULL DEFAULT 0,        -- 来自登录
    post_count INT NOT NULL DEFAULT 0,            -- 当日发帖数
    comment_count INT NOT NULL DEFAULT 0,         -- 当日评论数
    like_given_count INT NOT NULL DEFAULT 0,      -- 当日点赞数
    like_received_count INT NOT NULL DEFAULT 0,   -- 当日被点赞数
    created_at TIMESTAMP NOT NULL,
    UNIQUE INDEX idx_user_date (user_id, stat_date)
);
```

---

## 5. 业务逻辑设计

### 5.1 经验值计算与发放

#### 5.1.1 经验值发放流程

```csharp
public class ExperienceService : IExperienceService
{
    public async Task<bool> GrantExperienceAsync(
        long userId,
        int amount,
        string expType,
        string? businessType = null,
        long? businessId = null,
        string? remark = null)
    {
        // 1. 检查用户经验值是否冻结
        var userExp = await _userExpRepository.QueryByIdAsync(userId);
        if (userExp.ExpFrozen && userExp.FrozenUntil > DateTime.Now)
        {
            Log.Warning("用户 {UserId} 经验值已冻结,无法获得经验值", userId);
            return false;
        }

        // 2. 检查每日上限
        var todayExpKey = $"exp:daily:{userId}:{expType}:{DateTime.Today:yyyyMMdd}";
        var todayExp = await _cache.GetAsync<int>(todayExpKey);
        var dailyLimit = GetDailyLimit(expType);

        if (todayExp + amount > dailyLimit)
        {
            amount = Math.Max(0, dailyLimit - todayExp); // 只发放剩余额度
            if (amount == 0)
            {
                Log.Debug("用户 {UserId} 经验值类型 {ExpType} 已达每日上限", userId, expType);
                return false;
            }
        }

        // 3. 使用乐观锁更新经验值
        var currentExp = userExp.CurrentExp;
        var totalExp = userExp.TotalExp;
        var currentLevel = userExp.CurrentLevel;

        var newExp = currentExp + amount;
        var newTotalExp = totalExp + amount;

        // 4. 检查是否升级
        var (newLevel, remainingExp) = await CalculateLevelAsync(newExp);

        // 5. 更新数据库
        using var transaction = await _db.BeginTransactionAsync();
        try
        {
            // 更新用户经验值
            var updated = await _db.Updateable<UserExperience>()
                .SetColumns(u => new UserExperience
                {
                    CurrentExp = remainingExp,
                    TotalExp = newTotalExp,
                    CurrentLevel = newLevel,
                    LevelUpAt = newLevel > currentLevel ? DateTime.Now : u.LevelUpAt,
                    Version = u.Version + 1,
                    UpdateTime = DateTime.Now
                })
                .Where(u => u.UserId == userId && u.Version == userExp.Version)
                .ExecuteCommandAsync();

            if (updated == 0)
            {
                throw new DbUpdateConcurrencyException("乐观锁冲突");
            }

            // 记录交易日志
            await _expTransactionRepository.AddAsync(new ExpTransaction
            {
                UserId = userId,
                ExpType = expType,
                ExpAmount = amount,
                BusinessType = businessType,
                BusinessId = businessId,
                Remark = remark,
                ExpBefore = currentExp,
                ExpAfter = remainingExp,
                LevelBefore = currentLevel,
                LevelAfter = newLevel,
                CreatedDate = DateTime.Today
            });

            await transaction.CommitAsync();

            // 6. 更新缓存
            await _cache.IncrementAsync(todayExpKey, amount, TimeSpan.FromHours(24));

            // 7. 如果升级,触发升级事件
            if (newLevel > currentLevel)
            {
                await OnLevelUpAsync(userId, currentLevel, newLevel);
            }

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // 计算等级和剩余经验值
    private async Task<(int level, long remainingExp)> CalculateLevelAsync(long totalExp)
    {
        var levels = await _levelConfigRepository.QueryAsync(
            l => l.IsEnabled,
            orderBy: l => l.Level
        );

        for (int i = levels.Count - 1; i >= 0; i--)
        {
            if (totalExp >= levels[i].ExpCumulative)
            {
                var remainingExp = totalExp - levels[i].ExpCumulative;
                return (levels[i].Level, remainingExp);
            }
        }

        return (0, totalExp);
    }
}
```

#### 5.1.2 升级事件处理

```csharp
private async Task OnLevelUpAsync(long userId, int oldLevel, int newLevel)
{
    // 1. 发送通知
    await _notificationService.CreateNotificationAsync(new Notification
    {
        UserId = userId,
        Type = "LevelUp",
        Title = "恭喜升级!",
        Content = $"恭喜您从 {GetLevelName(oldLevel)} 升级到 {GetLevelName(newLevel)}!",
        BusinessType = "User",
        BusinessId = userId
    });

    // 2. 发放升级奖励(萝卜币)
    var coinReward = newLevel * 100; // 升级奖励萝卜币 = 等级 × 100
    await _coinService.GrantCoinAsync(userId, coinReward, "LEVEL_UP_REWARD",
        remark: $"升级到 Lv.{newLevel} 奖励");

    // 3. 记录升级日志
    Log.Information("用户 {UserId} 从 Lv.{OldLevel} 升级到 Lv.{NewLevel}",
        userId, oldLevel, newLevel);

    // 4. 触发成就检查(可选)
    await _achievementService.CheckLevelAchievementAsync(userId, newLevel);
}
```

### 5.2 与业务模块集成

#### 5.2.1 发帖奖励

```csharp
// 在 PostService.PublishPostAsync 中集成
public async Task<long> PublishPostAsync(Post post, List<string>? tagNames = null)
{
    // 现有发帖逻辑...
    var postId = await AddAsync(post);

    // 新增:发放经验值
    await _expService.GrantExperienceAsync(
        userId: post.UserId,
        amount: 10,
        expType: "POST_CREATE",
        businessType: "Post",
        businessId: postId,
        remark: "发布帖子"
    );

    // 检查是否首次发帖
    var isFirstPost = await IsFirstPostAsync(post.UserId);
    if (isFirstPost)
    {
        await _expService.GrantExperienceAsync(
            userId: post.UserId,
            amount: 20,
            expType: "FIRST_POST",
            businessType: "Post",
            businessId: postId,
            remark: "首次发帖奖励"
        );
    }

    return postId;
}
```

#### 5.2.2 点赞奖励

```csharp
// 在 PostService.ToggleLikeAsync 中集成
public async Task<PostLikeResultDto> ToggleLikeAsync(long userId, long postId)
{
    // 现有点赞逻辑...
    var result = await ToggleLike(userId, postId);

    if (result.IsLiked) // 新增点赞
    {
        // 奖励帖子作者
        await _expService.GrantExperienceAsync(
            userId: post.UserId,
            amount: 5,
            expType: "POST_LIKED",
            businessType: "Post",
            businessId: postId,
            remark: "帖子被点赞"
        );

        // 奖励点赞者
        await _expService.GrantExperienceAsync(
            userId: userId,
            amount: 1,
            expType: "LIKE_OTHERS",
            businessType: "Post",
            businessId: postId,
            remark: "点赞他人帖子"
        );
    }
    // 取消点赞不扣除经验值

    return result;
}
```

#### 5.2.3 神评/沙发奖励

```csharp
// 在 CommentHighlightJob.ExecuteAsync 中集成
public async Task ExecuteAsync()
{
    // 现有神评/沙发统计逻辑...
    var highlights = await CalculateHighlightsAsync();

    foreach (var highlight in highlights)
    {
        // 更新神评/沙发记录...
        await UpdateHighlightAsync(highlight);

        // 发放经验值
        var expAmount = highlight.HighlightType == "GodComment" ? 50 : 20;
        var expType = highlight.HighlightType == "GodComment"
            ? "GOD_COMMENT"
            : "SOFA_COMMENT";

        await _expService.GrantExperienceAsync(
            userId: highlight.UserId,
            amount: expAmount,
            expType: expType,
            businessType: "Comment",
            businessId: highlight.CommentId,
            remark: $"成为{(highlight.HighlightType == "GodComment" ? "神评" : "沙发")}"
        );
    }
}
```

---

## 6. API 设计

### 6.1 查询用户经验值信息

```http
GET /api/v1/Experience/GetUserExperience
Authorization: Bearer {token}
Query Parameters:
  - userId: long (可选,不传则查询当前用户)

Response:
{
  "isSuccess": true,
  "responseData": {
    "userId": 12345,
    "currentLevel": 3,
    "currentLevelName": "金丹",
    "currentExp": 450,
    "totalExp": 1250,
    "expToNextLevel": 550,         // 距离下一级还需多少经验
    "nextLevel": 4,
    "nextLevelName": "元婴",
    "levelProgress": 0.45,         // 当前等级进度(0-1)
    "themeColor": "#FFC107",
    "badgeUrl": "/assets/badges/level-3.png",
    "levelUpAt": "2025-12-20T10:30:00Z",
    "rank": 1234,                  // 经验值排名(可选)
    "expFrozen": false
  }
}
```

### 6.2 查询经验值记录

```http
GET /api/v1/Experience/GetExpTransactions
Authorization: Bearer {token}
Query Parameters:
  - pageIndex: int (默认 1)
  - pageSize: int (默认 20, 最大 100)
  - expType: string (可选,筛选类型)
  - startDate: date (可选)
  - endDate: date (可选)

Response:
{
  "isSuccess": true,
  "responseData": {
    "page": 1,
    "pageSize": 20,
    "dataCount": 156,
    "pageCount": 8,
    "data": [
      {
        "id": 123,
        "expType": "POST_LIKED",
        "expAmount": 5,
        "businessType": "Post",
        "businessId": 456,
        "remark": "帖子被点赞",
        "levelBefore": 2,
        "levelAfter": 2,
        "createdAt": "2026-01-02T10:30:00Z"
      }
    ]
  }
}
```

### 6.3 查询每日经验值统计

```http
GET /api/v1/Experience/GetDailyStats
Authorization: Bearer {token}
Query Parameters:
  - days: int (默认 7, 最大 30) - 查询最近N天

Response:
{
  "isSuccess": true,
  "responseData": [
    {
      "statDate": "2026-01-02",
      "expEarned": 45,
      "expFromPost": 10,
      "expFromComment": 6,
      "expFromLike": 24,
      "expFromHighlight": 0,
      "expFromLogin": 5,
      "postCount": 1,
      "commentCount": 3,
      "likeGivenCount": 8,
      "likeReceivedCount": 4
    }
  ]
}
```

### 6.4 查询等级排行榜

```http
GET /api/v1/Experience/GetLeaderboard
Authorization: Bearer {token}
Query Parameters:
  - pageIndex: int (默认 1)
  - pageSize: int (默认 50, 最大 100)
  - type: string (默认 total_exp, 可选: current_level)

Response:
{
  "isSuccess": true,
  "responseData": {
    "page": 1,
    "pageSize": 50,
    "dataCount": 10000,
    "data": [
      {
        "rank": 1,
        "userId": 789,
        "userName": "修仙大佬",
        "avatarUrl": "/avatars/789.jpg",
        "currentLevel": 9,
        "currentLevelName": "渡劫",
        "totalExp": 98765,
        "isCurrentUser": false
      }
    ],
    "currentUserRank": 1234,        // 当前用户排名
    "currentUserExp": {
      "userId": 12345,
      "currentLevel": 3,
      "totalExp": 1250
    }
  }
}
```

### 6.5 管理员调整经验值(Admin)

```http
POST /api/v1/Experience/Admin/AdjustExperience
Authorization: Bearer {token}
Roles: System, Admin
Content-Type: application/json

Body:
{
  "userId": 12345,
  "expAmount": -100,               // 负数=扣除, 正数=增加
  "reason": "违规发帖,扣除经验值"
}

Response:
{
  "isSuccess": true,
  "messageInfo": "经验值调整成功"
}
```

---

## 7. 前端展示设计

### 7.1 个人主页经验条

**位置**:`radish.client/src/apps/profile/components/ExperienceBar.tsx`

**功能**:
- 显示当前等级昵称和图标
- 显示经验条进度(当前经验/升级所需经验)
- 鼠标悬停显示详细信息(累计经验、排名等)
- 升级时播放动画效果

**示例**:
```tsx
import React from 'react';
import styles from './ExperienceBar.module.css';

interface ExperienceBarProps {
  currentLevel: number;
  currentLevelName: string;
  currentExp: number;
  expToNextLevel: number;
  levelProgress: number;
  themeColor: string;
  totalExp: number;
  rank?: number;
}

export const ExperienceBar: React.FC<ExperienceBarProps> = ({
  currentLevel,
  currentLevelName,
  currentExp,
  expToNextLevel,
  levelProgress,
  themeColor,
  totalExp,
  rank
}) => {
  return (
    <div className={styles.experienceBar}>
      <div className={styles.levelInfo}>
        <div className={styles.levelBadge} style={{ borderColor: themeColor }}>
          <span className={styles.levelText}>Lv.{currentLevel}</span>
          <span className={styles.levelName}>{currentLevelName}</span>
        </div>

        <div className={styles.expDetails}>
          <div className={styles.expText}>
            {currentExp} / {currentExp + expToNextLevel}
          </div>
          <div className={styles.expSubText}>
            距离下一级还需 {expToNextLevel} 经验
          </div>
        </div>
      </div>

      <div className={styles.progressBar}>
        <div
          className={styles.progressFill}
          style={{
            width: `${levelProgress * 100}%`,
            backgroundColor: themeColor
          }}
        >
          <div className={styles.progressGlow} />
        </div>
      </div>

      <div className={styles.statsRow}>
        <div className={styles.statItem}>
          <span className={styles.statLabel}>累计经验</span>
          <span className={styles.statValue}>{totalExp.toLocaleString()}</span>
        </div>
        {rank && (
          <div className={styles.statItem}>
            <span className={styles.statLabel}>经验排名</span>
            <span className={styles.statValue}>#{rank}</span>
          </div>
        )}
      </div>
    </div>
  );
};
```

**CSS 动画**(升级特效):
```css
/* ExperienceBar.module.css */
@keyframes levelUpGlow {
  0%, 100% {
    box-shadow: 0 0 10px rgba(255, 215, 0, 0.5);
  }
  50% {
    box-shadow: 0 0 30px rgba(255, 215, 0, 1);
  }
}

.levelBadge.levelUp {
  animation: levelUpGlow 1.5s ease-in-out 3;
}

@keyframes progressFill {
  0% {
    width: 0%;
  }
  100% {
    width: var(--target-width);
  }
}

.progressFill {
  animation: progressFill 1s ease-out;
}
```

### 7.2 升级动画弹窗

**位置**:`radish.client/src/components/LevelUpModal/LevelUpModal.tsx`

**触发时机**:
- WebSocket 推送升级事件时
- 或刷新页面时检测到等级变化

**功能**:
- 全屏烟花/光效动画
- 显示旧等级 → 新等级
- 显示解锁的新特权
- 自动 3 秒后关闭或手动关闭

**示例**:
```tsx
import React, { useEffect, useState } from 'react';
import { Modal } from '@radish/ui';
import Confetti from 'react-confetti';
import styles from './LevelUpModal.module.css';

interface LevelUpModalProps {
  visible: boolean;
  oldLevel: number;
  oldLevelName: string;
  newLevel: number;
  newLevelName: string;
  newPrivileges: string[];
  themeColor: string;
  onClose: () => void;
}

export const LevelUpModal: React.FC<LevelUpModalProps> = ({
  visible,
  oldLevel,
  oldLevelName,
  newLevel,
  newLevelName,
  newPrivileges,
  themeColor,
  onClose
}) => {
  const [showConfetti, setShowConfetti] = useState(false);

  useEffect(() => {
    if (visible) {
      setShowConfetti(true);
      const timer = setTimeout(() => {
        setShowConfetti(false);
        onClose();
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [visible, onClose]);

  return (
    <>
      {showConfetti && <Confetti recycle={false} numberOfPieces={500} />}

      <Modal visible={visible} onClose={onClose} showCloseButton={false}>
        <div className={styles.levelUpModal}>
          <div className={styles.title}>恭喜升级!</div>

          <div className={styles.levelTransition}>
            <div className={styles.oldLevel}>
              <span className={styles.levelNumber}>Lv.{oldLevel}</span>
              <span className={styles.levelName}>{oldLevelName}</span>
            </div>

            <div className={styles.arrow}>→</div>

            <div className={styles.newLevel} style={{ color: themeColor }}>
              <span className={styles.levelNumber}>Lv.{newLevel}</span>
              <span className={styles.levelName}>{newLevelName}</span>
            </div>
          </div>

          {newPrivileges.length > 0 && (
            <div className={styles.privileges}>
              <div className={styles.privilegesTitle}>解锁新特权</div>
              <ul className={styles.privilegesList}>
                {newPrivileges.map((privilege, index) => (
                  <li key={index} className={styles.privilegeItem}>
                    ✓ {privilege}
                  </li>
                ))}
              </ul>
            </div>
          )}

          <button className={styles.closeButton} onClick={onClose}>
            确定
          </button>
        </div>
      </Modal>
    </>
  );
};
```

### 7.3 Dock 等级徽章显示

**位置**:`radish.client/src/desktop/Dock.tsx`

**功能**:
- 在用户头像旁显示当前等级徽章
- 点击跳转到个人主页经验详情
- 鼠标悬停显示等级进度

**示例**:
```tsx
// 在 Dock 用户信息区域添加
<div className={styles.userInfo}>
  <img src={user.avatarUrl} alt="头像" className={styles.avatar} />

  <div
    className={styles.levelBadge}
    style={{ backgroundColor: user.themeColor }}
    title={`Lv.${user.currentLevel} ${user.currentLevelName}`}
  >
    Lv.{user.currentLevel}
  </div>

  <span className={styles.userName}>{user.userName}</span>
</div>
```

### 7.4 经验值明细页面

**位置**:`radish.client/src/apps/profile/pages/ExperienceDetail.tsx`

**功能**:
- 经验值曲线图(最近 7 天/30 天)
- 经验值来源饼图(发帖/评论/点赞等占比)
- 经验值明细列表(分页)
- 等级排行榜

**使用图表库**:Recharts

**示例**:
```tsx
import React from 'react';
import { LineChart, Line, PieChart, Pie, Cell, XAxis, YAxis, Tooltip, ResponsiveContainer } from 'recharts';
import { ExperienceBar } from '../components/ExperienceBar';
import styles from './ExperienceDetail.module.css';

export const ExperienceDetail: React.FC = () => {
  // 数据从 API 获取...
  const dailyStats = useDailyStats(7);
  const sourceDistribution = useExpSourceDistribution();
  const transactions = useExpTransactions(1, 20);

  return (
    <div className={styles.experienceDetail}>
      <h2>经验值详情</h2>

      {/* 经验条 */}
      <ExperienceBar {...userExp} />

      {/* 经验值曲线 */}
      <div className={styles.chartSection}>
        <h3>经验值趋势</h3>
        <ResponsiveContainer width="100%" height={300}>
          <LineChart data={dailyStats}>
            <XAxis dataKey="statDate" />
            <YAxis />
            <Tooltip />
            <Line type="monotone" dataKey="expEarned" stroke="#8884d8" strokeWidth={2} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* 经验值来源饼图 */}
      <div className={styles.chartSection}>
        <h3>经验值来源</h3>
        <ResponsiveContainer width="100%" height={300}>
          <PieChart>
            <Pie data={sourceDistribution} dataKey="value" nameKey="name" cx="50%" cy="50%" label>
              {sourceDistribution.map((entry, index) => (
                <Cell key={`cell-${index}`} fill={entry.color} />
              ))}
            </Pie>
            <Tooltip />
          </PieChart>
        </ResponsiveContainer>
      </div>

      {/* 经验值明细列表 */}
      <div className={styles.transactionList}>
        <h3>经验值明细</h3>
        {transactions.map(tx => (
          <div key={tx.id} className={styles.transactionItem}>
            <div className={styles.txInfo}>
              <span className={styles.txType}>{tx.remark}</span>
              <span className={styles.txTime}>{new Date(tx.createdAt).toLocaleString()}</span>
            </div>
            <span className={styles.txAmount}>+{tx.expAmount}</span>
          </div>
        ))}
      </div>
    </div>
  );
};
```

---

## 8. 性能与安全

### 8.1 缓存策略

**用户经验值缓存**:
```
Key: exp:user:{userId}
Value: { currentLevel, currentExp, totalExp, ... }
TTL: 10 分钟
更新策略: Write-Through(写入DB后立即更新缓存)
```

**等级配置缓存**:
```
Key: exp:level:config
Value: [{ level, levelName, expRequired, ... }]
TTL: 1 小时
更新策略: Cache-Aside(配置变更后清除缓存)
```

**每日上限缓存**:
```
Key: exp:daily:{userId}:{action_type}:{date}
Value: 已获得的经验值
TTL: 次日凌晨
```

### 8.2 并发控制

**乐观锁**:
- 使用 `version` 字段防止并发更新冲突
- 冲突时自动重试(最多 3 次)

**示例**:
```csharp
var retryPolicy = Policy
    .Handle<DbUpdateConcurrencyException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt))
    );

await retryPolicy.ExecuteAsync(async () =>
{
    await GrantExperienceAsync(userId, amount, expType);
});
```

### 8.3 防刷与风控

见 **3.2 防刷机制**。

### 8.4 数据清理策略

**经验值交易记录**:
- 保留 1 年(可配置)
- 超过 1 年的记录归档到对象存储
- 保留汇总统计(每月/每年)

**每日统计表**:
- 保留 3 个月
- 超过 3 个月的按月汇总

**定时任务**:
```csharp
[AutomaticRetry(Attempts = 3)]
public async Task CleanExpiredDataAsync()
{
    var oneYearAgo = DateTime.Now.AddYears(-1);

    // 归档旧数据
    await ArchiveExpTransactionsAsync(oneYearAgo);

    // 删除已归档数据
    await _db.Deleteable<ExpTransaction>()
        .Where(t => t.CreatedAt < oneYearAgo)
        .ExecuteCommandAsync();
}
```

---

## 9. 与其他系统集成

### 9.1 与萝卜币系统联动

**升级奖励**:
- 每次升级奖励萝卜币 = 等级 × 100
- 例如:升到 Lv.5 奖励 500 胡萝卜

**等级特权**:
- Lv.3+ 转账手续费 9 折
- Lv.5+ 转账手续费 8 折
- Lv.7+ 转账手续费 7 折

### 9.2 与通知系统联动

**升级通知**:
- 实时推送升级通知(见 **5.1.2 升级事件处理**)
- Toast 提示 + 升级动画弹窗

**经验值获得通知**:
- 获得大量经验值时(≥50)实时推送
- Toast 提示:"+50 经验值:成为神评"

### 9.3 与权限系统集成

**等级特权配置**(`level_config.privileges` 字段):
```json
{
  "Lv.3": [
    "发帖置顶(每周1次)",
    "自定义个人主页背景"
  ],
  "Lv.5": [
    "创建话题",
    "评论高亮(每天3次)",
    "萝卜币转账手续费8折"
  ],
  "Lv.7": [
    "社区管理员候选资格",
    "专属勋章",
    "萝卜币转账手续费7折"
  ],
  "Lv.9": [
    "社区决策投票权",
    "自定义等级昵称",
    "专属头像框"
  ]
}
```

**权限验证**:
```csharp
public async Task<bool> CheckLevelPrivilegeAsync(long userId, string privilege)
{
    var userExp = await _userExpRepository.QueryByIdAsync(userId);
    var levelConfig = await _levelConfigRepository.QueryByIdAsync(userExp.CurrentLevel);

    var privileges = JsonSerializer.Deserialize<List<string>>(levelConfig.Privileges);
    return privileges?.Contains(privilege) ?? false;
}
```

---

## 10. 监控与运维

### 10.1 关键指标

| 指标 | 说明 | 告警阈值 |
|-----|------|---------|
| **每日新增经验值总量** | 系统每日发放的经验值总和 | 异常波动 > 50% |
| **每日升级用户数** | 每日有多少用户升级 | 异常波动 > 50% |
| **经验值冻结用户数** | 作弊被冻结的用户数 | > 100 |
| **API 响应时间** | 经验值相关 API 的 P95 响应时间 | > 500ms |
| **缓存命中率** | 经验值查询的缓存命中率 | < 90% |

### 10.2 运维工具

**管理后台功能**:
1. 经验值统计面板
   - 每日经验值发放量曲线
   - 各等级用户分布
   - 经验值来源占比
2. 用户经验值管理
   - 查询用户经验值详情
   - 手动调整经验值
   - 冻结/解冻用户经验值
3. 异常检测日志
   - 作弊行为检测记录
   - 异常经验值发放记录

### 10.3 日志记录

**关键日志点**:
- 经验值发放成功/失败
- 用户升级
- 每日上限触发
- 作弊检测触发
- 经验值冻结/解冻

**日志级别**:
- 正常发放:Debug
- 升级:Information
- 每日上限触发:Information
- 作弊检测:Warning
- 冻结操作:Warning

---

## 11. 测试策略

### 11.1 单元测试

**测试范围**:
- 等级计算逻辑
- 经验值发放逻辑
- 每日上限检查
- 去重规则
- 升级判定

**示例**:
```csharp
[Fact]
public async Task GrantExperience_ShouldUpgradeLevel_WhenExpEnough()
{
    // Arrange
    var userId = 123;
    await InitUserExpAsync(userId, level: 0, exp: 90); // 距离升级还需 10

    // Act
    await _expService.GrantExperienceAsync(userId, 20, "POST_CREATE");

    // Assert
    var userExp = await _userExpRepository.QueryByIdAsync(userId);
    userExp.CurrentLevel.Should().Be(1); // 已升级到 Lv.1
    userExp.CurrentExp.Should().Be(10);  // 剩余 10 经验(100 → Lv.1, 110 - 100 = 10)
}

[Fact]
public async Task GrantExperience_ShouldNotExceedDailyLimit()
{
    // Arrange
    var userId = 123;
    await InitUserExpAsync(userId, level: 0, exp: 0);

    // Act - 连续发 20 篇帖子(每篇 +10 经验,上限 50)
    for (int i = 0; i < 20; i++)
    {
        await _expService.GrantExperienceAsync(userId, 10, "POST_CREATE");
    }

    // Assert
    var userExp = await _userExpRepository.QueryByIdAsync(userId);
    userExp.TotalExp.Should().Be(50); // 只获得 50 经验(达到每日上限)
}
```

### 11.2 集成测试

**测试范围**:
- 发帖 → 经验值发放 → 升级通知
- 点赞 → 双方经验值发放
- 成为神评 → 经验值发放

**示例**:
```csharp
[Fact]
public async Task PublishPost_ShouldGrantExp_AndTriggerLevelUp()
{
    // Arrange
    var userId = 123;
    await InitUserExpAsync(userId, level: 0, exp: 95); // 距离升级还需 5

    // Act
    await _postService.PublishPostAsync(new Post
    {
        UserId = userId,
        Title = "测试帖子",
        Content = "内容"
    });

    // Assert
    var userExp = await _userExpRepository.QueryByIdAsync(userId);
    userExp.CurrentLevel.Should().Be(1); // 发帖 +10 经验,已升级

    var notifications = await _notificationRepository.QueryAsync(n => n.UserId == userId);
    notifications.Should().ContainSingle(n => n.Type == "LevelUp"); // 收到升级通知
}
```

### 11.3 压力测试

**测试场景**:
1. 1000 并发用户同时发帖(经验值发放)
2. 100 用户同时升级(升级通知推送)
3. 10000 用户查询经验值(缓存命中率)

**目标**:
- 经验值发放成功率 > 99.9%
- API 响应时间 P95 < 500ms
- 缓存命中率 > 95%

---

## 12. 实施计划

### 12.1 Phase 0(准备阶段) - 1 天 ✅ 已完成

- [x] 确定等级昵称和经验值曲线
- [x] 设计数据库表结构
- [x] 定义 API 接口契约

### 12.2 Phase 1(核心功能) - 5-7 天 ✅ P0 阶段已完成 (2026-01-11)

**目标**:实现经验值发放、等级计算、个人主页展示

**任务清单**:
1. **数据模型** ✅
   - [x] 创建 `UserExperience` 实体
   - [x] 创建 `ExpTransaction` 实体
   - [x] 创建 `LevelConfig` 实体
   - [x] 创建 `UserExpDailyStats` 实体
   - [x] 配置 AutoMapper 映射 (`ExperienceProfile`)

2. **Repository 层** ✅
   - [x] 使用 `IBaseRepository<UserExperience>`
   - [x] 使用 `IBaseRepository<ExpTransaction>`
   - [x] 使用 `IBaseRepository<LevelConfig>`
   - [x] 使用 `IBaseRepository<UserExpDailyStats>`

3. **Service 层** ✅
   - [x] 创建 `IExperienceService` 接口
   - [x] 实现 `ExperienceService`(发放、升级、统计)
   - [x] 实现乐观锁重试机制 (3次重试，指数退避)
   - [x] 实现等级计算逻辑 (二分查找式算法)
   - [ ] 实现每日上限检查 (P1)

4. **API 接口** ✅
   - [x] `GET /api/v1/Experience/GetMyExperience` (当前用户)
   - [x] `GET /api/v1/Experience/GetUserExperience/{userId}` (指定用户)
   - [x] `GET /api/v1/Experience/GetLevelConfigs` (所有等级配置)
   - [x] `GET /api/v1/Experience/GetLevelConfig/{level}` (指定等级)
   - [x] `GET /api/v1/Experience/GetMyTransactions` (交易记录)
   - [x] `POST /api/v1/Experience/AdminAdjustExperience` (管理员调整)

5. **数据初始化** ✅
   - [x] 初始化 `LevelConfig` 表数据(Lv.0-10)
   - [x] 数据库表结构创建成功
   - [x] 11个等级配置种子数据初始化成功

6. **测试** ⏳ P1
   - [ ] 单元测试:等级计算、经验值发放
   - [ ] 集成测试:完整流程
   - [ ] 与发帖功能集成测试

### 12.3 Phase 2(业务集成) - 3-5 天

**目标**:与论坛功能集成,实现所有经验值获取途径

**任务清单**:
1. **发帖奖励**
   - [ ] 在 `PostService.PublishPostAsync` 集成
   - [ ] 首次发帖额外奖励

2. **评论奖励**
   - [ ] 在 `CommentService.AddCommentAsync` 集成
   - [ ] 首次评论额外奖励

3. **点赞奖励**
   - [ ] 在 `PostService.ToggleLikeAsync` 集成(帖子)
   - [ ] 在 `CommentService.ToggleLikeAsync` 集成(评论)

4. **神评/沙发奖励**
   - [ ] 在 `CommentHighlightJob.ExecuteAsync` 集成

5. **签到奖励**
   - [ ] 实现每日登录奖励
   - [ ] 实现连续登录奖励

6. **测试**
   - [ ] 端到端测试:各种互动场景

### 12.4 Phase 3(前端展示) - 3-5 天

**目标**:完善前端展示,包括经验条、升级动画、明细页面

**任务清单**:
1. **组件开发**
   - [ ] `ExperienceBar` 组件(经验条)
   - [ ] `LevelUpModal` 组件(升级动画)
   - [ ] `ExperienceDetail` 页面(明细)
   - [ ] `Leaderboard` 页面(排行榜)

2. **个人主页集成**
   - [ ] 在个人主页添加经验条
   - [ ] 添加"经验详情"入口

3. **Dock 集成**
   - [ ] 显示当前等级徽章
   - [ ] 鼠标悬停显示进度

4. **WebSocket 推送**
   - [ ] 监听升级事件
   - [ ] 触发升级动画

5. **测试**
   - [ ] UI 测试:各种等级展示
   - [ ] 动画测试:升级特效

### 12.5 Phase 4(完善与优化) - 2-3 天

**目标**:防刷机制、排行榜、管理后台

**任务清单**:
1. **防刷机制**
   - [ ] 实现异常检测规则
   - [ ] 实现经验值冻结功能
   - [ ] 实现互刷检测

2. **排行榜**
   - [ ] `GET /api/v1/Experience/GetLeaderboard`
   - [ ] 前端排行榜页面
   - [ ] 定时任务:更新排行榜缓存

3. **管理后台**
   - [ ] 经验值统计面板
   - [ ] 用户经验值管理
   - [ ] 异常检测日志

4. **性能优化**
   - [ ] 缓存策略优化
   - [ ] 数据库索引优化
   - [ ] 压力测试

5. **文档**
   - [ ] API 文档更新
   - [ ] 运维文档编写

---

## 13. 待确认问题

### 13.1 设计细节

- [ ] 等级昵称是否采用修仙体系?还是其他主题(如动物、植物、星座等)?
- [ ] Lv.10 之后是否需要继续扩展?还是 Lv.10 为最高等级?
- [ ] 升级奖励(萝卜币)的金额是否合理?
- [ ] 各行为的经验值奖励是否合理?是否需要调整?

### 13.2 业务规则

- [ ] 点赞后取消,是否扣除已获得的经验值?(建议:不扣除)
- [ ] 帖子/评论被删除,是否扣除已获得的经验值?(建议:不扣除)
- [ ] 用户被封禁,经验值是否清零?(建议:不清零,但冻结)

### 13.3 性能与容量

- [ ] 预期用户规模?影响排行榜缓存策略
- [ ] 经验值交易记录保留多久?
- [ ] 是否需要每月/每年汇总报表?

### 13.4 UI/UX

- [ ] 升级动画是否需要音效?
- [ ] 经验条是否需要在所有页面显示?(如帖子列表、评论区)
- [ ] 等级徽章的样式是否需要专业设计师设计?

---

## 14. 参考资料

### 14.1 内部文档

- [萝卜币系统设计](/guide/radish-coin-system) - 积分系统设计参考
- [通知系统实时推送](/guide/notification-realtime) - 升级通知推送
- [神评/沙发功能](/features/comment-highlight) - 经验值触发点

### 14.2 外部资源

- [游戏化设计:让产品更有黏性](https://book.douban.com/subject/26883342/)
- [等级系统设计的经验与教训](https://www.gamasutra.com/view/feature/134842/the_chemistry_of_game_design.php)
- [Reddit Karma 系统](https://www.reddit.com/wiki/karma)
- [Stack Overflow 声望系统](https://stackoverflow.com/help/whats-reputation)

---

**文档版本**:v1.2
**创建日期**:2026-01-02
**最后更新**:2026-01-11
**实施状态**:P0 阶段已完成（含经验值计算公式配置化），P1/P2 进行中
**负责人**:待定
**审核状态**:P0 已实施

**v1.2 更新内容**（2026-01-11）:
- ✅ 新增 2.3 节：经验值计算公式配置化
- ✅ 支持 4 种计算公式（混合/指数/多项式/分段）
- ✅ 提供推荐配置方案（新社区/长期运营/硬核/修仙阶段）
- ✅ 实现动态调整流程和管理员 API
- ✅ 扩展层 ExperienceExtension 完整实现
- ✅ 数据库初始化支持动态计算
