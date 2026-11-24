using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace DonQuixote;

/// <summary>
/// Static frame cues for the non-Spine combat visuals: RitsuLib drives the Sprite2D named Visuals in res://DonQuixote/scenes/creature_visuals/don_quixote.tscn.
/// </summary>
internal static class DonQuixoteCombatCues
{
    private const string DonQuixote_IdleTexture = "res://DonQuixote/images/char/idle.png";
    private const string DonQuixote_HitTexture = "res://DonQuixote/images/char/hit.png";
    private const string DonQuixote_DefendTexture = "res://DonQuixote/images/char/defend.png";
    private const string DonQuixote_CastTexture = "res://DonQuixote/images/char/cast.png";
    private const string DonQuixote_DeadTexture = "res://DonQuixote/images/char/dead.png";

    private static readonly string[] DonQuixote_AttackFrames =
    {
        "res://DonQuixote/images/char/attack_f0.png",
        "res://DonQuixote/images/char/attack_f1.png",
        "res://DonQuixote/images/char/attack_f2.png",
        "res://DonQuixote/images/char/attack_f3.png",
    };

    private static readonly float[] DonQuixote_AttackDelays = { 0.2f, 0.16f, 0.16f, 0.48f };

    public static VisualCueSet DonQuixote_Cues { get; } = DonQuixote_BuildCues();

    private static VisualCueSet DonQuixote_BuildCues()
    {
        VisualCueSetBuilder DonQuixote_Builder = VisualCueSetBuilder.Create();

        foreach (string DonQuixote_Cue in new[] { "idle", "Relaxed", "relaxed" })
        {
            DonQuixote_Builder.Single(DonQuixote_Cue, DonQuixote_IdleTexture);
        }

        foreach (string DonQuixote_Cue in new[] { "hit", "Hit" })
        {
            DonQuixote_Builder.Single(DonQuixote_Cue, DonQuixote_HitTexture, 0.35f);
        }

        foreach (string DonQuixote_Cue in new[] { "defend", "Defend" })
        {
            DonQuixote_Builder.Single(DonQuixote_Cue, DonQuixote_DefendTexture, 0.35f);
        }

        foreach (string DonQuixote_Cue in new[] { "cast", "Cast" })
        {
            DonQuixote_Builder.Single(DonQuixote_Cue, DonQuixote_CastTexture, 0.45f);
        }

        foreach (string DonQuixote_Cue in new[] { "dead", "Dead" })
        {
            DonQuixote_Builder.Single(DonQuixote_Cue, DonQuixote_DeadTexture);
        }

        foreach (string DonQuixote_Cue in new[] { "attack", "Attack" })
        {
            DonQuixote_Builder.Sequence(
                DonQuixote_Cue,
                DonQuixote_Seq =>
                {
                    for (int DonQuixote_I = 0; DonQuixote_I < DonQuixote_AttackFrames.Length; DonQuixote_I++)
                    {
                        DonQuixote_Seq.Frame(
                            DonQuixote_AttackFrames[DonQuixote_I],
                            DonQuixote_AttackDelays[DonQuixote_I]);
                    }

                    DonQuixote_Seq.Loop(false);
                });
        }

        return DonQuixote_Builder.Build();
    }
}
