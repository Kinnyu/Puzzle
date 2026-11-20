using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;

namespace Puzzle.Story
{
    public sealed class StarryNightChapterTwo : MonoBehaviour
    {
        internal const string ChapterId = "chapter-2";
        const string ReturnCheckpoint = "miner-helped";
        const float TillingReach = 0.85f;
        const float TillingVerticalReach = 0.8f;

        enum Stage { Vincent, Miner, Farmer, Till, FarmReward, Deliver, Return, Finished }

        [SerializeField] StarryNightWalk walk;
        [SerializeField] DialoguePlayer dialogue;
        [SerializeField] GameObject chapterOneVincent;
        [SerializeField] DialogueConversation script;
        [SerializeField] ChapterOpeningText opening;
        [SerializeField] ChapterOpeningText retry;
        [SerializeField] Sprite vincentSprite;
        [SerializeField] Sprite minerSprite;
        [SerializeField] Sprite farmerSprite;
        [SerializeField] Sprite theoSprite;
        [SerializeField] Vector3 church = new(5, 0, 2);
        [SerializeField] Vector3 mine = new(34, 0, 0);
        [SerializeField] Vector3 farm = new(36, 0, 12);
        [SerializeField] Vector3 road = new(21, 0, 0);
        [SerializeField] Collider[] groundSurfaces;

        readonly MeshRenderer[] rows = new MeshRenderer[3];
        readonly bool[] tilled = new bool[3];
        StorySceneProgress progress;
        DialogueTrigger vincent;
        DialogueTrigger miner;
        DialogueTrigger farmer;
        DialogueTrigger theo;
        Material drySoil;
        Material tilledSoil;
        Stage stage;
        bool hasLetter;
        int tilledCount;

        internal void Begin(StorySceneProgress scene, string checkpoint, bool continuing = false)
        {
            progress = scene;
            if (chapterOneVincent != null) chapterOneVincent.SetActive(false);
            SpawnActors();

            if (checkpoint == ReturnCheckpoint)
            {
                ReturnToRoad();
                return;
            }

            SetStage(Stage.Vincent);
            if (continuing)
                dialogue.ShowOpening(opening, StartAtChurch);
            else
            {
                StartAtChurch();
                dialogue.ShowOpening(opening);
            }
        }

        void StartAtChurch() => MovePlayer(church + Vector3.back * 3.5f, church);

        void SpawnActors()
        {
            vincent = SpawnActor("Vincent - Chapter 2", vincentSprite, church);
            miner = SpawnActor("Miner", minerSprite, mine);
            farmer = SpawnActor("Farmer", farmerSprite, farm);
            theo = SpawnActor("Theo", theoSprite, road);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            drySoil = new Material(shader) { color = new Color(0.46f, 0.28f, 0.14f) };
            tilledSoil = new Material(shader) { color = new Color(0.20f, 0.11f, 0.07f) };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = GameObject.CreatePrimitive(PrimitiveType.Cube);
                row.name = $"Field row {i + 1}";
                row.transform.SetParent(transform);
                var position = farm + new Vector3(i * 1.7f, 0, 2.5f);
                row.transform.position = Ground(position) + Vector3.up * 0.035f;
                row.transform.localScale = new Vector3(1.1f, 0.09f, 2.2f);
                row.GetComponent<Collider>().enabled = false;
                rows[i] = row.GetComponent<MeshRenderer>();
                rows[i].sharedMaterial = drySoil;
            }
        }

        DialogueTrigger SpawnActor(string actorName, Sprite sprite, Vector3 position)
        {
            var character = new GameObject(actorName);
            character.SetActive(false);
            character.transform.SetParent(transform);
            character.transform.position = Ground(position);
            var renderer = character.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.86f, 0.88f, 0.97f);
            character.AddComponent<CharacterBillboard>();
            var trigger = character.AddComponent<DialogueTrigger>();
            trigger.Initialize(dialogue, walk.Player, script);
            trigger.SetInteraction(null, null);
            character.SetActive(true);
            return trigger;
        }

        internal Vector3 Ground(Vector3 position)
        {
            float surfaceHeight = float.NegativeInfinity;
            foreach (var surface in groundSurfaces ?? System.Array.Empty<Collider>())
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                var bounds = surface.bounds;
                var above = new Vector3(position.x, bounds.max.y + 1f, position.z);
                if (surface.Raycast(new Ray(above, Vector3.down), out var hit, bounds.size.y + 2f))
                    surfaceHeight = Mathf.Max(surfaceHeight, hit.point.y);
            }
            if (!float.IsNegativeInfinity(surfaceHeight)) position.y = surfaceHeight;
            return position;
        }

        void MovePlayer(Vector3 from, Vector3 toward)
        {
            walk.Player.enabled = false;
            Vector3 heading = toward - from;
            heading.y = 0f;
            walk.EnterWalkingAt(Ground(from) + Vector3.up * 0.04f, Quaternion.LookRotation(heading));
        }

        void SetStage(Stage next)
        {
            stage = next;
            vincent.SetInteraction(null, null);
            miner.SetInteraction(null, null);
            farmer.SetInteraction(null, null);
            theo.SetInteraction(null, null);
            theo.gameObject.SetActive(stage == Stage.Return && !hasLetter);
            switch (stage)
            {
                case Stage.Vincent:
                    vincent.SetInteraction("request", () => SetStage(Stage.Miner));
                    break;
                case Stage.Miner:
                    miner.SetInteraction("miner", () => SetStage(Stage.Farmer));
                    break;
                case Stage.Farmer:
                    farmer.SetInteraction("farm", () => SetStage(Stage.Till));
                    break;
                case Stage.FarmReward:
                    farmer.SetInteraction("reward", () =>
                    {
                        progress.Notify(dialogue.Local("獲得　馬鈴薯 ×1", "入手　ジャガイモ ×1"), 2.5f);
                        SetStage(Stage.Deliver);
                    });
                    break;
                case Stage.Deliver:
                    miner.SetInteraction("delivery", () =>
                    {
                        SetStage(Stage.Return);
                        progress.SaveCheckpoint(ReturnCheckpoint);
                        progress.Notify(dialogue.Local("伸出援手 — CLEAR · 已記錄", "救いの手 — CLEAR ・ セーブ済み"), 2.8f);
                    });
                    break;
                case Stage.Return:
                    vincent.SetInteraction(hasLetter ? "return-with-letter" : "return-without-letter",
                        hasLetter ? Complete : Retry);
                    if (!hasLetter)
                        theo.SetInteraction("theo", () =>
                        {
                            hasLetter = true;
                            SetStage(Stage.Return);
                            progress.Notify(dialogue.Local("獲得　西奧的信 ×1", "入手　テオの手紙 ×1"), 2.5f);
                        });
                    break;
            }
        }

        void Update()
        {
            if (progress == null || walk.InputSuppressed || dialogue.BlocksGameplayInput) return;

            int row = -1;
            if (stage == Stage.Till)
            {
                row = ClosestUntilledRow(out float distance);
                if (row >= 0 && distance <= TillingReach &&
                    Mathf.Abs(walk.Player.transform.position.y - rows[row].transform.position.y) <= TillingVerticalReach &&
                    dialogue.TryConsumeGameplayInteraction())
                {
                    tilled[row] = true;
                    rows[row].sharedMaterial = tilledSoil;
                    if (++tilledCount == rows.Length) SetStage(Stage.FarmReward);
                    else row = ClosestUntilledRow(out _);
                }
            }

            var objective = stage switch
            {
                Stage.Vincent => WithDistance("與教堂前的梵谷交談", "教会前のゴッホと話す", vincent.transform.position),
                Stage.Miner => WithDistance("伸出援手　前往村莊東邊找礦工", "救いの手　村の東で鉱夫を探す", miner.transform.position),
                Stage.Farmer => WithDistance("尋找食物　到附近農田問問", "食べ物を探す　近くの畑へ", farmer.transform.position),
                Stage.Till when row >= 0 => WithDistance(
                    $"農田小幫手　靠近田壟按 {dialogue.InteractionKeyLabel} 耕地（{tilledCount}/{rows.Length}）",
                    $"畑仕事　畝のそばで {dialogue.InteractionKeyLabel}（{tilledCount}/{rows.Length}）", rows[row].transform.position),
                Stage.FarmReward => WithDistance($"農田小幫手（{tilledCount}/{rows.Length}）　回去和農夫交談",
                    $"畑仕事（{tilledCount}/{rows.Length}）　農夫のところへ戻る", farmer.transform.position),
                Stage.Deliver => WithDistance("分享溫暖　把馬鈴薯交給礦工", "ジャガイモを鉱夫に渡す", miner.transform.position),
                Stage.Return => WithDistance(hasLetter ? "回教堂找梵谷，轉交西奧的信" : "回教堂向梵谷分享經歷",
                    hasLetter ? "教会に戻ってテオの手紙を渡す" : "教会へ戻り、ゴッホに報告する", vincent.transform.position),
                _ => (Text: "", Detail: "")
            };
            progress.SetObjective(objective.Text, objective.Detail);
        }

        int ClosestUntilledRow(out float distance)
        {
            float closest = float.PositiveInfinity;
            int index = -1;
            var playerPosition = walk.Player.transform.position;
            for (int i = 0; i < rows.Length; i++)
            {
                if (tilled[i]) continue;
                var bounds = rows[i].bounds;
                var feet = new Vector3(playerPosition.x, bounds.center.y, playerPosition.z);
                float distanceSquared = (bounds.ClosestPoint(feet) - feet).sqrMagnitude;
                if (distanceSquared >= closest) continue;
                closest = distanceSquared;
                index = i;
            }
            distance = Mathf.Sqrt(closest);
            return index;
        }

        (string Text, string Detail) WithDistance(string chinese, string japanese, Vector3 target)
        {
            var offset = target - walk.Player.transform.position;
            offset.y = 0;
            var forward = walk.transform.forward;
            forward.y = 0;
            float angle = Vector3.SignedAngle(forward, offset, Vector3.up);
            string arrow = Mathf.Abs(angle) > 135 ? "↓" : angle > 45 ? "→" : angle < -45 ? "←" : "↑";
            return (dialogue.Local(chinese, japanese), $"    {arrow} {Mathf.RoundToInt(offset.magnitude)} m");
        }

        void ReturnToRoad()
        {
            hasLetter = false;
            tilledCount = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                tilled[i] = true;
                rows[i].sharedMaterial = tilledSoil;
            }
            MovePlayer(mine + Vector3.left * 3f, church);
            SetStage(Stage.Return);
        }

        void Retry()
        {
            dialogue.ShowOpening(retry, ReturnToRoad);
        }

        void Complete()
        {
            hasLetter = false;
            SetStage(Stage.Finished);
            progress.SetObjective("");
            progress.CompleteChapter();
        }

        void OnDestroy()
        {
            if (drySoil != null) Destroy(drySoil);
            if (tilledSoil != null) Destroy(tilledSoil);
        }

        internal void EndVisit()
        {
            progress = null;
            if (chapterOneVincent != null) chapterOneVincent.SetActive(false);
            if (vincent != null) vincent.gameObject.SetActive(false);
            if (miner != null) miner.gameObject.SetActive(false);
            if (farmer != null) farmer.gameObject.SetActive(false);
            if (theo != null) theo.gameObject.SetActive(false);
            foreach (var row in rows)
                if (row != null) row.gameObject.SetActive(false);
        }
    }
}
