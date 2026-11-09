using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlotsamTutor
{
    public sealed partial class TutorPanel
    {
        private static bool synchronizingSail;
        private static readonly int MovingAnimation = Animator.StringToHash("IsMoving");
        private readonly Dictionary<Animator, bool> sailingAnimators = new Dictionary<Animator, bool>();
        private readonly Dictionary<Transform, Quaternion> sailingPropellers = new Dictionary<Transform, Quaternion>();
        private float propellerAngle;

        private void StartSailAnimations()
        {
            propellerAngle = 0f;
            foreach (var building in Community.PlayerCommunity.Buildables)
            {
                if (building == null || building.BuildPhase != BuildPhase.Finished ||
                    !building.TryGetComponent<Engine>(out var engine) ||
                    (engine != Community.PlayerCommunity.Engine && !engine.IsEnabled()) ||
                    building.SpawnedVisual == null)
                    continue;
                foreach (var rotor in building.SpawnedVisual.GetComponentsInChildren<Transform>())
                {
                    if (rotor.name.IndexOf("Propell", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        rotor.name.IndexOf("_LOD", StringComparison.OrdinalIgnoreCase) < 0)
                        sailingPropellers[rotor] = rotor.localRotation;
                }
                foreach (var animator in building.SpawnedVisual.GetComponentsInChildren<Animator>())
                {
                    if (animator.runtimeAnimatorController == null || sailingAnimators.ContainsKey(animator))
                        continue;
                    foreach (var parameter in animator.parameters)
                    {
                        if (parameter.nameHash != MovingAnimation || parameter.type != AnimatorControllerParameterType.Bool)
                            continue;
                        sailingAnimators.Add(animator, animator.GetBool(MovingAnimation));
                        animator.SetBool(MovingAnimation, true);
                        break;
                    }
                }
            }
        }

        private void AdvanceSailPropellers(float distance)
        {
            propellerAngle = Mathf.Repeat(propellerAngle + distance * 72f, 360f);
            var rotation = Quaternion.AngleAxis(propellerAngle, Vector3.forward);
            foreach (var rotor in sailingPropellers)
                if (rotor.Key != null)
                    rotor.Key.localRotation = rotor.Value * rotation;
        }

        private void StopSailAnimations()
        {
            foreach (var state in sailingAnimators)
                if (state.Key != null && state.Key.runtimeAnimatorController != null)
                    state.Key.SetBool(MovingAnimation, state.Value);
            sailingAnimators.Clear();
            foreach (var rotor in sailingPropellers)
                if (rotor.Key != null)
                    rotor.Key.localRotation = rotor.Value;
            sailingPropellers.Clear();
        }
    }
}
