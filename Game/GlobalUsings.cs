// Global usings of the game library: the engine's and the game's C-style globals and free functions
// (g_Scene, Sprintf, GetPath, ...) are imported statically so the ported code reads like the C++.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using static WC2.Globals;
global using static WC2.CStr;
global using static WC2.GameGlobals;
global using static WC2.FontGlobals;
global using static WC2.StringTableGlobals;
global using static WC2.ToolBox;
global using static WC2.ArmyDefFunctions;
global using static WC2.BattleScenario;
global using static WC2.ecTextExtensions;
global using static WC2.GUIElement;
