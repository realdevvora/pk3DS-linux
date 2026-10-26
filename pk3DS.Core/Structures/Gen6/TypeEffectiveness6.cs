using System;

namespace pk3DS.Core.Structures;

/// <summary>
/// The type effectiveness table in <c>DllBattle.cro</c> (the data part of the WinForms TypeChart6; drawing the chart stays with the image code).
/// Row = attacking type, column = defending type; 0 no effect, 2 not very effective, 4 regular, 8 super effective.
/// </summary>
public sealed class TypeEffectiveness6
{
    public const string FileName = "DllBattle.cro";
    public const int TypeCount = 18;
    /// <summary>The values TypeChart6 cycles through.</summary>
    public static readonly byte[] Values = [0, 2, 4, 8];

    private readonly int offset;

    public byte[,] Chart { get; } = new byte[TypeCount, TypeCount];

    public TypeEffectiveness6(CroFile battle, bool oras)
    {
        offset = oras ? 0x000DB428 : 0x000D12A8;
        for (int attack = 0; attack < TypeCount; attack++)
        {
            for (int defend = 0; defend < TypeCount; defend++)
                Chart[attack, defend] = battle.Data[offset + (attack * TypeCount) + defend];
        }
    }

    public void Write(CroFile battle)
    {
        for (int attack = 0; attack < TypeCount; attack++)
        {
            for (int defend = 0; defend < TypeCount; defend++)
                battle.Data[offset + (attack * TypeCount) + defend] = Chart[attack, defend];
        }
    }
}
