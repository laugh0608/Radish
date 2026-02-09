# 经验值与等级系统：性能、安全、运维与测试

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)

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

见 [3.2 防刷机制](/guide/experience-level-earning)。

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
- 实时推送升级通知(见 [5.1.2 升级事件处理](/guide/experience-level-backend))
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
