using System;
using System.Collections.Generic;
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

        enum Stage { House, Guest, Escort, Cemetery, Survey, Review, ReviewResponse, Debate, Ending, Finished }

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

        static readonly (bool Gauguin, string Chinese, string Japanese)[] Debate =
        {
            (false, "我想把眼前這片秋天，連同它帶給我的感受，一起留在畫裡。", "目の前の秋と、それを見て感じたことを、一緒に絵に残したいんだ。"),
            (true, "但畫不只是眼前的風景。構圖和想像，也能帶你去別的地方。", "絵は目の前の景色だけじゃない。構図と想像力で、別の場所へも行ける。"),
            (false, "我不是在複製風景。顏色和筆觸，就是我對這片土地的回答。", "景色を写しているだけじゃない。色と筆遣いが、この土地への僕の答えなんだ。"),
            (true, "我懂。但你願不願意試一次，先離開眼前的景物，憑記憶畫？", "分かるよ。でも一度、目の前の景色を離れて、記憶から描いてみないか？"),
            (false, "為什麼總是我得改？難道我看見的東西不值得被理解嗎？", "なぜいつも僕が変わらなきゃいけない？ 僕の見ているものは理解する価値もないのか？"),
            (true, "我沒有要你放棄自己的畫。可是今天，我想我們需要先停一下。", "自分の絵を捨てろとは言っていない。ただ今日は、一度ここでやめておこう。")
        };

        readonly Queue<Vector3> trail = new();
        StorySceneProgress progress;
        ChapterThreeScenery scenery;
        DialogueTrigger vincent;
        DialogueTrigger gauguin;
        GameObject neighbor;
        VisualElement review;
        Label reviewTitle, result, vincentCaption, gauguinCaption, timer, debateSpeaker, debateLine;
        Image studyImage;
        Button reviewContinue;
        VisualElement debateSubtitle;
        Stage stage;
        PaintingStudy elements;
        Vector3 lastTrailPoint;
        string lastNode;
        bool secondChance;
        bool sentForHelp;
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
                    elements = PaintingStudy.All;
                    BeginDebate(checkpoint == RetryCheckpoint);
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

        internal ChapterThreeScenery GetVillageScenery()
        {
            if (scenery != null) return scenery;
            scenery = new ChapterThreeScenery(transform, previousChapter.Ground, cottage.transform.position,
                cemetery, gauguinPainting);
            cottage.SetActive(false);
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
            var renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            actor.AddComponent<CharacterBillboard>();
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
            trail.Clear();
            lastTrailPoint = Player;
            trail.Enqueue(lastTrailPoint);
        }

        void FollowTrail()
        {
            Vector3 position = Player;
            if (HorizontalDistance(lastTrailPoint, position) >= .45f)
            {
                trail.Enqueue(position);
                lastTrailPoint = position;
            }
            var follower = gauguin.transform;
            if (!trail.TryPeek(out Vector3 next)) return;
            if (trail.Count < 4 && HorizontalDistance(follower.position, position) < 1.55f) return;
            if (HorizontalDistance(follower.position, next) < .12f) trail.Dequeue();
            else
            {
                var destination = Vector3.MoveTowards(follower.position, next, 5.5f * Time.deltaTime);
                follower.position = previousChapter.Ground(destination);
            }
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
                    FollowTrail();
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
                case Stage.Debate:
                    UpdateDebate();
                    break;
                case Stage.Ending:
                    if (sentForHelp) Play("ending", Finish);
                    else Play("failure", RetryDebate);
                    break;
            }
        }

        void UpdateSurvey()
        {
            string key = dialogue.InteractionKeyLabel;
            if (!elements.HasFlag(PaintingStudy.Leaves) && Near(scenery.Leaves, 1.1f))
            {
                Objective($"按 {key} 觀察金黃色的樹葉", $"{key} で金色の葉を観察");
                if (dialogue.TryConsumeGameplayInteraction()) Play("leaves", () => Discover(PaintingStudy.Leaves));
                return;
            }
            if (!elements.HasFlag(PaintingStudy.Stone) && Near(scenery.Stone, 1.1f))
            {
                Objective($"按 {key} 觀察墓園的石棺", $"{key} で石棺を観察");
                if (dialogue.TryConsumeGameplayInteraction()) Play("stone", () => Discover(PaintingStudy.Stone));
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
                else if (!hasPerspective)
                    Objective("站到梵谷面前，面向墓園道路", "ゴッホの前に立ち、並木道に目を向ける");
                else
                    Objective($"按 {key} 與梵谷交談", $"{key} でゴッホと話す");
                if (dialogue.TryConsumeGameplayInteraction())
                {
                    Play(goodAngle ? "survey-angle" : "survey", () =>
                    {
                        if (lastNode == "perspective") Discover(PaintingStudy.Perspective);
                        else if (lastNode == "submit") ShowReview();
                    });
                }
                return;
            }

            var target = !elements.HasFlag(PaintingStudy.Perspective) ? scenery.Lookout
                : !elements.HasFlag(PaintingStudy.Leaves) ? scenery.Leaves : !elements.HasFlag(PaintingStudy.Stone)
                    ? scenery.Stone : scenery.Cemetery;
            int collected = (elements.HasFlag(PaintingStudy.Perspective) ? 1 : 0)
                + (elements.HasFlag(PaintingStudy.Leaves) ? 1 : 0) + (elements.HasFlag(PaintingStudy.Stone) ? 1 : 0);
            Objective($"尋找取景素材（{collected}/3）　道路、秋葉、石棺",
                $"写生の題材を探す（{collected}/3）　道・紅葉・石棺", target);
        }

        void Discover(PaintingStudy item)
        {
            elements |= item;
            var name = item switch
            {
                PaintingStudy.Perspective => dialogue.Local("道路的透視感", "並木道の奥行き"),
                PaintingStudy.Leaves => dialogue.Local("金黃色的秋葉", "金色の秋の葉"),
                _ => dialogue.Local("墓園的石棺", "墓地の石棺")
            };
            progress.Notify(dialogue.Local($"取景素材　{name}", $"写生の題材　{name}"), 2.8f);
        }

        void ShowReview()
        {
            stage = Stage.Review;
            reviewCapturedCursor = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            studyImage.image = scenery.Study(elements);
            reviewTitle.text = dialogue.Local("同一片秋色", "同じ秋の色");
            vincentCaption.text = dialogue.Local("梵谷的取景習作", "ゴッホの写生習作");
            gauguinCaption.text = dialogue.Local("高更《阿里斯康墓園》", "ゴーギャン《アリスカン》");
            result.text = elements == PaintingStudy.All ? dialogue.Local("三項素材已齊全", "三つの題材がそろった") : MissingStudies();
            reviewContinue.text = dialogue.Local("繼續", "続ける");
            StoryMenu.Show(review, true);
            reviewContinue.Focus();
        }

        string MissingStudies()
        {
            var names = new List<string>(3);
            if (!elements.HasFlag(PaintingStudy.Perspective)) names.Add(dialogue.Local("從梵谷面前望向道路", "ゴッホの前から並木道を見る"));
            if (!elements.HasFlag(PaintingStudy.Leaves)) names.Add(dialogue.Local("觀察金黃色的秋葉", "金色の秋の葉を見る"));
            if (!elements.HasFlag(PaintingStudy.Stone)) names.Add(dialogue.Local("觀察路邊的石棺", "道沿いの石棺を見る"));
            return string.Join("　·　", names);
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
            if (elements == PaintingStudy.All)
            {
                Play("complete-study", () =>
                {
                    progress.Notify(dialogue.Local("取景完成", "写生完了"), 2.5f);
                    progress.SaveCheckpoint(DebateCheckpoint);
                    BeginDebate(false);
                });
            }
            else
            {
                Play("incomplete", () =>
                {
                    stage = Stage.Survey;
                    progress.Notify(MissingStudies(), 4f);
                    MovePlayer(scenery.Lookout + Vector3.forward * .7f, scenery.Cemetery);
                });
            }
        }

        void BeginDebate(bool tryAgain)
        {
            stage = Stage.Debate;
            secondChance = tryAgain;
            sentForHelp = false;
            remaining = DebateDuration;
            debateBeat = -1;
            scenery.HangStudy();
            vincent.transform.position = scenery.House + new Vector3(-.88f, 0, 1.13f);
            gauguin.gameObject.SetActive(true);
            neighbor.SetActive(tryAgain);
            gauguin.transform.position = scenery.House + new Vector3(.88f, 0, 1.13f);
            MovePlayer(scenery.House + Vector3.back * .55f, scenery.House + Vector3.forward);
            if (tryAgain) dialogue.ShowOpening(retry);
            else RevealDebate();
        }

        void RevealDebate()
        {
            StoryMenu.Show(timer, true);
            StoryMenu.Show(debateSubtitle, true);
        }

        void UpdateDebate()
        {
            RevealDebate();
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            timer.text = $"00:{Mathf.CeilToInt(remaining):00}";
            if (remaining <= 0f)
            {
                StoryMenu.Show(timer, false);
                StoryMenu.Show(debateSubtitle, false);
                gauguin.gameObject.SetActive(false);
                neighbor.SetActive(false);
                if (!sentForHelp) progress.Notify(dialogue.Local("未能及時求援　— RETRY —", "助けを呼ぶのに間に合わなかった　— RETRY —"), 3f);
                progress.SetObjective("");
                stage = Stage.Ending;
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

            if (!secondChance)
                Objective("藝術的辯論　聆聽兩位畫家", "芸術の議論　二人の話を聞く");
            else if (sentForHelp)
                Objective("鄰居已去請醫生，留在小屋附近等候", "近所の人が医師を呼びに行った。家のそばで待とう");
            else if (Near(scenery.Door, 1.15f))
            {
                var key = dialogue.InteractionKeyLabel;
                Objective($"按 {key} 請門外的鄰居找醫生", $"{key} で玄関の近所の人に医師を呼んでもらう");
                if (dialogue.TryConsumeGameplayInteraction())
                {
                    sentForHelp = true;
                    neighbor.SetActive(false);
                    progress.Notify(dialogue.Local("鄰居已出發求援", "近所の人が助けを呼びに向かった"), 3f);
                }
            }
            else
                Objective("在倒數結束前，到門口請鄰居找醫生", "時間内に玄関で近所の人に医師を呼んでもらう", scenery.Door);
        }

        void RetryDebate()
        {
            progress.SaveCheckpoint(RetryCheckpoint);
            BeginDebate(true);
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

            timer = new Label { pickingMode = PickingMode.Ignore };
            timer.AddToClassList("chapter-three-timer");
            progress.AddChapterUI(timer);
            debateSubtitle = new VisualElement { pickingMode = PickingMode.Ignore };
            debateSubtitle.AddToClassList("chapter-three-subtitle");
            progress.AddChapterUI(debateSubtitle);
            debateSpeaker = StoryMenu.Label(debateSubtitle, "", "chapter-three-speaker");
            debateLine = StoryMenu.Label(debateSubtitle, "", "chapter-three-line");
            StoryMenu.Show(timer, false);
            StoryMenu.Show(debateSubtitle, false);
        }

        void OnDestroy()
        {
            if (dialogue != null) dialogue.LineChanged -= TrackNode;
            scenery?.Dispose();
        }
    }
}
