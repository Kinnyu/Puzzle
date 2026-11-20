using System;
using System.Collections.Generic;
using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    public sealed class StarryNightChapterFour : MonoBehaviour
    {
        internal const string ChapterId = "chapter-4";
        const string StarryCheckpoint = "starry-night-painted";
        const string ReadyCheckpoint = "exhibition-ready";
        const string OpenCheckpoint = "exhibition-open";
        const string DayCheckpoint = "a-quiet-day";
        const string SpringCheckpoint = "gift-of-spring";
        const int FameThreshold = 80;

        enum Stage
        {
            Visit, Inspiration, Proposal, Collect, Hang, InviteBrief, Invite, InviteRetry,
            FameBrief, Fame, FameRetry, ExhibitEnd, Doctor, Breakfast, Walk, Lunch, Tools,
            Setup, Paint, Gift, Evening, Morning, Finished
        }

        readonly struct Interaction
        {
            internal readonly Vector3 Position;
            internal readonly string Chinese, Japanese;
            internal readonly Action Action;
            internal readonly bool Guide;

            internal Interaction(Vector3 position, string chinese, string japanese, Action action, bool guide)
            {
                Position = position;
                Chinese = chinese;
                Japanese = japanese;
                Action = action;
                Guide = guide;
            }
        }

        static readonly (string Chinese, string Japanese)[] PaintingNames =
        {
            ("《星夜》", "『星月夜』"), ("《吃馬鈴薯的人》", "『ジャガイモを食べる人々』"),
            ("《阿里斯康墓園》", "『アリスカン』")
        };
        static readonly (string Chinese, string Japanese)[] ToolNames =
        {
            ("畫布", "カンヴァス"), ("畫筆", "筆"), ("顏料", "絵の具"), ("畫架", "イーゼル")
        };
        static readonly (string Chinese, string Japanese)[] SetupNames =
        {
            ("架好畫架", "イーゼルを立てる"), ("放上畫布", "カンヴァスを載せる"), ("擺好畫筆與顏料", "筆と絵の具を並べる")
        };

        [SerializeField] StarryNightWalk walk;
        [SerializeField] DialoguePlayer dialogue;
        [SerializeField] StarryNightChapterTwo terrain;
        [SerializeField] StarryNightChapterThree previous;
        [SerializeField] GameObject clinicLot, galleryLot;
        [SerializeField] DialogueConversation script;
        [SerializeField] ChapterOpeningText opening;
        [SerializeField] Sprite vincentSprite, theoSprite, doctorSprite, minerSprite;
        [SerializeField] Sprite[] visitorSprites;
        [SerializeField] Texture2D starryNight, potatoEaters;
        [SerializeField] Vector3 church = new(5, 0, 2);
        [SerializeField] Vector3 mine = new(34, 0, 0);

        readonly List<Interaction> interactions = new();
        readonly GameObject[] visitors = new GameObject[5];
        StorySceneProgress progress;
        ChapterThreeScenery oldVillage;
        ChapterFourScenery scenery;
        GameObject vincent, theo, doctor, miner, letter;
        Vector3 letterPoint;
        VisualElement artCard;
        Image artwork;
        Label artTitle, timer;
        Button artContinue;
        Action afterArtwork;
        Stage stage;
        int checkedItems;
        int fame;
        float remaining;
        string lastNode;
        string goalChinese, goalJapanese;
        bool artHadCursorLock;

        Vector3 Player => walk.Player.transform.position;
        Vector3 House => scenery.House;
        Vector3 Clinic => scenery.Clinic;
        Vector3 Gallery => scenery.Gallery;

        internal void Begin(StorySceneProgress sceneProgress, string checkpoint)
        {
            progress = sceneProgress;
            terrain.EndVisit();
            previous.EndVisit();
            oldVillage = previous.GetVillageScenery();
            var art = new[] { starryNight, potatoEaters, oldVillage.Study(PaintingStudy.All) };
            scenery = new ChapterFourScenery(transform, terrain.Ground, clinicLot.transform.position, galleryLot.transform.position,
                oldVillage.House, art);
            clinicLot.SetActive(false);
            galleryLot.SetActive(false);
            vincent = Actor("Vincent - Chapter 4", vincentSprite);
            theo = Actor("Theo - Chapter 4", theoSprite);
            doctor = Actor("Saint-Rémy physician", doctorSprite);
            miner = Actor("Miner - Chapter 4", minerSprite);
            letterPoint = terrain.Ground(oldVillage.Lookout + Vector3.right * 1.3f);
            letter = scenery.PlaceLetter(terrain.Ground(letterPoint + Vector3.left * .58f));
            letter.SetActive(false);
            for (int i = 0; i < visitors.Length; i++)
                visitors[i] = Actor($"Exhibition visitor {i + 1}", visitorSprites[i % visitorSprites.Length]);
            SetVisible(scenery.Copies, false);
            SetVisible(scenery.Exhibits, false);
            SetVisible(scenery.Supplies, false);
            SetVisible(scenery.Setup, false);
            BuildUI();
            dialogue.LineChanged += TrackNode;
            switch (checkpoint)
            {
                case StarryCheckpoint:
                    Move(Clinic + Vector3.back, Clinic);
                    SetStage(Stage.Proposal);
                    break;
                case ReadyCheckpoint:
                case OpenCheckpoint:
                    SetVisible(scenery.Exhibits, true);
                    Move(Gallery + Vector3.back, Gallery);
                    SetStage(checkpoint == ReadyCheckpoint ? Stage.InviteBrief : Stage.FameBrief);
                    break;
                case DayCheckpoint:
                    SetVisible(scenery.Exhibits, true);
                    Move(Gallery + Vector3.back, Gallery);
                    SetStage(Stage.ExhibitEnd);
                    break;
                case SpringCheckpoint:
                    SetVisible(scenery.Exhibits, true);
                    Move(House, House + Vector3.forward);
                    SetStage(Stage.Tools);
                    break;
                default:
                    Move(church + Vector3.back * 3.5f, Clinic);
                    SetStage(Stage.Visit);
                    dialogue.ShowOpening(opening);
                    break;
            }
        }

        GameObject Actor(string name, Sprite sprite)
        {
            var actor = new GameObject(name);
            actor.transform.SetParent(transform);
            actor.AddComponent<SpriteRenderer>().sprite = sprite;
            actor.AddComponent<CharacterBillboard>();
            actor.SetActive(false);
            return actor;
        }

        static void Put(GameObject actor, Vector3 position)
        {
            actor.transform.position = position;
            actor.SetActive(true);
        }

        void Move(Vector3 point, Vector3 direction)
        {
            Vector3 facing = direction - point;
            facing.y = 0;
            point = terrain.Ground(point);
            foreach (var room in new[] { House, Clinic, Gallery })
                if (Mathf.Abs(point.x - room.x) < 2.5f && Mathf.Abs(point.z - room.z) < 1.95f)
                    point.y = room.y;
            walk.EnterWalkingAt(point + Vector3.up * .04f, Quaternion.LookRotation(facing));
        }

        void SetStage(Stage next)
        {
            stage = next;
            checkedItems = 0;
            remaining = 0f;
            SetVisible(visitors, false);
            vincent.SetActive(false);
            theo.SetActive(false);
            doctor.SetActive(false);
            miner.SetActive(false);
            letter.SetActive(false);
            RefreshInteractions();
        }

        void RefreshInteractions()
        {
            interactions.Clear();
            switch (stage)
            {
                case Stage.Visit:
                    Goal("前往療養院探望梵谷", "療養院のゴッホを訪ねる");
                    Talk(vincent, Clinic + new Vector3(.1f, 0, 1.38f), "visit", () => SetStage(Stage.Inspiration), "梵谷", "ゴッホ");
                    break;
                case Stage.Inspiration:
                    SetInspiration();
                    break;
                case Stage.Proposal:
                    Goal("聽西奧的畫展提議", "テオの展覧会の提案を聞く");
                    Put(vincent, Clinic + new Vector3(-.22f, 0, .96f));
                    Talk(theo, Clinic + new Vector3(.72f, 0, .65f), "proposal", () =>
                    {
                        SetVisible(scenery.Copies, true);
                        SetStage(Stage.Collect);
                    }, "西奧", "テオ");
                    break;
                case Stage.Collect:
                    SetPaintings(false);
                    break;
                case Stage.Hang:
                    SetPaintings(true);
                    break;
                case Stage.InviteBrief:
                    Goal("向小畫館裡的西奧回報", "画廊のテオに報告する");
                    Talk(theo, Gallery + new Vector3(-1.53f, 0, -.91f), "invite-brief", () =>
                    {
                        Move(church + new Vector3(2f, 0, -3.8f), church);
                        SetStage(Stage.Invite);
                        remaining = 90f;
                    }, "西奧", "テオ");
                    break;
                case Stage.Invite:
                    SetVisitors(false);
                    break;
                case Stage.InviteRetry:
                    Goal("重新準備宣傳", "もう一度、宣伝の準備をする");
                    Talk(theo, Gallery + new Vector3(-1.53f, 0, -.91f), "invite-fail",
                        () => SetStage(Stage.InviteBrief), "西奧", "テオ");
                    break;
                case Stage.FameBrief:
                    Goal("回畫館找西奧開幕", "画廊に戻り、テオと開幕する");
                    Talk(theo, Gallery + new Vector3(-1.53f, 0, -.91f), "fame-brief", () =>
                    {
                        fame = 50;
                        SetStage(Stage.Fame);
                        remaining = 120f;
                    }, "西奧", "テオ");
                    break;
                case Stage.Fame:
                    SetVisitors(true);
                    break;
                case Stage.FameRetry:
                    Goal("重新準備作品介紹", "もう一度、作品の紹介を準備する");
                    Talk(theo, Gallery + new Vector3(-1.53f, 0, -.91f), "fame-fail",
                        () => SetStage(Stage.FameBrief), "西奧", "テオ");
                    break;
                case Stage.ExhibitEnd:
                    Goal("到《星夜》前找梵谷", "『星月夜』の前にいるゴッホに会う");
                    Put(theo, Gallery + new Vector3(.92f, 0, .88f));
                    Talk(vincent, scenery.Display[0], "exhibition-end", () => SetStage(Stage.Doctor), "梵谷", "ゴッホ");
                    break;
                case Stage.Doctor:
                    Goal("到療養院和醫師談談", "療養院で医師と話す");
                    Put(theo, Clinic + new Vector3(-.86f, 0, .46f));
                    Talk(doctor, Clinic + new Vector3(.1f, 0, 1.25f), "doctor", () => SetStage(Stage.Breakfast), "醫師", "医師");
                    break;
                case Stage.Breakfast:
                    Goal("陪伴梵谷的一天　回黃色小屋吃早餐", "ゴッホと過ごす一日　黄色い家で朝食を取る");
                    Put(vincent, House + new Vector3(.8f, 0, .98f));
                    Talk(theo, House + new Vector3(-.6f, 0, .70f), "breakfast", () => SetStage(Stage.Walk), "西奧", "テオ");
                    break;
                case Stage.Walk:
                    SetWalk();
                    break;
                case Stage.Lunch:
                    Goal("散步結束　回黃色小屋吃午餐", "散歩を終え、黄色い家で昼食を取る");
                    Talk(vincent, House + new Vector3(.23f, 0, .9f), "lunch", () =>
                    {
                        progress.SaveCheckpoint(SpringCheckpoint);
                        SetStage(Stage.Tools);
                    }, "梵谷", "ゴッホ");
                    break;
                case Stage.Tools:
                    SetTools();
                    break;
                case Stage.Setup:
                    SetCanvas();
                    break;
                case Stage.Paint:
                    Goal("畫具已經備好，請梵谷開始作畫", "画材がそろった。ゴッホに絵を描いてもらう");
                    Put(vincent, scenery.Gift + Vector3.left * .72f);
                    Add(scenery.Gift, "請梵谷開始畫畫", "ゴッホに絵を描いてもらう", PaintGift);
                    break;
                case Stage.Gift:
                    Goal("在杏樹旁和梵谷聊聊", "杏の木のそばでゴッホと話す");
                    Talk(vincent, scenery.Gift + Vector3.left * .72f, "thanks", () => SetStage(Stage.Evening), "梵谷", "ゴッホ");
                    break;
                case Stage.Evening:
                    Goal("回小屋一起吃晚餐並休息", "家に戻り、夕食を取って休む");
                    Talk(theo, House + new Vector3(-.60f, 0, .66f), "evening", () => SetStage(Stage.Morning), "西奧", "テオ");
                    break;
                case Stage.Morning:
                    Goal("嶄新的早晨　跟梵谷道早安", "新しい朝　ゴッホにおはようを言う");
                    Put(theo, House + new Vector3(-.6f, 0, .7f));
                    Talk(vincent, House + new Vector3(.76f, 0, .96f), "morning", () =>
                    {
                        SetStage(Stage.Finished);
                        progress.CompleteChapter();
                    }, "梵谷", "ゴッホ");
                    break;
                case Stage.Finished:
                    Goal("", "");
                    break;
            }
        }

        void SetInspiration()
        {
            Goal($"尋找《星夜》的靈感（{Count}/3）", $"『星月夜』の着想を探す（{Count}/3）");
            if (!Has(0)) Add(scenery.Inspiration[0], "觀察窗外的星空", "窓の外の星空を見る", () => Play("sky", () => Mark(0)));
            if (!Has(1)) Add(scenery.Inspiration[1], "觀察庭院的柏樹", "中庭の糸杉を見る", () => Play("cypress", () => Mark(1)));
            if (!Has(2)) Add(scenery.Inspiration[2], "在畫架前構思村莊", "イーゼルの前で村を思い描く", () => Play("village", () => Mark(2)));
            var position = Clinic + new Vector3(.1f, 0, 1.38f);
            Put(vincent, position);
            if (Count == 3)
                Add(position, "把靈感告訴梵谷", "ゴッホに着想を伝える", () => Play("starry-made", () =>
                    ShowArtwork(starryNight, "《星夜》", "『星月夜』", () =>
                    {
                        progress.SaveCheckpoint(StarryCheckpoint);
                        SetStage(Stage.Proposal);
                    })));
            else
                Add(position, "向梵谷詢問靈感", "ゴッホに着想について聞く", () => Play("starry-missing", null), false);
        }

        void SetPaintings(bool hanging)
        {
            Goal(hanging ? $"布置展覽（{Count}/3）" : $"收集展覽畫作（{Count}/3）",
                hanging ? $"作品を展示する（{Count}/3）" : $"展示作品を集める（{Count}/3）");
            for (int i = 0; i < PaintingNames.Length; i++)
            {
                if (Has(i)) continue;
                int index = i;
                var name = PaintingNames[index];
                Add(hanging ? scenery.Display[index] : scenery.Paintings[index],
                    (hanging ? "掛上" : "拿取") + name.Chinese, name.Japanese + (hanging ? "を掛ける" : "を受け取る"),
                    () =>
                    {
                        if (hanging) scenery.Exhibits[index].SetActive(true);
                        else scenery.Copies[index].SetActive(false);
                        checkedItems |= 1 << index;
                        if (Count < 3) RefreshInteractions();
                        else if (hanging)
                        {
                            progress.SaveCheckpoint(ReadyCheckpoint);
                            SetStage(Stage.InviteBrief);
                        }
                        else SetStage(Stage.Hang);
                    });
            }
        }

        void SetVisitors(bool atGallery)
        {
            int total = atGallery ? 3 : visitors.Length;
            Goal(atGallery ? $"讓作品被理解（{Count}/3）" : $"邀請觀眾（{Count}/5）",
                atGallery ? $"作品の背景を伝える（{Count}/3）" : $"来場者を誘う（{Count}/5）");
            for (int i = 0; i < total; i++)
            {
                int index = i;
                var visitor = visitors[i];
                if (Has(i))
                {
                    visitor.SetActive(atGallery);
                    continue;
                }
                Vector3 position = atGallery ? scenery.Display[i] + Vector3.back * .63f
                    : terrain.Ground(church + new Vector3(-.85f + 1.30f * i, 0, -2.14f - .42f * (i % 2)));
                Put(visitor, position);
                string node = atGallery ? i == 0 ? "night" : i == 1 ? "potato" : "cemetery" : $"invite-{i + 1}";
                Add(position, atGallery ? "向觀眾介紹" + PaintingNames[i].Chinese : "邀請這位路人",
                    atGallery ? PaintingNames[i].Japanese + "を紹介する" : "展覧会に誘う",
                    () => Play(node, () => AnswerVisitor(index, atGallery)));
            }
        }

        void AnswerVisitor(int i, bool atGallery)
        {
            if (atGallery) fame = Mathf.Clamp(fame + (lastNode.EndsWith("-good", StringComparison.Ordinal) ? 15 : -10), 0, 100);
            checkedItems |= 1 << i;
            if (Count != (atGallery ? 3 : 5))
            {
                RefreshInteractions();
                return;
            }
            FinishTimedStage(atGallery, !atGallery || fame >= FameThreshold);
        }

        void FinishTimedStage(bool atGallery, bool success)
        {
            StoryMenu.Show(timer, false);
            if (dialogue.IsPlaying) dialogue.Cancel();
            Move(Gallery + Vector3.back * 1.19f, Gallery);
            if (!success)
            {
                progress.Notify(dialogue.Local("CHAPTER 4　— FAILED —　再試一次", "CHAPTER 4　— FAILED —　もう一度"), 3f);
                SetStage(atGallery ? Stage.FameRetry : Stage.InviteRetry);
                return;
            }
            if (atGallery)
            {
                progress.Notify(dialogue.Local($"畫展名聲達標　{fame}/100", $"展覧会の評判　{fame}/100"), 3f);
                progress.SaveCheckpoint(DayCheckpoint);
                SetStage(Stage.ExhibitEnd);
            }
            else
            {
                progress.Notify(dialogue.Local("觀眾招攬成功　5/5", "来場者の招待に成功　5/5"), 3f);
                progress.SaveCheckpoint(OpenCheckpoint);
                SetStage(Stage.FameBrief);
            }
        }

        void SetWalk()
        {
            Goal($"重訪過去的足跡（{Count}/3）", $"思い出の場所を訪ねる（{Count}/3）");
            var locations = new[] { terrain.Ground(church + Vector3.back * 2f), terrain.Ground(mine), letterPoint };
            string[] nodes = { "walk-church", "walk-farm", "walk-graves" };
            string[] chinese = { "和神職人員交談", "和礦工交談", "讀高更的信" };
            string[] japanese = { "聖職者と話す", "鉱夫と話す", "ゴーギャンの手紙を読む" };
            Put(doctor, locations[0]);
            Put(miner, locations[1]);
            letter.SetActive(!Has(2));
            for (int i = 0; i < locations.Length; i++)
            {
                if (Has(i)) continue;
                int index = i;
                Add(locations[i], chinese[i], japanese[i], () =>
                {
                    Put(vincent, terrain.Ground(locations[index] + Vector3.left * .7f));
                    Play(nodes[index], () =>
                    {
                        checkedItems |= 1 << index;
                        if (Count == 3) SetStage(Stage.Lunch);
                        else RefreshInteractions();
                    });
                });
            }
        }

        void SetTools()
        {
            Goal($"準備繪畫工具（{Count}/4）", $"画材を集める（{Count}/4）");
            for (int i = 0; i < ToolNames.Length; i++)
            {
                int index = i;
                scenery.Supplies[i].SetActive(!Has(i));
                if (Has(i)) continue;
                var name = ToolNames[i];
                Add(scenery.Tools[i], "拿取" + name.Chinese, name.Japanese + "を取る", () =>
                {
                    scenery.Supplies[index].SetActive(false);
                    checkedItems |= 1 << index;
                    if (Count == 4) SetStage(Stage.Setup);
                    else RefreshInteractions();
                });
            }
        }

        void SetCanvas()
        {
            Goal($"把畫具放到杏樹旁（{Count}/3）", $"杏の木のそばで画材を用意する（{Count}/3）");
            int index = Count;
            var placement = SetupNames[index];
            Add(scenery.Gift, placement.Chinese, placement.Japanese, () =>
            {
                scenery.Setup[index].SetActive(true);
                checkedItems |= 1 << index;
                if (Count == 3)
                {
                    SetStage(Stage.Paint);
                    PaintGift();
                }
                else RefreshInteractions();
            });
        }

        void PaintGift() => Play("spring", () =>
        {
            scenery.FinishCanvas();
            ShowArtwork(scenery.GiftPainting, "《春天的禮物》", "『春の贈り物』", () => SetStage(Stage.Gift));
        });

        void Update()
        {
            if (progress == null || walk.InputSuppressed) return;
            if (stage is Stage.Invite or Stage.Fame)
            {
                remaining = Mathf.Max(remaining - Time.deltaTime, 0);
                if (remaining <= 0f)
                {
                    FinishTimedStage(stage == Stage.Fame, false);
                    return;
                }
                int seconds = Mathf.CeilToInt(remaining);
                timer.text = $"{seconds / 60:00}:{seconds % 60:00}" + (stage == Stage.Fame ? $"   {fame}/100" : $"   {Count}/5");
                StoryMenu.Show(timer, true);
            }
            if (dialogue.BlocksGameplayInput || interactions.Count == 0) return;

            int nearest = -1, available = -1;
            float nearestDistance = float.PositiveInfinity, availableDistance = float.PositiveInfinity;
            for (int i = 0; i < interactions.Count; i++)
            {
                var offset = interactions[i].Position - Player;
                float elevation = Mathf.Abs(offset.y);
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                if (interactions[i].Guide && distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
                if (elevation <= 1.1f && distance < availableDistance && distance <= 1.30f * 1.30f)
                {
                    available = i;
                    availableDistance = distance;
                }
            }
            string detail = available >= 0
                ? $"    [{dialogue.InteractionKeyLabel}] {dialogue.Local(interactions[available].Chinese, interactions[available].Japanese)}"
                : nearest >= 0 ? Direction(interactions[nearest].Position, nearestDistance) : "";
            progress.SetObjective(dialogue.Local(goalChinese, goalJapanese), detail);
            if (available >= 0 && dialogue.TryConsumeGameplayInteraction()) interactions[available].Action();
        }

        string Direction(Vector3 point, float squaredDistance)
        {
            Vector3 direction = point - Player;
            direction.y = 0f;
            float degrees = Vector3.SignedAngle(Vector3.ProjectOnPlane(walk.transform.forward, Vector3.up), direction, Vector3.up);
            string arrow = Mathf.Abs(degrees) > 135 ? "↓" : degrees > 45 ? "→" : degrees < -45 ? "←" : "↑";
            return $"    {arrow} {Mathf.RoundToInt(Mathf.Sqrt(squaredDistance))} m";
        }

        void Goal(string chinese, string japanese)
        {
            goalChinese = chinese;
            goalJapanese = japanese;
            progress.SetObjective(dialogue.Local(chinese, japanese));
        }

        void Add(Vector3 position, string chinese, string japanese, Action action, bool guide = true) =>
            interactions.Add(new Interaction(position, chinese, japanese, action, guide));

        void Talk(GameObject actor, Vector3 position, string node, Action completion, string chinese, string japanese)
        {
            Put(actor, position);
            Add(position, "和" + chinese + "交談", japanese + "と話す", () => Play(node, completion));
        }

        bool Has(int index) => (checkedItems & (1 << index)) != 0;
        int Count
        {
            get
            {
                int count = 0;
                for (int bits = checkedItems; bits != 0; bits &= bits - 1) count++;
                return count;
            }
        }

        void Mark(int index)
        {
            checkedItems |= 1 << index;
            RefreshInteractions();
        }

        static void SetVisible(GameObject[] objects, bool visible)
        {
            foreach (var item in objects) item.SetActive(visible);
        }

        void TrackNode(DialogueConversation asset, string node)
        {
            if (asset == script) lastNode = node;
        }

        void Play(string node, Action complete) => dialogue.TryPlay(script, completed: complete, startNodeId: node);

        void BuildUI()
        {
            timer = new Label { pickingMode = PickingMode.Ignore };
            timer.AddToClassList("chapter-three-timer");
            progress.AddChapterUI(timer);
            StoryMenu.Show(timer, false);
            artCard = new VisualElement();
            artCard.AddToClassList("story-overlay");
            progress.AddChapterUI(artCard);
            var card = StoryMenu.Panel(artCard, "story-card");
            card.AddToClassList("chapter-three-review");
            artTitle = StoryMenu.Label(card, "", "story-card-title");
            artwork = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            artwork.AddToClassList("chapter-three-image");
            card.Add(artwork);
            artContinue = StoryMenu.Button(card, "", CloseArtwork, "story-primary");
            StoryMenu.Show(artCard, false);
        }

        void ShowArtwork(Texture2D texture, string chinese, string japanese, Action complete)
        {
            afterArtwork = complete;
            artHadCursorLock = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            artwork.image = texture;
            artTitle.text = dialogue.Local(chinese, japanese);
            artContinue.text = dialogue.Local("繼續", "続ける");
            StoryMenu.Show(artCard, true);
            artContinue.Focus();
        }

        void CloseArtwork()
        {
            var complete = afterArtwork;
            afterArtwork = null;
            StoryMenu.Show(artCard, false);
            walk.InputSuppressed = false;
            StoryMenu.SetCursor(artHadCursorLock);
            complete?.Invoke();
        }

        void OnDestroy()
        {
            if (dialogue != null) dialogue.LineChanged -= TrackNode;
            scenery?.Dispose();
        }
    }
}
