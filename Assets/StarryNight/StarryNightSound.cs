using UnityEngine;

namespace Puzzle.StarryNight
{
    public sealed class StarryNightSound : MonoBehaviour
    {
        CharacterController player;
        AudioSource wind, details;
        AudioClip footsteps, motif, brush;
        Vector3 previousPosition;
        float stride;
        bool wasWalking;

        internal void Initialize(CharacterController walker)
        {
            player = walker;
            wind = gameObject.AddComponent<AudioSource>();
            wind.playOnAwake = false;
            wind.loop = true;
            wind.volume = .36f;
            wind.clip = Resources.Load<AudioClip>("StarryNight/Sound/Wind");
            if (wind.clip != null) wind.Play();

            details = gameObject.AddComponent<AudioSource>();
            details.playOnAwake = false;
            footsteps = Resources.Load<AudioClip>("StarryNight/Sound/Footstep");
            motif = Resources.Load<AudioClip>("StarryNight/Sound/Motif");
            brush = Resources.Load<AudioClip>("StarryNight/Sound/Brush");
        }

        internal void PlayMotif()
        {
            if (motif != null) details.PlayOneShot(motif, .55f);
        }

        internal void PlayBrush()
        {
            if (brush != null) details.PlayOneShot(brush, .65f);
        }

        void LateUpdate()
        {
            if (player == null) return;
            Vector3 position = player.transform.position;
            if (player.enabled && wasWalking && player.isGrounded && Time.deltaTime > 0f)
            {
                var offset = position - previousPosition;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance > 1f) stride = 0f;
                else
                {
                    stride += distance;
                    if (stride >= 1.42f)
                    {
                        stride -= 1.42f;
                        if (footsteps != null) details.PlayOneShot(footsteps, .28f);
                    }
                }
            }
            else stride = 0f;
            previousPosition = position;
            wasWalking = player.enabled;
        }

        void OnDisable()
        {
            if (wind != null) wind.Pause();
            if (details != null) details.Stop();
            wasWalking = false;
        }

        void OnEnable()
        {
            if (wind != null) wind.UnPause();
        }
    }
}
