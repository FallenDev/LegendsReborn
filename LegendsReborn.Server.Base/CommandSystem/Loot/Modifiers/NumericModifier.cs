﻿#region

#endregion

namespace Darkages.CommandSystem.Loot.Modifiers;

public enum Operation
{
    Add,

    Subtract,

    Divide,

    Multiply,

    Equal
}

[Serializable]
public class NumericModifier : BaseModifier
{
    public NumericModifier()
    {
    }

    public NumericModifier(string propertyName, double min, double max, Operation operation)
        : base(propertyName)
    {
        Min = min;
        Max = max;
        Operation = operation;
    }

    public double Max { get; set; }
    public double Min { get; set; }
    public Operation Operation { get; set; }

    public override void Apply(object itemToModify)
    {
        var value = GetValue<double>(itemToModify);

        if (Max < Min)
            Max = Min;

        var number = Min;

        switch (Operation)
        {
            case Operation.Add:
                value += number;
                break;

            case Operation.Subtract:
                value -= number;
                break;

            case Operation.Divide:
                value /= number;
                break;

            case Operation.Multiply:
                value *= number;
                break;

            case Operation.Equal:
                value = number;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(Operation));
        }

        SetValue(itemToModify, Convert.ChangeType(value, value.GetType()));
    }
}