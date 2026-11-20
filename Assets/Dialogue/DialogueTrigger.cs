using System;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class DialogueTrigger : MonoBehaviour
    {
        enum Activation { Manual, InteractNearby, EnterRange }

        [SerializeField] DialoguePlayer player;
        [SerializeField] DialogueConversation conversation;
        [SerializeField] Activation activation = Activation.InteractNearby;
        [SerializeField, Tooltip("Assign the player collider for proximity modes. A disabled collider cannot activate this trigger.")]
        Collider actor;
        [SerializeField, Min(0.1f)] float range = 3f;
        [SerializeField, Tooltip("After a completed conversation, disable this trigger for the rest of the scene visit.")]
        bool once;
        [SerializeField, Tooltip("Optional appearance override for this trigger.")] DialogueAppearance appearance;
        [SerializeField] UnityEvent onCompleted;

        bool completed;
        bool playedOnEntry;
        string entryNode;
        Action afterDialogue;

        internal void Initialize(DialoguePlayer dialogue, Collider interactingActor, DialogueConversation asset,
            float interactionRange = 3f)
        {
            player = dialogue;
            actor = interactingActor;
            conversation = asset;
            range = interactionRange;
        }

        internal void SetInteraction(string nodeId, Action onFinished)
        {
            entryNode = nodeId;
            afterDialogue = onFinished;
            enabled = !string.IsNullOrEmpty(nodeId);
        }

        void OnEnable()
        {
            if (player != null) player.Register(this);
        }

        void OnDisable()
        {
            if (player != null) player.Unregister(this);
            playedOnEntry = false;
        }

        // Assign this method to a button, puzzle event, or cutscene signal.
        public void Play() => TryPlay();

        public void PlayFromNode(string nodeId) => TryPlay(nodeId);

        internal void PlayOnEntry()
        {
            playedOnEntry = true;
            TryPlay();
        }

        internal bool TryPlay(string nodeId = null)
        {
            if (!isActiveAndEnabled || (once && completed) || player == null) return false;
            if (!player.TryPlay(conversation, appearance, HandleCompleted, nodeId ?? entryNode)) return false;
            playedOnEntry = true;
            return true;
        }

        internal bool TryGetDistance(out float distance, out bool automatic)
        {
            automatic = activation == Activation.EnterRange;
            distance = float.PositiveInfinity;
            if (activation == Activation.Manual || conversation == null || (once && completed)) return false;

            if (actor != null && actor.enabled && actor.gameObject.activeInHierarchy)
                distance = (actor.transform.position - transform.position).sqrMagnitude;

            if (distance > range * range)
            {
                playedOnEntry = false;
                return false;
            }
            return !automatic || !playedOnEntry;
        }

        void HandleCompleted()
        {
            completed = true;
            onCompleted?.Invoke();
            afterDialogue?.Invoke();
        }
    }
}
