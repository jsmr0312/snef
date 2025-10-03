using UnityEngine;

public class MusicPlayer : MonoBehaviour
{
    public AudioClip musicClip;
    private AudioSource source;

    void Awake()
    {
        // Evita duplicados si persiste entre escenas
        if (FindObjectsOfType<MusicPlayer>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = musicClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        source.volume = 0.6f;
        source.Play();
    }
}
