using UnityEngine;
using StarterAssets;
using System.Collections.Generic;
using System.Reflection;

public class FallRespawner : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Tag del trigger que provoca el respawn")]
    public string deathTag = "Death";

    [Header("Avatares")]
    [Tooltip("Padre que contiene todos los avatares (Avatar1..Avatar12). Si se deja vacío, usa este mismo GameObject.")]
    public Transform avatarsRoot;

    [Tooltip("Punto opcional para respawn común. Si es null, cada avatar vuelve a su posición inicial.")]
    public Transform customSpawnPoint;

    [Tooltip("Lista opcional manual. Si está vacía, se detectan automáticamente CharacterController en hijos de avatarsRoot.")]
    public CharacterController[] targets;

    struct AvatarInfo
    {
        public CharacterController cc;
        public Transform t;
        public Vector3 startPos;
        public Component mini2D;     // MiniGame2DController (si existe)
        public Component tpc;        // ThirdPersonController (si existe)
    }

    readonly List<AvatarInfo> _avatars = new List<AvatarInfo>();

    void Awake()
    {
        var root = avatarsRoot ? avatarsRoot : transform;

        if (targets == null || targets.Length == 0)
            targets = root.GetComponentsInChildren<CharacterController>(true);

        _avatars.Clear();
        foreach (var cc in targets)
        {
            if (!cc) continue;
            var go = cc.gameObject;

            _avatars.Add(new AvatarInfo
            {
                cc = cc,
                t = go.transform,
                startPos = go.transform.position,
                mini2D = go.GetComponent("MiniGame2DController"),
                tpc = go.GetComponent<ThirdPersonController>()
            });
        }

        if (_avatars.Count == 0)
            Debug.LogWarning("[FallRespawner] No se encontraron CharacterController en hijos.");
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(deathTag))
            Respawn(); // Solo activos por defecto
    }

    /// <summary>Respawnea el/los avatar(es) activo(s) en la jerarquía.</summary>
    public void Respawn()
    {
        bool any = false;
        for (int i = 0; i < _avatars.Count; i++)
        {
            var a = _avatars[i];
            if (!a.cc || !a.t || !a.cc.gameObject.activeInHierarchy) continue;
            TeleportAndReset(a);
            any = true;
        }
        if (!any) Debug.LogWarning("[FallRespawner] No hay avatares activos para respawnear.");
    }

    /// <summary>Respawnea todos los avatares (activos o inactivos).</summary>
    public void RespawnAll()
    {
        foreach (var a in _avatars)
        {
            if (!a.cc || !a.t) continue;
            TeleportAndReset(a);
        }
    }

    void TeleportAndReset(AvatarInfo a)
    {
        bool prev = a.cc.enabled;
        a.cc.enabled = false;

        Vector3 targetPos = customSpawnPoint ? customSpawnPoint.position : a.startPos;
        a.t.position = targetPos;

        // Reset vertical velocity en MiniGame2DController o ThirdPersonController (ambos tienen _verticalVelocity privado)
        TryResetVertical(a.mini2D, "_verticalVelocity");
        TryResetVertical(a.tpc, "_verticalVelocity");

        a.cc.enabled = prev;
    }

    static void TryResetVertical(Component comp, string fieldName)
    {
        if (comp == null) return;
        var f = comp.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(float))
            f.SetValue(comp, 0f);
    }
}
