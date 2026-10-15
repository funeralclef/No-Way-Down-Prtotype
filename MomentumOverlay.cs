using Godot;

public partial class MomentumOverlay : Control
{
    public float Intensity { get; set; }
    public float RushAmount { get; set; }
    public float SpeedBoostAmount { get; set; }
    public int DetailLevel { get; set; } = 3;

    private double _elapsedTime;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _elapsedTime += delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Intensity <= 0.01f)
        {
            return;
        }

        Vector2 viewportSize = Size;
        int detailLevel = Mathf.Clamp(DetailLevel, 0, 3);
        int edgeLayers = detailLevel switch
        {
            0 => 3,
            1 => 5,
            2 => 7,
            _ => 9
        };
        float layerWidth = Mathf.Max(7.0f, viewportSize.Y * 0.014f);

        for (int layer = 0; layer < edgeLayers; layer++)
        {
            float layerRatio = 1.0f - layer / (float)edgeLayers;
            float alpha = 0.035f * Intensity * layerRatio * layerRatio;
            float inset = layer * layerWidth;
            Color edgeColor = new(1.0f, 0.34f + RushAmount * 0.18f, 0.04f, alpha);

            DrawRect(new Rect2(inset, inset, layerWidth, viewportSize.Y - inset * 2.0f), edgeColor);
            DrawRect(new Rect2(
                viewportSize.X - inset - layerWidth,
                inset,
                layerWidth,
                viewportSize.Y - inset * 2.0f), edgeColor);
            DrawRect(new Rect2(inset, inset, viewportSize.X - inset * 2.0f, layerWidth), edgeColor);
            DrawRect(new Rect2(
                inset,
                viewportSize.Y - inset - layerWidth,
                viewportSize.X - inset * 2.0f,
                layerWidth), edgeColor);
        }

        if (RushAmount > 0.01f)
        {
            DrawRushLines(viewportSize, detailLevel);
        }

        if (SpeedBoostAmount > 0.01f)
        {
            DrawSpeedBoostLines(viewportSize, detailLevel);
        }
    }

    private void DrawRushLines(Vector2 viewportSize, int detailLevel)
    {
        float timeOffset = (float)(_elapsedTime * 1.8 % 1.0);
        float lineWidth = Mathf.Max(1.5f, viewportSize.Y * 0.004f);
        int lineCount = detailLevel switch
        {
            0 => 3,
            1 => 6,
            2 => 9,
            _ => 12
        };

        for (int index = 0; index < lineCount; index++)
        {
            float lane = index / (float)(lineCount - 1);
            float sideX = index % 2 == 0
                ? Mathf.Lerp(viewportSize.X * 0.03f, viewportSize.X * 0.23f, lane)
                : Mathf.Lerp(viewportSize.X * 0.97f, viewportSize.X * 0.77f, lane);
            float phase = Mathf.PosMod(timeOffset + index * 0.137f, 1.0f);
            float y = phase * viewportSize.Y;
            float length = Mathf.Lerp(18.0f, 74.0f, RushAmount);
            Color lineColor = new(1.0f, 0.72f, 0.24f, 0.2f * RushAmount);

            DrawLine(
                new Vector2(sideX, y),
                new Vector2(sideX, Mathf.Min(viewportSize.Y, y + length)),
                lineColor,
                lineWidth,
                true);
        }
    }

    private void DrawSpeedBoostLines(
        Vector2 viewportSize,
        int detailLevel)
    {
        int lineCount = detailLevel switch
        {
            0 => 7,
            1 => 11,
            2 => 16,
            _ => 22
        };
        Vector2 centre = viewportSize * new Vector2(0.5f, 0.54f);
        float shortSide = Mathf.Min(viewportSize.X, viewportSize.Y);
        float timeOffset = (float)(_elapsedTime * 3.8 % 1.0);
        float lineWidth = Mathf.Lerp(
            1.25f,
            3.2f,
            SpeedBoostAmount);

        for (int index = 0; index < lineCount; index++)
        {
            float angle =
                Mathf.Tau * index / lineCount +
                Mathf.Sin(index * 12.37f) * 0.16f;
            Vector2 ray = Vector2.FromAngle(angle);
            float phase = Mathf.PosMod(
                timeOffset + index * 0.173f,
                1.0f);
            float startDistance =
                shortSide * Mathf.Lerp(0.12f, 0.54f, phase);
            float streakLength =
                shortSide *
                Mathf.Lerp(0.035f, 0.13f, SpeedBoostAmount) *
                Mathf.Lerp(0.55f, 1.0f, phase);
            Vector2 start = centre + ray * startDistance;
            Vector2 end = start + ray * streakLength;
            float alpha =
                Mathf.Lerp(0.12f, 0.42f, SpeedBoostAmount) *
                Mathf.Lerp(0.55f, 1.0f, phase);
            Color streakColor = new(
                0.35f,
                0.9f,
                1.0f,
                alpha);
            DrawLine(
                start,
                end,
                streakColor,
                lineWidth,
                true);
        }
    }
}
