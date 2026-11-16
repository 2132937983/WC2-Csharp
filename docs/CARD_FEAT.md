# 卡牌配置与效果系统设计（`carddef.xml`）

> 状态：设计提案，尚未实现。本文先固定第一阶段的结构和语义；存在争议的部分集中列在“待确认事项”中。

## 1. 目标

当前 `carddef.xml` 已经配置卡牌名称、数字 ID、分类、价格、工业、冷却、科技要求、关联兵种、图片和介绍文本，也支持国家专属覆盖。但以下内容仍然依赖 `CARD_ID` 的硬编码分支：

- 目标是否合法（目前由代码中的卡牌分支隐式判断）；
- 造兵所需的城市、工业、港口等规则；
- 卡牌实际效果；
- 动画、特效和音效；
- 少数特殊价格、冷却及使用限制。

第一阶段的目标是建立一套可扩展的声明式框架：

1. 扩展 `carddef.xml`，允许配置逻辑效果和表现；
2. 在加载阶段把 XML 解析为强类型定义，并完成错误检查；
3. 定义带 `CanApply()` 的效果处理器及执行上下文接口；
4. 用注册表把稳定的 XML 类型名绑定到 C# 处理器；
5. 先接入建筑和工事卡；
6. 为复杂卡牌建立接口和带说明的占位处理器，具体规则随后实现。

本阶段不追求一次移除所有 `CARD_ID`。旧数字 ID 暂时保留，用于存档、现有数组、GUI 顺序及旧代码兼容。

## 2. 核心原则

### 2.1 XML 保存声明，不保存 C# 方法名

推荐：

```xml
<effect type="area-structure"
        slot="construction"
        structure="city"
        operation="adjust"
        amount="+1" />
```

不推荐：

```xml
<effect function="CCountry.BuildCity" />
```

XML 直接引用类名或方法名会产生以下问题：

- XML 与代码重构绑定；
- 参数类型无法在加载时可靠校验；
- 需要反射，错误通常延迟到使用卡牌时才暴露；
- 对 Android 裁剪、AOT 和方法重命名不友好；
- 配置文件获得了不必要的任意代码调用能力。

### 2.2 只解析一次

执行流程应为：

```PlainText
carddef.xml
    ↓ 游戏加载时解析
CardDef / CardEffectDef
    ↓ 加载时校验并通过 type 找到处理器
ICardEffectHandler
    ↓ 使用卡牌时直接执行
结果、表现和公共结算
```

禁止在每次使用卡牌时重新读取 XML 或反复解析 `amount="+inf"` 等字符串。加载后只保留枚举、整数、布尔值和已解析的定义对象。

处理器可以由注册表在加载时解析，也可以在执行时通过已规范化的 `type` 查表。第一阶段优先在定义中保存稳定的处理器键，不把委托序列化进存档。

### 2.3 逻辑与表现分离

一张卡分为两部分：

```PlainText
effects       对游戏状态的实际修改
presentation  动画、特效、音效和延迟执行时机
```

目标是否合法由对应效果处理器的 `CanApply()` 隐式判断，不在第一阶段为建筑和工事建立通用 condition 配置。UI、AI 和真正执行前共享同一个 `CanApply()`，避免三套规则不一致。

普通卡可以立即执行效果后播放音效。空袭、轰炸、核弹和空降属于异步表现：应先启动 `CBomber`，在命中时执行效果，飞机离场后结束行动。

表现层不得自行决定价格、目标合法性或逻辑伤害。

### 2.4 配置使用稳定 key

新配置应逐步使用字符串 key：

```xml
<card key="elite-infantry"
      legacy-id="28"
      related-unit="elite-infantry" />
```

- `key`：卡牌的稳定标识；
- `legacy-id`：兼容当前 `CARD_ID`、存档和 UI；
- `related-unit`：引用兵种注册表的字符串 key，不依赖动态单位 ID。

第一阶段可以继续读取旧 `id` 和 `relatedArmyId`，但解析后应尽量转换为稳定 key 或已解析的兵种定义引用。

## 3. 建议的 XML 层级

```PlainText
<cards>
└── <country call="others">
    └── <card>
        ├── 现有基础属性
        ├── <effects>
        │   └── <effect ... />
        └── <presentation ... />
```

完整示例：

```xml
<card key="city"
      legacy-id="14"
      name="City"
      type="development"
      price="60"
      industry="0"
      cooldown="0"
      tech="1"
      image="card_city.png"
      intro="City Intro">
    <effects>
        <effect type="area-structure"
                slot="construction"
                structure="city"
                operation="adjust"
                amount="+1"
                initial-level="area-default" />
    </effects>
    <presentation sound="build" />
</card>
```

为了平滑迁移，加载器应同时兼容现有属性名：

```text
id             → legacy-id
round          → cooldown
relatedArmyId  → related-unit 的旧数字形式
```

是否立刻重命名原 XML 属性属于迁移策略，不影响运行时模型。

## 4. 运行时数据模型

建议的数据关系：

```csharp
public sealed class CardDef
{
    public string key;
    public CARD_ID id; // legacy
    public List<CardEffectDef> effects;
    public CardPresentationDef presentation;
}
```

基础定义只保存加载后的声明：

```csharp
public abstract class CardEffectDef
{
    public string Type;
}
```

建筑/工事使用专门的强类型定义，不建议把所有属性长期留在 `Dictionary<string, string>` 中：

```csharp
public sealed class AreaStructureEffectDef : CardEffectDef
{
    public AreaStructureSlot Slot;
    public AreaStructureKind Structure;
    public AreaStructureOperation Operation;
    public int LevelDelta;
    public bool ToMaximum;
    public ConstructionInitialLevel InitialLevel;
}
```

未知扩展属性可以在解析阶段保留原始 XML 节点或属性表用于报错，但正式执行应使用强类型字段。

## 5. 接口

### 5.1 执行上下文

```csharp
public sealed class CardEffectContext
{
    public CCountry SourceCountry;
    public CardDef Card;
    public CArea TargetArea;
    public CArmy TargetArmy;
    public int TargetAreaId;
    public int TargetArmyIndex;
}
```

后续可以增加：

- 当前 `CountryAction`；
- 是否由 AI 使用；
- 随机数源；
- 表现调度器；
- 日志和失败原因收集器。

### 5.2 效果接口

```csharp
public interface ICardEffectHandler
{
    bool CanApply(
        CardEffectContext context,
        CardEffectDef effect,
        out string errorCode);

    CardEffectResult Apply(
        CardEffectContext context,
        CardEffectDef effect);
}
```

`CanApply()` 同时服务于：

1. UI 标记可选目标；
2. AI 搜索合法目标；
3. 玩家点击后的即时验证；
4. 真正应用效果前的最终验证。

最终验证不能省略，因为目标状态可能在行动排队后发生变化，AI、存档动作或未来网络动作也可能绕过 UI。`CanApply()` 只能读取状态，不得产生副作用。

返回值不应只有 `bool`：

```csharp
public readonly struct CardEffectResult
{
    public bool Success { get; init; }
    public string ErrorCode { get; init; }
}
```

第一阶段建议的错误码：

```text
unknown-effect
invalid-attribute
missing-target-area
target-wrong-owner
target-wrong-terrain
construction-type-mismatch
construction-max-level
construction-min-level
installation-already-exists
no-installation-to-destroy
effect-not-implemented
```

### 5.4 注册表

```csharp
registry.RegisterEffect("area-structure", new AreaStructureEffectHandler());
registry.RegisterEffect("airstrike", new AirstrikeCardEffectHandler());
```

注册表需要拒绝：

- 重复注册；
- 空 key；
- XML 引用了不存在的效果处理器；
- 处理器与解析出的定义类型不匹配。

## 6. 建筑与工事统一模型

建筑与工事共用一个 `area-structure` 效果处理器，但必须保留两个槽位：

```text
construction  城市、工业、机场；有等级
installation  要塞、战壕、防空炮、雷达；无等级
```

同一区域可以同时存在一个建筑和一个工事，但同一槽位只能有一种结构。

建议枚举：

```text
AreaStructureSlot:
  construction
  installation

AreaStructureOperation:
  set
  adjust
  clear
```

建筑和工事是有隐藏逻辑的（除非未来要更改）：即一个地块上只能有一种建筑或工事，因此属于“软柿子”。

### 6.1 建筑操作

#### 增加一级

```xml
<effect type="area-structure"
        slot="construction"
        structure="city"
        operation="adjust"
        amount="+1" />
```

#### 增加多级

```xml
<effect type="area-structure"
        slot="construction"
        structure="industry"
        operation="adjust"
        amount="+2" />
```

#### 降低一级

```xml
<effect type="area-structure"
        slot="construction"
        structure="airport"
        operation="adjust"
        amount="-1" />
```

#### 直接升满

```xml
<effect type="area-structure"
        slot="construction"
        structure="city"
        operation="adjust"
        amount="+inf" />
```

#### 摧毁建筑

规范写法：

```xml
<effect type="area-structure"
        slot="construction"
        operation="clear" />
```

兼容简写：

```xml
<effect type="area-structure"
        slot="construction"
        operation="adjust"
        amount="-inf" />
```

加载器应把 `-inf` 规范化为 `clear`，运行时不保存“无穷大整数”。`+inf` 解析为 `ToMaximum = true`。

有限等级调整应钳制在合法范围内。降到零时是否自动删除建筑，第一阶段建议为“是”。

### 6.2 空地初始等级

原版 `CArea.Construct()` 在空地上建造时会根据区域类型决定部分建筑的初始等级，例如首都、大城市和普通城市。为保留原版行为，建议：

```xml
initial-level="area-default"
```

预留值：

```text
area-default  沿用原版区域类型规则
one           统一从 1 级开始
explicit      使用 initial-level-value
```

第一阶段只要求实现 `area-default`，其他值可以完成解析和校验后返回 `effect-not-implemented`，或暂不接受。

### 6.3 建筑类型冲突

第一阶段默认语义：

```text
空地         → 可以创建指定建筑
相同建筑     → 可以调整等级
不同建筑     → CanApply() 返回 false，不隐式替换
```

未来若需要替换建筑，可新增明确的 `operation="replace"`，不建议用 `set` 隐式完成替换。

### 6.4 工事操作

添加工事：

```xml
<effect type="area-structure"
        slot="installation"
        structure="fort"
        operation="set" />
```

摧毁工事：

```xml
<effect type="area-structure"
        slot="installation"
        operation="clear" />
```

第一阶段工事没有等级，因此不允许：

- `operation="adjust"`；
- `amount`；
- `+inf` 或 `-inf`（摧毁统一写 `clear`）。

处理器的隐式验证规则：

```text
set    目标必须没有工事
clear  目标必须已有工事
```

工事替换留到后续，不在第一阶段隐式支持。

## 7. 建筑/工事的隐式目标规则

第一阶段不把建筑和工事的目标规则写入 XML。`AreaStructureEffectHandler.CanApply()` 根据效果定义和目标当前状态统一判断。

建筑正向调整：

```text
目标属于使用者
目标是陆地
目标没有建筑，或已有相同类型建筑
当前等级未达到上限
```

建筑负向调整或摧毁：

```text
目标属于允许操作的一方（第一阶段为使用者）
目标存在指定类型建筑
当前等级大于零
```

添加工事：

```text
目标属于使用者
目标是陆地
目标当前没有工事
```

摧毁工事：

```text
目标属于允许操作的一方（第一阶段为使用者）
目标当前存在工事
```

同一张建筑卡负责从无建筑到创建、以及之后的连续升级，不为“一升二”“二升三”拆分不同卡牌。AI 只需调用同一个 `CanApply()` 判断目标是否合法；第一阶段不为每个建筑等级建立独立卡牌或独立 AI 分支。

如果未来确实出现大量可组合规则，再评估增加 condition 系统；它不属于当前第一阶段。

## 8. 公共结算边界

效果处理器只负责卡牌的业务效果，不负责所有公共结算。以下行为应继续由统一卡牌执行器管理：

- 扣除金钱和工业；
- 增加本回合使用次数；
- 设置冷却；
- 处理勋章造成的冷却变化；
- 标记行动完成；
- 统一错误日志。

只有当所有处理器的 `CanApply()` 成功且效果执行成功后，才应扣费和进入冷却。多效果卡需要明确事务语义：第一阶段建议先执行全部 `CanApply()` 和参数校验，再按顺序执行；不支持执行到一半失败后的自动回滚，因此处理器必须避免在验证阶段修改状态。

## 9. 表现层

简单表现：

```xml
<presentation sound="build" />
```

第一阶段可将 `sound` 映射到现有 `SND_EFFECT` 注册表，不在 XML 中保存枚举整数。

复杂表现预留：

```xml
<presentation handler="bomber"
              effect="effect_airstrike.xml"
              sound="machine-gun"
              apply-at="impact" />
```

第一阶段复杂表现只解析和校验，不要求替换现有 `CBomber` 流程。

## 10. 复杂卡牌占位处理器

以下卡牌暂时保留旧逻辑，同时建立处理器接口：

- 空袭；
- 轰炸；
- 核弹；
- 空降；
- 指挥官；
- 补给线；
- 王牌部队；
- 单位状态卡；
- 普通造兵和勋章加成。

占位处理器必须：

1. 写明 XML 已解析字段；
2. 写明旧 C# 逻辑位置；
3. 写明 C++ 对应逻辑位置；
4. 说明输入、输出和失败语义；
5. 默认返回 `effect-not-implemented`，不得扣费或静默成功；
6. 保证工程可以编译。

示例：

```csharp
public sealed class AirborneCardEffectHandler : ICardEffectHandler
{
    public CardEffectResult Apply(CardEffectContext context, CardEffectDef effect)
    {
        // TODO(USER): 实现空降后的归属变更、亡国判定和单位生成。
        // 旧逻辑：Game/BasicElements/GameUseCard.cs，AIRBORNE_FORCE_CARD 分支。
        // 表现：Game/MapScene/CBomber.cs；效果应在降落伞命中时应用。
        return CardEffectResult.NotImplemented("airborne");
    }
}
```

## 11. 加载期错误检查

加载器至少检查：

- `key` 是否为空或重复；
- `legacy-id` 是否重复；
- 国家覆盖是否引用不存在的基础卡；
- effect/presentation 类型是否已注册；
- 必填属性是否存在；
- 枚举字符串是否合法；
- `amount` 是否为有符号整数、`+inf` 或 `-inf`；
- construction 是否使用合法建筑类型；
- installation 是否使用合法工事类型；
- 工事是否错误使用 `adjust` 或 `amount`；
- `clear` 是否错误携带 `structure`/`amount`（可警告并忽略，也可严格报错，见待确认事项）；
- `related-unit` 是否能在兵种注册表中解析；
- 国家覆盖是否继承未重复声明的 effects/presentation。

错误信息应包含文件、国家、卡牌 key 和 XML 节点，例如：

```text
carddef.xml: country=others card=city effect[0]:
invalid amount "infinity"; expected signed integer, +inf or -inf
```

建议开发环境采用严格模式：配置错误阻止进入游戏。发布环境仍应记录完整错误，不应把未知效果当作成功执行。

## 12. 国家覆盖与继承

当前国家节点只覆盖同 ID 卡牌的基础字段。扩展后需要明确嵌套列表的继承方式。

第一阶段建议：

- 没有 `<effects>` 时继承基础卡全部效果；
- 没有 `<presentation>` 时继承基础表现；
- 一旦提供对应节点，默认整体替换该列表；
- 暂不支持按索引合并或删除单个基础效果。

如果需要只覆盖价格而不复制逻辑，上述规则最简洁。

## 13. 协作边界

Codex 负责：

- XML 结构与兼容解析；
- 强类型定义；
- 接口、上下文和注册表；
- 加载期校验和错误日志；
- 建筑/工事通用处理器的框架与确定性逻辑；
- 复杂处理器的可编译占位和详细注释；
- 编译验证及 C++ 调用链核对。

用户负责或最终确认：

- 复杂卡牌的游戏规则；
- 指挥官、空降等特殊状态变化；
- 动画与逻辑效果的最终触发时机；
- 新扩展功能的平衡和 AI 语义；
- 文档中“待确认事项”的最终选择。

为避免冲突，建议文件边界为：

```PlainText
Codex:
  CardXmlLoader.cs
  CardEffectDef.cs
  CardEffectContext.cs
  CardHandlerRegistry.cs
  AreaStructureEffectHandler.cs

用户：
  CardEffectHandlers/AirstrikeCardEffectHandler.cs
  CardEffectHandlers/AirborneCardEffectHandler.cs
  CardEffectHandlers/CommanderCardEffectHandler.cs
  其他复杂规则处理器
```

## 14. 第一阶段完成标准

- 旧 `carddef.xml` 仍能加载；
- 新 effects/presentation 节点能解析；
- 所有定义在进入游戏前完成校验；
- 注册表能检测未知和重复处理器；
- 城市、工业、机场卡不再依赖对应 `CARD_ID` 分支执行；
- 要塞、战壕、防空炮、雷达卡不再依赖对应 `CARD_ID` 分支执行；
- 建筑支持有限正负等级、`+inf` 和摧毁；
- 工事支持添加和摧毁；
- 不同建筑默认不能隐式替换；
- 公共扣费、次数和冷却仍只有一个入口；
- 复杂卡牌仍通过旧逻辑工作，或明确处于未迁移状态；
- 桌面与 Android 工程均能编译；
- 至少有解析器测试和建筑/工事效果测试。

## 15. 待确认事项

以下内容应在动笔前或实现对应功能时确认：

1. `amount="-inf"` 是否正式支持，还是只允许更清晰的 `operation="clear"`；
2. 有限负数把建筑降到 0 时是否自动删除建筑；
3. `+inf` 是“设置到当前类型上限”，还是“增加一个不会溢出的极大值”（建议前者）；
4. 在空地上执行 `adjust +N` 时，是先按 `area-default` 创建再额外增加 `N-1` 级，还是创建后再完整增加 N 级；
5. `set` 对已有不同类型建筑是否拒绝，还是允许显式替换；
6. `clear` 携带多余 `structure` 时采用严格报错还是警告；
7. 多个 effects 中途失败时，未来是否需要事务回滚；
8. 国家覆盖的嵌套列表采用整体替换是否满足需求；
9. 第一阶段是否同时把 `relatedArmyId` 迁移到 `related-unit` 字符串 key；
10. 表现失败是否影响逻辑效果执行（建议表现失败只记录日志，逻辑仍可完成；异步命中流程除外）。
