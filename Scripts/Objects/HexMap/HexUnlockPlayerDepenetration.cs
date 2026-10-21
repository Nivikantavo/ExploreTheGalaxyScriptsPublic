using System.Collections.Generic;
using ECM2;
using UnityEngine;

/// <summary>
/// Prevents the player from embedding into a hex that unlocks beside/above them.
/// MeshColliders on hexes are often non-convex, so Physics.ComputePenetration / ECM MTD
/// silently fail — we disable colliders during the grow animation and push out geometrically.
/// </summary>
public static class HexUnlockPlayerDepenetration
{
    private const float PenetrationContactPad = 0.02f;
    private const float HorizontalPushStep = 0.35f;
    private const float UpwardPushFraction = 0.35f;
    private const int MaxPushSteps = 24;
    private const int MaxComputePenetrationPasses = 4;

    private static readonly Collider[] OverlapBuffer = new Collider[32];
    private static readonly HashSet<Collider> ColliderSet = new HashSet<Collider>();

    public static CharacterMovement FindPlayerMovement()
    {
        Player player = Object.FindFirstObjectByType<Player>();
        if (player == null)
            return null;

        Character character = player.GetComponent<Character>();
        if (character != null && character.characterMovement != null)
            return character.characterMovement;

        return player.GetComponent<CharacterMovement>();
    }

    public static Collider[] CollectSolidColliders(GameObject root)
    {
        if (root == null)
            return System.Array.Empty<Collider>();

        Collider[] all = root.GetComponentsInChildren<Collider>(true);
        int solidCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].isTrigger)
                solidCount++;
        }

        if (solidCount == 0)
            return System.Array.Empty<Collider>();

        Collider[] solid = new Collider[solidCount];
        int write = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].isTrigger)
                solid[write++] = all[i];
        }

        return solid;
    }

    public static void StoreEnabledStates(Collider[] colliders, bool[] enabledStates)
    {
        if (colliders == null || enabledStates == null)
            return;

        int count = Mathf.Min(colliders.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
            enabledStates[i] = colliders[i] != null && colliders[i].enabled;
    }

    public static void RestoreEnabledStates(Collider[] colliders, bool[] enabledStates)
    {
        if (colliders == null || enabledStates == null)
            return;

        int count = Mathf.Min(colliders.Length, enabledStates.Length);
        for (int i = 0; i < count; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabledStates[i];
        }
    }

    public static void SetCollidersEnabled(Collider[] colliders, bool enabled)
    {
        if (colliders == null)
            return;

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    /// <summary>
    /// Pushes the player out of the given hex colliders. Prefer geometric push because
    /// non-convex MeshColliders make ComputePenetration return false.
    /// </summary>
    public static void DepenetrateFromHex(CharacterMovement movement, Vector3 hexWorldCenter, Collider[] colliders)
    {
        if (movement == null || colliders == null || colliders.Length == 0)
            return;

        TryComputePenetrationPush(movement, colliders);

        if (!OverlapsAny(movement, colliders))
        {
            movement.ForceResolveOverlaps();
            return;
        }

        PushAwayFromCenter(movement, hexWorldCenter, colliders);
        movement.ForceResolveOverlaps();
    }

    private static void TryComputePenetrationPush(CharacterMovement movement, Collider[] colliders)
    {
        CapsuleCollider capsule = GetCapsule(movement);
        if (capsule == null)
            return;

        Vector3 position = movement.position;
        Quaternion rotation = movement.rotation;
        bool moved = false;

        for (int pass = 0; pass < MaxComputePenetrationPasses; pass++)
        {
            bool resolvedAny = false;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider other = colliders[i];
                if (other == null || !other.enabled || other.isTrigger)
                    continue;

                if (!Physics.ComputePenetration(
                        capsule, position, rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                    continue;

                position += direction * (distance + PenetrationContactPad);
                resolvedAny = true;
                moved = true;
            }

            if (!resolvedAny)
                break;
        }

        if (moved)
            movement.SetPosition(position, true);
    }

    private static void PushAwayFromCenter(CharacterMovement movement, Vector3 hexWorldCenter, Collider[] colliders)
    {
        Vector3 start = movement.position;
        Vector3 away = start - hexWorldCenter;
        away.y = 0f;

        if (away.sqrMagnitude < 0.0001f)
        {
            Vector3 forward = movement.transform.forward;
            forward.y = 0f;
            away = forward.sqrMagnitude > 0.0001f ? forward : Vector3.forward;
        }

        away.Normalize();

        float maxPush = EstimateMaxPush(colliders, movement.radius);
        Vector3 up = movement.transform.up;

        for (int step = 1; step <= MaxPushSteps; step++)
        {
            float distance = Mathf.Min(HorizontalPushStep * step, maxPush);
            Vector3 candidate = start + away * distance + up * (distance * UpwardPushFraction);
            movement.SetPosition(candidate, true);

            if (!OverlapsAny(movement, colliders))
                return;
        }

        movement.SetPosition(start + away * maxPush, true);
    }

    private static float EstimateMaxPush(Collider[] colliders, float capsuleRadius)
    {
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (!hasBounds)
            return 4f + capsuleRadius;

        float horizontal = Mathf.Max(bounds.extents.x, bounds.extents.z);
        return horizontal + capsuleRadius + 1.5f;
    }

    private static bool OverlapsAny(CharacterMovement movement, Collider[] colliders)
    {
        ColliderSet.Clear();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
                ColliderSet.Add(colliders[i]);
        }

        if (ColliderSet.Count == 0)
            return false;

        if (!TryGetCapsuleWorldEnds(movement, out Vector3 bottom, out Vector3 top, out float radius))
            return false;

        int count = Physics.OverlapCapsuleNonAlloc(
            bottom, top, radius + PenetrationContactPad, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = OverlapBuffer[i];
            if (hit != null && ColliderSet.Contains(hit))
                return true;
        }

        return false;
    }

    private static CapsuleCollider GetCapsule(CharacterMovement movement)
    {
        CapsuleCollider capsule = movement.collider as CapsuleCollider;
        if (capsule == null)
            capsule = movement.GetComponent<CapsuleCollider>();
        return capsule;
    }

    private static bool TryGetCapsuleWorldEnds(
        CharacterMovement movement, out Vector3 bottom, out Vector3 top, out float radius)
    {
        bottom = default;
        top = default;
        radius = movement.radius;

        CapsuleCollider capsule = GetCapsule(movement);
        if (capsule == null)
            return false;

        Vector3 position = movement.position;
        Quaternion rotation = movement.rotation;
        Vector3 up = rotation * Vector3.up;
        float height = Mathf.Max(movement.height, radius * 2f);
        float half = Mathf.Max(height * 0.5f - radius, 0f);
        Vector3 center = position + rotation * capsule.center;

        bottom = center - up * half;
        top = center + up * half;
        return true;
    }
}
