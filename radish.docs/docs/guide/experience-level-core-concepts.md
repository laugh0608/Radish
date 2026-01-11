# 2. 核心概念

> 入口页：[经验值与等级系统设计方案](/guide/experience-level-system)

## 2.1 等级体系(修仙主题)

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

## 2.2 等级配置化设计

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

## 2.3 经验值计算公式配置化

**设计理念**:
- **灵活调整**:通过配置文件动态调整经验值曲线,无需修改代码
- **公式多样**:支持多种数学模型(指数/多项式/混合/分段)
- **热更新**:管理员可通过 API 重新计算等级配置
- **可追溯**:配置变更记录操作日志

### 2.3.1 支持的计算公式

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

### 2.3.2 配置文件示例

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

### 2.3.3 推荐配置方案

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

### 2.3.4 动态调整流程

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

### 2.3.5 技术实现

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

## 2.4 经验值特性

- **存储单位**:整数(BIGINT),避免浮点计算误差
- **不可转让**:经验值仅个人所有,不可转账或交易
- **不可兑换**:经验值与萝卜币独立,互不兑换
- **可追溯**:每笔经验值变动记录完整日志
- **防刷机制**:同一行为有每日上限,异常检测自动封禁

---
