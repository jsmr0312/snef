using UnityEngine;

public class ClothingBinder : MonoBehaviour
{
    [Header("Referencia al personaje base")]
    public Transform boneRoot; // El transform del esqueleto del personaje (ej: "Skeleton")
    public SkinnedMeshRenderer clothingRenderer; // SkinnedMeshRenderer de la prenda original (ej: de un prefab)

    void Start()
    {
        if (boneRoot == null || clothingRenderer == null)
        {
            Debug.LogWarning("Faltan referencias en el ClothingBinder.");
            return;
        }

        // Obtener todos los huesos del personaje por nombre
        Transform[] characterBones = boneRoot.GetComponentsInChildren<Transform>(true);

        // Crear nuevo array de huesos para la prenda
        Transform[] newBones = new Transform[clothingRenderer.bones.Length];

        for (int i = 0; i < clothingRenderer.bones.Length; i++)
        {
            string boneName = clothingRenderer.bones[i].name;
            foreach (Transform t in characterBones)
            {
                if (t.name == boneName)
                {
                    newBones[i] = t;
                    break;
                }
            }

            if (newBones[i] == null)
            {
                Debug.LogWarning("No se encontró el hueso: " + boneName);
            }
        }

        // Asignar los huesos correctos
        clothingRenderer.bones = newBones;

        // Asignar el root bone del personaje
        clothingRenderer.rootBone = FindMatchingBone(clothingRenderer.rootBone.name, characterBones);
    }

    Transform FindMatchingBone(string boneName, Transform[] characterBones)
    {
        foreach (Transform t in characterBones)
        {
            if (t.name == boneName)
                return t;
        }
        return characterBones[0]; // fallback
    }
}