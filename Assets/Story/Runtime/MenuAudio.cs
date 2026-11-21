using UnityEngine;

namespace Puzzle
{
    internal enum MenuCue { Select, Confirm, Back }

    internal static class MenuAudio
    {
        static AudioSource player;
        static AudioClip select, confirm, back;

        internal static void Play(MenuCue cue)
        {
            if (player == null)
            {
                var owner = new GameObject("Menu audio");
                Object.DontDestroyOnLoad(owner);
                player = owner.AddComponent<AudioSource>();
                player.playOnAwake = false;
                player.spatialBlend = 0f;
                player.ignoreListenerPause = true;
                select = Resources.Load<AudioClip>("MenuAudio/Select");
                confirm = Resources.Load<AudioClip>("MenuAudio/Confirm");
                back = Resources.Load<AudioClip>("MenuAudio/Back");
            }

            (player.clip, player.volume) = cue switch
            {
                MenuCue.Confirm => (confirm, .26f),
                MenuCue.Back => (back, .15f),
                _ => (select, .16f)
            };
            if (player.clip != null) player.Play();
        }
    }
}
