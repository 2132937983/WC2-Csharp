# ArmyDef 扁平化映射清单

本文只定义旧的 `(国家, 兵种)` 到新全局 ArmyDef key 的映射，暂不修改
`armydef.xml`、场景 XML 或运行时代码。

## 命名规则

- `others` 使用无国家前缀的 key，例如 `infantry`、`tank`。
- 没有独立数据且没有独立兵模的国家，也使用无前缀 key。
- 具有独立数据或独立兵模的变体使用国家前缀，例如 `de-infantry`。
- 场景中的 `country` 属性引用国家实例 `id`；派系由对应国家节点的 `name` 决定。
  例如 `id="de1" name="de"` 仍使用 `de-*` 变体。
- `unit-type` 是上层战斗分类，`army-type` 是中层兵种分类，key/ID 是最低层的
  精确单位身份。

示例：

```text
unit-type=infantry, army-type=infantry, key=infantry
unit-type=infantry, army-type=infantry, key=de-infantry
unit-type=armor,   army-type=tank,     key=de-tank
```

## 基础定义

以下定义均为无前缀的默认叶节点。任何未在“国家变体”中列出的组合都映射到这里。

| army-type | 全局 key | 旧 ID | 说明 |
|---|---|---:|---|
| infantry | `infantry` | 0 | 默认步兵 |
| panzer | `panzer` | 1 | 默认装甲车 |
| artillery | `artillery` | 2 | 默认火炮 |
| rocket | `rocket` | 3 | 默认火箭炮 |
| tank | `tank` | 4 | 默认坦克 |
| heavytank | `heavytank` | 5 | 默认重型坦克 |
| destroyer | `destroyer` | 6 | 默认驱逐舰 |
| cruiser | `cruiser` | 7 | 默认巡洋舰 |
| battleship | `battleship` | 8 | 默认战列舰 |
| aircraftcarrier | `aircraftcarrier` | 9 | 默认航空母舰 |
| carrier | `carrier` | 10 | 运输船/运输状态定义 |
| airstrike | `airstrike` | 11 | 默认空袭定义 |
| bomber | `bomber` | 12 | 默认轰炸定义 |
| airborne | `airborne` | 13 | 默认空降定义 |
| nuclearbomb | `nuclearbomb` | 14 | 默认核打击定义 |
| eliteinfantry | `eliteinfantry` | 15 | 默认特种步兵 |
| realbomber | `realbomber` | 16 | 当前分支新增的地图轰炸机 |

## 备份与兵模中的候选变体

本节是对 `armydef-bak.xml` 和 `army_hd.xml` 的资源盘点，不是最终采用的转换表。
最终方案见文末“正式采用的转换方案”。

来源标记：

- `D`：`armydef-bak.xml` 中数值相对 `others` 不同。
- `I`：`army_hd.xml` 中有独立兵模。
- `D+I`：两者都有。

| army-type | 国家 | 新全局 key | 来源 |
|---|---|---|---|
| infantry | am | `am-infantry` | D+I |
| infantry | cn | `cn-infantry` | D+I |
| infantry | de | `de-infantry` | D+I |
| infantry | fr | `fr-infantry` | D+I |
| infantry | gb | `gb-infantry` | D+I |
| infantry | it | `it-infantry` | I |
| infantry | ja | `ja-infantry` | D+I |
| infantry | ru | `ru-infantry` | D+I |
| infantry | tw | `tw-infantry` | I |
| panzer | am | `am-panzer` | D+I |
| panzer | de | `de-panzer` | D+I |
| panzer | fr | `fr-panzer` | D+I |
| panzer | gb | `gb-panzer` | D+I |
| panzer | it | `it-panzer` | D+I |
| panzer | ja | `ja-panzer` | D+I |
| panzer | ru | `ru-panzer` | D+I |
| artillery | am | `am-artillery` | D+I |
| artillery | cn | `cn-artillery` | D |
| artillery | de | `de-artillery` | D+I |
| artillery | fr | `fr-artillery` | D |
| artillery | gb | `gb-artillery` | D+I |
| artillery | ja | `ja-artillery` | D |
| artillery | ru | `ru-artillery` | D+I |
| rocket | am | `am-rocket` | D |
| rocket | de | `de-rocket` | D+I |
| rocket | gb | `gb-rocket` | D |
| rocket | ru | `ru-rocket` | D+I |
| tank | am | `am-tank` | D+I |
| tank | de | `de-tank` | D+I |
| tank | fr | `fr-tank` | D+I |
| tank | gb | `gb-tank` | D+I |
| tank | it | `it-tank` | D+I |
| tank | ja | `ja-tank` | D+I |
| tank | ru | `ru-tank` | D+I |
| heavytank | am | `am-heavytank` | D+I |
| heavytank | de | `de-heavytank` | D+I |
| heavytank | fr | `fr-heavytank` | D+I |
| heavytank | gb | `gb-heavytank` | I |
| heavytank | ja | `ja-heavytank` | I |
| heavytank | ru | `ru-heavytank` | D+I |
| airstrike | am | `am-airstrike` | D |
| airstrike | de | `de-airstrike` | D |
| airstrike | fr | `fr-airstrike` | D |
| airstrike | gb | `gb-airstrike` | D |
| airstrike | it | `it-airstrike` | D |
| airstrike | ja | `ja-airstrike` | D |
| airstrike | ru | `ru-airstrike` | D |
| bomber | am | `am-bomber` | D |
| bomber | de | `de-bomber` | D |
| bomber | fr | `fr-bomber` | D |
| bomber | gb | `gb-bomber` | D |
| bomber | it | `it-bomber` | D |
| bomber | ja | `ja-bomber` | D |
| bomber | ru | `ru-bomber` | D |
| eliteinfantry | de | `de-eliteinfantry` | I |

## 默认回退示例

以下组合没有独立叶节点：

```text
(cn, panzer)          -> panzer
(tw, tank)            -> tank
(it, heavytank)       -> heavytank
(de, destroyer)       -> destroyer
(ru, aircraftcarrier) -> aircraftcarrier
(id=de1, name=de, infantry) -> de-infantry
(id=ru2, name=ru, tank)     -> ru-tank
(任意未知国家, rocket) -> rocket
```

四种海军目前都没有国家独立数据或兵模，因此全部映射到无前缀定义。
`carrier`、`airborne`、`nuclearbomb` 和 `realbomber` 同样没有国家变体。

## 数量

```text
基础定义：17
国家变体：55
预计总数：72
```

上面的 72 个定义是把备份数据差异也全部恢复时的候选数量，不是本次正式方案的数量。

## 正式采用的转换方案

### 数据来源

- 所有实体单位的 HP、攻击、移动、战斗规则等数据均以当前 `armydef.xml` 为准。
- 国家独立兵模按照 `army_hd.xml` 建立独立叶节点。
- 当前 `armydef.xml` 已有的 `cn` infantry 数据特化继续保留。
- 不从 `armydef-bak.xml` 搬运其他国家的实体单位数值。
- 仅为测试空袭扁平化，从备份搬运 `am`、`de` 的 airstrike 和 bomber 数值。

### 场景国家解析

转换 battle/conquest 时，不能直接使用 area 上的国家实例 ID：

```xml
<country id="de1" name="de" .../>
<area country="de1">
    <army type="infantry"/>
</area>
```

转换步骤为：

```text
area.country = de1
de1 对应的 country.name = de
(de, infantry) -> de-infantry
```

所以 `de1`、`de2` 都属于 `de` 派系，`ru1`、`ru2` 都属于 `ru` 派系。
只有找不到国家节点、`name` 为空，或该派系没有特化叶节点时，才回退到无前缀单位。

### 实体单位映射

以下列表是正式使用的国家变体。未列出的 `(派系 name, army-type)` 均使用无前缀 key。

| army-type | 使用国家前缀的派系 name | 数据来源 | 图片来源 |
|---|---|---|---|
| infantry | am, cn, de, fr, gb, it, ja, ru, tw | 当前 `armydef.xml`；cn 使用当前特化数据，其余复制默认数据 | 对应 `soldier_*.png` |
| panzer | am, de, fr, gb, it, ja, ru | 当前默认数据 | 对应 `panzer_*.png` |
| artillery | am, de, gb, ru | 当前默认数据 | 对应 `cannon_*.png` |
| rocket | de, ru | 当前默认数据 | 对应 `rocketlauncher_*.png` |
| tank | am, de, fr, gb, it, ja, ru | 当前默认数据 | 对应 `tank_*.png` |
| heavytank | am, de, fr, gb, ja, ru | 当前默认数据 | 对应 `heavytank_*.png` |
| eliteinfantry | de | 当前默认数据 | `eliteinfantry_de.png` |

以下定义没有国家变体：

```text
destroyer
cruiser
battleship
aircraftcarrier
carrier
airborne
nuclearbomb
realbomber
```

实体单位国家变体共 36 个：

```text
infantry 9 + panzer 7 + artillery 4 + rocket 2
+ tank 7 + heavytank 6 + eliteinfantry 1 = 36
```

### 非实体空袭测试定义

airstrike 和 bomber 不参与 battle/conquest 的 `<army>` 转换，也不计入实体单位映射。
为了测试空袭逻辑去国家查表，额外建立四个精确定义：

| 派系 | army-type | 新 key | 数据来源 | 图片 |
|---|---|---|---|---|
| am | airstrike | `am-airstrike` | `armydef-bak.xml` 的 am airstrike | 共用 fighter |
| de | airstrike | `de-airstrike` | `armydef-bak.xml` 的 de airstrike | 共用 fighter |
| am | bomber | `am-bomber` | `armydef-bak.xml` 的 am bomber | 共用 bomber |
| de | bomber | `de-bomber` | `armydef-bak.xml` 的 de bomber | 共用 bomber |

这些定义以后由对应的战略卡效果直接引用，不再在执行空袭时根据攻击国家二次解析。

### 正式方案数量

```text
当前基础定义：17
实体单位国家变体：36
AM/DE 空袭测试定义：4
预计全局 ArmyDef 总数：57
```

其中只有基础实体单位和 36 个实体变体会出现在 battle/conquest 转换结果中。
