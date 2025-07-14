using StarterAssets;
using UnityEngine;

public class FallRespawner : MonoBehaviour
{
    [Tooltip("Tag del trigger que reinicia al jugador")]
    public string deathTag = "Death";

    private Vector3 _startPosition;
    private CharacterController _controller;
    private MiniGame2DController _movement;

    void Awake()
    {
        _startPosition = transform.position;
        _controller = GetComponent<CharacterController>();
        _movement = GetComponent<MiniGame2DController>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(deathTag))
            Respawn();
    }

    public void Respawn()
    {
        // Deshabilita el CharacterController para teletransportar sin interferencias
        _controller.enabled = false;

        // Reinicia posición y velocidad vertical
        transform.position = _startPosition;
        if (_movement != null)
            typeof(MiniGame2DController)
              .GetField("_verticalVelocity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
              ?.SetValue(_movement, 0f);

        // Reactiva el CharacterController
        _controller.enabled = true;
    }
}
