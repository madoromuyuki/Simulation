using System;
using UnityEngine;

public partial class TidepoolEvolution
{
    GUIStyle title, heading, label, small, button, welcomeTitle, foodArrow, startButton, endMessage;
    Texture2D pixel, buttonNormal, buttonHover, buttonPressed;
    
    void Styles()
    {
        if (pixel != null) return;
        pixel = SolidTexture(Color.white);
        title = new GUIStyle(GUI.skin.label) { fontSize = 27, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.86f, .96f, .91f) } };
        heading = new GUIStyle(title) { fontSize = 17 };
        label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(.8f, .88f, .87f) } };
        small = new GUIStyle(label) { fontSize = 12 };
        buttonNormal = SolidTexture(new Color(.12f, .32f, .35f));
        buttonHover = SolidTexture(new Color(.18f, .42f, .44f));
        buttonPressed = SolidTexture(new Color(.08f, .24f, .27f));
        button = new GUIStyle
        {
            font = GUI.skin.font, fontSize = 14, fixedHeight = 36,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(0, 0, 0, 0), padding = new RectOffset(12, 12, 6, 6)
        };
        foreach (var state in new[] { button.normal, button.onNormal, button.hover, button.onHover, button.active, button.onActive, button.focused, button.onFocused })
            state.textColor = Color.white;
        button.normal.background = button.onNormal.background = buttonNormal;
        button.hover.background = button.onHover.background = button.focused.background = button.onFocused.background = buttonHover;
        button.active.background = button.onActive.background = buttonPressed;
        welcomeTitle = new GUIStyle(title) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
        endMessage = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        foodArrow = new GUIStyle(title) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
        startButton = new GUIStyle(button) { fontSize = 20, fixedHeight = 44, fontStyle = FontStyle.Bold };
    }
    Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color); texture.Apply();
        return texture;
    }
    void OnDestroy()
    {
        foreach (var texture in new[] { pixel, buttonNormal, buttonHover, buttonPressed })
            if (texture != null) Destroy(texture);
    }
    void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, pixel); GUI.color = Color.white; }
    void OnGUI()
    {
        Event click = Event.current;
        if (click.type == EventType.MouseDown && click.button == 0 && TryPlaceNutrient(new Vector2(click.mousePosition.x, Screen.height - click.mousePosition.y))) click.Use();
        Styles();
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 800f, 1));
        Box(new Rect(0, 0, 1280, 78), new Color(.025f, .08f, .105f, .98f));
        GUI.Label(new Rect(25, 9, 730, 35), "The Pond", title);
        GUI.Label(new Rect(26, 46, 700, 24), "A small world. A delicate balance. Every life has a ripple.", small);
        Box(new Rect(934, 78, 346, 722), new Color(.035f, .10f, .125f, .98f));
        GUILayout.BeginArea(new Rect(954, 92, 305, 690));
        GUILayout.Label($"Algae   {Count(Species.Algae)}     Shrimp   {Count(Species.Shrimp)}     Fish   {Count(Species.Fish)}", label);
        GUILayout.Space(18);
        GUILayout.Label($"SIMULATION SPEED   {rate:0.##}×", heading);
        int speedIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(SpeedIndex, 0, SpeedSteps.Length - 1));
        rate = SpeedSteps[speedIndex];
        GUILayout.BeginHorizontal();
        foreach (float step in SpeedSteps) GUILayout.Label($"{step:0.##}×", small, GUILayout.Width(47));
        GUILayout.EndHorizontal();
        GUILayout.Space(18);
        GUILayout.Label("Pond Notes", heading);
        DrawFoodChain(GUILayoutUtility.GetRect(305, 64));
        GUILayout.Space(8);
        GUILayout.Label("Algae divide, while shrimp and fish grow. Algae live for 20 seconds, shrimp for 45 seconds, and fish for 90 seconds. The game ends when any species goes extinct. Each organism has a lifespan countdown that resets whenever it eats.", label);
        GUILayout.FlexibleSpace();
        if (!gameOver)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Resume" : "Pause", button)) paused = !paused;
            if (GUILayout.Button("Restart", button)) Restart();
            GUILayout.EndHorizontal();
        }
        GUILayout.EndArea();
        Box(new Rect(0, 730, 934, 70), new Color(.025f, .08f, .105f, .97f)); GUI.Label(new Rect(25, 746, 885, 45), notice, label);
        if (help || gameOver) DrawDialog();
    }
    void DrawDialog()
    {
        Box(new Rect(160, 230, 620, 340), new Color(.025f, .075f, .095f, .99f));
        GUI.Label(new Rect(190, 257, 560, 55), gameOver ? "Game Over." : "Welcome to the Pond!", welcomeTitle);
        if (gameOver)
            GUI.Label(new Rect(190, 345, 560, 95), "One Specie has gone extinct.\nThe live system collapsed.", endMessage);
        else DrawFoodChain(new Rect(199, 350, 520, 84));
        if (!GUI.Button(new Rect(230, 485, 480, 44), gameOver ? "Restart" : "Start", startButton)) return;
        if (gameOver) Restart();
        else help = false;
    }

    Rect ChainRect(Rect area, float x, float y, float width, float height)
    {
        return new Rect(area.x + x * area.width / 520, area.y + y * area.height / 84, width * area.width / 520, height * area.height / 84);
    }
    void DrawFoodChain(Rect area)
    {
        int fontSize = foodArrow.fontSize;
        foodArrow.fontSize = Mathf.Clamp(Mathf.RoundToInt(32 * area.width / 520), 16, 32);
        DrawCreatureIcon(Species.Fish, ChainRect(area, 0, 0, 110, 84));
        GUI.Label(ChainRect(area, 114, 0, 36, 84), ">", foodArrow);
        DrawCreatureIcon(Species.Shrimp, ChainRect(area, 156, 7, 88, 70));
        GUI.Label(ChainRect(area, 252, 0, 36, 84), ">", foodArrow);
        DrawCreatureIcon(Species.Algae, ChainRect(area, 311, 15, 54, 54));
        GUI.Label(ChainRect(area, 390, 0, 36, 84), ">", foodArrow);
        var nutrient = nutrientPrefab.GetComponentInChildren<SpriteRenderer>();
        DrawSprite(ChainRect(area, 475, 34, 16, 16), nutrient.sprite, nutrient.color);
        foodArrow.fontSize = fontSize;
    }
    void DrawSprite(Rect rect, Sprite sprite, Color color)
    {
        Rect uv = sprite.textureRect;
        uv = new Rect(uv.x / sprite.texture.width, uv.y / sprite.texture.height, uv.width / sprite.texture.width, uv.height / sprite.texture.height);
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTextureWithTexCoords(rect, sprite.texture, uv, true);
        GUI.color = previous;
    }
    void DrawCreatureIcon(Species species, Rect area)
    {
        var prefab = organismPrefabs[(int)species];
        var parts = prefab.GetComponentsInChildren<SpriteRenderer>();
        Array.Sort(parts, (a, b) => a.sortingOrder.CompareTo(b.sortingOrder));
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (var part in parts)
        {
            if (first) { bounds = part.bounds; first = false; } else bounds.Encapsulate(part.bounds);
        }
        float scale = Mathf.Min(area.width / bounds.size.x, area.height / bounds.size.y);
        foreach (var part in parts)
        {
            Bounds b = part.bounds;
            Rect rect = new Rect(area.center.x + (b.min.x - bounds.center.x) * scale, area.center.y - (b.max.y - bounds.center.y) * scale, b.size.x * scale, b.size.y * scale);
            Color color = part == prefab.body ? (species == Species.Fish ? Color.cyan : species == Species.Shrimp ? new Color(1, .7f, .3f) : Color.green) : part.color;
            DrawSprite(rect, part.sprite, color);
        }
    }

}
