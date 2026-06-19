// ============================================================================
// RobotAnimator.cs — Smooth visual animation for robot movement
// Handles position lerping, rotation, pick-up bob, and wall bonk.
// All methods are callback-based: they call onComplete when done.
// ============================================================================

using UnityEngine;
using System;
using System.Collections;

public class RobotAnimator : MonoBehaviour
{
    [Header("Animation Speeds")]
    [SerializeField] private float moveSpeed  = 3.0f;  // units per second
    [SerializeField] private float rotateSpeed = 5.0f;  // rotations per second factor

    [Header("Movement Polish")]
    [SerializeField] private float hopHeight  = 0.15f;  // arc height during move
    [SerializeField] private float hopFrequency = 1.0f; // one full arc per tile

    private bool isAnimating = false;
    private Animator _animator;

    /// <summary>True while any animation coroutine is running.</summary>
    public bool IsAnimating => isAnimating;

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
    }

    // ── Public Animation Methods ─────────────────────────────────────────

    /// <summary>Smoothly move to a target world position with a small hop arc.</summary>
    public void AnimateMoveTo(Vector3 targetPosition, Action onComplete)
    {
        StartCoroutine(MoveCoroutine(targetPosition, onComplete));
    }

    /// <summary>Smoothly rotate to face a new direction.</summary>
    public void AnimateRotateTo(Quaternion targetRotation, Action onComplete)
    {
        StartCoroutine(RotateCoroutine(targetRotation, onComplete));
    }

    /// <summary>Play a "pick up" bob animation (down then up).</summary>
    public void AnimatePickUp(Action onComplete)
    {
        StartCoroutine(PickUpCoroutine(onComplete));
    }

    /// <summary>Play a "bonk" animation: move slightly toward wall then bounce back.</summary>
    public void AnimateWallBonk(Vector3 wallDirection, Action onComplete)
    {
        StartCoroutine(WallBonkCoroutine(wallDirection, onComplete));
    }

    // ── Snap (no animation) ──────────────────────────────────────────────

    /// <summary>Immediately set position, cancelling any running animation.</summary>
    public void SnapToPosition(Vector3 position)
    {
        StopAllCoroutines();
        isAnimating = false;
        transform.position = position;
    }

    /// <summary>Immediately set rotation, cancelling any running animation.</summary>
    public void SnapToRotation(Quaternion rotation)
    {
        StopAllCoroutines();
        isAnimating = false;
        transform.rotation = rotation;
    }

    // ── Coroutines ───────────────────────────────────────────────────────

    private IEnumerator MoveCoroutine(Vector3 target, Action onComplete)
    {
        isAnimating = true;

        Vector3 start = transform.position;
        float distance = Vector3.Distance(
            new Vector3(start.x, 0, start.z),
            new Vector3(target.x, 0, target.z)
        );
        float duration = distance / moveSpeed;
        float elapsed = 0f;

        if (_animator != null) _animator.SetBool("IsWalking", true);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth step (ease in-out)
            float smooth = t * t * (3f - 2f * t);

            // Lerp XZ position
            Vector3 pos = Vector3.Lerp(start, target, smooth);
            
            // Stay flat on the ground (no hop)
            pos.y = target.y;

            transform.position = pos;
            yield return null;
        }

        transform.position = target;
        isAnimating = false;
        if (_animator != null) _animator.SetBool("IsWalking", false);
        onComplete?.Invoke();
    }

    private IEnumerator RotateCoroutine(Quaternion target, Action onComplete)
    {
        isAnimating = true;

        Quaternion start = transform.rotation;
        float duration = 1f / rotateSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smooth = t * t * (3f - 2f * t);

            transform.rotation = Quaternion.Slerp(start, target, smooth);
            yield return null;
        }

        transform.rotation = target;
        isAnimating = false;
        onComplete?.Invoke();
    }

    private IEnumerator PickUpCoroutine(Action onComplete)
    {
        isAnimating = true;

        Vector3 startPos = transform.position;
        float duration = 0.4f;
        float elapsed = 0f;
        float dip = 0.2f;

        if (_animator != null) _animator.SetTrigger("Interact");

        // We can still keep a tiny bob or just wait for the animation
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float yOffset = -Mathf.Sin(t * Mathf.PI) * dip;
            transform.position = new Vector3(startPos.x, startPos.y + yOffset, startPos.z);
            yield return null;
        }

        transform.position = startPos;
        isAnimating = false;
        onComplete?.Invoke();
    }

    private IEnumerator WallBonkCoroutine(Vector3 wallDir, Action onComplete)
    {
        isAnimating = true;

        Vector3 startPos = transform.position;
        Vector3 direction = wallDir.normalized;
        float duration = 0.45f;
        float maxPush = 0.3f;
        float elapsed = 0f;

        // Phase 1 (0–30%): move toward wall
        // Phase 2 (30–100%): bounce back with overshoot
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float offset;
            if (t < 0.3f)
            {
                // Move forward toward wall
                offset = Mathf.Lerp(0f, maxPush, t / 0.3f);
            }
            else
            {
                // Bounce back
                float bounceT = (t - 0.3f) / 0.7f;
                offset = Mathf.Lerp(maxPush, 0f, bounceT);
                // Add slight overshoot for "bonk" feel
                offset -= Mathf.Sin(bounceT * Mathf.PI) * 0.05f;
            }

            transform.position = startPos + direction * offset;
            yield return null;
        }

        transform.position = startPos;
        isAnimating = false;
        onComplete?.Invoke();
    }
}
