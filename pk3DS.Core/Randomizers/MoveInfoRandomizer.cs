using System;
using pk3DS.Core.Structures;

namespace pk3DS.Core.Randomizers;

/// <summary>
/// Randomizes each move's damage category and type (moved from the WinForms MoveEditor6).
/// </summary>
public class MoveInfoRandomizer(Move[] moves) : IRandomizer
{
    private const int TypeCount = 18;

    /// <summary>Gives each damaging move a random category (physical or special); status moves stay status.</summary>
    public bool Category;
    public bool Type;

    public void Execute()
    {
        if (!Category && !Type)
            throw new InvalidOperationException("Choose the category, the type or both to randomize moves.");
        var rnd = Util.Rand;
        for (int i = 1; i < moves.Length; i++)
        {
            // Struggle and Curse keep their data. MoveEditor6 meant these two, but skipped moves 166 and 175 because its list starts at move 1.
            if (i is 165 or 174)
                continue;
            if (moves[i].Category > 0 && Category)
                moves[i].Category = rnd.Next(1, 3);
            if (Type)
                moves[i].Type = rnd.Next(0, TypeCount);
        }
    }
}
