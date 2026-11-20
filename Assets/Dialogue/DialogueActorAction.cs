using System.Collections;
using UnityEngine;

namespace Puzzle.Dialogue
{
    [DisallowMultipleComponent]
    public sealed class DialogueActorAction : MonoBehaviour
    {
        [SerializeField, Tooltip("Scene markers to visit in order. Keep them outside the moving character.")]
        Transform[] waypoints;
        [SerializeField, Min(0.1f)] float speed = 2f;
        [SerializeField] bool hideOnArrival;

        bool moving;

        public void Perform()
        {
            if (!isActiveAndEnabled || moving) return;
            if (waypoints is not { Length: > 0 })
            {
                if (hideOnArrival) gameObject.SetActive(false);
                return;
            }
            moving = true;
            StartCoroutine(FollowWaypoints());
        }

        IEnumerator FollowWaypoints()
        {
            foreach (var waypoint in waypoints)
            {
                if (waypoint == null) continue;
                Vector3 destination = waypoint.position;
                while (Vector3.Distance(transform.position, destination) > 0.01f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
                    yield return null;
                }
            }

            moving = false;
            if (hideOnArrival) gameObject.SetActive(false);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            moving = false;
        }
    }
}
