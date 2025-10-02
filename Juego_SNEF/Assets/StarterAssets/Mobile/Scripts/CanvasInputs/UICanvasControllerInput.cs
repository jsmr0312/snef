using UnityEngine;
using System.Collections.Generic;

namespace StarterAssets
{
    public class UICanvasControllerInput : MonoBehaviour
    {
        public enum RoutingMode { CurrentOnly, BroadcastAll }

        [Header("Targets (StarterAssetsInputs)")]
        [Tooltip("Lista de personajes controlables. Puedes llenarla a mano o usar Auto Discover.")]
        public List<StarterAssetsInputs> targets = new List<StarterAssetsInputs>();

        [Tooltip("Cómo enrutar las entradas: solo al activo o a todos.")]
        public RoutingMode routing = RoutingMode.CurrentOnly;

        [Header("Auto Discover (opcional)")]
        [Tooltip("Buscar automáticamente todos los StarterAssetsInputs al iniciar.")]
        public bool autoDiscoverOnAwake = true;
        [Tooltip("Si no está vacío, se prefiere como target al que tenga este tag (ej. 'Player').")]
        public string preferTag = "Player";

        [Header("Estado")]
        [SerializeField] private int currentIndex = 0;

        public StarterAssetsInputs CurrentTarget
        {
            get
            {
                if (targets == null || targets.Count == 0) return null;
                if (currentIndex < 0 || currentIndex >= targets.Count) currentIndex = 0;
                return targets[currentIndex];
            }
        }

        void Awake()
        {
            if (autoDiscoverOnAwake && (targets == null || targets.Count == 0))
                AutoDiscover();
        }

        void AutoDiscover()
        {
            targets = new List<StarterAssetsInputs>(FindObjectsOfType<StarterAssetsInputs>(true));

            // Preferir el que tenga el tag indicado, si existe
            if (!string.IsNullOrEmpty(preferTag))
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var go = targets[i] ? targets[i].gameObject : null;
                    if (go != null && go.CompareTag(preferTag))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
        }

        // --- API para tu selector de personajes ---
        public void SetTarget(StarterAssetsInputs target)
        {
            if (target == null) return;
            int idx = targets.IndexOf(target);
            if (idx == -1)
            {
                targets.Add(target);
                idx = targets.Count - 1;
            }
            currentIndex = idx;
        }

        public void SetTargetByIndex(int index)
        {
            if (targets == null || targets.Count == 0) return;
            currentIndex = Mathf.Clamp(index, 0, targets.Count - 1);
        }

        public void RegisterTarget(StarterAssetsInputs target)
        {
            if (target == null) return;
            if (!targets.Contains(target)) targets.Add(target);
        }

        public void UnregisterTarget(StarterAssetsInputs target)
        {
            if (target == null) return;
            int idx = targets.IndexOf(target);
            if (idx >= 0)
            {
                targets.RemoveAt(idx);
                if (currentIndex >= targets.Count) currentIndex = Mathf.Max(0, targets.Count - 1);
            }
        }

        // --- Enrutamiento interno ---
        private IEnumerable<StarterAssetsInputs> RouteTo()
        {
            if (routing == RoutingMode.BroadcastAll)
            {
                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] != null) yield return targets[i];
            }
            else
            {
                var t = CurrentTarget;
                if (t != null) yield return t;
            }
        }

        // --- Tus handlers UI existentes, sin cambiar la firma ---
        public void VirtualMoveInput(Vector2 virtualMoveDirection)
        {
            foreach (var t in RouteTo()) t.MoveInput(virtualMoveDirection);
        }

        public void VirtualLookInput(Vector2 virtualLookDirection)
        {
            foreach (var t in RouteTo()) t.LookInput(virtualLookDirection);
        }

        public void VirtualJumpInput(bool virtualJumpState)
        {
            foreach (var t in RouteTo()) t.JumpInput(virtualJumpState);
        }

        public void VirtualSprintInput(bool virtualSprintState)
        {
            foreach (var t in RouteTo()) t.SprintInput(virtualSprintState);
        }
    }
}
