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
        [SerializeField] Sprite vincentSprite;
        [SerializeField] Sprite minerSprite;
        [SerializeField] Sprite farmerSprite;
        [SerializeField] Sprite theoSprite;
        [SerializeField] Vector3 church = new(5, 0, 2);
        [SerializeField] Vector3 mine = new(34, 0, 0);
        [SerializeField] Vector3 farm = new(36, 0, 12);
        [SerializeField] Vector3 road = new(21, 0, 0);
        [SerializeField] Collider[] groundSurfaces;

        [SerializeField] Transform churchInterior, workersHome, farmhouse;
        [SerializeField] Material paintedProps;
        [SerializeField] Texture2D canvasGrain;
        [SerializeField] PeriodModels periodModels;
        internal PeriodModels Models => periodModels;
        internal Texture2D CanvasGrain => canvasGrain;
        internal Material PaintedProps => paintedProps;
        internal Vector3 SideAisle => churchInterior.position + Vector3.left * .8f;
        internal Vector3 ChapelPoint(float x, float y, float z) =>
            churchInterior.TransformPoint(new Vector3(x, y, z));

        internal RaycastHit ChapelWindowWall()
        {
            var ray = new Ray(ChapelPoint(-1.55f, 1.47f, 0), churchInterior.forward);
            foreach (var wall in churchInterior.GetComponents<BoxCollider>())
                if (wall.Raycast(ray, out var hit, 12f)) return hit;
            throw new System.InvalidOperationException("The chapel repair window needs its rear wall collider.");
        }
        internal Vector3 WorkersHome => workersHome.position;
        internal Vector3 Farmhouse => farmhouse.position;
        internal Vector3 Church => church;
        internal Vector3 Mine => mine;
        internal Vector3 Farm => farm;
        internal Sprite VincentSprite => vincentSprite;
        internal Sprite PeterSprite => minerSprite;
        internal Sprite FarmerSprite => farmerSprite;

        readonly MeshRenderer[] rows = new MeshRenderer[3];
        StorySceneProgress progress;
        DialogueTrigger vincent;
        DialogueTrigger miner;
        DialogueTrigger farmer;
        DialogueTrigger theo;
        Material drySoil;
        Material tilledSoil;
        Stage stage;
        bool hasLetter;
        bool vincentAskedForTheo;

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

            drySoil = new Material(paintedProps) { color = new Color(0.46f, 0.28f, 0.14f) };
            tilledSoil = new Material(paintedProps) { color = new Color(0.20f, 0.11f, 0.07f) };
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
                StoryPropInk.SetSurface(row, drySoil);
            }
        }

        DialogueTrigger SpawnActor(string actorName, Sprite sprite, Vector3 position)
        {
            var character = new GameObject(actorName);
            character.SetActive(false);
            character.transform.SetParent(transform);
            character.transform.position = Ground(position);
            var renderer = CharacterBillboard.AddSprite(character, sprite);
            renderer.color = new Color(0.86f, 0.88f, 0.97f);
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
                        SetStage(Stage.Deliver);
                    });
                    break;
                case Stage.Deliver:
                    miner.SetInteraction("delivery", () =>
                    {
                        SetStage(Stage.Return);
                        progress.SaveCheckpoint(ReturnCheckpoint);
                    });
                    break;
                case Stage.Return:
                    if (hasLetter)
                        vincent.SetInteraction("return-with-letter", Complete);
                    else if (!vincentAskedForTheo)
                        vincent.SetInteraction("return-without-letter", () =>
                        {
                            vincentAskedForTheo = true;
                            vincent.SetInteraction(null, null);
                        });
                    if (!hasLetter)
                        theo.SetInteraction("theo", () =>
                        {
                            hasLetter = true;
                            SetStage(Stage.Return);
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
                row = ClosestRow(out float distance);
                if (row >= 0 && distance <= TillingReach &&
                    Mathf.Abs(walk.Player.transform.position.y - rows[row].transform.position.y) <= TillingVerticalReach &&
                    dialogue.TryConsumeGameplayInteraction())
                {
                    StoryPropInk.SetSurface(rows[row].gameObject, tilledSoil);
                    SetStage(Stage.FarmReward);
                }
            }

            var objective = stage switch
            {
                Stage.Vincent => WithDistance("與教堂前的梵谷交談", "教会前のゴッホと話す", vincent.transform.position),
                Stage.Miner => WithDistance("伸出援手　前往村莊東邊找礦工", "救いの手　村の東で鉱夫を探す", miner.transform.position),
                Stage.Farmer => WithDistance("尋找食物　到附近農田問問", "食べ物を探す　近くの畑へ", farmer.transform.position),
                Stage.Till when row >= 0 => WithDistance(
                    $"靠近田壟，按 {dialogue.InteractionKeyLabel} 幫農夫鬆土",
                    $"畝のそばで {dialogue.InteractionKeyLabel} を押し、土をほぐす", rows[row].transform.position),
                Stage.FarmReward => WithDistance("回去和農夫談談今天的收成",
                    "農夫のところへ戻り、今日の収穫について聞く", farmer.transform.position),
                Stage.Deliver => WithDistance("分享溫暖　把馬鈴薯交給礦工", "ジャガイモを鉱夫に渡す", miner.transform.position),
                Stage.Return when vincentAskedForTheo && !hasLetter => WithDistance(
                    "到路上找西奧", "道に出てテオに会う", theo.transform.position),
                Stage.Return => WithDistance(hasLetter ? "回教堂找梵谷，轉交西奧的信" : "回教堂向梵谷分享經歷",
                    hasLetter ? "教会に戻ってテオの手紙を渡す" : "教会へ戻り、ゴッホに報告する", vincent.transform.position),
                _ => (Text: "", Detail: "")
            };
            progress.SetObjective(objective.Text, objective.Detail);
        }

        int ClosestRow(out float distance)
        {
            float closest = float.PositiveInfinity;
            int index = -1;
            var playerPosition = walk.Player.transform.position;
            for (int i = 0; i < rows.Length; i++)
            {
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
            StoryPropInk.SetSurface(rows[0].gameObject, tilledSoil);
            MovePlayer(mine + Vector3.left * 3f, church);
            SetStage(Stage.Return);
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
