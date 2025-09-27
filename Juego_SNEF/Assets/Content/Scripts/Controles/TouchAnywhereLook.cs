// TouchAnywhereLook.cs
using UnityEngine;
using UnityEngine.EventSystems;
using StarterAssets;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
using TouchPhaseNew = UnityEngine.InputSystem.TouchPhase;
#endif

public class TouchAnywhereLook : MonoBehaviour
{
    [Header("Refs")]
    public StarterAssetsInputs inputs;           // Asigna el StarterAssetsInputs del Player
    public GameObject lookJoystickToDisable;     // (Opcional) Joystick de look a desactivar

    [Header("Ajustes")]
    public bool onlyOnMobile = true;             // true = solo en touch
    public float pixelsToUnits = 0.02f;          // delta píxeles -> delta look
    public float sensitivityX = 1.0f;
    public float sensitivityY = 1.0f;
    public bool invertY = true;

    int _activeFingerId = -1;
    Vector2 _lastPos;

    void Awake()
    {
        if (inputs == null) inputs = GetComponentInParent<StarterAssetsInputs>();
        if (lookJoystickToDisable) lookJoystickToDisable.SetActive(false);
#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
    }

    bool IsMobileLike()
    {
        if (!onlyOnMobile) return true;
        return (Input.touchSupported && SystemInfo.deviceType != DeviceType.Desktop)
               || Application.isMobilePlatform;
    }

    void Update()
    {
        if (inputs == null || !IsMobileLike()) return;

#if ENABLE_INPUT_SYSTEM
        var touches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
        if (touches.Count == 0) { Release(); return; }

        for (int i = 0; i < touches.Count; i++)
        {
            var t = touches[i];

            if (_activeFingerId == -1)
            {
                if (t.phase == TouchPhaseNew.Began && !IsOverUI(t.touchId))
                {
                    _activeFingerId = t.touchId;
                    _lastPos = t.screenPosition;
                    break;
                }
            }
            else if (t.touchId == _activeFingerId)
            {
                if (t.phase == TouchPhaseNew.Moved || t.phase == TouchPhaseNew.Stationary)
                {
                    var delta = t.screenPosition - _lastPos;
                    _lastPos = t.screenPosition;
                    ApplyDelta(delta);
                }
                else
                {
                    Release();
                }
                break;
            }
        }
#else
        if (Input.touchCount == 0) { Release(); return; }

        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);

            if (_activeFingerId == -1)
            {
                if (t.phase == TouchPhase.Began && !IsOverUI(t.fingerId))
                {
                    _activeFingerId = t.fingerId;
                    _lastPos = t.position;
                    break;
                }
            }
            else if (t.fingerId == _activeFingerId)
            {
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    var delta = t.position - _lastPos;
                    _lastPos = t.position;
                    ApplyDelta(delta);
                }
                else
                {
                    Release();
                }
                break;
            }
        }
#endif
    }

    void ApplyDelta(Vector2 delta)
    {
        inputs.look = new Vector2(
            delta.x * pixelsToUnits * sensitivityX,
            (invertY ? -delta.y : delta.y) * pixelsToUnits * sensitivityY
        );
    }

    void Release()
    {
        inputs.look = Vector2.zero;
        _activeFingerId = -1;
    }

    bool IsOverUI(int fingerId)
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
    }
}
