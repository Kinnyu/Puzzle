using System;
using System.Collections;
using System.Collections.Generic;
using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.InputSystem;
using Cursor = UnityEngine.Cursor;
using Mask = Puzzle.Story.StoryBrush.Shape;
using Ink = Puzzle.Story.StoryBrush.Option;

namespace Puzzle.Story
{
    internal sealed partial class ExpandedStarryNight : MonoBehaviour
    {
        sealed class Interaction
        {
            internal Vector3 Point;
            internal Transform Object;
            internal string Verb;
            internal Action Use;
            internal bool Brush;
            internal float HoldSeconds;
            internal Vector3 Offset;
            internal Vector3 Position => Object != null ? Object.position + Offset : Point;
        }

        enum Location { Square, Church, Mine, WorkersHome, Farm, Farmhouse, Studio, Cemetery, Clinic, Gallery }

        readonly List<(string id, Location location, Action open)> scenes = new();
        readonly List<Interaction> targets = new();
        readonly List<Texture2D> illustrations = new();
        readonly Collider[] footingHits = new Collider[16];
        readonly RaycastHit[] sightHits = new RaycastHit[24];
        StorySceneProgress progress;
        StarryNightWalk walk;
        Camera eyes;
        DialoguePlayer dialogue;
        DialogueConversation conversation;
        StarryNightChapterTwo terrain;
        StarryNightChapterThree third;
        StarryNightChapterFour fourth;
        ChapterThreeScenery village;
        ChapterFourScenery exhibition;
        StoryBrush canvas;
        GameObject scene;
        Material propMaterial;
        Action resumeDialogue;
        int sceneIndex;
        float deadline;
        bool extendedRetry;
        Action timedOut;
        string goal;
        Sprite youngVincent;
        Sprite chapterVincent;
        Interaction heldTarget;
        float heldSince;
        bool keepNearbyPosition;

        Vector3 Church => terrain.Ground(terrain.Church);
        Vector3 Side => terrain.SideAisle;
        Vector3 Mine => terrain.Ground(terrain.Mine);
        Vector3 Farm => terrain.Ground(terrain.Farm);
        Vector3 House => village.House;
        Vector3 Cemetery => terrain.Ground(village.Cemetery + Vector3.back * .66f);
        static readonly Color Wood = new(.47f, .29f, .15f);
        static readonly Color Linen = new(.83f, .73f, .53f);
        static readonly Color Gold = new(.97f, .69f, .24f);
        static readonly Color Blue = new(.23f, .40f, .62f);
        static readonly Color Green = new(.30f, .49f, .23f);

        internal bool CanvasOpen => canvas?.IsOpen == true;
        internal bool CloseCanvas() => canvas?.Hide() == true;

        internal void Initialize(StorySceneProgress owner, StarryNightWalk walker, DialoguePlayer speaker,
            DialogueTrigger originalVincent, StarryNightChapterTwo land, StarryNightChapterThree yellowHouse,
            StarryNightChapterFour gallery)
        {
            progress = owner;
            walk = walker;
            eyes = walk.GetComponent<Camera>();
            dialogue = speaker;
            terrain = land;
            third = yellowHouse;
            fourth = gallery;
            youngVincent = originalVincent.GetComponentInChildren<SpriteRenderer>()?.sprite;
            originalVincent.gameObject.SetActive(false);
            dialogue.SceneInteractionPrompt = Interact;
            conversation = ScriptableObject.CreateInstance<DialogueConversation>();
            conversation.name = "星夜：劇本2";
            propMaterial = terrain.PaintedProps;
            village = third.GetVillageScenery();
            exhibition = fourth.CreateExpandedScenery(village, transform);
            Visible(exhibition.Copies, false);
            Visible(exhibition.Supplies, false);
            Visible(exhibition.Setup, false);
            ShowExhibition(false);
            canvas = new StoryBrush(progress, terrain.CanvasGrain);
        }

        internal void Begin(string chapterId, string checkpoint)
        {
            progress.TransitionScene(() => LoadChapter(chapterId, checkpoint), distant: true, opening: scene == null);
        }

        void LoadChapter(string chapterId, string checkpoint)
        {
            scenes.Clear();
            extendedRetry = false;
            switch (chapterId)
            {
                case "chapter-1":
                    chapterVincent = youngVincent != null ? youngVincent : terrain.VincentSprite;
                    ChurchStory();
                    break;
                case "chapter-2":
                    chapterVincent = terrain.VincentSprite;
                    MineStory();
                    break;
                case "chapter-3":
                    chapterVincent = third.VincentSprite;
                    YellowHouseStory();
                    break;
                case "chapter-4":
                    chapterVincent = fourth.VincentSprite;
                    ExhibitionStory();
                    break;
            }
            sceneIndex = Mathf.Max(0, scenes.FindIndex(item => item.id == checkpoint));
            OpenScene();
        }

        void Scene(string id, Location location, Action open) => scenes.Add((id, location, open));

        void OpenScene()
        {
            ClearTargets();
            StopTimer();
            resumeDialogue = null;
            if (scene != null)
            {
                scene.SetActive(false);
                Destroy(scene);
            }
            var entry = scenes[sceneIndex];
            scene = new GameObject(entry.id);
            scene.transform.SetParent(transform);
            Physics.SyncTransforms();
            progress.SaveCheckpoint(entry.id);
            entry.open();
        }

        void Next()
        {
            ClearTargets();
            StopTimer();
            extendedRetry = false;
            int previous = sceneIndex++;
            if (sceneIndex < scenes.Count)
            {
                bool distant = scenes[previous].location != scenes[sceneIndex].location;
                progress.TransitionScene(() =>
                {
                    keepNearbyPosition = !distant;
                    OpenScene();
                    keepNearbyPosition = false;
                }, distant);
            }
            else
            {
                Goal("");
                progress.CompleteChapter();
            }
        }

        void Arrive(Vector3 spot, Vector3? toward = null)
        {
            Vector3 destination = FloorAndClearance(spot);
            Vector3 direction = toward.GetValueOrDefault(spot + Vector3.forward) - destination;
            direction.y = 0;
            var playerPosition = walk.Player.transform.position;
            if (keepNearbyPosition && (destination - playerPosition).sqrMagnitude < 1.45f * 1.45f &&
                Vector3.Dot(Vector3.ProjectOnPlane(eyes.transform.forward, Vector3.up).normalized, direction.normalized) > .66f &&
                CanStand(playerPosition, walk.Player.radius))
                return;
            walk.EnterWalkingAt(destination,
                Quaternion.LookRotation(direction.sqrMagnitude > .0001f ? direction : Vector3.forward));
        }

        void Travel(Vector3 spot, Vector3 toward, Action onArrival = null)
        {
            progress.TransitionScene(() =>
            {
                Arrive(spot, toward);
                onArrival?.Invoke();
            }, distant: true);
        }

        Vector3 FloorAndClearance(Vector3 spot) =>
            FindClearFooting(spot, walk.Player.radius, walk.Player.height) + Vector3.up * .035f;

        Vector3 FindClearFooting(Vector3 spot, float radius, float height)
        {
            Vector3 floor = FloorAt(spot);
            if (CanStand(floor, radius, height)) return floor;

            for (float distance = .35f; distance <= 3.6f; distance += .35f)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI / 8f;
                    var candidate = FloorAt(spot + new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * distance);
                    if (Mathf.Abs(candidate.y - floor.y) > .75f || !CanStand(candidate, radius, height)) continue;
                    return candidate;
                }
            }
            Debug.LogWarning($"找不到有足夠空間的站立點 {spot}", this);
            return floor;
        }

        Vector3 FloorAt(Vector3 point)
        {
            Vector3 floor = terrain.Ground(point);
            int count = Physics.RaycastNonAlloc(floor + Vector3.up * .90f, Vector3.down, sightHits, 1.15f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.point.y <= floor.y || hit.normal.y < .7f) continue;
                if (hit.collider.name.EndsWith(" floor", StringComparison.Ordinal) ||
                    hit.collider.name.EndsWith(" threshold", StringComparison.Ordinal))
                    floor.y = hit.point.y;
            }
            return floor;
        }

        bool CanStand(Vector3 feet, float radius, float height = 1.75f)
        {
            float padding = .09f;
            int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * (radius + padding),
                feet + Vector3.up * Mathf.Max(radius + padding, height - radius), radius, footingHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (footingHits[i] != walk.Player) return false;
            return true;
        }

        void Goal(string text)
        {
            goal = text;
            progress.SetObjective(text);
        }

        void Offer(Vector3 point, string verb, Action use, Transform physical = null, bool brush = false, float hold = 0)
        {
            targets.Add(new Interaction
            {
                Point = point, Object = physical, Offset = physical != null ? point - physical.position : Vector3.zero,
                Verb = verb, Use = use, Brush = brush, HoldSeconds = hold
            });
        }

        void ObjectAction(GameObject prop, string verb, Action use, float hold = 0)
        {
            Offer(prop.transform.position, verb, use, prop.transform, hold: hold);
        }

        void ActorAction(GameObject actor, string verb, Action use)
        {
            Offer(actor.transform.position + Vector3.up * 1.10f, verb, use, actor.transform);
        }

        void ClearTargets()
        {
            targets.Clear();
            heldTarget = null;
        }

        string Interact()
        {
            if (walk.InputSuppressed || CanvasOpen) return null;
            if (resumeDialogue != null)
            {
                if (dialogue.TryConsumeGameplayInteraction())
                {
                    resumeDialogue();
                    return null;
                }
                return $"按 {dialogue.InteractionKeyLabel} 繼續剛才的對話";
            }
            Interaction best = null, closest = null;
            float bestScore = float.PositiveInfinity;
            float closestRange = float.PositiveInfinity;
            foreach (var target in targets)
            {
                Vector3 position = target.Position;
                float distance = Vector3.Distance(walk.Player.transform.position + Vector3.up * .9f, position);
                if (distance < closestRange)
                {
                    closest = target;
                    closestRange = distance;
                }
                if (distance > 3.15f) continue;
                Vector3 screen = eyes.WorldToViewportPoint(position);
                if (screen.z <= 0 || screen.x < .14f || screen.x > .86f || screen.y < .07f || screen.y > .93f) continue;
                float aim = new Vector2(screen.x - .5f, screen.y - .5f).sqrMagnitude;
                float score = aim + distance * .004f;
                if (score >= bestScore || Obstructed(position, target.Object)) continue;
                best = target;
                bestScore = score;
            }

            string detail = deadline > 0 ? $"    {Mathf.CeilToInt(Mathf.Max(0, deadline - Time.time))} 秒" : "";
            if (best == null && closest != null)
            {
                Vector3 heading = closest.Position - walk.transform.position;
                float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(walk.transform.forward, Vector3.up),
                    Vector3.ProjectOnPlane(heading, Vector3.up), Vector3.up);
                string arrow = Mathf.Abs(angle) > 145 ? "↓" : angle < -35 ? "←" : angle > 35 ? "→" : "↑";
                detail += $"    {arrow} {Mathf.RoundToInt(closestRange)} m";
            }
            progress.SetObjective(goal ?? "", detail);
            if (heldTarget != best || !dialogue.HoldingInteractionKey) heldTarget = null;
            if (best == null || string.IsNullOrEmpty(dialogue.InteractionKeyLabel)) return null;

            if (best.HoldSeconds > 0)
            {
                if (heldTarget == null && dialogue.TryConsumeGameplayInteraction())
                {
                    heldTarget = best;
                    heldSince = Time.time;
                }
                if (heldTarget == best && Time.time - heldSince >= best.HoldSeconds)
                {
                    heldTarget = null;
                    best.Use();
                    return null;
                }
                return $"按住 {dialogue.InteractionKeyLabel} {best.Verb}";
            }

            if (dialogue.TryConsumeGameplayInteraction()
                || (best.Brush && Keyboard.current?.bKey.wasPressedThisFrame == true))
            {
                best.Use();
                return null;
            }
            return $"按 {dialogue.InteractionKeyLabel} {best.Verb}";
        }

        bool Obstructed(Vector3 point, Transform target)
        {
            Vector3 ray = point - eyes.transform.position;
            int count = Physics.RaycastNonAlloc(eyes.transform.position, ray.normalized, sightHits,
                Mathf.Max(0, ray.magnitude - .055f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.collider != walk.Player && (target == null || !hit.transform.IsChildOf(target))) return true;
            }
            return false;
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (CanvasOpen && keys?.bKey.wasPressedThisFrame == true) canvas.Hide();
            if (keys?.tabKey.wasPressedThisFrame == true && !walk.InputSuppressed &&
                !dialogue.BlocksGameplayInput && !string.IsNullOrEmpty(goal))
                progress.Notify(goal, 2f);
            if (timedOut == null) return;
            if ((walk.InputSuppressed && !CanvasOpen) || canvas.IsReview || dialogue.IsPlaying || resumeDialogue != null)
                deadline += Time.deltaTime;
            if (CanvasOpen) canvas.ShowTime(Mathf.CeilToInt(Mathf.Max(0, deadline - Time.time)));
            if (Time.time < deadline) return;
            var fail = timedOut;
            StopTimer();
            fail();
        }

        void TimeLimit(float seconds, Action fail)
        {
            deadline = Time.time + seconds * (extendedRetry ? 2 : 1);
            timedOut = fail;
        }

        void StopTimer()
        {
            deadline = 0;
            timedOut = null;
        }

        void Retry(string actor, string line)
        {
            ClearTargets();
            canvas.Abort();
            Ask(actor, line, choice =>
            {
                extendedRetry = choice == 1;
                progress.TransitionScene(OpenScene, distant: false);
            }, "再試一次", "延長時間再試");
        }

        void Speak(Action after, params string[] lines) => SpeakWithChoice(after, lines, null, null);

        void Ask(string actor, string line, Action<int> after, params string[] options) =>
            SpeakWithChoice(null, new[] { $"{actor}|{line}" }, options, after);

        void SpeakWithChoice(Action after, string[] lines, string[] options, Action<int> choose)
        {
            var nodes = new DialogueConversation.Node[lines.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                int split = lines[i].IndexOf('|');
                string actor = split < 0 ? "旁白" : lines[i].Substring(0, split);
                string text = split < 0 ? lines[i] : lines[i].Substring(split + 1);
                nodes[i] = new DialogueConversation.Node
                {
                    id = i == 0 ? "start" : i.ToString(),
                    nextNodeId = i + 1 < nodes.Length ? (i + 1).ToString() : null,
                    speaker = new DialogueText(actor),
                    line = new DialogueText(text),
                    portrait = Portrait(actor),
                    portraitOnRight = actor is "高更" or "西奧"
                };
            }
            if (options != null)
            {
                nodes[^1].choices = Array.ConvertAll(options,
                    option => new DialogueConversation.Choice { text = new DialogueText(option) });
            }
            conversation.SetRuntimeNodes(nodes);
            int answer = -1;
            void StartDialogue()
            {
                if (!dialogue.TryPlay(conversation, completed: () =>
                    {
                        resumeDialogue = null;
                        if (answer >= 0) choose?.Invoke(answer);
                        after?.Invoke();
                    })) return;
                dialogue.RuntimeChoiceSelected = options == null ? null : selected => answer = selected;
            }
            resumeDialogue = StartDialogue;
            progress.AfterReveal(StartDialogue);
        }

        void PauseTogether(float seconds, Action next) => StartCoroutine(WaitTogether(seconds, next));

        IEnumerator WaitTogether(float seconds, Action next)
        {
            bool suppressed = walk.InputSuppressed;
            walk.InputSuppressed = true;
            walk.SetSeated(true);
            progress.Sound?.PlayMotif();
            yield return new WaitForSeconds(seconds);
            walk.SetSeated(false);
            walk.InputSuppressed = suppressed;
            next();
        }

        void Collect(string task, (GameObject prop, string verb, Action found)[] items, Action finish)
        {
            ClearTargets();
            int count = 0;
            Goal($"{task}  0/{items.Length}");
            foreach (var item in items)
            {
                ObjectAction(item.prop, item.verb, () =>
                {
                    targets.RemoveAll(target => target.Object == item.prop.transform);
                    Goal($"{task}  {++count}/{items.Length}");
                    item.found?.Invoke();
                    if (count == items.Length) finish();
                });
            }
        }

        void BrushAt(GameObject prop, string verb, Action<int> finish, bool erase, params Ink[] options)
            => OfferBrush(prop, verb, finish, erase, options, null);

        void OfferBrush(GameObject prop, string verb, Action<int> finish, bool erase, Ink[] options, Color32[] priorLayers)
        {
            Offer(prop.transform.position, verb, () => canvas.Open(verb, options, erase, answer =>
            {
                targets.RemoveAll(target => target.Object == prop.transform);
                Pigment(prop.transform.position, prop.transform.position + Vector3.up * .24f, options[answer].Color);
                finish(answer);
            }, priorLayers), prop.transform, brush: true);
        }

        void Layers(GameObject prop, string title, Action<int> finish, params Ink[][] layers)
        {
            int branch = 0;
            Color32[] previous = null;
            void Layer(int index)
            {
                Goal($"{title}  {index + 1}/{layers.Length}　〔B 畫筆〕");
                var options = layers[index];
                OfferBrush(prop, options.Length > 1 ? "選擇構圖並作畫" : options[0].Name, chosen =>
                {
                    if (options.Length > 1) branch = chosen;
                    previous = canvas.CopyPaintedLayers();
                    if (index + 1 < layers.Length) Layer(index + 1);
                    else finish(branch);
                }, false, options, previous);
            }
            Layer(0);
        }

        static Ink[] Layer(string name, Mask shape, Color color) => new[] { new Ink(name, shape, color) };

        static Ink[] FreeLayer() => Layer("留下自己的筆觸", Mask.Free, Blue);

        void Save(string id, int value) => progress.SaveChoice(id, value);
        int Recall(int chapter, string id, int fallback = 0) => progress.ReadChoice(chapter, id, fallback);

        void Answer(GameObject actor, string action, Action use)
        {
            ClearTargets();
            Goal(action);
            ActorAction(actor, action, () =>
            {
                ClearTargets();
                use();
            });
        }

        GameObject Actor(string name, Sprite sprite, Vector3 spot, Color? tint = null)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(scene.transform);
            actor.transform.position = FindClearFooting(spot, .25f, 1.5f);
            var renderer = CharacterBillboard.AddSprite(actor, sprite);
            if (tint.HasValue) renderer.color = tint.Value;
            return actor;
        }

        GameObject Vincent(Vector3 spot) => Actor("梵谷", chapterVincent, spot);
        GameObject Peter(Vector3 spot) => Actor("彼得", terrain.PeterSprite, spot);
        GameObject Theo(Vector3 spot) => Actor("西奧", fourth.TheoSprite, spot);

        Texture2D Portrait(string actor) => (actor switch
        {
            "梵谷" => chapterVincent,
            "高更" => third.GauguinSprite,
            "西奧" => fourth.TheoSprite,
            "彼得" => terrain.PeterSprite,
            "醫師" => fourth.DoctorSprite,
            _ => null
        })?.texture;

        GameObject Prop(string name, Vector3 center, Vector3 size, Color color,
            PrimitiveType shape = PrimitiveType.Cube)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.name = name;
            obj.transform.SetParent(scene.transform);
            obj.transform.position = center;
            obj.transform.localScale = size;
            obj.GetComponent<Collider>().enabled = false;
            ColorProp(obj, color);
            return obj;
        }

        void ColorProp(GameObject obj, Color color, Texture2D texture = null)
        {
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = propMaterial;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            if (texture != null) block.SetTexture("_BaseMap", texture);
            renderer.SetPropertyBlock(block);
        }

        GameObject ImageProp(string name, Vector3 point, Texture2D texture, float width = .8f, float height = .62f)
        {
            var frame = Prop(name, point, new Vector3(width, height, .07f), Wood);
            var picture = Prop(name + "畫面", point + Vector3.back * .043f,
                new Vector3(width * .92f, height * .9f, .015f), Linen);
            picture.transform.SetParent(frame.transform, true);
            if (texture != null) ColorProp(picture, Color.white, texture);
            return frame;
        }

        GameObject Period(Mesh mesh, Vector3 point, string name = null) =>
            terrain.Models.Place(mesh, scene.transform, point, name);

        GameObject EaselPicture(string name, Vector3 center, float width = .80f, float height = .62f)
        {
            var frame = ImageProp(name, center, null, width, height);
            var foot = center + new Vector3(0, -height * .5f - .815f, .07f);
            var stand = Period(terrain.Models.easel, foot);
            stand.transform.SetParent(frame.transform, true);
            return frame;
        }

        GameObject Seat(Vector3 foot, bool bench = false)
        {
            if (!bench) return Period(terrain.Models.chair, foot, "草編椅");
            const float width = 1.55f;
            var seat = Prop("長椅", foot + Vector3.up * .45f,
                new Vector3(width, .09f, .48f), Wood);
            var back = Prop("椅背", foot + new Vector3(0, .71f, .20f), new Vector3(width, .39f, .07f), Wood);
            back.transform.SetParent(seat.transform, true);
            for (int i = -1; i <= 1; i += 2)
                Prop("椅腳", foot + new Vector3(i * width * .37f, .23f, 0), new Vector3(.075f, .44f, .39f), Wood)
                    .transform.SetParent(seat.transform, true);
            return seat;
        }

        GameObject Table(Vector3 foot)
        {
            var table = Prop("桌子", foot + Vector3.up * .70f, new Vector3(1.3f, .105f, .72f), Wood);
            for (int i = -1; i <= 1; i += 2)
                Prop("桌腳", foot + new Vector3(i * .53f, .35f, 0), new Vector3(.10f, .70f, .55f), Wood);
            return table;
        }

        void Lamp(Vector3 point, Mesh model = null)
        {
            var lamp = Period(model != null ? model : terrain.Models.oilLamp, point);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .74f, .40f);
            light.range = 3.5f;
            light.intensity = 2.2f;
        }

        Texture2D MakeSketch(string title, Func<float, float, Color> draw)
        {
            const int width = 240, height = 150;
            var pixels = new Color32[width * height];
            var grainTile = terrain.CanvasGrain;
            var relief = grainTile.GetPixels32();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color color = draw(x / (float)width, y / (float)height);
                float grain = (relief[(y % grainTile.height) * grainTile.width + x % grainTile.width].r / 255f - .5f) * .18f;
                pixels[y * width + x] = color + new Color(grain, grain, grain, 0);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = title };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            illustrations.Add(texture);
            return texture;
        }

        void TurnSign(GameObject sign, Color color, string title)
        {
            sign.name = title;
            var arrow = MakeSketch(title, (x, y) =>
                (x > .16f && x < .63f && Mathf.Abs(y - .5f) < .075f) ||
                (x >= .58f && x < .83f && Mathf.Abs(y - .5f) < (.83f - x) * 1.65f) ? color : Blue);
            ColorProp(sign.transform.GetChild(0).gameObject, Color.white, arrow);
        }

        void Pigment(Vector3 from, Vector3 toward, Color color)
        {
            var burst = new GameObject("顏料筆觸");
            burst.transform.position = from;
            var particles = burst.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.startLifetime = .46f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.045f, .095f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emitter = particles.emission;
            emitter.enabled = false;
            particles.Play();
            for (int i = 0; i < 16; i++)
            {
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = from + UnityEngine.Random.insideUnitSphere * .18f,
                    velocity = (toward - from) * 2.15f + UnityEngine.Random.insideUnitSphere * .65f
                }, 1);
            }
        }

        void OnDestroy()
        {
            if (dialogue != null)
            {
                dialogue.SceneInteractionPrompt = null;
                dialogue.RuntimeChoiceSelected = null;
            }
            canvas?.Dispose();
            exhibition?.Dispose();
            if (conversation != null) Destroy(conversation);
            foreach (var texture in illustrations) Destroy(texture);
        }
    }
}
