using UnityEngine;

// Aiming helpers that work in first and third person.
// In third person the camera is behind the player: rays from it would hit the player's own back, and melee
// reach measured from it would be far too short. So the aim point skips the player, and melee / pickups
// start from the player's eyes instead of the camera.
public static class AimUtil
{
    static readonly RaycastHit[] hits = new RaycastHit[32];

    public static bool IsThirdPerson(Camera camera)
    {
        if (camera == null) return false;
        ThirdPersonCamera thirdPerson = camera.GetComponent<ThirdPersonCamera>();
        return thirdPerson != null && thirdPerson.IsThirdPerson;
    }

    // Where melee hits and pickups start: the camera in first person, the eyes in third person
    public static Vector3 Origin(Camera camera)
    {
        ThirdPersonCamera thirdPerson = camera.GetComponent<ThirdPersonCamera>();
        return thirdPerson != null && thirdPerson.IsThirdPerson ? thirdPerson.PivotWorld : camera.transform.position;
    }

    // The point under the crosshair, skipping the owner's colliders (and triggers)
    public static Vector3 AimPoint(Camera camera, Transform owner, int mask, float maxDistance)
    {
        Transform view = camera.transform;
        int count = Physics.RaycastNonAlloc(view.position, view.forward, hits, maxDistance, mask, QueryTriggerInteraction.Ignore);
        float best = maxDistance;
        for (int i = 0; i < count; i++)
        {
            if (owner != null && hits[i].transform.IsChildOf(owner)) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return view.position + view.forward * best;
    }

    // Direction from the origin to what the crosshair points at
    public static Vector3 Direction(Camera camera, Vector3 origin, Transform owner, int mask, float maxDistance = 100f)
    {
        Vector3 dir = AimPoint(camera, owner, mask, maxDistance) - origin;
        if (dir.sqrMagnitude < 0.0001f) return camera.transform.forward;
        dir.Normalize();
        // Something right in front of the camera (behind the player): just use the view direction
        return Vector3.Dot(dir, camera.transform.forward) > 0.2f ? dir : camera.transform.forward;
    }

    // Melee direction: the view direction with its up / down tilt limited, so looking down at the ground
    // (third person camera above the player) still swings forward into the enemy instead of into the floor
    public static Vector3 MeleeDirection(Camera camera, float maxTilt = 25f)
    {
        Vector3 forward = camera.transform.forward;
        Vector3 flat = new Vector3(forward.x, 0f, forward.z);
        if (flat.sqrMagnitude < 0.0001f) flat = Vector3.ProjectOnPlane(camera.transform.up, Vector3.up);
        flat.Normalize();

        float tilt = Mathf.Clamp(Vector3.SignedAngle(flat, forward, Vector3.Cross(Vector3.up, flat)), -maxTilt, maxTilt);
        return Quaternion.AngleAxis(tilt, Vector3.Cross(Vector3.up, flat)) * flat;
    }
}