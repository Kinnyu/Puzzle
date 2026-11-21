using System;
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

        const int Width = 160;
        const int Height = 100;
        const float RequiredCoverage = .90f;
        readonly StorySceneProgress progress;
        readonly StarryNight.StarryNightWalk walk;
        readonly VisualElement overlay;
        readonly Label title, hint, coverage, clock;
        readonly VisualElement palette;
        readonly Image surface;
        readonly Button help;
        readonly Texture2D texture;
        readonly Color32[] pixels = new Color32[Width * Height];
        readonly bool[] mask = new bool[Width * Height];
        readonly bool[] visited = new bool[Width * Height];
        Option[] choices;
        Color32[] underpaint;
        Action<int> complete;
        Action close;
        int selected;
        int pointer = -1;
        int total, filled, strokes;
        bool moved;
        float strokeLength;
        Vector2 last;
        bool erase;
        bool hadSuppressedInput;
        bool capturedCursor;
        bool review;
        IVisualElementScheduledItem zoom;

        internal bool IsOpen { get; private set; }
        internal bool IsReview => IsOpen && review;
        internal Texture2D LastImage => texture;

        internal StoryBrush(StorySceneProgress progress)
        {
            this.progress = progress;
            walk = progress.Walker;
            overlay = new VisualElement();
            overlay.AddToClassList("story-overlay");
            progress.AddChapterUI(overlay);
            var card = StoryMenu.Panel(overlay, "story-card");
            card.AddToClassList("brush-card");
            title = StoryMenu.Label(card, "", "story-card-title");
            hint = StoryMenu.Label(card, "", "brush-hint");
            clock = StoryMenu.Label(card, "", "brush-hint");
            palette = StoryMenu.Panel(card, "brush-options");
            surface = new Image { scaleMode = ScaleMode.StretchToFill };
            surface.AddToClassList("brush-surface");
            card.Add(surface);
            coverage = StoryMenu.Label(card, "", "brush-hint");
            var actions = StoryMenu.Panel(card, "brush-options");
            help = StoryMenu.Button(actions, "使用輔助操作", Assist);
            StoryMenu.Button(actions, "關閉畫布", () => Hide());
            texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) { name = "星夜畫布", filterMode = FilterMode.Bilinear };
            surface.RegisterCallback<PointerDownEvent>(OnDown);
            surface.RegisterCallback<PointerMoveEvent>(OnMove);
            surface.RegisterCallback<PointerUpEvent>(OnUp);
            surface.RegisterCallback<PointerCaptureOutEvent>(evt =>
            {
                if (evt.pointerId == pointer) pointer = -1;
            });
            StoryMenu.Show(overlay, false);
        }

        internal void Open(string heading, Option[] options, bool eraser, Action<int> finished, Color32[] previousLayers)
        {
            title.text = heading;
            bool same = ReferenceEquals(choices, options) && erase == eraser && selected >= 0;
            choices = options;
            underpaint = previousLayers;
            erase = eraser;
            complete = finished;
            close = null;
            review = false;
            palette.Clear();
            for (int i = 0; i < options.Length && options.Length > 1; i++)
            {
                int index = i;
                StoryMenu.Button(palette, options[i].Name, () =>
                {
                    if (selected != index) Select(index);
                });
            }
            surface.image = texture;
            surface.uv = new Rect(0, 0, 1, 1);
            if (!same) Select(0);
            else
            {
                MarkChoice();
                RefreshLabels();
            }
            Show();
        }

        internal void Review(string heading, Texture2D art, string caption, Action finished, Rect? detail = null)
        {
            title.text = heading;
            hint.text = caption;
            coverage.text = "";
            palette.Clear();
            surface.image = art;
            surface.uv = new Rect(0, 0, 1, 1);
            surface.scaleMode = ScaleMode.ScaleToFit;
            help.text = "繼續";
            close = finished;
            complete = null;
            review = true;
            Show();
            if (!detail.HasValue) return;
            Rect crop = detail.Value;
            float start = Time.unscaledTime;
            zoom = surface.schedule.Execute(() =>
            {
                float elapsed = Mathf.Clamp01((Time.unscaledTime - start) / .70f);
                float t = Mathf.SmoothStep(0, 1, elapsed);
                surface.uv = new Rect(crop.x * t, crop.y * t,
                    Mathf.Lerp(1, crop.width, t), Mathf.Lerp(1, crop.height, t));
                if (elapsed >= 1f) zoom?.Pause();
            }).Every(16);
        }

        void Show()
        {
            if (IsOpen) return;
            StoryMenu.Show(clock, false);
            IsOpen = true;
            hadSuppressedInput = walk.InputSuppressed;
            capturedCursor = CursorIsCaptured();
            walk.InputSuppressed = true;
            StoryMenu.SetCursor(false);
            StoryMenu.Show(overlay, true);
        }

        static bool CursorIsCaptured() => UnityEngine.Cursor.lockState == CursorLockMode.Locked;

        internal void ShowTime(int seconds)
        {
            clock.text = $"剩餘 {seconds} 秒";
            StoryMenu.Show(clock, true);
        }

        internal void Abort()
        {
            close = null;
            complete = null;
            choices = null;
            Hide();
        }

        internal bool Hide()
        {
            if (!IsOpen) return false;
            zoom?.Pause();
            zoom = null;
            if (pointer >= 0 && surface.HasPointerCapture(pointer)) surface.ReleasePointer(pointer);
            pointer = -1;
            IsOpen = false;
            StoryMenu.Show(overlay, false);
            walk.InputSuppressed = hadSuppressedInput;
            StoryMenu.SetCursor(capturedCursor);
            var onClosed = close;
            close = null;
            onClosed?.Invoke();
            return true;
        }

        void Select(int index)
        {
            selected = index;
            filled = strokes = total = 0;
            var option = choices[index];
            Array.Clear(visited, 0, visited.Length);
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                float nx = (x + .5f) / Width, ny = (y + .5f) / Height;
                mask[i] = InMask(option.Mask, nx, ny);
                if (mask[i]) total++;
                Color32 ground = PaperPixel(i, x, y);
                pixels[i] = mask[i] && option.Mask != Shape.Free
                    ? (erase ? option.Color : Color.Lerp(ground, new Color(.96f, .91f, .77f), .57f))
                    : ground;
            }
            surface.scaleMode = ScaleMode.StretchToFill;
            MarkChoice();
            PaintTexture();
            RefreshLabels();
        }

        static Color32 BasePixel(int x, int y)
        {
            byte shade = (byte)((x * 17 + y * 31) % 11);
            return new Color32((byte)(170 + shade), (byte)(150 + shade), (byte)(117 + shade), 255);
        }

        void MarkChoice()
        {
            for (int i = 0; i < palette.childCount; i++)
                palette[i].EnableInClassList("brush-choice-selected", i == selected);
        }

        Color32 PaperPixel(int index, int x, int y) => underpaint?[index] ?? BasePixel(x, y);

        internal Color32[] CopyPaintedLayers()
        {
            var layers = new Color32[pixels.Length];
            for (int i = 0; i < layers.Length; i++)
                layers[i] = visited[i] ? pixels[i] : PaperPixel(i, i % Width, i / Width);
            return layers;
        }

        void RefreshLabels()
        {
            string name = choices[selected].Name;
            bool free = choices[selected].Mask == Shape.Free;
            hint.text = free ? $"{name}　拖曳滑鼠，留下三筆"
                : erase ? $"{name}　按住右鍵擦除有顏色的範圍"
                : $"{name}　按住左鍵塗畫淡色範圍";
            coverage.text = free ? $"{strokes} / 3 筆" : $"{Percent()}% / 90%";
            help.text = "使用輔助操作";
        }

        int Percent() => total == 0 ? 0 : Mathf.FloorToInt(100f * filled / total);

        void OnDown(PointerDownEvent evt)
        {
            if (!IsOpen || review || pointer >= 0 || evt.button != (erase ? 1 : 0)) return;
            pointer = evt.pointerId;
            surface.CapturePointer(pointer);
            last = Coordinate(evt.localPosition);
            strokeLength = 0f;
            moved = false;
            progress.Sound?.PlayBrush();
            Deposit(last);
            PaintTexture();
            RefreshLabels();
            CheckCoverage();
            evt.StopPropagation();
        }

        void OnMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 position = Coordinate(evt.localPosition);
            float distance = Vector2.Distance(position, last);
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / 2f));
            for (int i = 1; i <= samples; i++)
                moved |= Deposit(Vector2.Lerp(last, position, i / (float)samples));
            strokeLength += distance;
            last = position;
            PaintTexture();
            RefreshLabels();
            CheckCoverage();
            evt.StopPropagation();
        }

        void OnUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            surface.ReleasePointer(pointer);
            pointer = -1;
            if (choices[selected].Mask == Shape.Free && moved && strokeLength >= 5f)
            {
                strokes++;
                RefreshLabels();
                if (strokes == 3) Finish();
            }
            evt.StopPropagation();
        }

        Vector2 Coordinate(Vector2 local) => new(
            Mathf.Clamp(local.x / Mathf.Max(1, surface.contentRect.width) * Width, -10, Width + 10),
            Mathf.Clamp((1 - local.y / Mathf.Max(1, surface.contentRect.height)) * Height, -10, Height + 10));

        bool Deposit(Vector2 position)
        {
            int radius = choices[selected].Mask == Shape.Free ? 3 : 8;
            int cx = Mathf.RoundToInt(position.x), cy = Mathf.RoundToInt(position.y);
            bool painted = false;
            for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(Height - 1, cy + radius); y++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(Width - 1, cx + radius); x++)
            {
                int dx = x - cx, dy = y - cy, i = y * Width + x;
                if (!mask[i] || dx * dx + dy * dy > radius * radius) continue;
                if (!visited[i]) filled++;
                visited[i] = true;
                pixels[i] = erase ? PaperPixel(i, x, y) : choices[selected].Color;
                painted = true;
            }
            return painted;
        }

        void CheckCoverage()
        {
            if (choices[selected].Mask != Shape.Free && total > 0 && filled >= Mathf.CeilToInt(total * RequiredCoverage))
                Finish();
        }

        void PaintTexture()
        {
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void Finish()
        {
            if (review)
            {
                Hide();
                return;
            }
            var finished = complete;
            int answer = selected;
            choices = null;
            close = null;
            complete = null;
            Hide();
            finished?.Invoke(answer);
        }

        void Assist()
        {
            if (review)
            {
                Finish();
                return;
            }
            if (choices[selected].Mask == Shape.Free)
            {
                for (int band = 0; band < 3; band++)
                for (int x = 28; x < 131; x++)
                    Deposit(new Vector2(x, 28 + band * 19 + Mathf.Sin(x * .051f + band * .36f) * 9));
            }
            else
            {
                for (int i = 0; i < mask.Length; i++)
                {
                    if (!mask[i]) continue;
                    visited[i] = true;
                    pixels[i] = erase ? PaperPixel(i, i % Width, i / Width) : choices[selected].Color;
                }
            }
            PaintTexture();
            Finish();
        }

        static bool InMask(Shape shape, float x, float y)
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

        public void Dispose()
        {
            UnityEngine.Object.Destroy(texture);
            overlay.RemoveFromHierarchy();
        }
    }
}
