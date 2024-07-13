﻿using Darkages.Object;

namespace Darkages.Templates;

public abstract class Template : ObjectManager
{
    public string Description { get; set; }
    public string Group { get; set; }
    public int ID { get; set; }
    public string Name { get; set; }

    public abstract string[] GetMetaData();

}