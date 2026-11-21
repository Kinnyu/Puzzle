using UnityEngine;
using Mask = Puzzle.Story.StoryBrush.Shape;
using Ink = Puzzle.Story.StoryBrush.Option;

namespace Puzzle.Story
{
    internal sealed partial class ExpandedStarryNight
    {
        GameObject Gauguin(Vector3 position) => Actor("高更", third.GauguinSprite, position);
        Vector3 InHouse => House + Vector3.back * 1.23f;

        void YellowHouseStory()
        {
            Scene("yellow-reunion", Location.Studio, () =>
            {
                Vincent(House + new Vector3(.95f, 0, -.05f));
                var postman = Actor("郵差", third.NeighborSprite,
                    terrain.Ground(village.Door + new Vector3(-.76f, 0, -.58f)));
                var frame = ImageProp("待搬的畫框", House + new Vector3(-.99f, 0, -1.775f), null);
                frame.transform.rotation = Quaternion.Euler(-9, 0, 0);
                PeriodModels.RestOnSurface(frame, FloorAt(frame.transform.position).y);
                var wall = StudioWallPicture("待補色的牆面", .44f, null, 1.08f, .86f);
                var tins = Prop("通道裡的顏料桶", House + new Vector3(.51f, .16f, -.37f),
                    new Vector3(.32f, .17f, .33f), Blue, PrimitiveType.Cylinder);
                Arrive(terrain.Ground(village.Door + Vector3.back * 1.75f), House);
                Answer(postman, "接過西奧寄來的信", () =>
                {
                    ClearTargets();
                    Speak(() => Ask("梵谷", "高更就快到了。我想讓這裡成為畫家們一起工作的地方。", _ =>
                    {
                        Goal("幫梵谷布置黃色小屋的畫室");
                        ObjectAction(frame, "搬開畫室入口的畫框", () =>
                        {
                            ClearTargets();
                            frame.transform.position = House + new Vector3(1.60f, 0, 1.775f);
                            frame.transform.rotation = Quaternion.Euler(9, 0, 0);
                            PeriodModels.RestOnSurface(frame, FloorAt(frame.transform.position).y);
                            Goal("補色並清出畫室通道　〔B 畫筆〕");
                            int finished = 0;
                            BrushAt(wall, "替黃色牆面補色", __ =>
                            {
                                ColorProp(wall.transform.GetChild(0).gameObject, Gold);
                                Done();
                            }, false, new Ink("黃色牆面", Mask.Panel, Gold));
                            BrushAt(tins, "擦除通道上的顏料桶", __ =>
                            {
                                Vector3 corner = House + new Vector3(-1.82f, .16f, .87f);
                                Pigment(tins.transform.position, corner, Blue);
                                tins.transform.position = corner;
                                Done();
                            }, true, new Ink("畫室的顏料桶", Mask.Pot, Blue));
                            void Done()
                            {
                                if (++finished < 2) return;
                                Speak(Next, "梵谷|你還記得教堂那扇窗嗎？那時我只想把一件小事做好。現在，我想把一個夢想做好。");
                            }
                        });
                    }, "你看起來很興奮。", "你也要記得休息。"),
                        "郵差|文森特，有西奧的信。",
                        Recall(2, "mine-memory") switch
                        {
                            1 => "梵谷|看到那張農家餐桌的畫片，我還會想起露絲說的話。",
                            2 => "梵谷|我把那張礦區燈火的畫片帶來了。有些事值得記住。",
                            _ => "梵谷|那張礦工手套的畫片，我一直留在行李裡。"
                        });
                });
            });

            Scene("yellow-arrange", Location.Studio, () =>
            {
                var guest = Gauguin(House + new Vector3(-.78f, 0, .6f));
                Vincent(House + new Vector3(.94f, 0, .66f));
                var card = EaselPicture("畫室的畫材草圖", House + new Vector3(.02f, 0, .41f));
                Arrive(InHouse, card.transform.position);
                Answer(guest, "和高更打招呼", () =>
                {
                    ClearTargets();
                    Speak(() =>
                    {
                        Goal("和兩位畫家準備畫具");
                        BrushAt(card, "畫出一件畫具", option =>
                        {
                            Save("yellow-tool", option);
                            card.SetActive(false);
                            DrawTool(option, House + new Vector3(.19f, 0, .72f));
                            DrawTool((option + 1) % 3, House + new Vector3(-1.48f, 0, .83f));
                            DrawTool((option + 2) % 3, House + new Vector3(1.58f, 0, .74f));
                            Ask("高更", "你看畫時，會從哪裡開始？", view =>
                            {
                                Save("artist-view", view);
                                Speak(Next, "高更|畫法不同，也可以對話。", "梵谷|我希望你這句話是真的。");
                            }, "從眼前的景物開始。", "把想像放進畫裡。");
                        }, false, new Ink("畫架", Mask.Frame, Wood), new Ink("調色盤", Mask.Circle, Gold),
                            new Ink("畫布", Mask.Panel, Linen));
                    }, "高更|這裡很有你的個性。顏色幾乎要從牆上跳出來。",
                        "梵谷|我希望我們能一起畫出新的東西。",
                        "高更|一起工作可以，但不代表我們要用同一種方法。");
                });
            }, "稍後　・　高更抵達黃色小屋");

            Scene("alyscamps", Location.Cemetery, () =>
            {
                Vincent(terrain.Ground(Cemetery + new Vector3(-.91f, 0, .27f)));
                Gauguin(terrain.Ground(Cemetery + new Vector3(1.01f, 0, .26f)));
                var art = EaselPicture("墓園的雙視角畫布", Cemetery + new Vector3(0, 1.20f, .18f));
                art.transform.rotation = Quaternion.Euler(0, 180, 0);
                Arrive(terrain.Ground(Cemetery + Vector3.forward * 2.06f), art.transform.position);
                Speak(() => Layers(art, "在同一條路上試兩種畫法", perspective =>
                {
                    Save("cemetery-view", perspective);
                    var study = village.Study(perspective == 0
                        ? PaintingStudy.Perspective | PaintingStudy.Leaves : PaintingStudy.Perspective | PaintingStudy.Stone);
                    canvas.Review("現場的光影", study, "梵谷從眼前的光和樹開始。", () =>
                        canvas.Review("想像中的色塊", third.GauguinArt, "高更把眼前的路畫成記憶裡的樣子。",
                            () => Speak(() => Layers(art, "協助完成《阿里斯康墓園》", _ =>
                            {
                                progress.SaveCemeteryStudy(PaintingStudy.All);
                                village.HangStudy(PaintingStudy.All);
                                canvas.Review("《阿里斯康墓園》", village.Study(PaintingStudy.All), "梵谷的作品", () =>
                                    Speak(Next, "梵谷|同一條路，兩個人走進去，帶回來的畫卻不一樣。"));
                            }, Layer("畫出秋色", Mask.Crop, Gold), Layer("補上石牆的陰影", Mask.Board, Blue)),
                                "梵谷|你畫得和我想的不一樣。", "高更|這正是有意思的地方。")));
                }, Layer("畫出道路的遠近", Mask.Road, Linen),
                    new[] { new Ink("強化現場的光影", Mask.Star, Gold), new Ink("改變象徵性的色塊", Mask.Cloth, Blue) },
                    FreeLayer()),
                    "梵谷|我想畫出這條路上的光。你看樹幹旁那一片顏色。",
                    "高更|我想讓畫面更像記憶，而不是眼睛的紀錄。");
            }, "外出寫生　・　阿里斯康墓園");

            Scene("yellow-house-debate", Location.Studio, () =>
            {
                var vincent = Vincent(House + new Vector3(-.88f, 0, .55f));
                var guest = Gauguin(House + new Vector3(.91f, 0, .55f));
                var plan = EaselPicture("畫室配置草圖", House + new Vector3(-.04f, 0, .58f));
                Arrive(InHouse, plan.transform.position);
                Speak(() =>
                {
                    Goal("給兩人留一點空間　〔B 畫筆〕");
                    BrushAt(plan, "調整畫室配置", choice =>
                    {
                        Save("yellow-space", choice);
                        if (choice == 0)
                        {
                            DrawTool(0, House + new Vector3(1.57f, 0, .34f));
                            guest.transform.position += Vector3.right * .45f;
                        }
                        else
                        {
                            Seat(House + new Vector3(-1.48f, 0, .78f));
                            vincent.transform.position += Vector3.left * .39f;
                        }
                        Speak(() => Ask("梵谷", "我明明很期待有人一起畫，卻又害怕他不認同我。", _ =>
                                Speak(Next, "梵谷|我知道。但知道和做得到，差得很遠。"),
                            "朋友可以有不同想法。", "你可以告訴他你需要什麼。"),
                            choice == 0 ? "高更|各畫各的，也許比較能看清彼此的作品。"
                                : "梵谷|先停一下吧。我剛剛說話太急了。");
                    }, false, new Ink("第二張畫架", Mask.Frame, Wood), new Ink("窗邊休息椅", Mask.Seat, Linen));
                }, "高更|你太依賴眼前看見的東西。", "梵谷|而你總想把眼前的東西改成自己的想像！");
            }, "寫生回來　・　黃色小屋的畫室");

            Scene("yellow-understanding", Location.Studio, () =>
            {
                Vincent(House + new Vector3(-1.06f, 0, .74f));
                Gauguin(House + new Vector3(1.03f, 0, .74f));
                if (Recall(3, "yellow-space") == 0) DrawTool(0, House + new Vector3(1.57f, 0, .34f));
                else Seat(House + new Vector3(-1.48f, 0, .78f));
                var board = StudioWallPicture("兩人視角對照板", .37f, null, 1.15f, .67f);
                Arrive(House + new Vector3(.47f, 0, -.49f), board.transform.position);
                Speak(() => Layers(board, "整理兩位畫家的創作想法", _ =>
                {
                    var contrast = MakeSketch("兩種畫法", (x, y) => x < .5f
                        ? (y < .26f + .45f * x ? Wood : Green)
                        : (y > .79f - x * .34f ? Blue : Gold));
                    ColorProp(board.transform.GetChild(0).gameObject, Color.white, contrast);
                    Ask("梵谷", "可我聽見的卻像是另一回事。", answer =>
                    {
                        Save("yellow-understanding", answer);
                        if (answer == 0)
                            Speak(Next, "高更|我想畫的是一段回憶。", "梵谷|我想抓住的是我們站在樹下的那一刻。");
                        else PauseTogether(3f, Next);
                    }, "先說說各自想表達什麼。", "今天先休息吧。");
                }, Layer("塗出對照板左側的現場景物", Mask.Road, Green),
                    Layer("塗出對照板右側的想像色塊", Mask.Cloth, Gold)),
                    "高更|我批評的是方法，不是說你沒有價值。");
            }, "稍後　・　轉到側牆前並看兩種畫法");

            Scene("yellow-house-retry", Location.Studio, () =>
            {
                Vincent(House + new Vector3(.85f, 0, .36f));
                var neighbor = Actor("鄰居", third.NeighborSprite,
                    terrain.Ground(village.Door + new Vector3(-.20f, 0, -.88f)));
                var table = Table(House + new Vector3(-.75f, 0, .65f));
                var utensil = Prop("畫室工具", table.transform.position + new Vector3(.20f, .10f, -.18f),
                    new Vector3(.33f, .041f, .071f), new Color(.57f, .58f, .60f));
                RestOnTable(utensil, table, .31f, -.15f);
                var paper = TableSketch("空白的求助畫片", table, .57f, .44f, -.26f);
                Arrive(InHouse, table.transform.position);
                Speak(() =>
                {
                    TimeLimit(90, () => Retry("旁白", "夜色越來越深。回到剛才，找人一起幫忙。"));
                    Goal("收好畫室工具，再找人幫忙　〔B 畫筆〕");
                    BrushAt(utensil, "擦除桌上的危險工具", _ =>
                    {
                        Vector3 safe = table.transform.position + Vector3.up * .12f + Vector3.forward * .31f;
                        Pigment(utensil.transform.position, safe, Linen);
                        utensil.SetActive(false);
                        BrushAt(paper, "畫出一種求助方式", choice =>
                        {
                            Save("yellow-help", choice);
                            if (choice == 0)
                                ColorProp(paper.transform.GetChild(0).gameObject, Linen);
                            else TurnSign(paper, Gold, "通往醫師處");
                            Answer(neighbor, choice == 0 ? "請鄰居把信送給西奧" : "請鄰居去找醫師", () =>
                            {
                                StopTimer();
                                ClearTargets();
                                Speak(() => PauseTogether(3, Next),
                                    "梵谷|我現在很難受。謝謝你沒有叫我立刻振作。",
                                    "你和他坐了一會兒。醫療人員來到黃色小屋，協助梵谷休養。");
                            });
                        }, false, new Ink("寫給西奧的信紙", Mask.Paper, Linen), new Ink("通往醫師處的路標", Mask.Arrow, Gold));
                    }, true, new Ink("畫室工具", Mask.Board, Linen));
                }, "夜深了。高更決定先離開黃色小屋。",
                    "梵谷|我以為這個家會讓我們都留下來。是不是我又把一切搞砸了？");
            }, "深夜　・　高更離開後的黃色小屋");

            Scene("yellow-goodbye", Location.Studio, () =>
            {
                var doorstep = terrain.Ground(village.Door + new Vector3(1.32f, 0, -.85f));
                var vincent = Vincent(doorstep);
                var art = StudioWallPicture("留在畫室裡的畫布", .48f, null);
                Arrive(terrain.Ground(village.Door + Vector3.back * 2.0f), vincent.transform.position);
                Speak(() =>
                {
                    Goal("回畫室，替一起畫畫的日子留個位置");
                    BrushAt(art, "畫出要留在牆上的東西", choice =>
                    {
                        Save("yellow-goodbye", choice);
                        art.SetActive(false);
                        if (choice == 0)
                        {
                            StudioWallPicture("梵谷的速寫", -.11f, village.Study(PaintingStudy.All));
                            StudioWallPicture("高更的速寫", 1.02f, third.GauguinArt);
                        }
                        else StudioWallPicture("一張新的空白畫布", .48f, null);
                        Speak(Next, choice == 0 ? "梵谷|我希望看著這些畫時，想起的不只有爭吵。"
                                : "梵谷|我想留一點空間給接下來的日子。",
                            "梵谷|以前我想把每個人都留在自己的理想裡。現在我開始懂，有些人只能陪你走一段路。",
                            "1888 年，梵谷與高更在亞爾共同生活與創作。梵谷經歷精神危機後接受照護，此後仍持續作畫。");
                    }, false, new Ink("並排的兩幅畫框", Mask.Frame, Wood), new Ink("一張空白畫布", Mask.Panel, Linen));
                }, "梵谷|我想念一起畫畫的日子，也知道我們可能不適合住在一起。");
            }, "次日上午　・　高更離去後，小屋門前");
        }

        GameObject StudioWallPicture(string name, float z, Texture2D art, float width = .80f, float height = .62f)
        {
            var hit = third.StudioWall(House + new Vector3(0, 1.44f, z));
            var picture = ImageProp(name, hit.point + hit.normal * .035f, art, width, height);
            picture.transform.rotation = Quaternion.LookRotation(-hit.normal);
            return picture;
        }

        void DrawTool(int tool, Vector3 point)
        {
            if (tool == 0) Period(terrain.Models.easel, point);
            else if (tool == 1)
            {
                RestOnTable(Period(terrain.Models.palette, point), village.DeskTop, .30f, .12f);
            }
            else EaselPicture("乾淨的畫布", point, .51f, .47f);
        }
    }
}
