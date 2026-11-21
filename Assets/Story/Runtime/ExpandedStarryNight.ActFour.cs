using System;
using UnityEngine;
using Mask = Puzzle.Story.StoryBrush.Shape;
using Ink = Puzzle.Story.StoryBrush.Option;

namespace Puzzle.Story
{
    internal sealed partial class ExpandedStarryNight
    {
        static readonly string[] Works = { "《星夜》", "《吃馬鈴薯的人》", "《阿里斯康墓園》" };
        Vector3 Clinic => exhibition.Clinic;
        Vector3 Gallery => exhibition.Gallery;
        Texture2D Work(int index) => index == 0 ? fourth.StarryArt : index == 1 ? fourth.PotatoArt : village.Study(PaintingStudy.All);

        void ExhibitionStory()
        {
            Visible(exhibition.Copies, false);
            Visible(exhibition.Supplies, false);
            Visible(exhibition.Setup, false);
            ShowExhibition(false);
            Scene("asylum-reunion", () =>
            {
                var point = terrain.Ground(Clinic + Vector3.back * 3.0f);
                var attendant = Actor("療養院工作人員", third.NeighborSprite, point + Vector3.right * .65f);
                var vincent = Vincent(Clinic + new Vector3(-.84f, 0, .36f));
                var guide = Actor("神職人員", third.NeighborSprite, Church + Vector3.right * .86f);
                Arrive(Church + Vector3.back * 1.55f, guide.transform.position);
                Answer(guide, "向神職人員詢問梵谷的近況", () => Speak(AskAttendant,
                    "神職人員|文森特在北方的療養院休養。先問問工作人員，今天方不方便探訪。"));
                void AskAttendant() => Answer(attendant, "沿北路走到療養院，詢問今天是否方便探訪梵谷", () =>
                    Speak(() => Answer(vincent, "到窗邊和梵谷談談", () =>
                    {
                        ClearTargets();
                        Ask("梵谷", "你來了。我還記得教堂的窗、礦區的燈，還有黃色小屋那面牆。", answer =>
                            Speak(Next, answer == 0 ? "梵谷|有一幅夜空一直在我腦海裡，還沒畫完。"
                                    : "梵谷|有些日子比較安穩，有些日子很難。我還在學著與它相處。",
                                "梵谷|你願意陪我把眼前和心裡的景色放到同一張畫上嗎？"),
                            "我想看看你最近畫了什麼。", "今天你的感覺怎麼樣？");
                    }), "工作人員|今天他願意見朋友。請先敲門，他在窗旁。"));
            });

            Scene("starry-paint", () =>
            {
                Vincent(Clinic + new Vector3(-.63f, 0, .88f));
                var easel = CanvasAtLedge("《星夜》的畫布", exhibition.ClinicEasel, .90f, .68f);
                var names = new[] { "窗邊的星空", "庭院的柏樹", "想像中的村莊" };
                var remarks = new[] { "梵谷|夜空有時很安靜，有時在我眼裡像不停流動。",
                    "梵谷|它往上伸展，像想碰到天。",
                    "梵谷|那不是窗外完整看見的村子，是我想放進畫裡的地方。" };
                var masks = new[] { Mask.Star, Mask.Branch, Mask.Panel };
                var inks = new[] { Gold, Green, Blue };
                int count = 0;
                Arrive(Clinic + Vector3.back * 1.25f, easel.transform.position);
                Goal("找出《星夜》的三片靈感  0/3");
                for (int i = 0; i < names.Length; i++)
                {
                    int piece = i;
                    Vector3 position = exhibition.Inspiration[piece] + Vector3.up * (piece == 0 ? 1.38f : 1.04f);
                    var card = PostedSign(names[i] + "畫片", position, .50f, .38f);
                    ObjectAction(card, $"觀察{names[i]}", () =>
                    {
                        targets.RemoveAll(target => target.Object == card.transform);
                        Speak(() =>
                        {
                            BrushAt(card, $"把{names[piece]}畫進小畫片", _ =>
                            {
                                card.SetActive(false);
                                ImageProp(names[piece], easel.transform.position +
                                    new Vector3((piece - 1) * .26f, -.21f, -.082f), null, .21f, .15f);
                                Goal($"找出《星夜》的三片靈感  {++count}/3");
                                if (count == 3) MakeNight();
                            }, false, new Ink(names[piece], masks[piece], inks[piece]));
                        }, remarks[piece]);
                    });
                }

                void MakeNight()
                {
                    Layers(easel, "和梵谷畫出《星夜》", choice =>
                    {
                        Save("starry-composition", choice);
                        ColorProp(easel.transform.GetChild(0).gameObject, Color.white, fourth.StarryArt);
                        canvas.Review("《星夜》", fourth.StarryArt, "1889 年。梵谷把觀察、記憶與想像放進同一幅畫。",
                            () => Speak(Next,
                                "梵谷|謝謝你幫我整理那些看見與想像的東西。這幅畫最後的樣子，我也想自己再想一想。"));
                    }, Layer("塗上深藍色夜空", Mask.Sky, Blue),
                        new[] { new Ink("先安排星星的位置", Mask.Star, Gold), new Ink("先安排柏樹的位置", Mask.Branch, Green) },
                        Layer("沿著旋轉的夜空留下筆觸", Mask.Spiral, Gold));
                }
            });

            Scene("starry-night-painted", () =>
            {
                var theo = Theo(Clinic + new Vector3(1.66f, 0, .05f));
                Vincent(Clinic + new Vector3(-.86f, 0, .55f));
                CanvasAtLedge("完成的《星夜》", exhibition.ClinicEasel, .90f, .68f, fourth.StarryArt);
                Arrive(Clinic + Vector3.back * 1.21f, theo.transform.position);
                Answer(theo, "聽西奧的展覽提議", () =>
                    Speak(() =>
                    {
                        var owner = Actor("畫廊主人", third.NeighborSprite, Gallery + new Vector3(.62f, 0, .69f));
                        Answer(owner, "向畫廊主人確認展覽空間", () =>
                            Speak(Next, "畫廊主人|場地不大，但每幅畫都能有自己的位置。"));
                    }, "西奧|文森特，我找到一個願意借我們場地的小畫廊。",
                        "梵谷|真的有人願意看嗎？我不想讓你再為我操心。",
                        "西奧|我只是希望作品有機會遇見觀眾。", "玩家|我們可以一起準備。"));
            }, cut: "稍後　・　西奧來看完成的《星夜》");

            Scene("gallery-hang", () =>
            {
                ShowExhibition(false);
                Visible(exhibition.Copies, true);
                Theo(Gallery + new Vector3(1.85f, 0, .83f));
                Vincent(Gallery + new Vector3(-1.75f, 0, .66f));
                Arrive(Clinic + new Vector3(1.47f, 0, -1.28f), exhibition.Copies[0].transform.position);
                var items = new (GameObject prop, string verb, Action found)[3];
                for (int i = 0; i < items.Length; i++)
                {
                    var copy = exhibition.Copies[i];
                    items[i] = (copy, $"拿起{Works[i]}", () => copy.SetActive(false));
                }
                Collect("到療養院和黃色小屋收集三幅展畫", items, () =>
                {
                    var plates = new GameObject[3];
                    int frames = 0;
                    Goal("在畫廊牆上畫出三個畫框  0/3");
                    for (int i = 0; i < plates.Length; i++)
                    {
                        int index = i;
                        plates[i] = ImageProp("空白掛畫位置", exhibition.Exhibits[i].transform.position + Vector3.back * .045f, null, 1.32f, .96f);
                        var plate = plates[i];
                        BrushAt(plate, $"畫出第{(i == 1 ? "一" : i == 0 ? "二" : "三")}個畫框", _ =>
                        {
                            plate.SetActive(false);
                            exhibition.Frames[index].SetActive(true);
                            exhibition.Backings[index].SetActive(true);
                            Goal($"在畫廊牆上畫出三個畫框  {++frames}/3");
                            if (frames == 3) Arrange();
                        }, false, new Ink("畫框的邊緣", Mask.Frame, Wood));
                    }
                });

                void Arrange()
                {
                    Ask("西奧", "你想用什麼順序把這些日子掛上牆？", order =>
                    {
                        Save("gallery-order", order);
                        ArrangeExhibits(order);
                        int hung = 0;
                        Goal("把三幅畫掛進畫框  0/3");
                        for (int i = 0; i < Works.Length; i++)
                        {
                            int index = i;
                            var artwork = exhibition.Exhibits[i];
                            Offer(artwork.transform.position, $"掛上{Works[i]}", () =>
                            {
                                targets.RemoveAll(target => target.Object == artwork.transform);
                                artwork.SetActive(true);
                                Goal($"把三幅畫掛進畫框  {++hung}/3");
                                if (hung == Works.Length)
                                    Speak(Next, "梵谷|你把它們排在一起，我才發現這些年真的走了很遠。",
                                        "西奧|那些你一直想理解的人，也都在這裡。");
                            }, artwork.transform);
                        }
                    }, "依人生的時間順序", "依色調由暗到亮");
                }
            });

            Scene("exhibition-ready", () =>
            {
                ShowExhibition(true);
                StartInvites();
            }, approach: (Church + Vector3.back * 1.75f, "走到教堂廣場，準備邀請街坊看畫展"));
            Scene("exhibition-open", () =>
            {
                ShowExhibition(true);
                StartQuestions();
            }, cut: "開幕當天　・　畫廊裡的第一批觀眾");

            Scene("a-quiet-day", () =>
            {
                ShowExhibition(true);
                var vincent = Vincent(terrain.Ground(Gallery + new Vector3(3.48f, 0, 1.03f)));
                Theo(vincent.transform.position + Vector3.right * 1.05f);
                Arrive(terrain.Ground(Gallery + new Vector3(4.37f, 0, -.78f)), vincent.transform.position);
                Answer(vincent, "聽梵谷說說畫展之後的感受", () =>
                    Ask("梵谷", "今天很美好。我卻還是擔心明天會不會再變得難熬。", _ =>
                        Speak(Next, "梵谷|好。我希望畫畫能繼續，也希望自己能平安地生活。"),
                        "我們可以把今天記下來。不用逼自己每天都一樣好。", "一起去找醫師，談談接下來的照顧。"));
            }, cut: "散場後　・　畫廊側門外");

            Scene("doctor", () =>
            {
                var physician = Actor("醫師", fourth.DoctorSprite, Clinic + new Vector3(1.56f, 0, .40f));
                Vincent(Clinic + new Vector3(-.55f, 0, .75f));
                Theo(Clinic + new Vector3(-.96f, 0, .11f));
                Arrive(terrain.Ground(Clinic + Vector3.back * 3.7f), Clinic);
                Answer(physician, "回北方療養院，和醫師討論接下來的照顧", () =>
                    Speak(Next, "醫師|我們從規律的作息、休息和定期見面開始。遇到困難時，請繼續告訴我們。",
                        "醫師|藝術可以留在你的生活裡，照護也會繼續。",
                        "西奧|明天我們陪你過一天，不用急著完成任何大事。"));
            });

            Scene("day-breakfast", () =>
            {
                var table = Table(House + Vector3.forward * .34f);
                Theo(House + new Vector3(.88f, 0, .66f));
                Vincent(House + new Vector3(-.91f, 0, .61f));
                RestOnTable(Period(terrain.Models.loaf, table.transform.position), table, -.34f, .06f);
                var card = TableSketch("早餐桌上的畫片", table, .49f, .36f, .30f);
                Arrive(InHouse, card.transform.position);
                Speak(() =>
                {
                    Goal("給早餐桌添一件餐具");
                    BrushAt(card, "畫出早餐用的餐具", option =>
                    {
                        card.SetActive(false);
                        Dish(table, .34f, -.05f, option);
                        Ask("梵谷", "今天好像不用趕著做什麼。", _ =>
                            Speak(() => { Save("walk-stops", 0); Next(); }, "梵谷|以前我總想抓住重要的日子。現在我想試試看，把普通的一天也過好。"),
                            "昨晚睡得如何？", "還記得昨晚觀眾問你的話嗎？");
                    }, false, new Ink("餐盤", Mask.Circle, Linen), new Ink("茶杯", Mask.Pot, Blue));
                }, "西奧|先吃飯，今天沒有畫展要趕。", "梵谷|聽起來竟然有點奢侈。");
            }, cut: "翌日早晨　・　黃色小屋");

            Scene("day-walk", DayWalk);

            Scene("day-lunch", () =>
            {
                Save("walk-stops", 0);
                var table = Table(House + Vector3.forward * .28f);
                Vincent(House + new Vector3(-.92f, 0, .62f));
                Theo(House + new Vector3(.88f, 0, .59f));
                Dish(table, 0, 0, 0);
                var seat = Seat(House + new Vector3(.55f, 0, -.48f));
                Arrive(InHouse, table.transform.position);
                Goal("回黃色小屋吃午餐");
                ObjectAction(seat, "坐下吃午餐", () =>
                {
                    ClearTargets();
                    Ask("梵谷", "今天每個地方都像有人替我保留了一小段人生。", _ =>
                        Speak(() => PauseTogether(3.5f, Next), "梵谷|是啊。我也記得我們一起走過的那一段。",
                            "午後的光落在院裡的杏花樹上。"),
                        "那些地方一直在，只是我們又回來看了。", "我們也一起走過了很多事。");
                });
            }, approach: (House + Vector3.back * .70f, "散步結束，回黃色小屋吃午餐"));

            Scene("gift-of-spring", PaintGift, cut: "午後　・　為院子裡的杏花備畫具");

            Scene("day-dinner", () =>
            {
                var table = Table(House + Vector3.forward * .29f);
                Vincent(House + new Vector3(-.91f, 0, .63f));
                Theo(House + new Vector3(.87f, 0, .59f));
                RestOnTable(Lamp(table.transform.position), table, .32f, .12f);
                Dish(table, -.24f, -.10f, 0);
                var seat = Seat(House + new Vector3(.56f, 0, -.44f));
                Arrive(InHouse, table.transform.position);
                Goal("一起吃晚餐");
                ObjectAction(seat, "坐到餐桌旁", () =>
                {
                    ClearTargets();
                    Speak(() => PauseTogether(4f, Next), "西奧|今天沒有訪客要你解釋畫。",
                        "梵谷|那我可以只說一句：湯很好喝。",
                        "晚飯後，梵谷按醫師建議休息。家人的支持和持續的照護，仍是明天的一部分。");
                });
            }, approach: (House + Vector3.back * .70f, "收起畫具，走回屋裡吃晚餐"));

            Scene("ending", () =>
            {
                var vincent = Vincent(House + new Vector3(.94f, 0, .47f));
                Theo(House + new Vector3(-.93f, 0, .50f));
                var gift = SpringArt(Recall(4, "spring-gift"));
                StudioWallPicture("昨晚的杏花畫作", .54f, gift);
                var empty = EaselPicture("清晨的空白畫布", House + new Vector3(.09f, 0, .43f));
                Arrive(InHouse, empty.transform.position);
                Answer(vincent, "到窗邊和梵谷道早安", () =>
                {
                    ClearTargets();
                    Speak(() => Layers(empty, "在畫布上留下三筆", _ =>
                    {
                        ColorProp(empty.transform.GetChild(0).gameObject, Color.white, canvas.LastImage);
                        Speak(Next, "真實歷史中，梵谷於 1890 年離世，年僅 37 歲。人們在他身後逐漸認識了他的藝術。",
                            "在這個想像的故事裡，他仍有明天，能繼續生活、接受支持，再畫一幅畫。");
                    }, FreeLayer()), "西奧|早安，文森特。今天想做什麼？", "梵谷|或許……再畫一幅畫吧。");
                });
            }, cut: "次日清晨　・　黃色小屋的畫室");
        }

        void StartInvites()
        {
            var spots = new[] { Church + new Vector3(.15f, 0, -.48f), Church + new Vector3(-5.1f, 0, -.58f),
                Church + new Vector3(2.85f, 0, .52f), Church + new Vector3(-1.93f, 0, .68f),
                terrain.Ground(Gallery + Vector3.back * 3.65f + Vector3.right * 1.08f) };
            var lines = new[] { "訪客|是畫那位農家的畫家嗎？我想看看。", "訪客|我聽過他的名字，但沒看過作品。",
                "訪客|有人會解釋畫的故事嗎？", "訪客|我看到告示了。我可以帶朋友一起來嗎？",
                "訪客|原來入口就在這條路上。" };
            var theo = Theo(terrain.Ground(Church + new Vector3(-.85f, 0, -1.15f)));
            var sign = PostedSign("空白展覽告示", terrain.Ground(spots[3] + new Vector3(.72f, 0, -.42f)) + Vector3.up * 1.18f);
            var arrow = PostedSign("空白的指路牌", terrain.Ground(spots[4] + new Vector3(-.81f, 0, -.32f)) + Vector3.up * 1.11f);
            var visitors = Array.ConvertAll(spots, _ => (GameObject)null);
            int invited = 0;
            Arrive(Church + Vector3.back * 1.75f, Church);
            Speak(() =>
            {
                TimeLimit(90, () => Retry("西奧", "這一輪還沒邀齊。我們回到廣場再試一次。"));
                Goal("邀請訪客來看畫  0/5");
                for (int i = 0; i < visitors.Length; i++)
                {
                    visitors[i] = Actor("街坊", fourth.VisitorSprite(i), terrain.Ground(spots[i]));
                    if (i < 3) Invite(i);
                }
                BrushAt(sign, "畫出展覽告示牌", _ =>
                {
                    var visual = MakeSketch("展覽", (x, y) =>
                        x > .25f && x < .75f && y > .23f && y < .80f
                            ? (y > .53f ? Gold : Blue) : Wood);
                    ColorProp(sign.transform.GetChild(0).gameObject, Color.white, visual);
                    visitors[3].transform.position += Vector3.right * .43f;
                    Invite(3);
                }, false, new Ink("展覽告示牌", Mask.Panel, Gold));
                BrushAt(arrow, "畫出通往畫廊的箭頭", _ =>
                {
                    TurnSign(arrow, Gold, "畫廊方向");
                    visitors[4].transform.position += Vector3.left * .36f;
                    Invite(4);
                }, false, new Ink("畫廊方向箭頭", Mask.Arrow, Gold));
                }, Recall(4, "gallery-order") == 0
                    ? "西奧|第一幅畫是農家，最後才到這片夜空。走完展牆，就像走過好幾年。"
                    : "西奧|從農家的暗處走到墓園的亮色，像眼睛慢慢適應了光。",
                    "西奧|附近有五個街坊。有人需要先看到告示，還有人找不到畫廊入口。");

            void Invite(int index)
            {
                var actor = visitors[index];
                Offer(actor.transform.position + Vector3.up * 1.1f, "邀請這位街坊來看畫展", () =>
                {
                    targets.RemoveAll(target => target.Object == actor.transform);
                    Goal($"邀請訪客來看畫  {++invited}/5");
                    if (invited == 5) StopTimer();
                    Speak(() =>
                    {
                        actor.SetActive(false);
                        if (invited == 5)
                            Answer(theo, "回教堂廣場告訴西奧五位訪客都會來", Next);
                    }, lines[index]);
                }, actor.transform);
            }
        }

        void StartQuestions()
        {
            ShowExhibition(true);
            Vincent(Gallery + new Vector3(-1.84f, 0, -.22f));
            Theo(Gallery + new Vector3(1.81f, 0, -.24f));
            var owner = Actor("畫廊主人", third.NeighborSprite, Gallery + new Vector3(.73f, 0, -1.04f));
            Arrive(Gallery + Vector3.back * .71f, Gallery + Vector3.forward);
            string[] questions = { "夜空真的會這樣旋轉嗎？", "這些農夫的手為什麼畫得那麼粗？", "墓園這條路，為什麼讓我想一路往裡看？" };
            string[][] options = {
                new[] { "他用旋轉的筆觸把觀察和想像放在一起。", "他在重現星星一整晚的移動位置。" },
                new[] { "他想用均勻的光凸顯餐桌的陳設。", "他讓我們看見這雙手怎麼換來一頓飯。" },
                new[] { "道路兩側往遠處收束，引導視線走進畫裡。", "兩側的樹幹把每個物件放在同樣的距離。" }
            };
            int answered = 0, score = 50;
            Goal("和梵谷一起迎接觀眾");
            Ask("梵谷", "我想聽他們說，又害怕聽見。", _ =>
            {
                TimeLimit(120, () => Retry("畫廊主人", "還有作品沒談到。回到開場，再把你看見的細節指給大家看。"));
                Goal("為觀眾解說三幅畫　聲望 50/80");
                for (int i = 0; i < Works.Length; i++)
                {
                    int index = i;
                    var art = exhibition.Exhibits[index];
                    Actor($"觀眾 {i + 1}", fourth.VisitorSprite(i), art.transform.position +
                        new Vector3(index % 2 == 0 ? -.62f : .6f, -1.6f, -.88f));
                    ObjectAction(art, $"和觀眾一起看{Works[i]}", () =>
                    {
                        targets.RemoveAll(target => target.Object == art.transform);
                        Ask("觀眾", questions[index], reply =>
                        {
                            if (reply != (index == 1 ? 1 : 0))
                            {
                                score -= 10;
                                Resolved();
                                return;
                            }
                            BrushAt(art, $"標出{Works[index]}的重點筆觸", __ =>
                            {
                                score += 15;
                                canvas.Review(Works[index], Work(index), new[] { "觀眾：原來筆觸自己也有方向。",
                                    "觀眾：我現在先看見了他們的手。", "觀眾：我的視線真的沿著路走進去了。" }[index], Resolved,
                                    new[] { new Rect(.34f, .48f, .62f, .48f), new Rect(.23f, .15f, .57f, .52f),
                                        new Rect(.22f, 0, .56f, .70f) }[index]);
                            }, false, new Ink(new[] { "旋轉的夜空", "桌邊的手", "往遠處收束的道路" }[index],
                                new[] { Mask.Spiral, Mask.Crop, Mask.Road }[index], Gold));
                        }, options[index]);
                    });
                }
            }, "我們可以先聽問題，再回答。", "不喜歡一幅畫，也不代表否定畫它的人。");

            void Resolved()
            {
                Goal($"為觀眾解說三幅畫　聲望 {score}/80");
                if (++answered < 3) return;
                StopTimer();
                if (score < 80)
                {
                    Retry("畫廊主人", "有人想再看看那些細節。再試一輪，從畫面找線索吧。");
                    return;
                }
                Answer(owner, "和畫廊主人聊聊展覽成果", () =>
                    Speak(Next, "畫廊主人|有收藏家願意洽談購畫，也有人希望之後繼續展出。",
                        "西奧|文森特，這是你多年創作累積的成果。",
                        "梵谷|我很高興。明天醒來，我也許還是會有些難熬。"));
            }
        }

        void DayWalk()
        {
            var theo = Theo(terrain.Ground(village.Door + new Vector3(.69f, 0, -.87f)));
            var clergyman = Actor("教堂的神職人員", third.NeighborSprite, Bench + Vector3.right * 1.04f);
            var peter = Peter(Farm + new Vector3(.93f, 0, .55f));
            var vincent = Vincent(terrain.Ground(Cemetery + new Vector3(.86f, 0, .19f)));
            int visited = Recall(4, "walk-stops");
            Arrive(terrain.Ground(village.Door + Vector3.back * 2.1f), theo.transform.position);
            RouteOptions();

            void RouteOptions()
            {
                ClearTargets();
                if (visited == 7)
                {
                    progress.AfterReveal(Next);
                    return;
                }
                Goal($"到教堂、農田和墓園重訪舊地  {((visited & 1) != 0 ? 1 : 0) + ((visited & 2) != 0 ? 1 : 0) + ((visited & 4) != 0 ? 1 : 0)}/3");
                if ((visited & 1) == 0) ActorAction(clergyman, "問問教堂那張長椅", VisitChurch);
                if ((visited & 2) == 0) ActorAction(peter, "和彼得談談那次農家的晚餐", VisitFarm);
                if ((visited & 4) == 0) ActorAction(vincent, "和梵谷聊聊墓園裡的畫", VisitCemetery);
                ActorAction(theo, "和西奧商量接下來去哪裡", () =>
                {
                    var routes = new System.Collections.Generic.List<(string name, GameObject actor, string goal)>();
                    if ((visited & 1) == 0)
                        routes.Add(("去教堂看看", clergyman, "走到教堂門旁的長椅，和神職人員交談"));
                    if ((visited & 2) == 0)
                        routes.Add(("去農田看看彼得", peter, "沿小路走到農田，和彼得談談"));
                    if ((visited & 4) == 0)
                        routes.Add(("去阿里斯康墓園", vincent, "走到阿里斯康墓園，和梵谷談談"));
                    var names = routes.ConvertAll(route => route.name);
                    names.Add("再走一會兒");
                    Ask("西奧", "你想沿哪條路走？", pick =>
                    {
                        if (pick == routes.Count) { RouteOptions(); return; }
                        preferredStop = routes[pick].actor.transform;
                        Goal(routes[pick].goal);
                    }, names.ToArray());
                });
            }

            void ReturnFrom(int bit)
            {
                vincent.transform.position = FindClearFooting(Cemetery + new Vector3(.86f, 0, .19f), .25f, 1.5f);
                visited |= bit;
                Save("walk-stops", visited);
                RouteOptions();
            }

            void VisitChurch()
            {
                ClearTargets();
                Seat(Bench, true);
                vincent.transform.position = FindClearFooting(Bench + Vector3.left * 1.03f, .25f, 1.5f);
                var sketch = BenchDrawing("當年的教堂速寫");
                ColorProp(sketch.transform.GetChild(0).gameObject, Color.white, ChurchArt(Recall(1, "church-sketch")));
                int original = Recall(1, "anna-object", -1);
                Speak(() =>
                {
                    Goal(original < 0 ? "重畫長椅旁的回憶"
                        : $"重畫長椅旁的回憶　你當時畫了{(original == 0 ? "椅子" : "燈")}");
                    var options = new[] { new Ink("畫一張椅子", Mask.Seat, Wood), new Ink("畫一盞燈", Mask.Lantern, Gold) };
                    BrushAt(sketch, "在教堂素描裡重畫記憶", selected =>
                    {
                        PutAnnaObject(selected);
                        vincent.transform.position += Vector3.back * .20f;
                        Speak(() => ReturnFrom(1),
                            selected == 0 ? "梵谷|我還記得她旁邊那張椅子。" : "梵谷|我還記得燈亮時，安娜才把信拿出來。",
                            "梵谷|原來我們從這裡開始。");
                    }, false, options);
                }, "神職人員|聽說你辦了畫展。還記得那張靠門的長椅嗎？");
            }

            void VisitFarm()
            {
                ClearTargets();
                vincent.transform.position = FindClearFooting(Farm + new Vector3(-1.25f, 0, .65f), .25f, 1.5f);
                Speak(() =>
                {
                    var card = PostedSign("收成籃的畫片", terrain.Ground(Farm + new Vector3(-1.08f, 0, -.22f)) + Vector3.up * .93f, .53f, .37f);
                    var basketFoot = Farm + new Vector3(-.64f, 0, -.54f);
                    int harvest = 0;
                    var plants = new GameObject[3];
                    Goal("幫彼得收三株馬鈴薯，或畫籃子給他");
                    for (int i = 0; i < plants.Length; i++)
                        plants[i] = Crop(terrain.Ground(Farm + new Vector3((i - 1) * .81f, 0, .62f)), true);
                    GameObject basket = null;
                    foreach (var plant in plants)
                        BrushAt(plant, "幫彼得採收這株馬鈴薯", _ =>
                        {
                            basket ??= Basket(basketFoot);
                            StoreCrop(plant, basket, harvest++);
                            Goal($"幫彼得採收  {harvest}/3");
                            if (harvest == plants.Length) FarmDone();
                        }, true, new Ink("成熟馬鈴薯", Mask.Crop, Green));
                    BrushAt(card, "畫一個收成籃交給彼得", _ =>
                    {
                        basket ??= Basket(basketFoot);
                        peter.transform.position = terrain.Ground(Farm + new Vector3(-.52f, 0, .21f));
                        FarmDone();
                    }, false, new Ink("收成籃", Mask.Pot, Wood));
                }, "彼得|我聽說有人買你的畫了。你現在也能被看見啦。",
                    "梵谷|我一直記得你說的：畫得走地上的煤，畫不走明天的工作。",
                    "彼得|我也記得你願意坐下來聽。");
                void FarmDone()
                {
                    ClearTargets();
                    Speak(() => ReturnFrom(2), "梵谷|我們一起坐在桌邊的那晚，我到現在還記得。");
                }
            }

            void VisitCemetery()
            {
                ClearTargets();
                var card = EaselPicture("墓園裡的畫布", Cemetery + new Vector3(-.36f, 0, .36f));
                Ask("梵谷", "我們曾在這裡畫過同一條路，卻總看見不一樣的東西。", reply =>
                    Speak(() => Layers(card, "替兩種畫法畫出各自的路", _ =>
                        {
                            for (int side = -1; side <= 1; side += 2)
                            for (int i = 0; i < 5; i++)
                            {
                                var point = terrain.Ground(Cemetery + new Vector3(side * (.15f + i * .20f), 0, -.31f + i * .29f));
                                Prop("投在地面的畫路", point + Vector3.up * .032f,
                                    new Vector3(.19f, .021f, .37f), side < 0 ? Gold : Blue)
                                    .transform.rotation = Quaternion.Euler(0, side * 28, 0);
                            }
                            Speak(() => ReturnFrom(4), "梵谷|我還是會珍惜曾經一起畫畫的日子。");
                        }, Layer("畫一條從眼前景色開始的路", Mask.Road, Gold),
                        Layer("畫一條朝記憶走去的路", Mask.Branch, Blue)),
                        reply == 0 ? "梵谷|謝謝你帶來。我想我可以讀了。" : "梵谷|可以。你幫我打開吧。",
                        "高更來信|我們對畫法有許多不同想法，但我仍記得一起工作的時光。願你繼續畫下去。"),
                    "把高更寄給他的信交給他", "先問他今天是否想讀高更的信");
            }
        }

        void PaintGift()
        {
            var vincent = Vincent(terrain.Ground(exhibition.Gift + new Vector3(-.61f, 0, .52f)));
            Arrive(InHouse, House + Vector3.forward);
            Visible(exhibition.Supplies, true);
            Visible(exhibition.Setup, false);
            string[] names = { "畫布", "畫筆", "顏料", "畫架" };
            string[] lines = { "梵谷|空白讓人害怕，也讓人有機會開始。", "梵谷|一支筆能留下人看見的東西。",
                "梵谷|我喜歡顏色彼此碰撞時的樣子。", "梵谷|讓它站穩，我們就可以慢慢畫。" };
            int count = 0;
            Goal("在黃色小屋找齊四件畫具  0/4");
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var supply = exhibition.Supplies[i];
                var point = supply.GetComponent<Renderer>() != null
                    ? supply.transform.position : exhibition.Tools[i] + Vector3.up * .67f;
                Offer(point, $"拿起{names[i]}", () =>
                {
                    targets.RemoveAll(target => target.Object == supply.transform);
                    supply.SetActive(false);
                    Goal($"在黃色小屋找齊四件畫具  {++count}/4");
                    Speak(() =>
                    {
                        if (count == names.Length)
                            Answer(vincent, "拿畫具去院子裡找梵谷", StartGift);
                    }, lines[index]);
                }, supply.transform);
            }

            void StartGift()
            {
                ClearTargets();
                Visible(exhibition.Setup, true);
                var stand = exhibition.Setup[1];
                Speak(() => Layers(stand, "和梵谷畫《春天的禮物》", result =>
                {
                    Save("spring-gift", result);
                    Texture2D finished = SpringArt(result);
                    stand.GetComponent<Renderer>().sharedMaterial.SetTexture("_BaseMap", finished);
                    Goal("收下梵谷送給你的畫");
                    ObjectAction(stand, "拿起梵谷送你的《春天的禮物》", () =>
                    {
                        stand.SetActive(false);
                        canvas.Review("《春天的禮物》", finished, "梵谷送給你的畫。這幅作品是為遊戲創作的。",
                            () => Ask("梵谷", "謝謝你陪我走過那些不知道答案的日子。", _ =>
                                Speak(Next, "梵谷|明天也許不容易，但我想再試一次。"),
                                "你還會繼續畫吧？", "我希望你也能為自己而畫。"));
                    });
                }, Layer("替天空塗上顏色", Mask.Sky, Blue), Layer("補上杏樹的枝條", Mask.Branch, Wood),
                    new[] { new Ink("畫上盛開的花朵", Mask.Blossom, Linen), new Ink("畫上剛冒出的新芽", Mask.Shoots, Green) },
                    FreeLayer()), "梵谷|我想畫一幅新畫。只是想把今天的光留住。");
            }
        }

        Texture2D SpringArt(int option)
        {
            if (option == 0) return exhibition.GiftPainting;
            return MakeSketch("《春天的禮物》・新芽", (x, y) =>
            {
                bool branch = Mathf.Abs(y - (.16f + x * .65f)) < .022f ||
                    (x > .38f && x < .77f && Mathf.Abs(y - (1.12f - x * .73f)) < .014f);
                if (branch) return Wood;
                for (int i = 0; i < 8; i++)
                {
                    float cx = .12f + i * .10f;
                    float cy = .16f + cx * .65f + (i % 2 == 0 ? .050f : -.044f);
                    if (Mathf.Pow((x - cx) / .031f, 2) + Mathf.Pow((y - cy - (x - cx) * .61f) / .025f, 2) < 1)
                        return i % 2 == 0 ? Green : new Color(.68f, .76f, .34f);
                }
                return Color.Lerp(new Color(.33f, .66f, .71f), new Color(.46f, .76f, .76f), y);
            });
        }

        void ShowExhibition(bool visible)
        {
            if (visible) ArrangeExhibits(Recall(4, "gallery-order"));
            Visible(exhibition.Frames, visible);
            Visible(exhibition.Backings, visible);
            Visible(exhibition.Exhibits, visible);
        }

        void ArrangeExhibits(int order)
        {
            int[] slots = order == 0 ? new[] { 2, 1, 0 } : new[] { 0, 1, 2 };
            for (int i = 0; i < slots.Length; i++)
            {
                var display = exhibition.Exhibits[i].transform;
                var position = display.position;
                position.x = exhibition.Display[slots[i]].x;
                display.position = position;
            }
        }

        static void Visible(GameObject[] objects, bool visible)
        {
            foreach (var item in objects) item.SetActive(visible);
        }
    }
}
