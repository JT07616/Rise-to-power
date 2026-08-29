using UnityEngine;

public class ResourcePulse : MonoBehaviour
{
    public int barHeight = 48;
    public float glowDuration = 0.7f;
    public float glowSize = 6f;
    public float numberSize = 30f;
    public float flyDuration = 1f;
    public float flyRise = 40f;
    public Color upColor = new Color(0.45f, 1f, 0.45f);
    public Color downColor = new Color(1f, 0.45f, 0.45f);

    [Header("Audio")]
    public AudioSource uiAudioSource;
    public AudioClip growSound;
    public AudioClip dropSound;

    private int[] last;
    private int[] change = new int[6];
    private float[] glow = new float[6];
    private float[] fly = new float[6];
    private GUIStyle style;

    void Update()
    {
        GameResources r = GameResources.Instance;
        if (r == null) return;

        int[] now = { r.novac, r.robaUTvornici, r.robaUSkladistu, r.SlobodniRadnici, r.rizik, r.utjecaj };

        // prvi frame se samo zapamti stanje, inace bi sve odmah zasvijetlilo
        if (last == null)
        {
            last = now;
            return;
        }

        bool played = false;
        for (int i = 0; i < 6; i++)
        {
            if (now[i] != last[i])
            {
                change[i] = now[i] - last[i];
                glow[i] = 0f;
                fly[i] = 0f;

                if (!played)
                {
                    AudioClip clip = change[i] > 0 ? growSound : dropSound;
                    if (uiAudioSource != null && clip != null) uiAudioSource.PlayOneShot(clip);
                    played = true;
                }
            }

            glow[i] = Mathf.Min(1f, glow[i] + Time.deltaTime / glowDuration);
            fly[i] = Mathf.Min(1f, fly[i] + Time.deltaTime / flyDuration);
        }

        last = now;
    }

    void OnGUI()
    {
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
        }

        // isti raspored kao traka resursa u GameEventManageru
        float gap = 12f;
        float width = Mathf.Max(1f, (Screen.width - 12f - gap * 5f) / 6f);
        float high = barHeight - 4f;

        for (int i = 0; i < 6; i++)
        {
            if (change[i] == 0) continue;

            float x = 6f + i * (width + gap);

            // rizik je jedini gdje je rast losa vijest
            Color color = (i == 4 ? change[i] < 0 : change[i] > 0) ? upColor : downColor;

            if (glow[i] < 1f)
            {
                Color previous = GUI.color;
                GUI.color = new Color(color.r, color.g, color.b, 1f - glow[i]);

                GUI.DrawTexture(new Rect(x - glowSize, 2f - glowSize, width + glowSize * 2f, glowSize), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - glowSize, 2f + high, width + glowSize * 2f, glowSize), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - glowSize, 2f, glowSize, high), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x + width, 2f, glowSize, high), Texture2D.whiteTexture);

                GUI.color = previous;
            }

            if (fly[i] < 1f)
            {
                color.a = 1f - fly[i];
                style.normal.textColor = color;
                style.fontSize = Mathf.RoundToInt(numberSize + 12f * (1f - fly[i]));

                float counted = change[i] * Mathf.Clamp01(fly[i] / 0.35f);
                int shown = change[i] > 0 ? Mathf.CeilToInt(counted) : Mathf.FloorToInt(counted);
                string text = shown > 0 ? "+" + shown : shown.ToString();
                float y = barHeight + flyRise * (1f - fly[i]);

                style.normal.textColor = new Color(0f, 0f, 0f, color.a * 0.6f);
                GUI.Label(new Rect(x + 2f, y + 2f, width, 44f), text, style);

                style.normal.textColor = color;
                GUI.Label(new Rect(x, y, width, 44f), text, style);
            }
        }
    }
}
