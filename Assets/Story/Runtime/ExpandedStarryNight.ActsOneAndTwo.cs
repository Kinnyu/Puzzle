using System;
using UnityEngine;
using Mask = Puzzle.Story.StoryBrush.Shape;
using Ink = Puzzle.Story.StoryBrush.Option;

namespace Puzzle.Story
{
    internal sealed partial class ExpandedStarryNight
    {
        GameObject Mary(Vector3 spot) => Actor("瑪莉", fourth.VisitorSprite(0), spot);
        GameObject Anna(Vector3 spot) => Actor("安娜", fourth.VisitorSprite(2), spot, Linen);
        GameObject Ruth(Vector3 spot) => Actor("露絲", fourth.VisitorSprite(1), spot);
        Vector3 Bench => Side + new Vector3(.42f, 0, 1.05f);
        Vector3 Family => terrain.WorkersHome;
        Vector3 Meal => terrain.Farmhouse;
        Vector3 MineExit => terrain.Ground(Mine + new Vector3(1.65f, 0, .70f));

        void ChurchStory()
        {
            Scene("church-meet", Location.Square, () =>
            {
                var spot = terrain.Ground(Church + Vector3.back * .62f);
                Vincent(spot + new Vector3(-1.2f, 0, .55f));
                Mary(spot + new Vector3(.95f, 0, .45f));
                var cart = Prop("卡住的手推車", spot + Vector3.up * .51f, new Vector3(1.1f, .36f, .74f), Wood);
                for (int side = -1; side <= 1; side += 2)
                {
                    var wheel = Period(terrain.Models.cartWheel, spot + new Vector3(side * .55f, .24f, 0), "車輪");
                    wheel.transform.SetParent(cart.transform, true);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Prop("車側木欄", spot + new Vector3(side * .51f, .77f, 0),
                        new Vector3(.07f, .16f, .78f), Wood).transform.SetParent(cart.transform, true);
                    Prop("推車握柄", spot + new Vector3(side * .41f, .58f, -.66f),
                        new Vector3(.065f, .085f, .66f), Wood).transform.SetParent(cart.transform, true);
                }
                Period(terrain.Models.loaf, spot + new Vector3(-.28f, .74f, -.18f)).transform.SetParent(cart.transform, true);
                Period(terrain.Models.book, spot + new Vector3(.28f, .71f, .14f)).transform.SetParent(cart.transform, true);
                Period(terrain.Models.cloth, spot + Vector3.up * .75f, "車上的布料")
                    .transform.SetParent(cart.transform, true);
                Arrive(spot + Vector3.back * 1.82f, spot);
                Goal("看看廣場上卡住的推車");
                Speak(() =>
                {
                    ObjectAction(cart, "試著推出卡住的手推車", () =>
                    {
                        ClearTargets();
                        Speak(() =>
                        {
                            Goal("和梵谷合力把推車推出石縫");
                            ObjectAction(cart, "扶穩車輪", () =>
                            {
                                ClearTargets();
                                Ask("瑪莉", "車輪動了，小心上面的布！", answer =>
                                {
                                    Save("cart-help", answer);
                                    cart.transform.position += Vector3.forward * .7f;
                                    Speak(() => Ask("梵谷", "我是文森特。你是剛來村裡的嗎？", meet =>
                                    {
                                        Save("meet-by-help", meet);
                                        Speak(Next, meet == 0 ? "梵谷|那我們算是一起迷路到同一件事裡了。"
                                                : "梵谷|這倒是很好的相遇方式。",
                                            "梵谷|如果不趕時間，願意陪我把東西送進教堂嗎？我一個人可能又會弄亂。");
                                    }, "我迷路了。", "我聽見有人需要幫忙。"),
                                        "瑪莉|多虧你們兩個！這些是給教堂的物資。文森特，你總是第一個跑來幫忙。",
                                        answer == 0 ? "梵谷|幸好你接住那塊布，我來扶穩車。" : "梵谷|車穩了，我來把布撿回去。");
                                }, "接住滑落的布料", "先扶穩車輪");
                            }, hold: .74f);
                        }, "梵谷|等一下，我來扶另一邊。你幫我看著輪子，好嗎？");
                    });
                }, "畫布上的顏料裂開。你落進村莊，遠處傳來鐘聲。", "瑪莉|糟了，今天偏偏又卡住！");
            });

            Scene("church-help", Location.Church, () =>
            {
                var table = Table(Side + new Vector3(-.75f, 0, .15f));
                Vincent(Side + new Vector3(.92f, 0, .72f));
                Mary(Side + new Vector3(-1.08f, 0, 1.04f));
                var dropped = Side + new Vector3(.30f, .14f, 1.63f);
                var pile = Prop("側廊散落木板", dropped, new Vector3(.92f, .10f, .26f), Wood);
                pile.transform.rotation = Quaternion.Euler(0, 28, 0);
                for (int i = 0; i < 2; i++)
                {
                    var board = Prop("散落木板", dropped + new Vector3(-.17f * i, .065f * (i + 1), .23f),
                        new Vector3(.92f, .085f, .22f), Wood);
                    board.transform.rotation = Quaternion.Euler(0, -19 + 42 * i, 0);
                    board.transform.SetParent(pile.transform, true);
                }
                Arrive(Side + Vector3.back * 1.02f, table.transform.position);
                string[] names = { "書本", "布料", "麵包" };
                string[] reactions = {
                    "梵谷|我在畫商那裡工作，常常見到很美的畫。但一本書被人翻得起毛邊，也有自己的故事。",
                    "梵谷|這塊布補過好幾次，還能繼續用。有人願意修，就能陪人久一點。",
                    "梵谷|我本來想先擺整齊，結果你提醒我：餓的人不會等我們擺得漂亮。"
                };
                int first = -1;
                var supplies = new (GameObject prop, string verb, Action found)[3];
                for (int i = 0; i < supplies.Length; i++)
                {
                    int index = i;
                    var parcel = Period(i == 0 ? terrain.Models.book : i == 1 ? terrain.Models.cloth : terrain.Models.loaf,
                        table.transform.position + new Vector3((i - 1) * .42f, .11f, 0), names[i]);
                    supplies[i] = (parcel, $"拿起{names[i]}並放到指定位置", () =>
                    {
                        if (first < 0) first = index;
                        parcel.transform.position += Vector3.forward * .21f;
                    });
                }
                Speak(() =>
                {
                    var coat = Prop("攤開的外套", table.transform.position + new Vector3(-.89f, .12f, .22f),
                        new Vector3(.52f, .09f, .28f), Blue);
                    Collect("分好教堂的物資", supplies, () =>
                    {
                        ClearTargets();
                        Speak(() =>
                        {
                            Goal("用畫筆整理側廊木板　〔B 畫筆〕");
                            BrushAt(pile, "擦除散落木板", _ =>
                            {
                                var stacked = dropped + new Vector3(-.76f, 0, .36f);
                                Pigment(pile.transform.position, stacked, Wood);
                                pile.transform.SetPositionAndRotation(stacked, Quaternion.identity);
                                for (int i = 0; i < pile.transform.childCount; i++)
                                {
                                    var board = pile.transform.GetChild(i);
                                    board.rotation = Quaternion.identity;
                                    board.position = stacked + Vector3.up * (.09f * (i + 1));
                                }
                                Ask("梵谷", "咦？剛才那裡還亂成一團……是我看錯了嗎？", _ =>
                                    Speak(Next, "梵谷|不管怎樣，路通了。謝謝你。"),
                                    "先把東西送進去吧。", "可能是我們一起整理好的。");
                            }, true, new Ink("散落木板", Mask.Board, Wood));
                        }, reactions[first], "梵谷|約瑟還沒回來。別硬搬，會傷到人。",
                            "那一小部分，像是還沒乾透的顏料。");
                    });
                    ObjectAction(coat, "摺好攤開的外套", () =>
                    {
                        targets.RemoveAll(target => target.Object == coat.transform);
                        coat.transform.localScale = new Vector3(.26f, .15f, .23f);
                        Save("folded-coat", 1);
                        Speak(null, "梵谷|你連袖子都收得很整齊。晚上來的人應該用得上。");
                    });
                }, "瑪莉|布料送到側廊，書放小桌，麵包留給晚間來訪的人。");
            });

            Scene("church-window", Location.Church, () =>
            {
                var window = ImageProp("漏風的窗", Side + new Vector3(-.72f, 1.32f, 1.35f), null, .83f, .97f);
                ColorProp(window.transform.GetChild(0).gameObject, Blue);
                var vincent = Vincent(Side + new Vector3(.72f, 0, 1.0f));
                Actor("約瑟", terrain.FarmerSprite, Side + new Vector3(-.99f, 0, .66f));
                Arrive(Side + Vector3.back * .75f, window.transform.position);
                Speak(() =>
                {
                    Goal("選一種方式擋住側廊的風　〔B 畫筆〕");
                    BrushAt(window, "修補漏風的窗", option =>
                    {
                        Save("church-window", option);
                        var center = window.transform.position + Vector3.back * .08f;
                        if (option == 0)
                        {
                            for (int i = -1; i <= 1; i++)
                                Prop("補好的窗板", center + Vector3.right * (i * .22f),
                                    new Vector3(.21f, .86f, .04f), Color.Lerp(Wood, Gold, .55f + i * .10f));
                        }
                        else
                        {
                            for (int i = 0; i < 8; i++)
                                Prop("厚窗簾的布褶", center + Vector3.right * ((i - 3.5f) * .10f),
                                    new Vector3(.105f, .99f, .065f + (i % 2) * .04f),
                                    Color.Lerp(Wood, Blue, .23f + (i % 2) * .27f));
                        }
                        var light = window.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.range = 4;
                        light.intensity = option == 0 ? 2.6f : .75f;
                        light.color = option == 0 ? new Color(.81f, .88f, 1) : new Color(1, .74f, .45f);
                        Speak(() => Ask("梵谷", "我以前以為，事情只有一個正確答案。", _ =>
                            Answer(vincent, "和梵谷去看看安娜", Next), "有時候先試試看。", "也可以先問需要的人。"),
                            option == 0 ? "約瑟|尺寸剛剛好，這下省事了。" : "約瑟|能先擋一晚，明天我再修窗。");
                    }, false, new Ink("補上窗板", Mask.Panel, Gold), new Ink("畫出厚窗簾", Mask.Cloth, Linen));
                }, "約瑟|今晚有人會在這裡休息，窗不能一直漏風。我去找合適的木框。", "梵谷|我可以幫忙，但我不知道怎麼修。");
            });

            Scene("church-anna", Location.Church, () =>
            {
                Seat(Bench, true);
                var anna = Anna(Bench + new Vector3(.51f, 0, .20f));
                Vincent(Bench + new Vector3(-1.05f, 0, -.16f));
                var drawing = ImageProp("長椅旁的教堂素描", Bench + new Vector3(-.43f, 1.25f, .68f), null);
                Arrive(Side + Vector3.back * 1.05f, Bench);
                Answer(anna, "問安娜在等誰", () =>
                {
                    Speak(() =>
                    {
                        Goal("在素描裡留下陪伴的方式　〔B 畫筆〕");
                        BrushAt(drawing, "在教堂素描上作畫", choice =>
                        {
                            Save("anna-object", choice);
                            PutAnnaObject(choice);
                            if (choice == 0)
                                Answer(anna, "和安娜一起坐下", RememberAnna);
                            else
                            {
                                var letter = Prop("安娜的信", anna.transform.position + new Vector3(-.31f, .83f, -.30f),
                                    new Vector3(.24f, .17f, .03f), Linen);
                                ObjectAction(letter, "聽安娜讀信", RememberAnna);
                                Goal("安娜拿出了信");
                            }
                        }, false, new Ink("畫一張椅子", Mask.Seat, Wood), new Ink("畫一盞燈", Mask.Lantern, Gold));
                    }, "梵谷|她常常坐在那裡。我不知道該怎麼開口。",
                        "安娜|我在等一位不會再來的老朋友。你們不用特地陪我，我只是想坐一會兒。");
                });

                void RememberAnna()
                {
                    ClearTargets();
                    Speak(() => Ask("安娜", "有人願意聽，我就不那麼孤單了。", choice =>
                    {
                        Save("anna-memory", choice);
                        if (choice == 0) PauseTogether(10f, Reflect);
                        else Speak(Reflect, "安娜|他以前總坐在窗邊。每次鐘響，他都會抬起頭，好像今天還有很長的時間。");
                    }, "安靜陪她坐一會兒", "問她想分享哪段回憶"),
                        "安娜|年輕的時候，總以為日子還很長。等到想說的話終於想好了，人卻不在了。",
                        "梵谷|我不知道怎麼回答您。");
                }

                void Reflect() => Speak(Next, "梵谷|我原本想說點漂亮的話，結果一句也說不出來。",
                    "梵谷|我總是太急著證明自己有用。");
            });

            Scene("church-sketch", Location.Church, () =>
            {
                Seat(Bench, true);
                PutAnnaObject(Recall(1, "anna-object"));
                Vincent(Bench + Vector3.right * 1.0f);
                var sketch = ImageProp("教堂速寫", Bench + new Vector3(-.28f, 1.02f, .14f), null);
                Arrive(Side + Vector3.back * .93f, sketch.transform.position);
                Speak(() => Layers(sketch, "和梵谷畫一張教堂速寫", choice =>
                {
                    Save("church-sketch", choice);
                    var art = ChurchArt(choice);
                    ColorProp(sketch.transform.GetChild(0).gameObject, Color.white, art);
                    canvas.Review("教堂速寫", art, "", () => Speak(Next,
                        "梵谷|同樣一個地方，你看見的卻不只是牆壁。",
                        "梵谷|今天我更想記住一個人被好好對待的樣子。"));
                }, Layer("塗出教堂的牆與窗", Mask.Panel, Linen),
                    new[] { new Ink("有人相伴的長椅", Mask.Seat, Wood), new Ink("光照進來的窗", Mask.Lantern, Gold) },
                    FreeLayer()),
                    "梵谷|如果要把今天留在紙上，你會畫什麼？是教堂本身，還是坐在裡面的人？");
            });

            Scene("church-value", Location.Church, () =>
            {
                var seat = Seat(Bench, true);
                Vincent(Bench + Vector3.right * .55f);
                PutAnnaObject(Recall(1, "anna-object"));
                Arrive(Side + Vector3.back * .55f, Bench);
                Goal("在長椅旁和梵谷聊聊今天");
                ObjectAction(seat, "坐下來和梵谷交談", () =>
                {
                    ClearTargets();
                    walk.SetSeated(true);
                    Speak(() => Ask("梵谷", "究竟要成為什麼樣的人，才算沒有白活？", meaning =>
                    {
                        Save("meaning", meaning);
                        Ask("梵谷", "那如果真的想幫助一個人，你覺得應該怎麼做？", help =>
                        {
                            Save("help", help);
                            Ask("梵谷", "如果要從今天開始，你最想記得什麼？", value =>
                            {
                                Save("value", value);
                                Speak(() => { walk.SetSeated(false); Next(); },
                                    value == 0 ? "梵谷|我也想找到值得投入一生的事。也許那條路還很長。"
                                        : "梵谷|我想記得今天這張長椅。很小的事，卻有重量。",
                                    "梵谷|今天有你在，我真的做了些事，也看見了一些人。",
                                    "梵谷|總有一天，我想用自己的方式幫助那些生活困苦的人。",
                                    "數年後，梵谷走進比利時的礦區。理想與現實的碰撞，才正要開始。");
                            }, "努力完成自己的夢想。", "讓別人感受到溫暖。");
                        }, "先照顧他眼前的需要。", "先聽懂他的處境。");
                    }, "找到喜歡的事，讓自己快樂。", "幫助別人，讓世界更好。"),
                        "梵谷|我在畫商那裡看見很多人追逐名聲，也有人一輩子只想做好手上的工作。",
                        "梵谷|今天幫忙推車時，我只想把車推出來。陪安娜坐著時，我卻開始想，她每天是不是都這樣一個人。");
                });
            });
        }

        void PutAnnaObject(int choice)
        {
            if (choice == 0) Seat(Bench + new Vector3(-.94f, 0, -.05f));
            else Lamp(Bench + new Vector3(-.92f, 1.15f, .35f));
        }

        void MineStory()
        {
            Scene("mine-reunion", Location.Mine, () =>
            {
                Vincent(Mine + Vector3.left * .93f);
                Peter(Mine + Vector3.right * .88f);
                var sack = Sack(Mine, "待搬的煤袋");
                Arrive(Mine + Vector3.back * 1.24f, Mine);
                Goal("過去幫兩人扶住煤袋");
                Speak(() => ObjectAction(sack, "扶住煤袋", () =>
                {
                    ClearTargets();
                    Ask("梵谷", "是你！我以為再也見不到你了。你還記得教堂裡那張長椅嗎？", reply =>
                        Speak(Next, "梵谷|這裡的人比我想的還辛苦，我也比自己想的更笨拙。",
                            "彼得|既然是朋友，就來幫忙吧。今天的活不會因為聊天變少。"),
                        "記得，也記得你說想理解別人的生活。", "你看起來累了很多。");
                }), "你從教堂的畫面走進礦區。煤灰落在梵谷的袖子上。",
                    "彼得|文森特，你不必跟著搬。你不是來講道的嗎？",
                    "梵谷|如果我只站在乾淨的地方說話，怎麼知道你們每天有多辛苦？");
            });

            Scene("mine-work", Location.Mine, () =>
            {
                Peter(Mine + new Vector3(1.15f, 0, .64f));
                Vincent(Mine + new Vector3(-1.15f, 0, .6f));
                var bag = Sack(Mine + Vector3.forward * 1.1f, "裝煤的布袋");
                var piles = new GameObject[3];
                for (int i = 0; i < piles.Length; i++)
                {
                    var point = terrain.Ground(Mine + new Vector3((i - 1) * .95f, 0, i == 1 ? .25f : -.34f)) +
                        Vector3.up * .15f;
                    piles[i] = Prop("散落煤塊", point, new Vector3(.55f, .19f, .39f),
                        new Color(.22f, .22f, .24f), PrimitiveType.Sphere);
                }
                Arrive(Mine + Vector3.back * 1.24f, Mine);
                int coal = 0;
                string[] accounts = { "彼得|天還沒亮，我們就來了。", "彼得|莉娜等我回家，也常等到睡著。",
                    "彼得|你們畫得走地上的煤，可畫不走明天的工作。" };
                Goal("把散落的煤收回袋中  0/3　〔B 畫筆〕");
                foreach (var pile in piles)
                    BrushAt(pile, "擦除散落的煤塊", _ =>
                    {
                        Pigment(pile.transform.position, bag.transform.position, new Color(.26f, .24f, .21f));
                        pile.SetActive(false);
                        string account = accounts[coal++];
                        Goal($"把散落的煤收回袋中  {coal}/3");
                        Speak(() => { if (coal == piles.Length) MarkExit(); }, account);
                    }, true, new Ink("煤塊", Mask.Crop, new Color(.26f, .24f, .21f)));

                void MarkExit()
                {
                    var sign = ImageProp("失去照明的出口", MineExit + Vector3.up * .89f, null, .62f, .44f);
                    Goal("給礦口標出可辨識的出口　〔B 畫筆〕");
                    BrushAt(sign, "畫出礦口照明或標記", option =>
                    {
                        Save("mine-light", option);
                        if (option == 0) Lamp(sign.transform.position + Vector3.up * .4f, terrain.Models.mineLamp);
                        else TurnSign(sign, Gold, "安全出口　→");
                        Speak(Next, "彼得|這樣好多了。進礦坑要聽熟手的，不要自己冒險。",
                            "梵谷|我原以為帶著熱情就夠了。原來還得學會聽。");
                    }, false, new Ink("提燈", Mask.Lantern, Gold), new Ink("反光標記", Mask.Arrow, Linen));
                }
            });

            Scene("mine-family", Location.WorkersHome, () =>
            {
                var table = Table(Family);
                var spot = table.transform.position + new Vector3(.05f, .37f, .07f);
                Ruth(Family + new Vector3(.91f, 0, .43f));
                Actor("莉娜", fourth.VisitorSprite(3), Family + new Vector3(-.74f, 0, .59f)).transform.localScale = Vector3.one * .68f;
                Vincent(Family + new Vector3(-1.22f, 0, -.03f));
                var card = ImageProp("工人住所的畫片", spot, null, .53f, .39f);
                Arrive(Family + Vector3.back * 1.56f, Family);
                Speak(() =>
                {
                    Goal("做一件他們現在用得上的東西　〔B 畫筆〕");
                    BrushAt(card, "畫一件支援工人家庭的物資", choice =>
                    {
                        Save("mine-supply", choice);
                        card.SetActive(false);
                        PlaceSupport(spot - Vector3.up * .18f, choice);
                        Speak(() => Listen(0, 0), "露絲|謝謝。不過明天我們還是要自己想辦法。你們願意聽聽我們怎麼過日子嗎？");
                    }, false, new Ink("麵包", Mask.Bread, Gold), new Ink("鍋子", Mask.Pot, Blue), new Ink("厚毯", Mask.Cloth, Linen));
                }, "露絲|今天能分的就這些。文森特，你不用為我們難過。", "梵谷|可我看見了，就沒辦法當作沒看見。");

                void Listen(int heard, int count)
                {
                    string[] titles = { "問問工作", "問問家裡的日子", "問問她希望改變什麼" };
                    string[] replies = { "露絲|煤袋再重，做完了也有回家的時候。難的是不知道下週有沒有工。",
                        "露絲|莉娜最近在學讀字，她想要自己看懂寄來的信。",
                        "露絲|我們需要的不只是被同情。也希望有人把我們當成能做決定的人。" };
                    var available = new System.Collections.Generic.List<int>();
                    var labels = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < titles.Length; i++)
                    {
                        if ((heard & (1 << i)) != 0) continue;
                        available.Add(i);
                        labels.Add(titles[i]);
                    }
                    if (count >= 2) labels.Add("謝謝她願意分享");
                    if (count == 3) { FinishListening(); return; }
                    Ask("露絲", "你想先聽哪一件事？", index =>
                    {
                        if (index == available.Count) { FinishListening(); return; }
                        int topic = available[index];
                        Speak(() => Listen(heard | (1 << topic), count + 1), replies[topic]);
                    }, labels.ToArray());
                }

                void FinishListening()
                {
                    Speak(() => Ask("露絲", "田裡的艾米爾差不多要收成了。你們接下來要去哪裡？", reply =>
                    {
                        if (reply == 0) { Next(); return; }
                        int first = Recall(2, "mine-supply");
                        var kinds = new[] { new Ink("麵包", Mask.Bread, Gold), new Ink("鍋子", Mask.Pot, Blue),
                            new Ink("厚毯", Mask.Cloth, Linen) };
                        var other = new[] { (first + 1) % kinds.Length, (first + 2) % kinds.Length };
                        card.SetActive(true);
                        var leave = Sack(Family + new Vector3(-1.15f, 0, -.70f), "前往田裡的物資袋");
                        Goal("可以再幫露絲準備一樣物資，也可以去農田");
                        ObjectAction(leave, "把物資帶去農田", Next);
                        BrushAt(card, "為工人家庭補畫一件物資", extra =>
                        {
                            card.SetActive(false);
                            PlaceSupport(spot + new Vector3(.39f, -.18f, .03f), other[extra]);
                            Save("mine-extra-supply", other[extra]);
                            Speak(Next, "露絲|謝謝，我會留著慢慢用。你們先去田裡吧。");
                        }, false, kinds[other[0]], kinds[other[1]]);
                    }, "現在去農田。", "留下來，再看看你們需要什麼。"),
                        "梵谷|我一直想幫忙，卻差點忘了問你們真正需要什麼。",
                        Recall(1, "help") == 1 ? "梵谷|你在教堂說過的話，我現在才慢慢懂。"
                            : "露絲|我們也希望有人把我們當成能做決定的人。");
                }
            });

            Scene("farm-harvest", Location.Farm, () =>
            {
                Actor("艾米爾", terrain.FarmerSprite, Farm + new Vector3(-1.22f, 0, .64f));
                Vincent(Farm + new Vector3(1.48f, 0, .15f));
                var basket = Basket(Farm + new Vector3(-.91f, 0, -.26f));
                var plants = new GameObject[6];
                for (int i = 0; i < plants.Length; i++)
                {
                    var foot = terrain.Ground(Farm + new Vector3((i % 3 - 1) * .86f, 0, (i / 3) * 1.15f));
                    plants[i] = Crop(foot, true);
                }
                for (int i = 0; i < 3; i++)
                    Crop(terrain.Ground(Farm + new Vector3((i - 1) * .84f, 0, 2.15f)), false);
                Arrive(terrain.Ground(Farm + Vector3.back * 2.1f), Farm);
                int harvest = 0;
                Speak(SupplyTargets, "艾米爾|只收有標記的，別踩進還沒成熟的那塊田！");
                void SupplyTargets()
                {
                    ClearTargets();
                    Goal($"採收有標記的成熟馬鈴薯  {harvest}/6　〔B 畫筆〕");
                    foreach (var plant in plants)
                    {
                        if (!plant.activeSelf) continue;
                        BrushAt(plant, "擦除成熟植株並收進籃中", _ =>
                        {
                            StoreCrop(plant, basket, harvest++);
                            if (harvest == 3) FixRoad();
                            else if (harvest == 6) MutualAid();
                            else Goal($"採收有標記的成熟馬鈴薯  {harvest}/6");
                        }, true, new Ink("成熟馬鈴薯", Mask.Crop, Green));
                    }
                }
                void FixRoad()
                {
                    ClearTargets();
                    Vector3 mud = terrain.Ground(Farm + new Vector3(-.68f, 0, -1.25f)) + Vector3.up * .025f;
                    var road = Prop("泥濘田埂", mud, new Vector3(1.01f, .04f, 1.4f), new Color(.27f, .18f, .13f));
                    Speak(() =>
                    {
                        Goal("鋪一條能搬收成的木板路　〔B 畫筆〕");
                        BrushAt(road, "修補泥濘的田埂", _ =>
                        {
                            for (int i = 0; i < 5; i++)
                                Prop("田埂木板", mud + new Vector3(0, .055f, (i - 2) * .26f), new Vector3(1f, .08f, .24f), Wood);
                            Speak(SupplyTargets, "艾米爾|這下能趕在雨大之前搬回去。");
                        }, false, new Ink("鋪上木板", Mask.Road, Wood));
                    }, "雨落下來。田埂一下子變得泥濘。");
                }
                void MutualAid()
                {
                    int supply = Recall(2, "mine-supply");
                    Vector3 visitor = Farm + new Vector3(1.38f, 0, -.52f);
                    if (supply == 0) Peter(visitor);
                    else if (supply == 1) Ruth(visitor);
                    else Actor("莉娜", fourth.VisitorSprite(3), visitor).transform.localScale = Vector3.one * .68f;
                    PlaceSupport(visitor + new Vector3(-.22f, .52f, -.13f), supply);
                    Speak(Next, new[] { "彼得|麵包還有一些。艾米爾，來分一塊吧。",
                        "露絲|鍋裡還有熱湯。我帶了一些來田裡。",
                        "莉娜|昨晚暖和多了。今天換我替你們送水。" }[supply],
                        "梵谷|先問哪一株可以收，比急著動手重要。");
                }
            });

            Scene("farm-meal", Location.Farmhouse, () =>
            {
                var table = Table(Meal);
                var vincent = Vincent(Meal + new Vector3(.88f, 0, .42f));
                FarmLamp(table.transform.position);
                Seat(Meal + new Vector3(-.92f, 0, .04f));
                Arrive(Meal + Vector3.back * 1.44f, mealCenter());
                Answer(vincent, "請梵谷停下來休息", () =>
                    Ask("梵谷", "可我總覺得一停下來，就像放棄了他們。", _ =>
                    {
                        var card = ImageProp("餐桌上的畫片", mealCenter(), null, .52f, .35f);
                        ClearTargets();
                        BrushAt(card, "畫一件餐具給梵谷", option =>
                        {
                            card.SetActive(false);
                            Dish(table.transform.position + new Vector3(.24f, .16f, -.05f), option);
                            PauseTogether(4f, () => Speak(Next, "梵谷|謝謝你提醒我。我希望明天還能有力氣站在這裡。"));
                        }, false, new Ink("餐盤", Mask.Circle, Linen), new Ink("水杯", Mask.Pot, Blue));
                    }, "先坐下吃一點。", "你不能一個人扛下所有人的生活。"));
                Vector3 mealCenter() => table.transform.position + Vector3.up * .32f;
            });

            Scene("potato-painting", Location.Farmhouse, () =>
            {
                var table = Table(Meal);
                Vincent(Meal + new Vector3(1.06f, 0, .54f));
                Ruth(Meal + new Vector3(-.82f, 0, .49f));
                Actor("艾米爾", terrain.FarmerSprite, Meal + new Vector3(-1.07f, 0, -.06f));
                FarmLamp(table.transform.position);
                Period(terrain.Models.coffeePot, table.transform.position + new Vector3(.41f, .184f, .12f));
                Dish(table.transform.position + new Vector3(-.20f, .09f, -.16f), 0);
                var easel = EaselPicture("農家晚餐畫布", Meal + new Vector3(.65f, 1.13f, .32f));
                Arrive(Meal + Vector3.back * 1.44f, easel.transform.position);
                Speak(() => Layers(easel, "和梵谷描畫農家的晚餐", choice =>
                {
                    Save("potato-focus", choice);
                    canvas.Review("《吃馬鈴薯的人》", fourth.PotatoArt, "1885 年。遊戲將不同時期的經歷合併呈現。",
                        () => Speak(Next, choice == 0 ? "梵谷|我想留住他們用雙手換來的這一餐。"
                            : "梵谷|我想留住他們在燈下看向彼此的樣子。"));
                }, Layer("畫出昏暗的背景", Mask.Panel, new Color(.28f, .24f, .18f)),
                    new[] { new Ink("著重桌上的馬鈴薯與手", Mask.Crop, Gold), new Ink("著重燈下交會的目光", Mask.Star, Linen) },
                    FreeLayer()),
                    "梵谷|我想記下這一幕。每個人都用自己的雙手換來這頓飯。",
                    "露絲|你要畫得像我們。別把我們畫成只會低頭的人。");
            });

            Scene("miner-helped", Location.Mine, () =>
            {
                var vincent = Vincent(Mine + new Vector3(.57f, 0, .19f));
                var card = ImageProp("礦區的記憶畫片", Mine + new Vector3(-.42f, 1.11f, .11f), null);
                Arrive(terrain.Ground(Mine + Vector3.back * 1.8f), Mine);
                Answer(vincent, "和梵谷讀那封信", () =>
                {
                    ClearTargets();
                    Speak(() => Ask("梵谷", "我以為只要靠近他們，就能證明自己走對了路。可是大家並不都認同。", _ =>
                    {
                        Goal("留一張畫片給即將離開的梵谷");
                        BrushAt(card, "畫一張礦區的回憶", option =>
                        {
                            Save("mine-memory", option);
                            Speak(Next, "梵谷|也許我可以畫下那些沒人願意仔細看的生活。",
                                "梵谷|你一直在旁邊，讓我看清自己真正放不下什麼。",
                                "礦區的傳教理想遭遇挫折後，梵谷逐漸把畫筆當成理解世界的另一條路。");
                        }, false, new Ink("礦工手套", Mask.Cloth, Wood),
                            new Ink("農家餐桌", Mask.Panel, Linen), new Ink("礦區燈火", Mask.Lantern, Gold));
                    }, "別人的否定，不會抹掉你做過的事。", "也許你可以換一種方式留下他們的故事。"),
                        "一封關於傳教工作的信到了礦區。梵谷讀完，久久沒有說話。");
                });
            });
        }

        GameObject Sack(Vector3 spot, string name)
        {
            var bag = Prop(name, spot + Vector3.up * .31f,
                new Vector3(.72f, .62f, .53f), new Color(.59f, .48f, .34f), PrimitiveType.Sphere);
            Prop("束起的袋口", spot + Vector3.up * .61f, new Vector3(.20f, .13f, .17f), Linen, PrimitiveType.Sphere)
                .transform.SetParent(bag.transform, true);
            Prop("麻繩", spot + Vector3.up * .57f, new Vector3(.18f, .017f, .16f), Wood, PrimitiveType.Cylinder)
                .transform.SetParent(bag.transform, true);
            return bag;
        }

        GameObject Basket(Vector3 spot)
        {
            return Period(terrain.Models.basket, spot + Vector3.up * .14f, "收成籃");
        }

        GameObject Crop(Vector3 spot, bool ripe)
        {
            var plant = Period(terrain.Models.crop, spot, ripe ? "成熟馬鈴薯" : "未成熟馬鈴薯");
            if (!ripe) plant.transform.localScale = Vector3.one * .73f;
            if (ripe)
                Prop("成熟標記", spot + new Vector3(.12f, .15f, -.23f), new Vector3(.08f, .28f, .045f), Linen)
                    .transform.SetParent(plant.transform, true);
            return plant;
        }

        void StoreCrop(GameObject plant, GameObject basket, int count)
        {
            Pigment(plant.transform.position, basket.transform.position + Vector3.up * .22f, Gold);
            plant.SetActive(false);
            float angle = count * 2.4f;
            Prop("收進籃子的馬鈴薯", basket.transform.position + new Vector3(Mathf.Cos(angle) * .14f, .07f + .014f * count,
                Mathf.Sin(angle) * .15f), new Vector3(.17f, .12f, .14f), Gold, PrimitiveType.Sphere);
        }

        void FarmLamp(Vector3 table)
        {
            var point = table + new Vector3(.03f, 1.08f, .04f);
            Prop("懸燈的屋樑", point + Vector3.up * .74f, new Vector3(5.43f, .15f, .16f), Wood);
            Lamp(point, terrain.Models.pendant);
        }

        void PlaceSupport(Vector3 point, int choice) =>
            Period(choice == 0 ? terrain.Models.loaf : choice == 1 ? terrain.Models.stewPot : terrain.Models.cloth, point);

        void Dish(Vector3 point, int choice) => Period(choice == 0 ? terrain.Models.platter : terrain.Models.coffeeCup, point);

        Texture2D ChurchArt(int choice) => MakeSketch("教堂速寫", (x, y) =>
        {
            bool floor = y < .29f + .045f * x;
            if (choice == 0 && y > .18f && y < .32f && x > .19f && x < .74f) return Wood;
            if (x > .32f && x < .65f && y > .38f && y < .82f)
                return Mathf.Abs(x - .482f) < .018f || Mathf.Abs(y - .61f) < .021f ? Wood : choice == 0 ? Blue : Gold;
            if (choice == 1 && y < .38f && Mathf.Abs(x - .49f - (.37f - y) * .8f) < .08f) return Gold;
            return floor ? new Color(.62f, .46f, .34f) : Linen;
        });
    }
}
