﻿using Darkages.Enums;
using Darkages.Interfaces;
using Darkages.Sprites;

namespace Darkages.ScriptingBase;

public abstract class DamageFormulaScript : IScriptBase
{
    public abstract int Calculate(Sprite obj, Sprite target, MonsterEnums type);
}