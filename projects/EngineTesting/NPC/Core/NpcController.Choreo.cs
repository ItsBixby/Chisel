using Chisel.Models;
using Chisel.Utils.Animation;
using Engine;
using static Chisel.Models.CModel;

namespace EngineTesting.NPC.Core;
public partial class NpcController
{
    private bool isActing;
    private bool permActing;
    public bool IsActing
    {
        get => isActing || permActing;
        set
        {
            if (value && !IsActing) StopMoving();
            isActing = value;
        }
    }

    protected void SetPermaActing(bool val) => permActing = val;

    WorldEntity currentChoreoLookTarget = null;

    float gestureTime;

    public void OnLookAt(string targetEntity)
    {
        currentChoreoLookTarget = null;

        if (targetEntity == "none")
        {
            return;
        }

        var targets = EntityManager.FindEntityIndexByName(targetEntity);

        if (targets == null || targets.Length == 0) return;

        if (targets[0] == -1) return;

        currentChoreoLookTarget = EntityManager.entities[targets[0]];
    }
    public void OnPlayMorph(string morph)
    {
        model.PlayMorphAnimation(morph, entity.Position);
    }
    public void OnPlayGesture(string gesture)
    {
        var seq = model.Model.FindSequence(gesture);
        if (seq == null) return;

        float duration = GetClipSequenceDuration(seq);
        if (duration <= 0f) return;

        gestureTime = duration;
        gestureLayer.ForceReplay(seq, loop: false);
    }

    // Gestures are expected to be simple Clip-type sequences
    private float GetClipSequenceDuration(CSequence seq)
    {
        if (seq.Root.Type != BlendSourceType.Clip) return 0f;

        var animDef = model.Model.FindAnimation(seq.Root.AnimationName);
        return animDef?.Animation?.DurationInSeconds ?? 0f;
    }
}