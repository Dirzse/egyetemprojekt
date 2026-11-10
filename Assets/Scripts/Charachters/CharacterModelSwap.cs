using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Replaces the player's character model (e.g. the default body -> Sara) at spawn and keeps everything that was
// attached to the old one: weapons in the hand, sockets, the knife strap, holstered weapons.
//  - Attachments move to the matching bone of the new model: the same Humanoid bone (works across different
//    rigs, e.g. Mixamo and Unreal skeletons), or else the bone with the same name.
//  - Every script field under the player that pointed into the old model (Animator, sockets, bones) is
//    redirected to the new model, so nothing has to be re-assigned by hand.
//  - The new model gets the old model's Animator Controller and settings.
public static class CharacterModelSwap
{
    const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // owner = the Player, oldModel = the model under it, newModelPrefab = the character's model. Returns the new model.
    public static Transform Swap(Transform owner, Transform oldModel, GameObject newModelPrefab, float scale = 1f)
    {
        if (owner == null || oldModel == null || newModelPrefab == null) return oldModel;

        // 1. Spawn the new model where the old one stands
        GameObject newGo = Object.Instantiate(newModelPrefab, oldModel.parent);
        newGo.name = newModelPrefab.name;
        Transform newModel = newGo.transform;
        newModel.localPosition = oldModel.localPosition;
        newModel.localRotation = oldModel.localRotation;
        newModel.localScale = oldModel.localScale * scale;
        SetLayerRecursively(newModel, oldModel.gameObject.layer);

        // 2. Animator: same controller and settings, the new model's own avatar
        Animator oldAnimator = oldModel.GetComponentInChildren<Animator>(true);
        Animator newAnimator = newModel.GetComponentInChildren<Animator>(true);
        if (newAnimator == null) newAnimator = newGo.AddComponent<Animator>();
        if (oldAnimator != null)
        {
            newAnimator.runtimeAnimatorController = oldAnimator.runtimeAnimatorController;
            newAnimator.applyRootMotion = oldAnimator.applyRootMotion;
            newAnimator.updateMode = oldAnimator.updateMode;
            newAnimator.cullingMode = oldAnimator.cullingMode;
        }
        if (newAnimator.avatar == null)
            Debug.LogWarning("CharacterModelSwap: '" + newModelPrefab.name + "' has no Avatar. Set its Rig to Humanoid in the import settings.");

        // 3. Which old bone is which new bone
        Dictionary<Transform, Transform> boneMap = BuildBoneMap(oldModel, newModel, oldAnimator, newAnimator);

        // 4. Find what was attached to the old model, then move it to the new bones
        HashSet<Transform> skeleton = CollectSkeleton(oldModel, boneMap);
        HashSet<Transform> referenced = CollectReferencedTransforms(owner, oldModel);
        List<Transform> attachments = new List<Transform>();
        foreach (Transform child in oldModel) FindAttachments(child, skeleton, referenced, attachments);

        foreach (Transform attachment in attachments)
        {
            Transform target = FindNewParent(attachment.parent, oldModel, newModel, boneMap);
            attachment.SetParent(target, false);   // keeps its offset relative to the bone
        }

        // 5. Point every script reference from the old model to the new one
        Dictionary<Object, Object> objectMap = BuildObjectMap(boneMap);
        if (oldAnimator != null) objectMap[oldAnimator] = newAnimator;
        RemapReferences(owner, oldModel, objectMap);

        // 6. Remove the old model (deactivate now, so nothing finds it during this frame)
        oldModel.gameObject.SetActive(false);
        Object.Destroy(oldModel.gameObject);
        return newModel;
    }

    // ---------------------------------------------------------------- bones

    static Dictionary<Transform, Transform> BuildBoneMap(Transform oldModel, Transform newModel, Animator oldAnimator, Animator newAnimator)
    {
        Dictionary<Transform, Transform> map = new Dictionary<Transform, Transform>();
        map[oldModel] = newModel;

        // Humanoid bones first: matches bones even when the two rigs use different names
        if (oldAnimator != null && newAnimator != null && oldAnimator.isHuman && newAnimator.isHuman)
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform o = oldAnimator.GetBoneTransform(bone);
                Transform n = newAnimator.GetBoneTransform(bone);
                if (o != null && n != null && !map.ContainsKey(o)) map[o] = n;
            }
        }

        // Then everything else by name
        Dictionary<string, Transform> newByName = new Dictionary<string, Transform>();
        foreach (Transform t in newModel.GetComponentsInChildren<Transform>(true))
            if (!newByName.ContainsKey(t.name)) newByName[t.name] = t;

        foreach (Transform t in oldModel.GetComponentsInChildren<Transform>(true))
            if (!map.ContainsKey(t) && newByName.TryGetValue(t.name, out Transform n)) map[t] = n;

        return map;
    }

    // The old model's own parts: its skinned meshes, their bones and everything above them, plus matched bones
    static HashSet<Transform> CollectSkeleton(Transform oldModel, Dictionary<Transform, Transform> boneMap)
    {
        HashSet<Transform> set = new HashSet<Transform> { oldModel };
        foreach (SkinnedMeshRenderer smr in oldModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            AddWithAncestors(set, smr.transform, oldModel);
            AddWithAncestors(set, smr.rootBone, oldModel);
            foreach (Transform bone in smr.bones) AddWithAncestors(set, bone, oldModel);
        }
        foreach (Transform t in boneMap.Keys) set.Add(t);
        return set;
    }

    static void AddWithAncestors(HashSet<Transform> set, Transform t, Transform stop)
    {
        while (t != null && t != stop && set.Add(t)) t = t.parent;
    }

    // Something added by you: not part of the model, and it has a renderer, a script, or a script points to it
    static void FindAttachments(Transform t, HashSet<Transform> skeleton, HashSet<Transform> referenced, List<Transform> result)
    {
        bool isAttachment = !skeleton.Contains(t) &&
                            (t.GetComponent<ModelAttachment>() != null ||
                             t.GetComponent<Renderer>() != null ||
                             t.GetComponent<MonoBehaviour>() != null ||
                             referenced.Contains(t));
        if (isAttachment)
        {
            result.Add(t);   // its children move with it
            return;
        }
        foreach (Transform child in t) FindAttachments(child, skeleton, referenced, result);
    }

    // The closest old parent that has a matching new bone (falls back to the new model's root)
    static Transform FindNewParent(Transform oldParent, Transform oldModel, Transform newModel, Dictionary<Transform, Transform> boneMap)
    {
        for (Transform t = oldParent; t != null; t = t.parent)
        {
            if (boneMap.TryGetValue(t, out Transform n)) return n;
            if (t == oldModel) break;
        }
        Debug.LogWarning("CharacterModelSwap: no matching bone for '" + oldParent.name + "', attached to the model root instead.");
        return newModel;
    }

    // ---------------------------------------------------------------- references

    static Dictionary<Object, Object> BuildObjectMap(Dictionary<Transform, Transform> boneMap)
    {
        Dictionary<Object, Object> map = new Dictionary<Object, Object>();
        foreach (KeyValuePair<Transform, Transform> pair in boneMap)
        {
            map[pair.Key] = pair.Value;
            map[pair.Key.gameObject] = pair.Value.gameObject;
            foreach (Component c in pair.Key.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                Component match = pair.Value.GetComponent(c.GetType());
                if (match != null) map[c] = match;
            }
        }
        return map;
    }

    static HashSet<Transform> CollectReferencedTransforms(Transform owner, Transform oldModel)
    {
        HashSet<Transform> set = new HashSet<Transform>();
        foreach (MonoBehaviour behaviour in owner.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            ForEachObjectField(behaviour, (value, _) =>
            {
                Transform t = ToTransform(value);
                if (t != null && t.IsChildOf(oldModel)) set.Add(t);
                return value;
            });
        }
        return set;
    }

    static void RemapReferences(Transform owner, Transform oldModel, Dictionary<Object, Object> map)
    {
        foreach (MonoBehaviour behaviour in owner.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour.transform.IsChildOf(oldModel)) continue;   // goes away with the old model
            ForEachObjectField(behaviour, (value, fieldName) =>
            {
                if (map.TryGetValue(value, out Object replacement)) return replacement;

                Transform t = ToTransform(value);
                if (t != null && t.IsChildOf(oldModel))
                    Debug.LogWarning("CharacterModelSwap: " + behaviour.GetType().Name + "." + fieldName + " on '" + behaviour.name +
                                     "' points to '" + t.name + "' which is removed with the old model. Add a ModelAttachment to it.");
                return value;
            });
        }
    }

    // Calls visit for every Unity object stored in a field (also inside arrays and lists); visit returns the new value
    static void ForEachObjectField(MonoBehaviour behaviour, Func<Object, string, Object> visit)
    {
        for (Type type = behaviour.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(FieldFlags))
            {
                object value = field.GetValue(behaviour);
                if (value is Object obj)
                {
                    if (obj == null) continue;   // destroyed or empty
                    Object result = visit(obj, field.Name);
                    if (!ReferenceEquals(result, obj)) field.SetValue(behaviour, result);
                }
                else if (value is IList list && !(value is string))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] is Object item && item != null)
                        {
                            Object result = visit(item, field.Name + "[" + i + "]");
                            if (!ReferenceEquals(result, item)) list[i] = result;
                        }
                    }
                }
            }
        }
    }

    static Transform ToTransform(Object value)
    {
        if (value is Transform t) return t;
        if (value is GameObject go) return go.transform;
        if (value is Component c) return c.transform;
        return null;
    }

    static void SetLayerRecursively(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t) SetLayerRecursively(child, layer);
    }
}
