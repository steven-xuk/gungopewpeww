using UnityEngine;

public class openingdoor : MonoBehaviour
{
    // Delay before rotating (seconds) if no pre-open audio is assigned
    public float delaySeconds = 5f;
    // Axis to rotate around (default Y)
    public Vector3 rotationAxis = Vector3.up;
    // Angle to rotate
    public float angle = 90f;
    // Enable smooth animation instead of instant snap
    public bool smooth = false;
    // Duration of smooth rotation
    public float smoothDuration = 1f;

    // Audio that plays first; door waits for it to finish before opening.
    public AudioSource preOpenAudio; // non-loop, e.g. latch / beep
    // Audio that plays while the door is moving; starts with movement and stops when done.
    public AudioSource movingAudio; // can be looping or not; will be stopped.

    float _timer;
    bool _rotating;
    Quaternion _startRot;
    Quaternion _endRot;
    bool _rotationStarted;

    void Start()
    {
        _startRot = transform.localRotation;
        if (rotationAxis == Vector3.zero) rotationAxis = Vector3.up; // safety
        _endRot = _startRot * Quaternion.AngleAxis(angle, rotationAxis.normalized);

        // If we have a pre-open audio clip, play it now. Rotation will begin after it finishes.
        if (preOpenAudio != null && preOpenAudio.clip != null)
        {
            preOpenAudio.Play();
        }
        else
        {
            // Fall back to using delaySeconds if no pre-open audio.
            _timer = 0f;
        }
    }

    void Update()
    {
        // If rotation not yet started, decide when to begin.
        if (!_rotationStarted)
        {
            if (preOpenAudio != null && preOpenAudio.clip != null)
            {
                // Wait until the pre-open sound finishes playing.
                if (!preOpenAudio.isPlaying && preOpenAudio.time > 0f)
                {
                    BeginRotation();
                }
            }
            else
            {
                // Original delay-based logic when no pre-open sound.
                _timer += Time.deltaTime;
                if (_timer >= delaySeconds)
                {
                    BeginRotation();
                }
            }
            return; // nothing else until rotation starts
        }

        // Smooth rotation progress
        if (_rotating)
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.0001f, smoothDuration));
            transform.localRotation = Quaternion.Slerp(_startRot, _endRot, t);
            if (t >= 1f)
            {
                if (movingAudio != null && movingAudio.isPlaying)
                    movingAudio.Stop();
                enabled = false; // done
            }
        }
    }

    void BeginRotation()
    {
        _rotationStarted = true;
        if (smooth)
        {
            _rotating = true;
            _timer = 0f; // reuse for animation time
            if (movingAudio != null)
            {
                if (movingAudio.clip != null)
                {
                    // Ensure it plays from start.
                    movingAudio.Stop();
                }
                movingAudio.Play();
            }
        }
        else
        {
            // Instant rotation
            transform.localRotation = _endRot;
            // Play moving sound briefly (optional); comment out if undesired for instant snap.
            if (movingAudio != null && movingAudio.clip != null)
            {
                movingAudio.Play();
                movingAudio.Stop(); // stop immediately since movement is instant
            }
            enabled = false;
        }
    }
}
