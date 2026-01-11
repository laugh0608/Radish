# 经验值与等级系统：后端设计

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)

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

见 [2.2 等级配置化设计](/guide/experience-level-core-concepts) 中的 `level_config` 表。

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
