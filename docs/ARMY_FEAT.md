# 兵种特性与添加兵种的配置文档（暨armydef.xml使用说明）

该文档没有覆盖一些别名属性，但我建议就用这个文档里写的名字。

## `armydef.xml`结构层级介绍

```PlainText
  <armies>
  ├── <army-types>
  │   └── <type>
  │       ├── name
  │       ├── id
  │       ├── image
  │       └── country-independent
  │
  ├── <weapon-armor-rules>
  │   ├── <weapon>
  │   │   └── <armor>
  │   │       ├── type
  │   │       └── rate
  │   └── <rule>                         [旧格式兼容]
  │       ├── weapon
  │       ├── armor
  │       └── rate
  │
  └── <country>
      ├── name
      │
      └── <army>
          ├── type
          ├── image
          ├── country-independent
          ├── unit-type
          ├── strength
          ├── movement
          ├── minatk
          ├── maxatk
          ├── weapon-type
          ├── armor-type
          │
          └── <combat>
              ├── traits
              ├── can-occupy
              ├── ai-no-counter-bonus
              │
              ├── <attack>
              │   ├── mode
              │   ├── payload
              │   ├── attack-cost
              │   ├── engagement
              │   │
              │   ├── <range>
              │   │   ├── shape
              │   │   ├── min
              │   │   ├── max
              │   │   ├── radius
              │   │   ├── max-inclusive
              │   │   └── radius-source
              │   │
              │   ├── <engagement>
              │   │   └── mode
              │   │
              │   └── <damage>
              │       ├── target
              │       ├── phase
              │       └── rate
              │
              ├── <counter>
              │   ├── inherit
              │   ├── <range>
              │   └── <damage>
              │
              ├── <damage>
              │   ├── target
              │   ├── phase
              │   └── rate
              │
              └── <result>
                  ├── construction-level-degrade
                  ├── construction-level-chance
                  └── movement-restore-on-kill
```

## 原版fallback特性开关

整个armydef\.xml的最上层是

```xml
<armies legacy-combat-defaults="false"> </armies>
```

其中`legacy-combat-defaults`这个字段表示是否启用原版的兵种特性fallback（比如坦克跨壕等等）

这是为了和完全没有接入我们的特性工程的xml保持一个最小化接入——我们的项目默认这个值是true，这样老的xml就完全不用修改即可接入代码。

## 定义兵种id

靠上的一片区域是这样写的：

```xml

<army-types>
    <!-- 陆军：对应 soldier_*.png、panzer_*.png 等图集条目。 -->
    <type name="infantry" id="0" image="soldier"/>
    <type name="panzer" id="1" image="panzer"/>
    <type name="artillery" id="2" image="cannon"/>
    <type name="rocket" id="3" image="rocketlauncher"/>
    <type name="tank" id="4" image="tank"/>
    <type name="heavytank" id="5" image="heavytank"/>
    <!-- 海军：对应 destroyer_others.png 等图集条目。 -->
    <type name="destroyer" id="6" image="destroyer"/>
    <type name="cruiser" id="7" image="cruiser"/>
    <type name="battleship" id="8" image="battleship"/>
    <type name="aircraftcarrier" id="9" image="aircraftcarrier"/>
    <!-- 特殊单位的图片名称沿用原版 CBomber/CGameRes 资源。 -->
    <type name="carrier" id="10" image="transportship" country-independent="true"/>
    <type name="airstrike" id="11" image="fighter" country-independent="true"/>
    <type name="bomber" id="12" image="bomber" country-independent="true"/>
    <type name="airborne" id="13" image="bomber" country-independent="true"/>
    <type name="nuclearbomb" id="14" image="bomber" country-independent="true"/>
    <type name="eliteinfantry" id="15" image="eliteinfantry"/>
    <type name="realbomber" id="16" image="realbomber"/>
</army-types>

```

这里承担起了添加新兵种的责任，其中id0-14是原版自有，下面两个是我作为范例添加的。

想要添加新兵种只需要复制一行然后修改各类属性，image里面填写的内容会被补全png之后查找。

这里可以注意到一个特殊属性`country-independent`。如果我们不设定，那么代码会优先查找`<image>_<country>.png`，然后fallback到`image_others.png`，最后是`image.png`。但是假如我们设置此值为true，则渲染时会直接查找`<image>.png`。另外，下文我们会介绍，在各个派系的army中我们也提供了设置image的方法，这样我们就不用为了给匈牙利加德国坦克而添加一个图片资源`tank_hu.png`——直接设置复用`tank_de.png`即可。

因此，实际上目前添加新兵种只需：
1. 添加一行新的，修改id
2. 在others中定义好一个通用属性（想给不同国家加不同的兵种特性就需要额外指定了）。
3. 可选：在army_hd（包含图片文件和xml）中添加新的兵模图片
4. 将armydef\.xml中的`image`设为想要设置的图片名称。

完成以上4步，就可以将一个新兵种加入游戏了。

未来我在考虑把兵种和国家解耦，这样就有可能让一个国家同时上场美国和法国的重坦和坦克（栋派尔装甲战.jpg）。但这会对现有的战役工程产生冲击性的改动，因此或许也不会急于实现。


## 武器-护甲类型判定

这个版本已经加入了武器-护甲类型判定

```xml
<weapon-armor-rules>
<!-- 推荐按 weapon 分组；weapon/armor 的 type 分别为 1..32 和 1..255。 -->
    <!-- 示例：武器 1 对护甲 2 造成 150% 伤害，对护甲 3 造成 80% 伤害。 -->
    <!--
    <weapon type="1">
        <armor type="2" rate="150%"/>
        <armor type="3" rate="0.8"/>
    </weapon>
    -->
    <!-- PS：可以写注释记录武器和护甲类型的数字对应的含义是什么，但是我们代码只解析数字 -->
    <!-- 旧格式 <rule weapon="1" armor="2" rate="150%"/> 仍可读取。 -->
    <!-- 在兵种属性中添加weapon-type和armor-type 即可指定护甲和武器类型-->
</weapon-armor-rules>
```

这里的案例直接摘自armydef.xml。

由于武器和护甲的克制关系是**通用的**，所以必须在进入各个派系的兵种特性之前设置。

没有涉及的特性对默认为1.0，也就是不增伤不减伤。

写法如上所示，还是比较直观的，支持百分比和小数两种写法。

那么如何指定单位的武器和护甲类型呢，请在army词条一级中加入`weapon-type`和`armor-type`。

值得一提的是，一个单位只能有一种护甲类型，但可以有**多种武器类型**——当然，代价就是武器的数量只有32，也即1到32。实际计算时，会取这些武器类型对目标护甲的最高倍率；未配置的武器-护甲组合按 1.0 处理。

不过其实对于轻量和一般量级的修改够用了（如果要再加，可以让ai帮着改一下数据类型，从int32改到64位整型）。

多种武器类型的写法类似：

```xml
<army type="artillery" unit-type="3" strength="50" movement="2" minatk="1" maxatk="9" weapon-type="1|2" armor-type="1">
```

其中分隔符可用`\t`、`|`、`,`和` `。


## 兵种特性设置

现阶段的设置主要是和原版中id相关的特性，未来会考虑对一些机制上的能力做一些拓展——这还会涉及到一些ai的修正。因此不一定会做很多。

后续还考虑吸收一些现有c版mod的兵种特性（比如c版复兴）——但这是另一个话题了。

目前只支持一套兵种组，不同国家在其上做差分——未来考虑定义多个兵种，但每个国家可以分到不同数量的兵种。

甚至为什么要有国别概念呢？我是说，am-infantry和de-infantry本来就是两种不同的兵种，不是吗？

但这涉及到现有关卡xml的兼容性问题，因此可能需要排在靠后完成。

### 基础属性字段

`type`: 填写兵种名称

`unit-type`: \[可选字段\] 填写数字，这个是自定义的，0是保留字段，表示无类型，且不产生克制（不建议定义对于无类型的克制倍率）。

`strength`: 血量

`movement`: 移动力

`minatk`: 最小攻击力

`maxatk`: 最大攻击力

`weapon-type`: \[可选字段\] 武器类型，一个部队可有多个武器类型，武器类型支持的表达方式有"1 3 4"、"1,3,4"、"1|3|4"等

`armor-type`: \[可选字段\] 护甲类型，一个部队只有一种护甲类型。

`navy`: 主要影响海域中的运输船显示。

### combat属性配置

#### traits支持的字段特性

`cross-entrenchment`：单位攻击后不会被战壕清空行动力

`recv_fort_second_counter`： 单位受到要塞炮地块的存活敌军的二次反击

`cannot-occupy`：单位击败敌人后不可占领

`secondary-attack`：单位可以造成二段伤害（支持解析，但暂未实装）

示例：

```xml
  <combat traits="cross-entrenchment cannot-occupy recv_fort_second_counter"/>
```

（combat是嵌套在army层级里的）

#### AI相关的combat属性
 
`ai-no-counter-bonus`: ai逻辑相关，当一个单位攻击某个目标不会受到反击时，这个单位会提高多少攻击倾向。

```xml
  <combat traits="recv_fort_second_counter" ai-no-counter-bonus="400"/>
```

#### result结果结算属性

目前只有3个字段

`construction-level-degrade`: 攻击触发城市降级的降级数量，原版作用于战列舰，值为1

`construction-level-chance`: 攻击触发城市降级的概率。

`movement-restore-on-kill`: 收割后回复的行动力数量（在代码中保证了不会超过这个兵种的行动力上限）

示例：

```xml
<combat>
  <result
      construction-level-degrade="1"
      construction-level-chance="30"
      movement-restore-on-kill="1"/>
</combat>
```

#### 可配置的攻击和反击属性

这些主要和伤害、索敌（射程）、反击判定有关

##### attack mode

目前一共有三种

实际上，attack mode主要决定攻击方式（normal attack vs 空袭）和攻击箭头（红色箭头 vs 黄色垂直箭头）

`0`: 近战，使用红色箭头，发起normal attack

`1`: 远程/袭扰（例如：忍者），使用黄色垂直箭头，发起normal attack

`2`: 空袭（或者法术/导弹，当然这些动画效果得自己做），使用黄色箭头，发起空袭

特别地，即使配置了`mode="0"`，如果下文中shape="circle"，或graph 的 max > 1000，代码也会自动将其视为远程投射火力/袭扰模式。

##### 空袭相关属性

`payload`: 使用什么空袭模式（比如空袭、轰炸、原子弹），其中：
    - `1`：空袭，也可写作airstrike或fighter
    - `2`：轰炸，也可写作bomber
    - `3`：核弹，也可写作nuclear

`attack-cost`：这个属性未来会推广到普通攻击上，但目前只用于空袭，字面意思：空袭后花费多少行动力，可以设置数值和inf（清空）
    - 未来还需要利用这个属性判定可攻击性，但这得到c#版本继续了

##### range射程和打击模式

`shape`: 形状，graph是拓扑模式，circle是圆半径模式（注意，可以通过最大和最小射程来配置环形模式）。circle模式还会额外渲染攻击范围的阴影。

`min`: 类型是 uint16，范围 0～65535，最小射界，且区间必定包含这个值。如果圆半径的话，你想设置100.0f的半径，就填入1000（乘以10即可）。在拓扑模式下，这个值除以1000进行计算。其中，如果设置min为1000，则单位可以攻击到邻近敌人，设置为1001，则不能攻击邻近敌人。

`max`: 类型是 uint16，范围 0～65535，最大射界。区间是否包含这个值由`max-inclusive`决定。如果max设置为1000，只能攻击到邻近敌人，如果设置为1001，则一定可以攻击到隔1格的敌人。

`max-inclusive`: 射程范围区间是否包含最大值，比如原版的空袭范围就不包含边界，我建议只在circle模式设置这个属性。

`radius`: 别名字段，`<range radius="3000"/>`等价于`<range shape="circle" max="3000"/>`

`radius-source`：主要支持使用国家自带的机场范围作为攻击范围的最大值，比如`radius-source="country"`

这样，给出两个示例：

```xml
  <range shape="circle"
         min="0"
         radius-source="country"
         max-inclusive="false"/>

  <range shape="graph"
         min="0"
         max="2000"/>
```

此外还有一个combat的子节点`engagement`，其代表的是单位发起攻击的方式，0为抵近交战，1为原地投射火力，其作用我们稍后解释反击机制时说明

因此，完整示例有如：

```xml
<combat>
    <attack>
        <range shape="graph"
            min="0"
            max="2000"/>
        <engagement="0"/>
  </attack>
</combat>
```

###### 索敌判定逻辑

索敌范围有两类：拓扑模式和圆半径模式。

对于拓扑模式，我们将min和max除以1000并上取整，从而得到范围。

譬如，拓扑模式下设置范围为1000-2000，则为1-2，如设置为1500-2500，则为2-3。

随后我们代码中用广搜（原版的图不带权，所以不用更复杂的代码）确定索敌目标即可。

对于circle模式，我们将min和max除以10，转为浮点数设置，作为圆（环）范围的最小和最大半径，即可。

###### 反击判定逻辑

我们讨论攻击者和反击者的特性，这里我们引入一个“交战距离”的概念：任何单位只能反击交战距离在自身射程内的敌人。

怎么定义交战距离呢？如果engagement设置为0（即抵近交战模式），则交战距离就是0。

否则如果engagement设置为1（即投射火力模式）：如果攻击者是circle模式，则直接以拓扑距离\*1000（反击者为graph模式时）或者地图距离\*10（反击者为circle模式时）计算交战距离；如果攻击者是graph模式，则需要额外讨论：当反击者为circle模式，直接以地图距离\*10作为交战距离，否则当反击者为graph模式时，以$$\min(攻击者的最大攻击距离, 拓扑距离*1000)$$作为交战距离。

这样就完成了反击的判定。

##### 反击counter的配置

如果不配置，counter默认和attack一样（空袭除外）。

但是可以额外配置counter的距离：

```xml
  <counter inherit="attack">
      <range shape="graph" min="0" max="500"/>
  </counter>
```

这样的话，就用我们自己配置的range覆盖了反击范围。

特别地，使用空袭作为攻击方式的单位，反击是采用特殊规则的，其反击距离为0-1000，采用拓扑模式，当然，也可以自行覆盖range。

目前 `<counter>` 不单独解析以下属性：

- mode
- payload
- engagement
- attack-cost

也就是说，反击属性其实只涉及范围和增伤。

##### damage

主要和**类型克制有关**，目前只支持主动增伤。

示例：

```xml
    <damage target="2" rate="100%"/>
```

其中`target`和`unit_type`相关

这个属性可以放在attack和counter词条下

rate支持多种写法：

```xml
rate="130%"
rate="1.3"
rate="0.5"
rate="0%"
rate="inf"
```

当然damage也可以单放，需要加上phase字段表示是攻击还是反击增伤，但我建议还是归到attack或者counter目录下。

目前还没做针对兵种类型的减伤，但是现在这个工作已经打算收尾了——未来到cpp版本再继续做。
