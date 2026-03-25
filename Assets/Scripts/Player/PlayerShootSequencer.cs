using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives the three-step ranged/spell sequence on the character's Animator.
///
///   Player presses [Shoot]
///       ↓
///   shootPrepare  ──(prepareDelay)──► shootCharge  ──(chargeDelay)──► shootFire
///                                                                          ↓ (fireEventTime)
///                                                                    OnFireEvent
///
/// Attach this component to the player GameObject.
/// Assign the matching PlayerAnimationData asset and the Animator reference.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerShootSequencer : MonoBehaviour
{
    [Header("Data")]
    public PlayerAnimationData animData;

    [Header("References")]
    public Animator animator;

    [Header("Events")]
    /// <summary>Fired at fireEventTime into the shoot animation — spawn your projectile here.</summary>
    public UnityEvent OnFireEvent;

    // ── State ──────────────────────────────────────────────────────────────────
    public enum ShootState { Idle, Preparing, Charging, Firing }
    public ShootState CurrentState { get; private set; } = ShootState.Idle;

    private Coroutine _sequenceCoroutine;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Call this when the player presses the shoot button.</summary>
    public void BeginShootSequence()
    {
        if (animData == null)
        {
            Debug.LogWarning("[ShootSequencer] No PlayerAnimationData assigned.", this);
            return;
        }

        // If already in a sequence, interrupt and restart
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(ShootSequence());
    }

    /// <summary>Cancels an in-progress sequence (e.g. player was hit).</summary>
    public void CancelShootSequence()
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }
        CurrentState = ShootState.Idle;
    }

    // ── Sequence coroutine ─────────────────────────────────────────────────────
    private IEnumerator ShootSequence()
    {
        // Step 1 — Prepare (raise weapon / aim / begin casting)
        CurrentState = ShootState.Preparing;
        PlayClip(animData.shootPrepare);
        yield return new WaitForSeconds(animData.prepareDelay);

        // Step 2 — Charge (hold aim / reload / channel spell)
        if (animData.shootCharge != null && animData.chargeDelay > 0f)
        {
            CurrentState = ShootState.Charging;
            PlayClip(animData.shootCharge);
            yield return new WaitForSeconds(animData.chargeDelay);
        }

        // Step 3 — Fire (release arrow / shoot bolt / cast spell)
        CurrentState = ShootState.Firing;
        PlayClip(animData.shootFire);

        // Wait for the fire event moment, then raise the event
        yield return new WaitForSeconds(animData.fireEventTime);
        OnFireEvent?.Invoke();

        // Wait for the fire clip to finish before returning to Idle
        float clipLength = animData.shootFire != null ? animData.shootFire.length : 0.5f;
        float remaining  = Mathf.Max(0f, clipLength - animData.fireEventTime);
        yield return new WaitForSeconds(remaining);

        CurrentState = ShootState.Idle;
        _sequenceCoroutine = null;

        // Return to idle animation
        PlayClip(animData.armedIdle);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private void PlayClip(AnimationClip clip)
    {
        if (clip == null || animator == null) return;

        // Uses the simple legacy/direct-play path; works without a pre-built controller.
        // For a full Animator Controller setup, replace with animator.Play(stateName).
        animator.Play(clip.name, 0, 0f);
    }

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }
}
