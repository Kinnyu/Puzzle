using Puzzle.Dialogue;
using Puzzle.StarryNight;
using UnityEngine;

namespace Puzzle.Story
{
    public sealed class StarryNightChapterOne : MonoBehaviour
    {
        enum Stage { Meet, Pack, Reflect, Seat, Talk, Finished }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        StorySceneProgress progress;
        StarryNightWalk walk;
        DialoguePlayer dialogue;
        DialogueTrigger vincent;
        Transform station;
        Transform parcel;
        Transform seat;
        Material propMaterial;
        Stage stage;

        internal void Begin(StorySceneProgress scene, StarryNightWalk player, DialoguePlayer speaker,
            DialogueTrigger actor, string checkpoint)
        {
            progress = scene;
            walk = player;
            dialogue = speaker;
            vincent = actor;
            propMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Transform origin = actor.transform;
            station = MakeBox("Coat table", origin.position - origin.forward * 1.65f + origin.right * .65f,
                new Vector3(.65f, .49f, .45f), new Color(.28f, .17f, .11f));
            parcel = MakeBox("Coat", station.position + Vector3.up * .25f, new Vector3(.47f, .10f, .33f),
                new Color(.34f, .42f, .57f));
            seat = MakeBox("Spare stool", origin.position - origin.forward * .45f - origin.right * .96f,
                new Vector3(.43f, .41f, .43f), new Color(.32f, .22f, .16f));
            switch (checkpoint)
            {
                case "church-help":
                    SetFoldedCoat();
                    BeginReflection("help");
                    vincent.TryPlay();
                    break;
                case "church-value":
                    SetFoldedCoat();
                    Sit();
                    break;
                default:
                    vincent.SetInteraction("church-arrival", BeginPacking);
                    break;
            }
        }

        Transform MakeBox(string label, Vector3 feet, Vector3 size, Color color)
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = label;
            prop.transform.SetParent(transform);
            prop.transform.position = feet + Vector3.up * (size.y * .5f);
            prop.transform.rotation = vincent.transform.rotation;
            prop.transform.localScale = size;
            prop.GetComponent<Collider>().enabled = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor(BaseColor, color);
            var renderer = prop.GetComponent<Renderer>();
            StoryPropInk.SetSurface(prop, propMaterial);
            renderer.SetPropertyBlock(properties);
            return prop.transform;
        }

        void BeginPacking()
        {
            stage = Stage.Pack;
            vincent.SetInteraction(null, null);
        }

        void FoldCoat()
        {
            SetFoldedCoat();
            BeginReflection("meaning");
        }

        void SetFoldedCoat()
        {
            parcel.localScale = new Vector3(.28f, .15f, .23f);
            parcel.position = station.position + Vector3.up * .32f;
        }

        void BeginReflection(string node)
        {
            stage = Stage.Reflect;
            vincent.SetInteraction(node, BeginSeating);
        }

        void BeginSeating()
        {
            stage = Stage.Seat;
            vincent.SetInteraction(null, null);
        }

        void Sit()
        {
            stage = Stage.Talk;
            vincent.SetInteraction("value", Finish);
            if (vincent.TryPlay()) walk.SetSeated(true);
        }

        void Finish()
        {
            stage = Stage.Finished;
            walk.SetSeated(false);
            vincent.SetInteraction(null, null);
            HideProps();
            vincent.GetComponent<DialogueActorAction>()?.Perform();
            progress.SetObjective("");
            progress.CompleteChapter();
        }

        void HideProps()
        {
            station.gameObject.SetActive(false);
            parcel.gameObject.SetActive(false);
            seat.gameObject.SetActive(false);
        }

        void Update()
        {
            if (stage == Stage.Finished || !walk.Player.enabled || walk.InputSuppressed || dialogue.BlocksGameplayInput) return;
            string key = dialogue.InteractionKeyLabel;
            switch (stage)
            {
                case Stage.Meet:
                    progress.SetObjective(dialogue.Local("到教堂裡和梵谷打個招呼", "教会でゴッホに声をかける"));
                    break;
                case Stage.Pack:
                    Offer(station, "靠近小桌，把外套摺好", "小さな台に近づき、上着をたたむ",
                        $"按 {key} 摺好外套", $"{key} で上着をたたむ", FoldCoat);
                    break;
                case Stage.Reflect:
                    progress.SetObjective(dialogue.Local("回到梵谷身旁，繼續和他聊聊", "ゴッホのところに戻り、話の続きを聞く"));
                    break;
                case Stage.Seat:
                    Offer(seat, "梵谷在等人，到他旁邊坐一會兒", "ゴッホのそばで一緒に待つ",
                        $"按 {key} 留下來陪他", $"{key} でそばに座る", Sit);
                    break;
                case Stage.Talk:
                    progress.SetObjective(dialogue.Local("聽聽梵谷想說的話", "ゴッホの話を聞く"));
                    break;
            }
        }

        void Offer(Transform target, string approach, string japaneseApproach, string action, string japaneseAction,
            System.Action interact)
        {
            Vector3 offset = target.position - walk.Player.transform.position;
            float elevation = Mathf.Abs(offset.y);
            offset.y = 0f;
            bool nearby = offset.sqrMagnitude < .9f * .9f && elevation < .9f;
            progress.SetObjective(nearby ? dialogue.Local(action, japaneseAction) : dialogue.Local(approach, japaneseApproach));
            if (nearby && dialogue.TryConsumeGameplayInteraction()) interact();
        }

        void OnDestroy()
        {
            if (propMaterial != null) Destroy(propMaterial);
        }
    }
}
