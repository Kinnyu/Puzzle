using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    internal sealed class StoryBrush : IDisposable
    {
        internal enum Shape
        {
            Panel, Cloth, Board, Seat, Lantern, Bread, Pot, Crop, Road, Sky, Star, Spiral,
            Branch, Blossom, Shoots, Circle, Frame, Arrow, Paper, Free
        }

        internal readonly struct Option
        {
            internal readonly string Name;
            internal readonly Shape Mask;
            internal readonly Color Color;

            internal Option(string name, Shape mask, Color color)
            {
                Name = name;
                Mask = mask;
                Color = color;
            }
        }

        readonly StorySceneProgress progress;
        readonly StarryNight.StarryNightWalk walk;
        readonly Camera eyes;
        readonly VisualElement overlay, card, tools, palette;
        readonly Label hint, status, clock, title, caption;
        readonly Button brushButton, eraserButton, assist;
        readonly Image surface;
        readonly float[] grain = new float[WorldBrushTarget.Width * WorldBrushTarget.Height];
        readonly List<WorldBrushTarget> targets = new();
        readonly Dictionary<WorldBrushTarget, Label> labels = new();
        readonly List<WorldBrushTarget> visible = new();
        readonly Material guide;
        readonly Func<Vector3, Transform, bool> obstructed;
        readonly Texture2D lastImage;
        Color32[] lastLayers;
        WorldBrushTarget active, stroke;
        bool eraser, review, suppressed, captured, rightDown, looking;
        Vector2 dragDelta, lastScreen, lastPixel;
        float strokeLength;
        int openedFrame;
        Action closeReview;
        IVisualElementScheduledItem zoom;
        internal bool IsOpen { get; private set; }
        internal bool IsReview => IsOpen && review;
        internal Texture2D LastImage => lastImage;
        internal Color32[] CopyPaintedLayers() => lastLayers;

        internal StoryBrush(StorySceneProgress progress, Texture2D canvasGrain, Func<Vector3, Transform, bool> obstructed)
        {
            this.progress = progress;
            this.obstructed = obstructed;
            walk = progress.Walker;
            eyes = walk.GetComponent<Camera>();
            guide = new Material(Resources.Load<Shader>("StarryNight/WorldBrushGuide"));
            var tile = canvasGrain.GetPixels32();
            for (int i = 0; i < grain.Length; i++)
                grain[i] = tile[(i / WorldBrushTarget.Width % canvasGrain.height) * canvasGrain.width +
                    i % WorldBrushTarget.Width % canvasGrain.width].r / 255f;
            lastImage = new Texture2D(WorldBrushTarget.Width, WorldBrushTarget.Height, TextureFormat.RGBA32, false);
            overlay = new VisualElement();
            overlay.AddToClassList("story-overlay");
            progress.AddChapterUI(overlay);

            card = StoryMenu.Panel(overlay, "story-card");
            card.AddToClassList("brush-card");
            title = StoryMenu.Label(card, "", "story-card-title");
            caption = StoryMenu.Label(card, "", "brush-hint");
            surface = new Image { scaleMode = ScaleMode.ScaleToFit };
            surface.AddToClassList("brush-surface");
            card.Add(surface);
            StoryMenu.Button(card, "繼續", () => Hide());

            tools = StoryMenu.Panel(overlay, "world-brush-tools");
            var row = StoryMenu.Panel(tools, "brush-options");
            brushButton = StoryMenu.Button(row, "畫筆", () => SetTool(false));
            eraserButton = StoryMenu.Button(row, "橡皮擦", () => SetTool(true));
            hint = StoryMenu.Label(tools, "左鍵作畫　右鍵拖曳環顧　點右鍵收起", "brush-hint");
            status = StoryMenu.Label(tools, "", "brush-hint");
            palette = StoryMenu.Panel(tools, "brush-options");
            assist = StoryMenu.Button(tools, "使用輔助操作", Assist);
            clock = StoryMenu.Label(tools, "", "brush-hint");
            SetTool(false);
            StoryMenu.Show(overlay, false);
        }

        void SetTool(bool erase)
        {
            eraser = erase;
            brushButton.EnableInClassList("brush-choice-selected", !erase);
            eraserButton.EnableInClassList("brush-choice-selected", erase);
            CancelStroke();
        }

        internal WorldBrushTarget Register(GameObject obj, string verb, bool erase, Option[] options,
            Action<int> finished, Color32[] previous)
        {
            var target = new WorldBrushTarget(obj, verb, erase, options, finished, previous, grain);
            targets.Add(target);
            var label = StoryMenu.Label(overlay, erase ? "擦除" : "補畫", "world-brush-marker");
            label.EnableInClassList("brush-marker-paint", !erase);
            label.generateVisualContent += context => DrawToolIcon(context, erase);
            overlay.Insert(0, label);
            labels.Add(target, label);
            StoryMenu.Show(label, false);
            return target;
        }

        static void DrawToolIcon(MeshGenerationContext context, bool erase)
        {
            var pen = context.painter2D;
            float y = context.visualElement.layout.height * .5f;
            pen.strokeColor = erase ? new Color(1f, .80f, .53f) : new Color(.57f, .92f, 1f);
            pen.lineWidth = 1.6f;
            pen.BeginPath();
            pen.MoveTo(new Vector2(6, y + 3));
            pen.LineTo(new Vector2(14, y - 5));
            pen.LineTo(new Vector2(erase ? 19 : 17, y - (erase ? 1 : 2)));
            pen.LineTo(new Vector2(erase ? 12 : 9, y + 6));
            pen.LineTo(new Vector2(6, y + (erase ? 3 : 7)));
            pen.ClosePath();
            pen.Stroke();
            pen.BeginPath();
            pen.MoveTo(new Vector2(erase ? 10 : 12, y - (erase ? 1 : 3)));
            pen.LineTo(new Vector2(15, y + (erase ? 3 : 0)));
            pen.Stroke();
        }

        internal void ClearTargets()
        {
            foreach (var target in targets)
            {
                labels[target].RemoveFromHierarchy();
                target.Dispose();
            }
            targets.Clear();
            visible.Clear();
            labels.Clear();
            Select(null);
            CancelStroke();
        }

        internal void Open(WorldBrushTarget focused = null)
        {
            if (IsOpen) return;
            review = false;
            Show();
            if (focused != null && CanSee(focused))
            {
                focused.Prepare(eyes);
                Select(focused);
                SetTool(focused.Erase);
            }
        }

        void Show()
        {
            IsOpen = true;
            openedFrame = Time.frameCount;
            suppressed = walk.InputSuppressed;
            captured = UnityEngine.Cursor.lockState == CursorLockMode.Locked;
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            overlay.EnableInClassList("world-brush-open", !review);
            StoryMenu.Show(tools, !review);
            StoryMenu.Show(card, review);
            StoryMenu.Show(clock, false);
            StoryMenu.Show(overlay, true);
        }

        internal void Review(string heading, Texture2D art, string text, Action finished, Rect? detail = null)
        {
            title.text = heading;
            caption.text = text;
            surface.image = art;
            surface.uv = new Rect(0, 0, 1, 1);
            closeReview = finished;
            review = true;
            Show();
            if (!detail.HasValue) return;
            Rect crop = detail.Value;
            float start = Time.unscaledTime;
            zoom = surface.schedule.Execute(() =>
            {
                float elapsed = Mathf.Clamp01((Time.unscaledTime - start) / .70f);
                float t = Mathf.SmoothStep(0, 1, elapsed);
                surface.uv = new Rect(crop.x * t, crop.y * t, Mathf.Lerp(1, crop.width, t), Mathf.Lerp(1, crop.height, t));
                if (elapsed >= 1f) zoom?.Pause();
            }).Every(16);
        }

        internal void ShowTime(int seconds)
        {
            clock.text = $"剩餘 {seconds} 秒";
            StoryMenu.Show(clock, true);
        }

        internal void Abort()
        {
            closeReview = null;
            Hide();
        }

        internal bool Hide()
        {
            if (!IsOpen) return false;
            IsOpen = false;
            zoom?.Pause();
            zoom = null;
            CancelStroke();
            rightDown = looking = false;
            Select(null);
            foreach (var label in labels.Values) StoryMenu.Show(label, false);
            visible.Clear();
            StoryMenu.Show(overlay, false);
            walk.InputSuppressed = suppressed;
            StoryMenu.SetCursor(captured);
            var after = closeReview;
            closeReview = null;
            after?.Invoke();
            return true;
        }

        bool CanSee(WorldBrushTarget target)
        {
            if (!target.Object || !target.Object.activeInHierarchy) return false;
            var v = eyes.WorldToViewportPoint(target.Center);
            return v.z > eyes.nearClipPlane && v.x >= 0 && v.x <= 1 && v.y >= 0 && v.y <= 1 &&
                !obstructed(target.Center, target.Object.transform);
        }

        bool InReach(WorldBrushTarget target) => Vector3.Distance(walk.Player.transform.position + Vector3.up * .9f,
            target.Object.transform.position) <= Reach;
        internal const float Reach = 3.15f;

        void Select(WorldBrushTarget target)
        {
            if (active == target) return;
            active = target;
            palette.Clear();
            if (target == null || target.Options.Length < 2) return;
            for (int i = 0; i < target.Options.Length; i++)
            {
                int choice = i;
                StoryMenu.Button(palette, target.Options[i].Name, () =>
                {
                    if (active != target || choice == target.Choice) return;
                    target.Select(choice);
                    MarkChoice(target);
                });
            }
            MarkChoice(target);
        }

        void MarkChoice(WorldBrushTarget target)
        {
            for (int i = 0; i < palette.childCount; i++)
                palette[i].EnableInClassList("brush-choice-selected", i == target.Choice);
        }

        internal void Tick()
        {
            if (!IsOpen || review || overlay.panel == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Time.frameCount != openedFrame && mouse.rightButton.wasPressedThisFrame)
            {
                rightDown = true;
                dragDelta = Vector2.zero;
                CancelStroke();
            }
            if (rightDown)
            {
                var delta = mouse.delta.ReadValue();
                dragDelta += delta;
                if (!looking && dragDelta.sqrMagnitude >= 25)
                {
                    looking = true;
                    StoryMenu.SetCursor(true);
                    walk.LookFromCanvas(dragDelta);
                }
                else if (looking) walk.LookFromCanvas(delta);
                if (!mouse.rightButton.isPressed)
                {
                    rightDown = false;
                    if (!looking) { Hide(); return; }
                    looking = false;
                    StoryMenu.SetCursor(false);
                }
            }

            Vector2 screen = mouse.position.ReadValue();
            bool overTools = tools.worldBound.Contains(PanelPoint(screen));
            visible.Clear();
            foreach (var target in targets)
            {
                bool shown = CanSee(target);
                if (shown)
                {
                    target.Prepare(eyes);
                    shown = CanSee(target);
                }
                var label = labels[target];
                StoryMenu.Show(label, shown);
                if (!shown) continue;
                visible.Add(target);
                var location = overlay.WorldToLocal(PanelPoint(eyes.WorldToScreenPoint(target.Center)));
                label.style.left = location.x;
                label.style.top = location.y;
                label.text = (target.Erase ? "擦除" : "補畫") + (InReach(target) ? "" : " · 靠近");
            }
            Vector2 pixel = default;
            var hover = rightDown || overTools ? null : Pick(screen, out pixel);
            if (stroke == null && hover != null) Select(hover);
            if (active != null && !visible.Contains(active)) Select(null);
            if (active == null && visible.Count == 1) Select(visible[0]);
            foreach (var target in visible)
            {
                target.Draw(eyes, guide, target == hover);
                labels[target].EnableInClassList("brush-marker-hover", target == hover);
            }
            bool usable = active != null && InReach(active) && eraser == active.Erase;
            status.text = active == null ? "環顧尋找畫筆或橡皮擦標記" :
                !InReach(active) ? $"{active.Verb}　收起畫板並靠近" :
                !usable ? $"{active.Verb}　選擇{(active.Erase ? "橡皮擦" : "畫筆")}" : $"{active.Verb}　{active.Progress}";
            StoryMenu.Show(assist, usable);
            if (overTools || rightDown || !usable) { CancelStroke(); return; }
            if (mouse.leftButton.wasPressedThisFrame && hover == active && active.Deposit(pixel))
            {
                stroke = active;
                strokeLength = 0;
                lastScreen = screen;
                lastPixel = pixel;
                progress.Sound?.PlayBrush();
                active.Upload();
            }
            if (stroke == null) return;
            int steps = Mathf.CeilToInt(Vector2.Distance(screen, lastScreen) / 3f);
            bool changed = false;
            for (int i = 1; i <= steps; i++)
            {
                var point = Vector2.Lerp(lastScreen, screen, i / (float)steps);
                if (Pick(point, out var ink) != stroke || !stroke.Deposit(ink))
                {
                    lastPixel = new Vector2(float.NaN, 0);
                    continue;
                }
                if (!float.IsNaN(lastPixel.x)) strokeLength += Vector2.Distance(lastPixel, ink);
                lastPixel = ink;
                changed = true;
            }
            if (changed) stroke.Upload();
            lastScreen = screen;
            if (!mouse.leftButton.isPressed) stroke.EndStroke(strokeLength);
            if (stroke.Finished) Finish(stroke);
            else if (!mouse.leftButton.isPressed) CancelStroke();
        }

        Vector2 PanelPoint(Vector2 screen) => RuntimePanelUtils.ScreenToPanel(overlay.panel,
            new Vector2(screen.x, Screen.height - screen.y));

        WorldBrushTarget Pick(Vector2 screen, out Vector2 pixel)
        {
            pixel = default;
            var ray = eyes.ScreenPointToRay(screen);
            float nearest = float.PositiveInfinity;
            WorldBrushTarget best = null;
            foreach (var target in visible)
            {
                if (!target.Hit(ray, out float distance, out var uv) || distance >= nearest ||
                    obstructed(ray.GetPoint(distance), target.Object.transform)) continue;
                nearest = distance;
                best = target;
                pixel = uv;
            }
            return best;
        }

        void CancelStroke() { stroke = null; strokeLength = 0; }

        void Assist()
        {
            if (review) { Hide(); return; }
            if (active == null || active.Erase != eraser || !CanSee(active) || !InReach(active)) return;
            active.Assist();
            Finish(active);
        }

        void Finish(WorldBrushTarget target)
        {
            if (!target.Finished || !targets.Contains(target) || !CanSee(target) || !InReach(target)) return;
            int answer = target.Choice;
            lastLayers = target.Layers();
            var art = (Color32[])lastLayers.Clone();
            for (int i = 0; i < art.Length; i++)
                if (art[i].a == 0) art[i] = Color.Lerp(new Color(.59f, .50f, .36f), new Color(.84f, .76f, .59f), grain[i]);
            lastImage.SetPixels32(art);
            lastImage.Apply();
            targets.Remove(target);
            labels[target].RemoveFromHierarchy();
            labels.Remove(target);
            Hide();
            target.Dispose();
            target.Complete(answer);
        }

        public void Dispose()
        {
            Abort();
            ClearTargets();
            overlay.RemoveFromHierarchy();
            UnityEngine.Object.Destroy(guide);
            UnityEngine.Object.Destroy(lastImage);
        }

        internal static bool InMask(Shape shape, float x, float y)
        {
            float dx = x - .5f, dy = y - .5f;
            bool inside(float l, float r, float b, float t) => x >= l && x <= r && y >= b && y <= t;
            bool oval(float a, float b, float rx, float ry) =>
                Mathf.Pow((x - a) / rx, 2) + Mathf.Pow((y - b) / ry, 2) <= 1;
            return shape switch
            {
                Shape.Panel => inside(.25f, .75f, .27f, .73f),
                Shape.Cloth => inside(.26f, .74f, .23f + .025f * Mathf.Cos(x * 65f), .77f),
                Shape.Board => inside(.19f, .82f, .44f + dx * .4f, .57f + dx * .4f),
                Shape.Seat => inside(.26f, .36f, .24f, .77f) || inside(.64f, .74f, .24f, .47f) ||
                    inside(.30f, .74f, .40f, .50f) || inside(.32f, .65f, .63f, .76f),
                Shape.Lantern => oval(.5f, .48f, .16f, .24f) || inside(.36f, .64f, .23f, .29f) ||
                    inside(.38f, .62f, .68f, .74f),
                Shape.Bread => oval(.5f, .48f, .30f, .19f),
                Shape.Pot => oval(.5f, .45f, .22f, .22f) || inside(.23f, .77f, .57f, .65f),
                Shape.Crop => oval(.40f, .44f, .17f, .14f) || oval(.58f, .49f, .18f, .18f) ||
                    oval(.48f, .58f, .14f, .17f),
                Shape.Road => y > .2f && y < .79f && Mathf.Abs(dx + dy * .34f) < (.80f - y) * .28f + .037f,
                Shape.Sky => inside(.12f, .88f, .42f + .05f * Mathf.Sin(x * 15), .84f),
                Shape.Star => oval(.64f, .66f, .095f, .14f) || oval(.32f, .58f, .073f, .104f),
                Shape.Spiral => Spiral(x, y),
                Shape.Branch => inside(.14f, .85f, .19f + x * .57f, .27f + x * .57f) ||
                    (x > .39f && x < .73f && Mathf.Abs(y - (1.08f - x * .9f)) < .047f),
                Shape.Blossom => oval(.29f, .40f, .10f, .12f) || oval(.50f, .58f, .09f, .12f) ||
                    oval(.73f, .70f, .11f, .13f),
                Shape.Shoots => oval(.33f, .44f, .075f, .13f) || oval(.69f, .67f, .065f, .14f),
                Shape.Circle => oval(.5f, .5f, .20f, .31f),
                Shape.Frame => inside(.19f, .81f, .22f, .78f) && !inside(.27f, .73f, .34f, .66f),
                Shape.Arrow => inside(.20f, .63f, .43f, .57f) || (x >= .60f && x < .83f && Mathf.Abs(dy) < .83f - x),
                Shape.Paper => inside(.32f, .68f, .24f, .76f),
                _ => inside(.04f, .96f, .06f, .94f)
            };
        }

        static bool Spiral(float x, float y)
        {
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                float r = .29f * t;
                var point = new Vector2(.5f + Mathf.Cos(t * 12.6f) * r, .5f + Mathf.Sin(t * 12.6f) * r);
                if (Vector2.Distance(new Vector2(x, y), point) <= .026f) return true;
            }
            return false;
        }

    }
}
