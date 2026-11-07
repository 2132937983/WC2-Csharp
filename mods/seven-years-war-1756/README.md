# 1756: The Seven Years' War

[English](#english) | [中文](#chinese)

<a id="english"></a>

Conquest 11: the world in spring 1756, on the eve of the Seven Years' War, on the Natural Earth world map.
Built into the game (`assets/build.json`, like the world map itself), so it is in the conquest list on
Windows and Android; nothing else in the game changes.

## The scenario

| Side | Countries | Relations |
|---|---|---|
| Anglo-Prussian Alliance | Great Britain (with Ireland and the colonies), Prussia, Hanover, Hesse-Kassel, Brunswick, the Hudson's Bay Company | |
| Franco-Austrian Alliance | France, the Habsburg Monarchy, Russia, Spain, Sweden, Saxony, Bavaria, the Palatinate, Württemberg, Cologne, Mainz, Trier, the Franconian prince-bishoprics | |
| Neutral powers (each a side of its own) | Ottoman Empire, Poland-Lithuania, Qing China, Tokugawa Japan, the Mughal Empire, Persia, Denmark-Norway | at peace with everyone, for good |
| Lesser neutral states | Portugal, the Dutch Republic, the Swiss, Venice, Genoa, the Papal States, Naples, Sardinia, Parma, Modena, Malta, Liège, Mecklenburg, Baden, the Crimean Khanate, Morocco, Ethiopia, Oman, the Marathas, Hyderabad, Bengal, Mysore, the Carnatic, Travancore, the Durrani Afghans, Kandy, the Kazakhs, Khiva, Bukhara, Korea, Đại Việt, Siam, Burma | the original game's neutrals: they do not act, anyone may attack them |

The war breaks out as it did. Britain and France are at war from round 1 (the war in the Ohio valley and the
Carnatic). Prussia is at peace with the Franco-Austrian side until **round 3**, when Frederick invades Saxony.
Russia joins in **round 5**, Sweden in **round 6** and Spain in **round 12**; a commander announces each one.
Victory: defeat every country at war with you or bound to be; the neutral powers need not fall.

The 1756 borders: Prussia with Silesia, Cleves, East Frisia and East Prussia; the Habsburgs with the Austrian
Netherlands, Milan, Tuscany, Hungary and Transylvania; New France from Quebec over the Great Lakes to
Louisiana; the Thirteen Colonies; Rupert's Land; New Spain, Florida and South America; Portuguese Brazil;
French Saint-Domingue, Martinique, Guadeloupe, Pondicherry and the Northern Circars; British Madras,
Bombay, Calcutta, Minorca, Jamaica and Barbados; Dutch Java, Ceylon and the Cape; the forts of the
West African coast. Lands no state held in 1756 (the interior of Africa and Australia, the American West)
have no owner.

## The flags

Every country flies a flag of the period, drawn for this mod from the heraldry: the first Union Flag of
1707, the white royal flag of France with golden fleurs-de-lis, the black Prussian eagle with sceptre, orb
and the cipher FR, the black double-headed eagle of the Habsburgs on gold, Peter the Great's white, blue and
red, the Cross of Burgundy of Spain, the eagle and the mounted knight of Poland-Lithuania, the crescent and
eight-pointed star of the Ottomans, the yellow triangular dragon flag of the Qing, the Danish split flag,
and the arms of the lesser states. Where a country uses the game's own units (Britain, France, Prussia, ...),
its period flag belongs to **this scenario only** (`flag_c11_<id>.png`): the game's flags stay as they are
in every other conquest and campaign.

## Simplifications

The map's provinces follow today's borders and those of 1910-1949; a 1756 border that cuts through one of
them goes around it, with the province given to the side that held most of it. Small states and enclaves
are merged into their neighbours: the free cities (Hamburg, Bremen, Frankfurt), Anhalt, Hesse-Darmstadt,
Nassau, the Saxon duchies (with Saxony), Ansbach and Bayreuth (with the Franconian bishoprics), Courland
(with Poland-Lithuania), Lucca (with Tuscany). Gibraltar shares its province with Cádiz and stays Spanish;
Chandernagore shares Calcutta's (British), Tranquebar Karaikal's (French), Elmina the western Gold Coast
(Dutch). Neuchâtel (Prussian) is part of a Swiss province. Bermuda is too small for the map.

## Changing it

Everything here is generated from `source/seven-years-war-1756.json`: the countries, their flags (designs and
the heraldic shapes they use), the factions, the relations and the rules that give each province its owner.

```powershell
dotnet run --project Tools -- build-scenario --config mods/seven-years-war-1756/source/seven-years-war-1756.json
# with  --preview owners.png  for a picture of the whole world in the owners' colours
```

Then rebuild the game (the mod is built in) or put the folder into a mods folder. The format is in
`MODDING.md`, section 4.

---

<a id="chinese"></a>

# 1756：七年战争

征服 11：1756 年春，七年战争前夕的世界，使用 Natural Earth 世界地图。它与世界地图一样内置在游戏中
（`assets/build.json`），因此在 Windows 和 Android 的征服列表中都能找到；游戏的其他内容不受影响。

## 剧本

| 阵营 | 国家 | 外交关系 |
|---|---|---|
| 英普同盟 | 大不列颠（含爱尔兰和殖民地）、普鲁士、汉诺威、黑森-卡塞尔、不伦瑞克、哈德逊湾公司 | |
| 法奥同盟 | 法国、哈布斯堡君主国、俄国、西班牙、瑞典、萨克森、巴伐利亚、普法尔茨、符腾堡、科隆、美因茨、特里尔、法兰肯采邑主教国 | |
| 中立强国（各自为一方） | 奥斯曼帝国、波兰立陶宛联邦、大清、德川日本、莫卧儿帝国、波斯、丹麦-挪威 | 与所有国家永久和平 |
| 较小的中立国 | 葡萄牙、荷兰共和国、瑞士、威尼斯、热那亚、教皇国、那不勒斯、撒丁、帕尔马、摩德纳、马耳他、列日、梅克伦堡、巴登、克里米亚汗国、摩洛哥、埃塞俄比亚、阿曼、马拉塔、海得拉巴、孟加拉、迈索尔、卡纳蒂克、特拉凡科尔、杜兰尼阿富汗、康提、哈萨克、希瓦、布哈拉、朝鲜、大越、暹罗、缅甸 | 原版游戏的中立国：它们不行动，任何国家都可以进攻它们 |

战争按历史进程爆发。英法两国从第 1 回合起就处于战争状态（俄亥俄河谷和卡纳蒂克的战事）。普鲁士与法奥一方和平
到**第 3 回合**，届时腓特烈入侵萨克森。俄国在**第 5 回合**参战，瑞典在**第 6 回合**，西班牙在**第 12 回合**；每次都有
一位将领宣布。胜利条件：击败所有正在或将要与你交战的国家；中立强国无需消灭。

1756 年的边界：普鲁士拥有西里西亚、克莱沃、东弗里斯兰和东普鲁士；哈布斯堡拥有奥属尼德兰、米兰、托斯卡纳、
匈牙利和特兰西瓦尼亚；新法兰西从魁北克经五大湖延伸到路易斯安那；十三殖民地；鲁珀特地；新西班牙、佛罗里达和
南美洲；葡属巴西；法属圣多明各、马提尼克、瓜德罗普、本地治里和北部沿海地区；英属马德拉斯、孟买、加尔各答、
梅诺卡、牙买加和巴巴多斯；荷属爪哇、锡兰和开普；西非海岸的要塞。1756 年没有国家控制的土地（非洲和澳大利亚
内陆、美洲西部）没有归属。

## 国旗

每个国家都使用那个时代的旗帜，由本模组根据纹章绘制：1707 年的第一面联合旗，法国饰有金色百合花的白色王旗，
手持权杖和宝球、胸前有 FR 字母的普鲁士黑鹰，金底上的哈布斯堡黑色双头鹰，彼得大帝的白蓝红三色旗，西班牙的
勃艮第十字，波兰立陶宛的白鹰与骑士，奥斯曼的新月和八角星，大清的黄色三角龙旗，丹麦的燕尾旗，以及各小国的
纹章。使用游戏自带单位的国家（英国、法国、普鲁士……）的时代旗帜**只属于本剧本**（`flag_c11_<id>.png`）：在其他
所有征服和战役中，游戏自己的国旗保持不变。

## 简化之处

地图的省份沿今天的边界以及 1910-1949 年的边界划分；穿过某个省份的 1756 年边界会绕过它，该省份归属占有其大部分
土地的一方。小邦和飞地并入邻国：自由市（汉堡、不来梅、法兰克福）、安哈尔特、黑森-达姆施塔特、拿骚、萨克森
诸公国（并入萨克森）、安斯巴赫和拜罗伊特（并入法兰肯主教国）、库尔兰（并入波兰立陶宛）、卢卡（并入托斯卡纳）。
直布罗陀与加的斯同属一个省份，因此仍归西班牙；金德讷格尔与加尔各答（英国）同属一个省份，特兰奎巴与开利开尔
（法国）同属一个省份，埃尔米纳属于黄金海岸西部（荷兰）。纳沙泰尔（普鲁士）属于瑞士的一个省份。百慕大太小，
不在地图上。

## 修改

这里的一切都由 `source/seven-years-war-1756.json` 生成：国家、国旗（设计及其使用的纹章图形）、阵营、外交关系，
以及决定每个省份归属的规则。

```powershell
dotnet run --project Tools -- build-scenario --config mods/seven-years-war-1756/source/seven-years-war-1756.json
# 加上  --preview owners.png  可以生成一张按归属着色的全世界图片
```

然后重新构建游戏（该模组是内置的），或者把文件夹放入模组文件夹。格式见 `MODDING.md` 第 4 节。
