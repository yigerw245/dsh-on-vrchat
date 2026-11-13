using TMPro;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.Core;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

public static class DafeiyuNpcMenu
{
    private const string TestScenePath = "Assets/Dafeiyu/Dafeiyu Test World.unity";
    private const string DefaultFontPath = "Assets/Dafeiyu/Fonts/NotoSansCJKsc-Regular.otf";
    private const string SubtitleFontPath = "Assets/Dafeiyu/Chinese Subtitles.asset";

    [MenuItem("Tools/Dafeiyu/Open Test World")]
    public static void OpenTestWorld()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) != null)
        {
            EditorSceneManager.OpenScene(TestScenePath);
            return;
        }

        TMP_FontAsset font = GetSubtitleFont(false);
        if (font == null) return;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = new GameObject("VRC World");
        var descriptor = world.AddComponent<VRCSceneDescriptor>();
        if (world.GetComponent<PipelineManager>() == null) world.AddComponent<PipelineManager>();
        var spawn = new GameObject("Spawn").transform;
        spawn.SetParent(world.transform, false);
        spawn.localPosition = new Vector3(0f, 0.1f, -3.4f);
        descriptor.spawns = new[] { spawn };
        descriptor.RespawnHeightY = -4f;

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Island";
        floor.transform.position = new Vector3(0f, -0.22f, 0f);
        floor.transform.localScale = new Vector3(14f, 0.22f, 14f);
        floor.GetComponent<Renderer>().sharedMaterial = ColorMaterial("Island", new Color(0.69f, 0.82f, 0.87f));
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        MeshCollider floorCollider = floor.AddComponent<MeshCollider>();
        floorCollider.sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;

        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.94f, 0.84f);
        light.intensity = 1.25f;
        light.transform.rotation = Quaternion.Euler(49f, -32f, 0f);
        light.shadows = LightShadows.Soft;
        RenderSettings.ambientLight = new Color(0.58f, 0.67f, 0.76f);

        CreateNpc(font);
        if (!EditorSceneManager.SaveScene(scene, TestScenePath))
        {
            throw new System.InvalidOperationException("The Dafeiyu test scene could not be saved.");
        }
    }

    [MenuItem("Tools/Dafeiyu/Add NPC to Scene")]
    private static void AddNpc()
    {
        TMP_FontAsset font = GetSubtitleFont(true);
        if (font != null) CreateNpc(font);
    }

    [MenuItem("Tools/Dafeiyu/Add Chat Panel to Test World")]
    public static void AddChatPanelToTestWorld()
    {
        Scene scene = SceneManager.GetSceneByPath(TestScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Additive);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var npc = root.GetComponent<DafeiyuNpc>();
            if (npc == null) continue;
            EnsureNpcProgram();
            CreateChatPanel(npc, npc.subtitles.font);
            if (!EditorSceneManager.SaveScene(scene))
                throw new System.InvalidOperationException("The chat panel could not be saved.");
            return;
        }
        throw new System.InvalidOperationException("The test scene does not contain the Dafeiyu NPC.");
    }

    private static void CreateNpc(TMP_FontAsset font)
    {
        EnsureNpcProgram();
        var root = new GameObject("Dafeiyu NPC");
        Undo.RegisterCreatedObjectUndo(root, "Add Dafeiyu NPC");
        var collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.2f, 0f);
        collider.size = new Vector3(1.25f, 0.95f, 1.5f);
        AddPickup(root, collider);

        var model = new GameObject("Whale model");
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var voice = model.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 1f;
        voice.rolloffMode = AudioRolloffMode.Linear;
        voice.minDistance = 1.5f;
        voice.maxDistance = 8f;
        Material blue = ColorMaterial("Ocean", new Color(0.19f, 0.56f, 0.89f));
        Material belly = ColorMaterial("Foam", new Color(0.86f, 0.96f, 1f));
        Material dark = ColorMaterial("Ink", new Color(0.04f, 0.12f, 0.23f));

        Shape(model.transform, "Body", blue, Vector3.zero, new Vector3(1.23f, 0.92f, 1.4f));
        Shape(model.transform, "Belly", belly, new Vector3(0f, -0.17f, 0.15f), new Vector3(1.1f, 0.64f, 1.14f));
        Shape(model.transform, "Left eye", dark, new Vector3(-0.28f, 0.07f, 0.59f), new Vector3(0.13f, 0.16f, 0.09f));
        Shape(model.transform, "Right eye", dark, new Vector3(0.28f, 0.07f, 0.59f), new Vector3(0.13f, 0.16f, 0.09f));
        Shape(model.transform, "Left fin", blue, new Vector3(-0.54f, -0.19f, 0.04f), new Vector3(0.43f, 0.14f, 0.27f)).localRotation = Quaternion.Euler(0f, -20f, 28f);
        Shape(model.transform, "Right fin", blue, new Vector3(0.54f, -0.19f, 0.04f), new Vector3(0.43f, 0.14f, 0.27f)).localRotation = Quaternion.Euler(0f, 20f, -28f);

        var tail = new GameObject("Tail").transform;
        tail.SetParent(model.transform, false);
        tail.localPosition = new Vector3(0f, -0.12f, -0.53f);
        Shape(tail, "Stem", blue, new Vector3(0f, 0.03f, -0.13f), new Vector3(0.32f, 0.25f, 0.55f));
        Shape(tail, "Left fluke", blue, new Vector3(-0.19f, 0.06f, -0.38f), new Vector3(0.45f, 0.11f, 0.29f)).localRotation = Quaternion.Euler(0f, -22f, 0f);
        Shape(tail, "Right fluke", blue, new Vector3(0.19f, 0.06f, -0.38f), new Vector3(0.45f, 0.11f, 0.29f)).localRotation = Quaternion.Euler(0f, 22f, 0f);

        var board = new GameObject("Subtitles", typeof(RectTransform));
        board.transform.SetParent(root.transform, false);
        board.transform.localPosition = new Vector3(0f, 1.98f, 0f);
        var subtitles = board.AddComponent<TextMeshPro>();
        subtitles.font = font;
        subtitles.rectTransform.sizeDelta = new Vector2(2.8f, 0.9f);
        subtitles.fontSize = 2.5f;
        subtitles.alignment = TextAlignmentOptions.Bottom;
        subtitles.richText = false;
        subtitles.text = "嗨，我是大肥鱼！有什么想和我聊的？";
        var subtitleProperties = new SerializedObject(subtitles);
        subtitleProperties.FindProperty("m_fontAsset").objectReferenceValue = font;
        subtitleProperties.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
        subtitleProperties.ApplyModifiedPropertiesWithoutUndo();

        var npc = root.AddUdonSharpComponent<DafeiyuNpc>();
        npc.whale = model.transform;
        npc.tail = tail;
        npc.subtitles = subtitles;
        npc.subtitleAnchor = board.transform;
        npc.voice = voice;
        npc.replyUrl = new VRCUrl("http://127.0.0.1:17821/npc/reply.pcm");
        CreateChatPanel(npc, font);
        Selection.activeGameObject = root;
    }

    private static void CreateChatPanel(DafeiyuNpc npc, TMP_FontAsset font)
    {
        if (npc.chatInput != null) return;
        var canvas = new GameObject("Dafeiyu Chat", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster), typeof(BoxCollider), typeof(VRCUiShape));
        Undo.RegisterCreatedObjectUndo(canvas, "Add Dafeiyu Chat");
        Scene scene = npc.gameObject.scene;
        SceneManager.MoveGameObjectToScene(canvas, scene);
        RectTransform panel = (RectTransform)canvas.transform;
        panel.position = npc.transform.position + new Vector3(1.22f, 1.30f, -0.65f);
        panel.rotation = Quaternion.Euler(0f, 15f, 0f);
        panel.localScale = Vector3.one * 0.0021f;
        panel.sizeDelta = new Vector2(540f, 332f);
        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var clickArea = canvas.GetComponent<BoxCollider>();
        clickArea.isTrigger = true;
        clickArea.size = new Vector3(panel.rect.width, panel.rect.height, 8f);

        Color ink = new Color32(21, 42, 64, 255);
        Color muted = new Color32(63, 86, 108, 255);
        Color pale = new Color32(234, 244, 250, 255);
        Color blue = new Color32(17, 77, 128, 255);

        AddImage("Panel background", panel, Vector2.zero, panel.sizeDelta, pale).raycastTarget = false;
        AddChatText("Title", panel, new Vector2(24f, -15f), new Vector2(492f, 45f),
            font, 30, ink, "和大肥鱼聊天");
        AddImage("Reply background", panel, new Vector2(24f, -68f), new Vector2(492f, 150f),
            Color.white).raycastTarget = false;
        var reply = AddChatText("Reply", panel, new Vector2(40f, -81f), new Vector2(460f, 124f),
            font, 24, ink, "嗨，我是大肥鱼！有什么想和我聊的？");
        reply.alignment = TextAlignmentOptions.TopLeft;
        reply.enableAutoSizing = true;
        reply.fontSizeMin = 18;
        reply.fontSizeMax = 24;

        var resources = new TMP_DefaultControls.Resources
        {
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
        };
        GameObject field = TMP_DefaultControls.CreateInputField(resources);
        field.name = "Message";
        PlaceUi((RectTransform)field.transform, panel, new Vector2(24f, -233f), new Vector2(382f, 71f));
        var input = field.GetComponent<TMP_InputField>();
        input.navigation = new Navigation { mode = Navigation.Mode.None };
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.scrollSensitivity = 0;
        input.onFocusSelectAll = false;
        input.characterLimit = 500;
        input.richText = false;
        StyleChatText(input.textComponent, font, 23, ink);
        input.textComponent.alignment = TextAlignmentOptions.TopLeft;
        var placeholder = (TMP_Text)input.placeholder;
        StyleChatText(placeholder, font, 23, muted);
        placeholder.text = "点这里输入文字";

        Image sendImage = AddImage("Send", panel, new Vector2(419f, -233f), new Vector2(97f, 71f), blue);
        var send = sendImage.gameObject.AddComponent<Button>();
        send.navigation = new Navigation { mode = Navigation.Mode.None };
        send.targetGraphic = sendImage;
        var sendLabel = AddChatText("Label", sendImage.rectTransform, Vector2.zero, new Vector2(97f, 71f),
            font, 24, Color.white, "发送");
        sendLabel.alignment = TextAlignmentOptions.Center;
        UdonBehaviour udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(npc);
        UnityEventTools.AddStringPersistentListener(input.onSelect, udon.SendCustomEvent, nameof(DafeiyuNpc.OnChatInputSelected));
        UnityEventTools.AddStringPersistentListener(send.onClick, udon.SendCustomEvent, nameof(DafeiyuNpc.SendChat));

        bool hasEventSystem = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<EventSystem>(true) != null) hasEventSystem = true;
        }
        if (!hasEventSystem)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(events, scene);
            Undo.RegisterCreatedObjectUndo(events, "Add EventSystem");
        }

        Undo.RecordObject(npc, "Connect Dafeiyu Chat");
        npc.chatInput = input;
        npc.chatReply = reply;
        npc.chatSend = send;
        npc.chatUrl = new VRCUrl(npc.replyUrl.Get().Replace("/npc/reply.pcm", "/npc/chat.pcm"));
        UdonSharpEditorUtility.CopyProxyToUdon(npc);
        EditorUtility.SetDirty(npc);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void AddPickup(GameObject root, BoxCollider collider)
    {
        collider.isTrigger = false;
        var body = root.AddComponent<Rigidbody>();
        body.mass = 3f;
        body.drag = 0.35f;
        body.angularDrag = 0.7f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var pickup = root.AddComponent<VRCPickup>();
        pickup.version = VRCPickup.Version.Version_1_1;
        pickup.AutoHold = VRC_Pickup.AutoHoldMode.No;
        pickup.InteractionText = "抱起大肥鱼";
        pickup.proximity = 1.6f;
    }

    private static Image AddImage(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        PlaceUi(image.rectTransform, parent, position, size);
        image.color = color;
        return image;
    }

    private static TMP_Text AddChatText(string name, RectTransform parent, Vector2 position, Vector2 size,
        TMP_FontAsset font, int fontSize, Color color, string content)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        PlaceUi(label.rectTransform, parent, position, size);
        StyleChatText(label, font, fontSize, color);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.text = content;
        return label;
    }

    private static void StyleChatText(TMP_Text label, TMP_FontAsset font, int size, Color color)
    {
        label.font = font;
        label.fontSharedMaterial = font.material;
        label.fontSize = size;
        label.color = color;
        label.richText = false;
        label.raycastTarget = false;
    }

    private static void PlaceUi(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void EnsureNpcProgram()
    {
        if (UdonSharpEditorUtility.GetUdonSharpProgramAsset(typeof(DafeiyuNpc)) == null)
        {
            const string scriptPath = "Assets/Dafeiyu/DafeiyuNpc.cs";
            const string programPath = "Assets/Dafeiyu/DafeiyuNpc.asset";
            var program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(program, programPath);
            AssetDatabase.ImportAsset(programPath, ImportAssetOptions.ForceSynchronousImport);
        }
        UdonSharpCompilerV1.CompileSync();
        if (UdonSharpProgramAsset.AnyUdonSharpScriptHasError())
        {
            throw new System.InvalidOperationException("UdonSharp compilation failed. Check the Unity Console.");
        }
    }

    private static TMP_FontAsset GetSubtitleFont(bool useSelection)
    {
        if (useSelection && Selection.activeObject is TMP_FontAsset selected) return selected;
        if (useSelection && Selection.activeObject is Font customFont)
        {
            return SaveFont(customFont, AssetDatabase.GenerateUniqueAssetPath(SubtitleFontPath));
        }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SubtitleFontPath);
        if (font != null) return font;
        Font source = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
        if (source != null) return SaveFont(source, SubtitleFontPath);
        EditorUtility.DisplayDialog("大肥鱼", "找不到中文字体。请恢复随工程附带的 Noto Sans CJK SC 字体。", "确定");
        return null;
    }

    private static TMP_FontAsset SaveFont(Font source, string path)
    {
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source);
        font.isMultiAtlasTexturesEnabled = true;
        font.TryAddCharacters("嗨，我是大肥鱼！有什么想和我聊的？对话服务还没连上。0123456789");
        AssetDatabase.CreateAsset(font, path);
        foreach (Texture2D texture in font.atlasTextures)
        {
            if (!EditorUtility.IsPersistent(texture)) AssetDatabase.AddObjectToAsset(texture, font);
        }
        if (!EditorUtility.IsPersistent(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
        AssetDatabase.SaveAssets();
        return font;
    }

    private static Material ColorMaterial(string name, Color color)
    {
        string path = $"Assets/Dafeiyu/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Transform Shape(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
        return part.transform;
    }
}
