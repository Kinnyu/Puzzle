using Puzzle.StarryNight;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Puzzle.Story
{
    public sealed class GalleryDrawingDesk : MonoBehaviour
    {
        [SerializeField] Material oak;
        [SerializeField] Material brass;
        [SerializeField] Material teal;
        [SerializeField] Material paperMaterial;
        [SerializeField] Collider playerBody;

        const float ChargeSeconds = 0.85f;
        const float CrumpleSeconds = 0.26f;
        const float RimHeight = 0.68f;
        static readonly Vector3 SheetPosition = new(0, 0.783f, 0.05f);
        static readonly Vector3 BinPosition = new(1.37f, 0, 0.83f);
        static readonly Color32[] InkColors =
        {
            new(33, 29, 39, 255), new(142, 55, 53, 255), new(227, 116, 61, 255),
            new(226, 174, 49, 255), new(63, 126, 86, 255), new(52, 109, 166, 255),
            new(106, 72, 149, 255), new(126, 79, 53, 255), GalleryPaper.Blank
        };
        static readonly string[] InkNames = { "墨黑", "磚紅", "橘色", "赭黃", "葉綠", "群青", "紫色", "棕色", "橡皮擦" };

        Camera view;
        StarryNightWalk walk;
        Transform furniture;
        VisualElement toolbar;
        SliderInt brushSize;
        ProgressBar power;
        Label feedback;
        Color32 ink = InkColors[0];
        PhysicsMaterial ballSurface, boardSurface;
        Collider backboard;
        Mesh crumpledMesh;
        GalleryPaper sheet, held;
        Vector3 standingPosition, seatedPosition;
        Quaternion standingRotation, seatedRotation;
        Vector2Int? lastPixel;
        float chargeStarted = -1f;
        float crumpleStarted = -1f;
        float feedbackUntil;
        bool drawing;
        bool strokeActive;

        internal bool IsSeated { get; private set; }

        internal void Initialize(Camera camera, StarryNightWalk movement, VisualElement screen)
        {
            view = camera;
            walk = movement;
            BuildFurniture();
            crumpledMesh = GalleryPaper.CreateCrumpledMesh();
            ballSurface = new PhysicsMaterial("Crumpled paper")
            {
                bounciness = 0.18f, bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0.35f, staticFriction = 0.45f
            };
            MakeSheet();
            BuildToolbar(screen);
        }

        internal bool HandleInput(Collider target, bool captured, out string hint)
        {
            hint = null;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (IsSeated)
            {
                if (keys?.escapeKey.wasPressedThisFrame == true)
                {
                    if (drawing) StopDrawing();
                    else Stand();
                }
                else if (keys?.fKey.wasPressedThisFrame == true) TakeSheet();
                else if (!drawing && keys?.eKey.wasPressedThisFrame == true) StartDrawing();

                if (drawing && mouse != null) UpdateStroke(mouse);
                if (IsSeated && !drawing) hint = "按 E 在畫紙上作畫　　F 拿起畫紙　　Esc 起身";
                return true;
            }

            if (keys?.escapeKey.wasPressedThisFrame == true)
            {
                CancelCharge();
                return false;
            }

            if (held != null)
            {
                if (!captured)
                {
                    CancelCharge();
                    hint = "點擊畫面，繼續探索";
                    return true;
                }

                if (!held.IsBall)
                {
                    if (keys?.rKey.wasPressedThisFrame == true && crumpleStarted < 0f)
                        crumpleStarted = Time.time;
                    if (crumpleStarted < 0f) hint = "R 揉成紙球";
                    return true;
                }

                hint = "按住左鍵蓄力，放開投出";
                if (mouse?.leftButton.wasPressedThisFrame == true) chargeStarted = Time.time;
                if (chargeStarted >= 0f)
                {
                    float strength = Mathf.Clamp01((Time.time - chargeStarted) / ChargeSeconds);
                    power.value = strength;
                    StoryMenu.Show(power, true);
                    if (mouse?.leftButton.isPressed != true)
                    {
                        if (mouse?.leftButton.wasReleasedThisFrame == true) Throw(strength);
                        CancelCharge();
                    }
                }
                return true;
            }

            if (!captured || target == null) return false;
            var paper = target.GetComponent<GalleryPaper>();
            if (paper != null && paper.IsBall)
            {
                hint = "按 E 撿起紙球";
                if (keys?.eKey.wasPressedThisFrame == true) Hold(paper);
                return true;
            }

            if (!target.transform.IsChildOf(furniture)) return false;
            hint = "按 E 坐到畫桌旁";
            if (keys?.eKey.wasPressedThisFrame == true)
            {
                Sit();
                hint = "按 E 在畫紙上作畫　　F 拿起畫紙　　Esc 起身";
            }
            return true;
        }

        void Sit()
        {
            standingPosition = view.transform.position;
            standingRotation = view.transform.rotation;
            seatedPosition = transform.TransformPoint(new Vector3(0, 1.27f, -0.62f));
            seatedRotation = transform.rotation * Quaternion.Euler(23f, 0, 0);
            IsSeated = true;
            walk.InputSuppressed = true;
            view.transform.SetPositionAndRotation(seatedPosition, seatedRotation);
            StoryMenu.SetCursor(true);
        }

        void StartDrawing()
        {
            Vector3 eye = transform.TransformPoint(new Vector3(0, 1.36f, -0.22f));
            Vector3 center = transform.TransformPoint(SheetPosition);
            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(center - eye, transform.up));
            drawing = true;
            StoryMenu.Show(toolbar, true);
            StoryMenu.SetCursor(false);
        }

        void StopDrawing()
        {
            if (!drawing) return;
            drawing = false;
            strokeActive = false;
            lastPixel = null;
            StoryMenu.Show(toolbar, false);
            view.transform.SetPositionAndRotation(seatedPosition, seatedRotation);
            StoryMenu.SetCursor(true);
        }

        void Stand()
        {
            if (drawing) StopDrawing();
            view.transform.SetPositionAndRotation(standingPosition, standingRotation);
            walk.InputSuppressed = false;
            IsSeated = false;
            StoryMenu.SetCursor(true);
        }

        void TakeSheet()
        {
            if (!IsSeated || held != null) return;
            Stand();
            Hold(sheet);
            MakeSheet();
        }

        void Hold(GalleryPaper paper)
        {
            held = paper;
            held.AttachTo(view.transform);
            crumpleStarted = -1f;
        }

        void UpdateStroke(Mouse mouse)
        {
            Vector2 position = mouse.position.ReadValue();
            Vector2 uiPosition = RuntimePanelUtils.ScreenToPanel(toolbar.panel, new Vector2(position.x, Screen.height - position.y));
            bool overTools = toolbar.worldBound.Contains(uiPosition);
            if (!overTools)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) brushSize.value += scroll > 0f ? 2 : -2;
            }

            Vector2Int pixel = default;
            bool overSheet = !overTools && sheet.TryGetPixel(view.ScreenPointToRay(position), out pixel);
            if (mouse.leftButton.wasPressedThisFrame) strokeActive = overSheet;
            if (!mouse.leftButton.isPressed || !Application.isFocused) strokeActive = false;
            if (!strokeActive || !overSheet)
            {
                lastPixel = null;
                return;
            }

            if (pixel == lastPixel) return;
            sheet.Paint(lastPixel ?? pixel, pixel, brushSize.value * 0.5f, ink);
            lastPixel = pixel;
        }

        void LateUpdate()
        {
            if (held != null && crumpleStarted >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - crumpleStarted) / CrumpleSeconds);
                if (t >= 1f)
                {
                    held.Crumple(crumpledMesh, ballSurface);
                    crumpleStarted = -1f;
                }
                else held.Fold(t);
            }

            if (feedbackUntil > 0f && Time.time >= feedbackUntil)
            {
                StoryMenu.Show(feedback, false);
                feedbackUntil = 0f;
            }
        }

        void Throw(float strength)
        {
            // Start clear of the view and player while respecting nearby walls and furniture.
            var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Vector3 origin = ray.origin - ray.direction * view.nearClipPlane;
            const float releaseDistance = 0.49f;
            foreach (var obstacle in Physics.SphereCastAll(origin, GalleryPaper.BallRadius, ray.direction,
                         releaseDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (obstacle.collider == playerBody) continue;
                ShowFeedback("前方有障礙物");
                return;
            }

            held.Release(transform, origin + ray.direction * releaseDistance,
                ray.direction * Mathf.Lerp(2.8f, 7.2f, strength), playerBody);
            held = null;
        }

        void CancelCharge()
        {
            chargeStarted = -1f;
            StoryMenu.Show(power, false);
        }

        void MakeSheet()
        {
            var item = new GameObject("Drawing paper");
            item.transform.SetParent(transform, false);
            item.transform.SetLocalPositionAndRotation(SheetPosition, Quaternion.Euler(90, 0, 0));
            sheet = item.AddComponent<GalleryPaper>();
            sheet.Initialize(this, paperMaterial);
        }

        internal bool CrossedBinOpening(Vector3 previous, Vector3 current)
        {
            Vector3 from = transform.InverseTransformPoint(previous) - BinPosition;
            Vector3 to = transform.InverseTransformPoint(current) - BinPosition;
            if (from.y < RimHeight || to.y >= RimHeight) return false;
            Vector3 entry = Vector3.Lerp(from, to, (from.y - RimHeight) / (from.y - to.y));
            return Mathf.Abs(entry.x) <= 0.315f - GalleryPaper.BallRadius &&
                   Mathf.Abs(entry.z) <= 0.305f - GalleryPaper.BallRadius;
        }

        internal bool IsBackboard(Collider obstacle) => obstacle == backboard;

        internal void Scored(bool banked) => ShowFeedback(banked ? "打板進桶！" : "紙球進桶！");

        void ShowFeedback(string message)
        {
            feedback.text = message;
            StoryMenu.Show(feedback, true);
            feedbackUntil = Time.time + 1.8f;
        }

        void BuildToolbar(VisualElement screen)
        {
            toolbar = StoryMenu.Panel(screen, "drawing-tools");
            var palette = StoryMenu.Panel(toolbar, "drawing-palette");
            for (int i = 0; i < InkColors.Length; i++)
            {
                Color32 color = InkColors[i];
                bool eraser = i == InkColors.Length - 1;
                var button = StoryMenu.Button(palette, eraser ? "擦" : "", () => ink = color, "drawing-swatch");
                button.style.backgroundColor = new StyleColor((Color)color);
                if (eraser) button.AddToClassList("drawing-eraser");
                button.tooltip = InkNames[i];
                button.name = InkNames[i];
                button.userData = color;
                button.EnableInClassList("drawing-swatch-selected", i == 0);
                button.clicked += () =>
                {
                    foreach (var swatch in palette.Children())
                        swatch.EnableInClassList("drawing-swatch-selected", ((Color32)swatch.userData).Equals(ink));
                };
            }

            var actions = StoryMenu.Panel(toolbar, "drawing-actions");
            brushSize = new SliderInt("筆刷", 2, 36) { value = 8, showInputField = false };
            brushSize.tooltip = "也可用滑鼠滾輪調整粗細";
            brushSize.AddToClassList("drawing-brush");
            actions.Add(brushSize);
            StoryMenu.Button(actions, "停筆", StopDrawing);
            StoryMenu.Button(actions, "拿起畫紙", TakeSheet, "story-primary");
            StoryMenu.Show(toolbar, false);

            power = new ProgressBar { lowValue = 0, highValue = 1, title = "力道", pickingMode = PickingMode.Ignore };
            power.AddToClassList("drawing-power");
            screen.Add(power);
            StoryMenu.Show(power, false);
            feedback = StoryMenu.Label(screen, "", "story-toast");
            StoryMenu.Show(feedback, false);
        }

        void BuildFurniture()
        {
            furniture = new GameObject("Table and chair").transform;
            furniture.SetParent(transform, false);
            Box(furniture, "Desktop", new Vector3(0, 0.735f, 0), new Vector3(1.32f, 0.09f, 0.73f), oak);
            Box(furniture, "Desk apron", new Vector3(0, 0.645f, 0.23f), new Vector3(1.19f, 0.11f, 0.07f), oak);
            foreach (int side in new[] { -1, 1 })
            {
                foreach (int end in new[] { -1, 1 })
                {
                    Box(furniture, "Table leg", new Vector3(side * 0.565f, 0.35f, end * 0.27f),
                        new Vector3(0.08f, 0.70f, 0.08f), oak);
                    Box(furniture, "Chair leg", new Vector3(side * 0.21f, 0.22f, -0.67f + end * 0.18f),
                        new Vector3(0.055f, 0.44f, 0.055f), oak);
                }
                Box(furniture, "Chair upright", new Vector3(side * 0.21f, 0.655f, -0.86f),
                    new Vector3(0.055f, 0.41f, 0.055f), oak);
            }
            Box(furniture, "Seat", new Vector3(0, 0.44f, -0.66f), new Vector3(0.52f, 0.095f, 0.47f), teal);
            Box(furniture, "Chair back", new Vector3(0, 0.81f, -0.86f), new Vector3(0.49f, 0.18f, 0.065f), oak);

            var bin = new GameObject("Open wastebasket").transform;
            bin.SetParent(transform, false);
            bin.localPosition = BinPosition;
            Box(bin, "Bottom", new Vector3(0, 0.055f, 0), new Vector3(0.71f, 0.10f, 0.69f), teal);
            foreach (int side in new[] { -1, 1 })
            {
                Box(bin, "Side", new Vector3(side * 0.335f, 0.34f, 0), new Vector3(0.04f, RimHeight, 0.69f), teal);
                Box(bin, "End", new Vector3(0, 0.34f, side * 0.325f), new Vector3(0.63f, RimHeight, 0.04f), teal);
                Box(bin, "Side rim", new Vector3(side * 0.344f, 0.66f, 0), new Vector3(0.055f, 0.038f, 0.71f), brass, false);
                Box(bin, "End rim", new Vector3(0, 0.66f, side * 0.334f), new Vector3(0.65f, 0.038f, 0.055f), brass, false);
            }

            boardSurface = new PhysicsMaterial("Paper ball backboard")
            {
                bounciness = 0.55f, bounceCombine = PhysicsMaterialCombine.Maximum,
                staticFriction = 0.12f, dynamicFriction = 0.08f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            var board = Box(bin, "Backboard", new Vector3(0, 1.18f, 0.394f), new Vector3(0.96f, 1.04f, 0.08f), oak);
            backboard = board.GetComponent<Collider>();
            backboard.sharedMaterial = boardSurface;
            foreach (int side in new[] { -1, 1 })
            {
                Box(bin, "Backboard support", new Vector3(side * 0.30f, 0.55f, 0.441f),
                    new Vector3(0.065f, 1.10f, 0.065f), oak);
                Box(bin, "Backboard guide", new Vector3(side * 0.239f, 1.20f, 0.352f),
                    new Vector3(0.014f, 0.43f, 0.004f), brass, false);
                Box(bin, "Backboard guide", new Vector3(0, 1.20f + side * 0.212f, 0.352f),
                    new Vector3(0.48f, 0.014f, 0.004f), brass, false);
            }
        }

        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) item.GetComponent<Collider>().enabled = false;
            return item;
        }

        void OnDestroy()
        {
            Destroy(ballSurface);
            Destroy(boardSurface);
            Destroy(crumpledMesh);
        }
    }
}
