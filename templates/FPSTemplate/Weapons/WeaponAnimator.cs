using Chisel.Models;
using Chisel.Utils.Animation;
using Engine;
using Engine.Console;
using Engine.Utils;
using FPSTemplate.Weapons;
using System;
using System.Collections.Generic;
using static Chisel.Models.CModel;

public class WeaponAnimator
{
    public string CurrentAnimation { get; private set; }
    public bool IsAnimating => queue.Count > 0 || (!isCurrentLooping && IsPlaying);
    public bool IsPlaying { get; private set; }

    public event Action<string> OnAnimationStarted;
    public event Action<string> OnAnimationFinished;
    public event Action<BaseWeapon.WeaponState> OnStateRequested;

    private readonly CModelDisplay display;
    private readonly LayerConfig layer;
    private readonly Queue<string> queue = new();
    private readonly Dictionary<string, bool> loopFlags = new();
    private readonly Dictionary<string, WeaponAnimator> slotAnimators = new();
    private readonly Dictionary<string, BaseWeapon.WeaponState> onStartState = new();
    private readonly Dictionary<string, BaseWeapon.WeaponState> onFinishState = new();
    private bool isCurrentLooping;

    public WeaponAnimator(CModelDisplay display, string layerName = "main", bool routeAnimEvents = true)
    {
        this.display = display;

        var animator = display.EnsureAnimator();
        layer = animator.GetOrAddLayer(layerName);
        layer.Influence = 1f;

        if (routeAnimEvents)
            animator.OnAnimEvent += HandleAnimEvent;
    }

    public WeaponAnimator RegisterSlot(string slotName)
    {
        var slotDisplay = display?.GetSlot(slotName);
        if (slotDisplay == null)
        {
            Logger.AppendWarn($"RegisterSlot: slot '{slotName}' not found on display.");
            return null;
        }

        // routeAnimEvents: false. the top-level animator already routes PlaySequence
        // events to the right slot by name (see HandleAnimEvent below), so a slot
        // animator subscribing independently would double-handle the same event.
        var slotAnimator = new WeaponAnimator(slotDisplay, routeAnimEvents: false);
        slotAnimators[slotName] = slotAnimator;
        return slotAnimator;
    }

    public WeaponAnimator GetSlotAnimator(string slotName)
        => slotAnimators.TryGetValue(slotName, out var a) ? a : null;

    public void UnregisterSlot(string slotName)
        => slotAnimators.Remove(slotName);

    public void RegisterAnimation(string name, bool loop) => loopFlags[name] = loop;

    public void RegisterStateOnStart(string animName, BaseWeapon.WeaponState state)
        => onStartState[animName] = state;

    public void RegisterStateOnFinish(string animName, BaseWeapon.WeaponState state)
        => onFinishState[animName] = state;

    public void RegisterSlotAnimation(string slotName, string animName, bool loop)
    {
        if (!slotAnimators.TryGetValue(slotName, out var slotAnim))
        {
            Logger.AppendWarn($"RegisterSlotAnimation: no animator for slot '{slotName}'.");
            return;
        }
        slotAnim.RegisterAnimation(animName, loop);
    }

    public void Play(string name, bool clearQueue = true, float fadeTime = 0f)
    {
        if (clearQueue) queue.Clear();
        StartAnimation(name, fadeTime);
    }

    public void PlayOnSlot(string slotName, string animName)
    {
        if (!slotAnimators.TryGetValue(slotName, out var slotAnim))
        {
            Logger.AppendWarn($"PlayOnSlot: no animator for slot '{slotName}'.");
            return;
        }
        slotAnim.Play(animName);
    }

    public void Enqueue(string name)
    {
        if (!IsPlaying) StartAnimation(name, 0f);
        else queue.Enqueue(name);
    }

    public void EnqueueOnSlot(string slotName, string animName)
    {
        if (slotAnimators.TryGetValue(slotName, out var slotAnim))
            slotAnim.Enqueue(animName);
    }

    public void ClearQueue() => queue.Clear();

    public void Update()
    {
        if (IsPlaying && !isCurrentLooping && layer.IsFinished)
        {
            NotifyFinished(CurrentAnimation);
        }

        foreach (var sa in slotAnimators.Values)
        {
            sa.Update();
        }
    }

    private void HandleAnimEvent(CAnimEvent ev)
    {
        if (ev.Type != AnimEventType.PlaySequence) return;
        if (string.IsNullOrEmpty(ev.Options)) return;

        if (string.IsNullOrEmpty(ev.Slot) || ev.Slot == "self")
        {
            Play(ev.Options);
            return;
        }

        if (slotAnimators.TryGetValue(ev.Slot, out var slotAnim))
            slotAnim.Play(ev.Options);
    }

    private void NotifyFinished(string finishedName)
    {
        IsPlaying = false;
        OnAnimationFinished?.Invoke(finishedName);

        if (onFinishState.TryGetValue(finishedName, out var finishState))
            OnStateRequested?.Invoke(finishState);

        if (queue.TryDequeue(out var next))
            StartAnimation(next, 0f);
    }

    private void StartAnimation(string name, float fadeTime)
    {
        var seq = display.Model.FindSequence(name);
        if (seq == null)
        {
            return;
        }

        bool loop = loopFlags.GetValueOrDefault(name, true);
        isCurrentLooping = loop;

        if (fadeTime > 0f) layer.Play(seq, fadeTime, loop);
        else layer.ForceReplay(seq, loop);

        CurrentAnimation = name;
        IsPlaying = true;
        OnAnimationStarted?.Invoke(name);

        if (onStartState.TryGetValue(name, out var startState))
            OnStateRequested?.Invoke(startState);
    }
}