using System;
using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    public sealed class StarryNightChapterThree : MonoBehaviour
    {
        internal const string ChapterId = "chapter-3";
        const string SurveyCheckpoint = "alyscamps";
        const string DebateCheckpoint = "yellow-house-debate";
        const string RetryCheckpoint = "yellow-house-retry";
        const float DebateDuration = 30f;

        enum Stage { House, Guest, Escort, Cemetery, Survey, Review, ReviewResponse, Return, Debate, Help, Stay, Finished }

        [SerializeField] StarryNightWalk walk;
        [SerializeField] DialoguePlayer dialogue;
        [SerializeField] StarryNightChapterTwo previousChapter;
        [SerializeField] GameObject cottage;
        [SerializeField] DialogueConversation script;
        [SerializeField] ChapterOpeningText opening;
        [SerializeField] ChapterOpeningText retry;
        [SerializeField] Sprite vincentSprite;
        [SerializeField] Sprite gauguinSprite;
        [SerializeField] Sprite neighborSprite;
        [SerializeField] Texture2D gauguinPainting;
        [SerializeField] Vector3 church = new(5, 0, 2);
        [SerializeField] Vector3 cemetery = new(10, 0, -27);

        internal Sprite GauguinSprite => gauguinSprite;
        internal Sprite VincentSprite => vincentSprite;
        internal Sprite NeighborSprite => neighborSprite;
        internal Texture2D GauguinArt => gauguinPainting;

        static readonly (bool Gauguin, string Chinese, string Japanese)[] Debate =
        {
            (false, "我想把眼前這片秋天，連同它帶給我的感受，一起留在畫裡。", "目の前の秋と、それを見て感じたことを、一緒に絵に残したいんだ。"),
            (true, "但畫不只是眼前的風景。構圖和想像，也能帶你去別的地方。", "絵は目の前の景色だけじゃない。構図と想像力で、別の場所へも行ける。"),
            (false, "我不是在複製風景。顏色和筆觸，就是我對這片土地的回答。", "景色を写しているだけじゃない。色と筆遣いが、この土地への僕の答えなんだ。"),
            (true, "我懂。但你願不願意試一次，先離開眼前的景物，憑記憶畫？", "分かるよ。でも一度、目の前の景色を離れて、記憶から描いてみないか？"),
            (false, "為什麼總是我得改？難道我看見的東西不值得被理解嗎？", "なぜいつも僕が変わらなきゃいけない？ 僕の見ているものは理解する価値もないのか？"),
            (true, "我沒有要你放棄自己的畫。可是今天，我想我們需要先停一下。", "自分の絵を捨てろとは言っていない。ただ今日は、一度ここでやめておこう。")
        };

        readonly WalkingTrail trail = new();
        StorySceneProgress progress;
        ChapterThreeScenery scenery;
        DialogueTrigger vincent;
        DialogueTrigger gauguin;
        GameObject neighbor;
        VisualElement review;
        Label reviewTitle, result, vincentCaption, gauguinCaption, debateSpeaker, debateLine;
        Image studyImage;
        Button reviewContinue;
        VisualElement debateSubtitle;
        Stage stage;
        PaintingStudy elements;
        string lastNode;
        PaintingStudy lookingAt;
        bool reviewCapturedCursor;
        int debateBeat;
        float remaining;

        Vector3 Player => walk.Player.transform.position;

        internal void Begin(StorySceneProgress sceneProgress, string checkpoint)
        {
            progress = sceneProgress;
            previousChapter.EndVisit();
            GetVillageScenery();
            vincent = Actor("Vincent - Chapter 3", vincentSprite, 1.9f);
            gauguin = Actor("Paul Gauguin", gauguinSprite);
            neighbor = Actor("Yellow House neighbor", neighborSprite).gameObject;
            neighbor.transform.position = previousChapter.Ground(scenery.Door + new Vector3(.8f, 0, -.8f));
            neighbor.SetActive(false);
            dialogue.LineChanged += TrackNode;
            BuildUI();

            switch (checkpoint)
            {
                case SurveyCheckpoint:
                    SetCemeteryCast();
                    MovePlayer(scenery.Lookout + Vector3.forward * .7f, scenery.Cemetery);
                    stage = Stage.Survey;
                    break;
                case DebateCheckpoint:
                case RetryCheckpoint:
                    elements = progress.LoadCemeteryStudy();
                    scenery.UpdateStudyColors(elements, 1f);
                    if (checkpoint == RetryCheckpoint)
                    {
                        PrepareDebate();
                        AskForHelp();
                        dialogue.ShowOpening(retry);
                    }
                    else BeginDebate();
                    break;
                default:
                    stage = Stage.House;
                    gauguin.gameObject.SetActive(false);
                    vincent.transform.position = scenery.House + new Vector3(.72f, 0, .8f);
                    vincent.SetInteraction("home", SendToGuest);
                    MovePlayer(church + Vector3.back * 3.5f, scenery.Door);
                    dialogue.ShowOpening(opening);
                    break;
            }
        }

        internal RaycastHit StudioWall(Vector3 point)
        {
            var ray = new Ray(point, cottage.transform.right);
            foreach (var wall in cottage.GetComponents<BoxCollider>())
                if (wall.Raycast(ray, out var hit, 6f)) return hit;
            throw new System.InvalidOperationException("The studio needs its right wall to hang artwork.");
        }

        internal ChapterThreeScenery GetVillageScenery()
        {
            if (scenery != null) return scenery;
            scenery = new ChapterThreeScenery(transform, previousChapter.Ground, cottage.transform.position,
                cemetery, gauguinPainting, previousChapter.PaintedProps, previousChapter.Models);
            return scenery;
        }

        internal void EndVisit()
        {
            progress = null;
            if (vincent != null) vincent.gameObject.SetActive(false);
            if (gauguin != null) gauguin.gameObject.SetActive(false);
            if (neighbor != null) neighbor.SetActive(false);
        }

        DialogueTrigger Actor(string name, Sprite sprite, float range = 3f)
        {
            var actor = new GameObject(name);
            actor.SetActive(false);
            actor.transform.SetParent(transform);
            CharacterBillboard.AddSprite(actor, sprite);
            var trigger = actor.AddComponent<DialogueTrigger>();
            trigger.Initialize(dialogue, walk.Player, script, range);
            trigger.SetInteraction(null, null);
            actor.SetActive(true);
            return trigger;
        }

        void MovePlayer(Vector3 point, Vector3 toward)
        {
            Vector3 heading = toward - point;
            heading.y = 0;
            point = previousChapter.Ground(point) + Vector3.up * .04f;
            var house = scenery.House;
            if (Mathf.Abs(point.x - house.x) < 2.45f && Mathf.Abs(point.z - house.z) < 1.9f)
                point.y = house.y + .04f;
            walk.EnterWalkingAt(point, Quaternion.LookRotation(heading));
        }

        void SendToGuest()
        {
            stage = Stage.Guest;
            vincent.SetInteraction(null, null);
            gauguin.transform.position = previousChapter.Ground(church + new Vector3(-1.25f, 0, -2f));
            gauguin.gameObject.SetActive(true);
            gauguin.SetInteraction("guest", StartEscort);
        }

        void StartEscort()
        {
            stage = Stage.Escort;
            gauguin.SetInteraction(null, null);
            trail.StartAt(Player);
        }

        void SetCemeteryCast()
        {
            vincent.SetInteraction(null, null);
            gauguin.SetInteraction(null, null);
            gauguin.gameObject.SetActive(true);
            vincent.transform.position = scenery.Cemetery;
            gauguin.transform.position = previousChapter.Ground(scenery.Cemetery + new Vector3(-.92f, 0, .62f));
        }

        void Update()
        {
            if (progress != null && lookingAt != PaintingStudy.None)
                scenery.UpdateStudyColors(elements | lookingAt, 1f - Mathf.Exp(-2.4f * Time.deltaTime));
            if (progress == null || walk.InputSuppressed || dialogue.BlocksGameplayInput) return;
            switch (stage)
            {
                case Stage.House:
                    Objective("畫家的新生活　前往黃色小屋找梵谷", "画家の新生活　黄色い家でゴッホを訪ねる", scenery.Door);
                    break;
                case Stage.Guest:
                    Objective("遠方的訪客　回到教堂找高更", "遠方からの客　教会でゴーギャンを探す", gauguin.transform.position);
                    break;
                case Stage.Escort:
                    trail.Follow(gauguin.transform, Player, previousChapter.Ground);
                    Objective("畫家的相遇　帶高更到黃色小屋", "画家たちの出会い　ゴーギャンを黄色い家へ案内する", scenery.Door);
                    if (Near(scenery.Door, 1.55f) && HorizontalDistance(gauguin.transform.position, scenery.Door) < 3.5f)
                    {
                        gauguin.transform.position = scenery.House + new Vector3(-.63f, 0, .7f);
                        Play("meeting", () =>
                        {
                            stage = Stage.Cemetery;
                            SetCemeteryCast();
                        });
                    }
                    break;
                case Stage.Cemetery:
                    Objective("共同創作　前往阿里斯康墓園", "共同制作　アリスカンへ向かう", scenery.Lookout);
                    if (Near(scenery.Lookout, 2.1f))
                        Play("arrival", () =>
                        {
                            stage = Stage.Survey;
                            progress.SaveCheckpoint(SurveyCheckpoint);
                        });
                    break;
                case Stage.Survey:
                    UpdateSurvey();
                    break;
                case Stage.ReviewResponse:
                    PlayReviewResponse();
                    break;
                case Stage.Return:
                    Objective("帶著習作回黃色小屋", "習作を持って黄色い家に戻る", scenery.Door);
                    if (Near(scenery.Door, 1.5f))
                    {
                        progress.SaveCheckpoint(DebateCheckpoint);
                        BeginDebate();
                    }
                    break;
                case Stage.Debate:
                    UpdateDebate();
                    break;
                case Stage.Help:
                    UpdateHelp();
                    break;
                case Stage.Stay:
                    if (Near(vincent.transform.position, 1.55f))
                    {
                        var key = dialogue.InteractionKeyLabel;
                        Objective($"按 {key} 留下來陪梵谷", $"{key} でゴッホのそばにいる");
                        if (dialogue.TryConsumeGameplayInteraction()) Play("ending", Finish);
                    }
                    else
                        Objective("回小屋陪梵谷等醫師", "家に戻ってゴッホと医師を待つ", vincent.transform.position);
                    break;
            }
        }

        void UpdateSurvey()
        {
            string key = dialogue.InteractionKeyLabel;
            if (!elements.HasFlag(PaintingStudy.Leaves) && Near(scenery.Leaves, 1.1f))
            {
                Objective($"按 {key} 觀察金黃色的樹葉", $"{key} で金色の葉を観察");
                if (dialogue.TryConsumeGameplayInteraction()) Observe(PaintingStudy.Leaves, "leaves");
                return;
            }
            if (!elements.HasFlag(PaintingStudy.Stone) && Near(scenery.Stone, 1.1f))
            {
                Objective($"按 {key} 觀察墓園的石棺", $"{key} で石棺を観察");
                if (dialogue.TryConsumeGameplayInteraction()) Observe(PaintingStudy.Stone, "stone");
                return;
            }
            if (Near(vincent.transform.position, 2.6f))
            {
                bool hasPerspective = elements.HasFlag(PaintingStudy.Perspective);
                Vector3 sight = scenery.Cemetery - Player;
                sight.y = 0f;
                bool goodAngle = !hasPerspective && Near(scenery.Lookout, .85f)
                    && Vector3.Angle(walk.transform.forward, sight) <= 37f;
                if (goodAngle)
                    Objective($"按 {key} 和梵谷談談構圖", $"{key} でゴッホと構図について話す");
                else if (elements == PaintingStudy.None)
                    Objective("站到梵谷面前，面向墓園道路", "ゴッホの前に立ち、並木道に目を向ける");
                else
                    Objective($"按 {key} 和梵谷看習作", $"{key} でゴッホと習作を見る");
                if (dialogue.TryConsumeGameplayInteraction())
                {
                    if (goodAngle && elements == PaintingStudy.None)
                        Observe(PaintingStudy.Perspective, "perspective");
                    else
                        Play(goodAngle ? "survey-angle" : elements == PaintingStudy.None ? "survey" : "survey-ready", () =>
                        {
                            if (lastNode == "perspective") Discover(PaintingStudy.Perspective);
                            else if (lastNode == "submit") ShowReview();
                        });
                }
                return;
            }

            if (elements == PaintingStudy.None)
                Objective("在墓園走走，選一樣想留在畫裡的景物", "並木道を歩いて、絵に残したいものを見つける", scenery.Lookout);
            else
                Objective("可以繼續看看，或回去和梵谷看習作", "もう少し見て回るか、ゴッホのところで習作を見る", scenery.Cemetery);
        }

        void Observe(PaintingStudy item, string node)
        {
            lookingAt = item;
            Play(node, () => Discover(item));
        }

        void Discover(PaintingStudy item)
        {
            elements |= item;
            lookingAt = item;
        }

        void ShowReview()
        {
            progress.Sound?.PlayMotif();
            stage = Stage.Review;
            reviewCapturedCursor = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            studyImage.image = scenery.Study(elements);
            reviewTitle.text = dialogue.Local("同一片秋色", "同じ秋の色");
            vincentCaption.text = dialogue.Local("梵谷的取景習作", "ゴッホの写生習作");
            gauguinCaption.text = dialogue.Local("高更《阿里斯康墓園》", "ゴーギャン《アリスカン》");
            result.text = dialogue.Local("看看你剛才停下來看的地方，在兩張畫裡各是什麼樣子。",
                "さっき足を止めた場所が、二枚の絵でどう描かれているか見比べてみよう。");
            reviewContinue.text = dialogue.Local("帶著習作回小屋", "習作を持って家に戻る");
            StoryMenu.Show(review, true);
            reviewContinue.Focus();
        }

        void CloseReview()
        {
            StoryMenu.Show(review, false);
            walk.InputSuppressed = false;
            StoryMenu.SetCursor(reviewCapturedCursor);
            stage = Stage.ReviewResponse;
        }

        void PlayReviewResponse()
        {
            Play("complete-study", () =>
            {
                progress.SaveCemeteryStudy(elements);
                stage = Stage.Return;
            });
        }

        void PrepareDebate()
        {
            scenery.HangStudy(elements);
            vincent.transform.position = scenery.House + new Vector3(-.88f, 0, 1.13f);
            gauguin.gameObject.SetActive(true);
            neighbor.SetActive(false);
            gauguin.transform.position = scenery.House + new Vector3(.88f, 0, 1.13f);
            MovePlayer(scenery.House + Vector3.back * .55f, scenery.House + Vector3.forward);
        }

        void BeginDebate()
        {
            PrepareDebate();
            stage = Stage.Debate;
            remaining = DebateDuration;
            debateBeat = -1;
            StoryMenu.Show(debateSubtitle, true);
        }

        void UpdateDebate()
        {
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            if (remaining <= 0f)
            {
                progress.SetObjective("");
                StoryMenu.Show(debateSubtitle, false);
                gauguin.gameObject.SetActive(false);
                Play("house-quiet", AskForHelp);
                return;
            }

            int beat = Mathf.Min((int)((DebateDuration - remaining) / 5f), Debate.Length - 1);
            if (debateBeat != beat)
            {
                debateBeat = beat;
                var line = Debate[beat];
                debateSpeaker.text = line.Gauguin ? dialogue.Local("高更", "ゴーギャン") : dialogue.Local("梵谷", "ゴッホ");
                debateLine.text = dialogue.Local(line.Chinese, line.Japanese);
                dialogue.RecordHistory(debateSpeaker.text, debateLine.text);
            }

            Objective("留在小屋，聽聽兩人的想法", "家に残り、二人の話を聞く");
        }

        void UpdateHelp()
        {
            if (Near(scenery.Door, 1.5f))
            {
                var key = dialogue.InteractionKeyLabel;
                Objective($"按 {key} 請門外的鄰居找醫生", $"{key} で玄関の近所の人に医師を呼んでもらう");
                if (dialogue.TryConsumeGameplayInteraction())
                    Play("neighbor-help", () =>
                    {
                        neighbor.SetActive(false);
                        stage = Stage.Stay;
                    });
            }
            else
                Objective("到門口請鄰居幫忙", "玄関で近所の人に助けを頼む", scenery.Door);
        }

        void AskForHelp()
        {
            gauguin.gameObject.SetActive(false);
            neighbor.SetActive(true);
            progress.SaveCheckpoint(RetryCheckpoint);
            stage = Stage.Help;
        }

        void Finish()
        {
            stage = Stage.Finished;
            progress.SetObjective("");
            progress.CompleteChapter();
        }

        void TrackNode(DialogueConversation conversation, string node)
        {
            if (conversation == script) lastNode = node;
        }

        void Play(string entry, Action complete) => dialogue.TryPlay(script, completed: complete, startNodeId: entry);

        bool Near(Vector3 position, float radius) =>
            HorizontalDistance(Player, position) <= radius && Mathf.Abs(Player.y - position.y) < 1.1f;

        static float HorizontalDistance(Vector3 from, Vector3 to)
        {
            var offset = from - to;
            offset.y = 0f;
            return offset.magnitude;
        }

        void Objective(string chinese, string japanese, Vector3? location = null)
        {
            string detail = "";
            if (location is Vector3 target)
            {
                var offset = target - Player;
                offset.y = 0f;
                var forward = walk.transform.forward;
                forward.y = 0f;
                float angle = Vector3.SignedAngle(forward, offset, Vector3.up);
                string arrow = Mathf.Abs(angle) > 135 ? "↓" : angle > 45 ? "→" : angle < -45 ? "←" : "↑";
                detail = $"    {arrow} {Mathf.RoundToInt(offset.magnitude)} m";
            }
            progress.SetObjective(dialogue.Local(chinese, japanese), detail);
        }

        void BuildUI()
        {
            review = new VisualElement();
            review.AddToClassList("story-overlay");
            progress.AddChapterUI(review);
            var card = StoryMenu.Panel(review, "story-card");
            card.AddToClassList("chapter-three-review");
            reviewTitle = StoryMenu.Label(card, "", "story-card-title");
            var works = StoryMenu.Panel(card, "chapter-three-works", false);
            var left = StoryMenu.Panel(works, "chapter-three-work", false);
            studyImage = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            studyImage.AddToClassList("chapter-three-image");
            left.Add(studyImage);
            vincentCaption = StoryMenu.Label(left, "", "chapter-three-caption");
            var right = StoryMenu.Panel(works, "chapter-three-work", false);
            var artwork = new Image { image = gauguinPainting, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            artwork.AddToClassList("chapter-three-image");
            right.Add(artwork);
            gauguinCaption = StoryMenu.Label(right, "", "chapter-three-caption");
            result = StoryMenu.Label(card, "", "story-detail");
            reviewContinue = StoryMenu.Button(card, "", CloseReview, "story-primary");
            StoryMenu.Show(review, false);

            debateSubtitle = new VisualElement { pickingMode = PickingMode.Ignore };
            debateSubtitle.AddToClassList("chapter-three-subtitle");
            progress.AddChapterUI(debateSubtitle);
            debateSpeaker = StoryMenu.Label(debateSubtitle, "", "chapter-three-speaker");
            debateLine = StoryMenu.Label(debateSubtitle, "", "chapter-three-line");
            StoryMenu.Show(debateSubtitle, false);
        }

        void OnDestroy()
        {
            if (dialogue != null) dialogue.LineChanged -= TrackNode;
            scenery?.Dispose();
        }
    }
}
